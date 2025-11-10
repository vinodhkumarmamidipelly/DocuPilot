using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SMEPilot.FunctionApp.Helpers;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using System.IO;
using Spire.Pdf;

// Configure Serilog for file logging
// Use app folder for better tracking (instead of temp directory)
var appFolder = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
var logPath = Path.Combine(appFolder, "Logs");
if (!Directory.Exists(logPath))
{
    Directory.CreateDirectory(logPath);
}

var logFile = Path.Combine(logPath, "sme-pilot-.log");

// Configure Serilog: Console for startup only, File for all logs
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    // File: ALL logs (including processing logs from ILogger) - write first, no filters
    .WriteTo.File(
        new CompactJsonFormatter(),
        logFile,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30, // Keep logs for 30 days
        fileSizeLimitBytes: 10 * 1024 * 1024, // 10 MB per file
        rollOnFileSizeLimit: true,
        shared: true, // Allow multiple processes to write to same file
        flushToDiskInterval: TimeSpan.FromSeconds(1)) // Flush to disk every second for better accessibility
    // Console: COMPLETELY DISABLED - All logs go to files only
    // No console output at all - user requested zero logs in output window
    .CreateLogger();

try
{
    Log.Information("🚀 SMEPilot Function App starting...");
    Log.Information("📁 Log files location: {LogPath}", logPath);
    
    var host = new HostBuilder()
        .ConfigureFunctionsWorkerDefaults()
               .ConfigureServices(services =>
               {
                   services.AddSingleton<Config>();
                   services.AddSingleton<GraphHelper>(sp => 
                   {
                       var cfg = sp.GetRequiredService<Config>();
                       var logger = sp.GetService<ILogger<GraphHelper>>();
                       return new GraphHelper(cfg, logger);
                   });
                   services.AddSingleton<SimpleExtractor>(sp => 
                   {
                       var logger = sp.GetService<ILogger<SimpleExtractor>>();
                       return new SimpleExtractor(logger);
                   });
                   services.AddSingleton<TemplateBuilder>();
                   services.AddSingleton<OcrHelper>(sp => 
                   {
                       var cfg = sp.GetRequiredService<Config>();
                       var logger = sp.GetService<ILogger<OcrHelper>>();
                       return new OcrHelper(cfg, logger);
                   }); // OCR support (optional - requires AzureVision_Endpoint and AzureVision_Key)
                   
                   // Add HybridEnricher for rule-based sectioning (NO AI)
                   var cfg = new Config();
                   services.AddSingleton<HybridEnricher>();
                   
                   // SetupSubscription requires ILogger
                   services.AddSingleton<SMEPilot.FunctionApp.Functions.SetupSubscription>(sp =>
                   {
                       var graph = sp.GetRequiredService<GraphHelper>();
                       var config = sp.GetRequiredService<Config>();
                       var logger = sp.GetService<ILogger<SMEPilot.FunctionApp.Functions.SetupSubscription>>();
                       return new SMEPilot.FunctionApp.Functions.SetupSubscription(graph, config, logger);
                   });
                   
                   // Add Serilog logging - CLEAR all default providers first to prevent console output
                   services.AddLogging(builder =>
                   {
                       // CRITICAL: Clear ALL default logging providers (including console)
                       builder.ClearProviders();
                       // Only add Serilog (which writes to files only, no console)
                       builder.AddSerilog();
                   });
               })
        .Build();
    
    // Log configuration
    var cfg = new Config();
    
    // Validate configuration
    if (!cfg.IsValid())
    {
        Log.Fatal("❌ [CONFIG] Invalid configuration - missing required Graph API credentials");
        Log.Fatal("   Required: Graph_TenantId, Graph_ClientId, Graph_ClientSecret");
        throw new InvalidOperationException("Invalid configuration - missing required Graph API credentials");
    }
    
    Log.Information("🔧 [CONFIG] Template formatting mode - Rule-based sectioning only (no AI, no database)");
    Log.Information("📋 [CONFIG] Configuration validated successfully");
    Log.Information("   MaxRetryAttempts: {MaxRetries}", cfg.MaxRetryAttempts);
    Log.Information("   MaxFileSizeBytes: {MaxSize} MB", cfg.MaxFileSizeBytes / 1024 / 1024);
    Log.Information("   MaxUploadRetries: {UploadRetries}", cfg.MaxUploadRetries);
    Log.Information("   NotificationDedupWindow: {DedupWindow}s", cfg.NotificationDedupWindowSeconds);
    
    // Initialize Spire.PDF license
    if (!string.IsNullOrWhiteSpace(cfg.SpirePdfLicense))
    {
        try
        {
            // Spire.PDF license initialization (version 10.2.2)
            // Note: LicenseProvider may be in Spire.Pdf.License namespace
            // Try different possible namespaces
            var pdfLicenseType = Type.GetType("Spire.Pdf.License.LicenseProvider, Spire.Pdf");
            if (pdfLicenseType != null)
            {
                var setLicenseKeyMethod = pdfLicenseType.GetMethod("SetLicenseKey", new[] { typeof(string) });
                if (setLicenseKeyMethod != null)
                {
                    setLicenseKeyMethod.Invoke(null, new object[] { cfg.SpirePdfLicense });
                    Log.Information("✅ [CONFIG] Spire.PDF license initialized successfully");
                }
                else
                {
                    Log.Warning("⚠️ [CONFIG] SetLicenseKey method not found in Spire.Pdf.License.LicenseProvider");
                }
            }
            else
            {
                // Fallback: Try setting license directly on PdfDocument if available
                Log.Warning("⚠️ [CONFIG] Spire.Pdf.License.LicenseProvider not found - license may need to be set differently");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ [CONFIG] Failed to initialize Spire.PDF license: {Error}", ex.Message);
        }
    }
    else
    {
        Log.Warning("⚠️ [CONFIG] Spire.PDF license not configured - PDF processing may be limited");
    }
    
    // Initialize Spire.Doc license (if configured, for future use)
    if (!string.IsNullOrWhiteSpace(cfg.SpireDocLicense))
    {
        try
        {
            // Note: Spire.Doc is not currently used, but license is set for future use
            // Spire.Doc.License.LicenseProvider.SetLicenseKey(cfg.SpireDocLicense);
            Log.Information("ℹ️ [CONFIG] Spire.Doc license configured (not currently used)");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ [CONFIG] Failed to initialize Spire.Doc license: {Error}", ex.Message);
        }
    }
    
    // Check if OCR is configured
    if (!string.IsNullOrWhiteSpace(cfg.AzureVisionEndpoint) && !string.IsNullOrWhiteSpace(cfg.AzureVisionKey))
    {
        Log.Information("✅ [CONFIG] OCR enabled - Azure Computer Vision configured");
    }
    else
    {
        Log.Information("ℹ️ [CONFIG] OCR disabled - Add AzureVision_Endpoint and AzureVision_Key to enable image text extraction");
    }
    
    Log.Information("✅ SMEPilot Function App started successfully");
    
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ SMEPilot Function App failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

