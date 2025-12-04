using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Basic logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Minimal API setup / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Single global semaphore to limit concurrent Word automation.
var wordLock = new SemaphoreSlim(1, 1);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Health check
app.MapGet("/", () => Results.Ok("WordTocService is running"));

// Main TOC update endpoint
app.MapPost("/api/toc/update", async (
    HttpRequest request,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var logger = loggerFactory.CreateLogger("TocEndpoint");

    // 1. Read incoming DOCX bytes
    using var ms = new MemoryStream();
    await request.Body.CopyToAsync(ms, cancellationToken);
    if (ms.Length == 0)
    {
        return Results.BadRequest("Empty request body");
    }

    // 2. Save to temp file
    var tempPath = Path.Combine(Path.GetTempPath(), $"toc_{Guid.NewGuid():N}.docx");
    await File.WriteAllBytesAsync(tempPath, ms.ToArray(), cancellationToken);

    logger.LogInformation("📑 [TOC-SVC] Received document ({Size} bytes). Temp path: {Path}", ms.Length, tempPath);

    try
    {
        await wordLock.WaitAsync(cancellationToken);

        // 3. Use late-bound Word Interop so we don't depend on a specific PIA version.
        Type? wordType = Type.GetTypeFromProgID("Word.Application");
        if (wordType == null)
        {
            logger.LogError("❌ [TOC-SVC] Could not locate Word.Application COM type. Is Microsoft Word installed?");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        dynamic? wordApp = null;
        dynamic? doc = null;

        try
        {
            wordApp = Activator.CreateInstance(wordType);
            wordApp.Visible = false;

            logger.LogInformation("📑 [TOC-SVC] Opening document in Word to update fields...");
            // Open without automatic repair; if Word considers the file invalid it will throw a COMException,
            // which we catch and surface as a failure so the caller can fall back to the original bytes.
            doc = wordApp.Documents.Open(tempPath, ReadOnly: false, Visible: false);

            // Update all fields (includes TOC and page numbers)
            doc.Fields.Update();

            // Save in-place
            doc.Save();
            logger.LogInformation("✅ [TOC-SVC] Fields updated and document saved.");
        }
        catch (COMException ex)
        {
            // Word sometimes reports "file appears to be corrupted" for documents it would otherwise repair interactively.
            // In service mode we can't interact with repair dialogs, so we log and return an error
            // so the caller can fall back to the original document bytes.
            logger.LogWarning(ex, "⚠️ [TOC-SVC] Word failed to open or update the document (COMException).");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
        finally
        {
            try
            {
                doc?.Close(false);
            }
            catch
            {
                // ignore
            }

            try
            {
                wordApp?.Quit(false);
            }
            catch
            {
                // ignore
            }
        }
    }
    catch (OperationCanceledException)
    {
        logger.LogWarning("⚠️ [TOC-SVC] Request was cancelled while updating TOC.");
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
    finally
    {
        wordLock.Release();
    }

    // 4. Read updated file and return
    byte[] updatedBytes;
    try
    {
        updatedBytes = await File.ReadAllBytesAsync(tempPath, cancellationToken);
    }
    finally
    {
        try
        {
            File.Delete(tempPath);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    logger.LogInformation("📑 [TOC-SVC] Returning updated document ({Size} bytes).", updatedBytes.Length);

    return Results.File(
        updatedBytes,
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
});

app.Run();
