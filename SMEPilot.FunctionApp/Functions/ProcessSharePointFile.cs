using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Newtonsoft.Json;
using SMEPilot.FunctionApp.Helpers;
using SMEPilot.FunctionApp.Models;
using System.Collections.Generic;
using System.Text;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Graph.Models;

namespace SMEPilot.FunctionApp.Functions
{
    public class ProcessSharePointFile
    {
        private readonly GraphHelper _graph;
        private readonly SimpleExtractor _extractor;
        private readonly OpenAiHelper _openai;
        private readonly Config _cfg;
        private readonly CosmosHelper _cosmos;
        
        // In-memory semaphore to prevent concurrent processing of the same file
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _processingLocks = new ConcurrentDictionary<string, SemaphoreSlim>();

        public ProcessSharePointFile(GraphHelper graph, SimpleExtractor extractor, OpenAiHelper openai, Config cfg, CosmosHelper cosmos)
        {
            _graph = graph;
            _extractor = extractor;
            _openai = openai;
            _cfg = cfg;
            _cosmos = cosmos;
        }

        [Function("ProcessSharePointFile")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            try
            {
                // Step 1: Handle Graph subscription validation handshake
                // Graph API sends validation token via GET request with query parameter
                // This ONLY happens during initial subscription creation (one-time validation)
                // After that, all notifications come as POST with empty query string (data in body)
                var query = req.Url.Query;

                // Check for validation token (only during initial subscription validation)
                if (!string.IsNullOrEmpty(query))
                {
                    var queryString = query.TrimStart('?');
                    var validationTokenMatch = System.Text.RegularExpressions.Regex.Match(queryString, @"validationToken=([^&]*)");

                    if (validationTokenMatch.Success)
                    {
                        var encodedToken = validationTokenMatch.Groups[1].Value;
                        Console.WriteLine($"=== VALIDATION REQUEST (Subscription Setup) ===");

                        // Properly decode URL-encoded token (+ becomes space, %XX becomes characters)
                        var validationToken = encodedToken
                            .Replace("+", " ")
                            .Replace("%3a", ":", StringComparison.OrdinalIgnoreCase)
                            .Replace("%3A", ":")
                            .Replace("%2F", "/")
                            .Replace("%2f", "/");
                        validationToken = Uri.UnescapeDataString(validationToken);

                        // Return validation token as plain text (decoded) - REQUIRED by Graph API
                        var vresp = req.CreateResponse(HttpStatusCode.OK);
                        vresp.Headers.Add("Content-Type", "text/plain; charset=utf-8");
                        await vresp.WriteStringAsync(validationToken);
                        Console.WriteLine($"✅ Validation token returned to Graph API");
                        Console.WriteLine($"=== END VALIDATION ===");
                        return vresp;
                    }
                }

                // If GET request without validation token, return OK (for Graph API health checks)
                if (req.Method == "GET")
                {
                    var okResp = req.CreateResponse(HttpStatusCode.OK);
                    await okResp.WriteStringAsync("SMEPilot ProcessSharePointFile endpoint is ready");
                    return okResp;
                }

                // Step 2: Parse request body
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                
                // Step 3: Try to parse as Graph notification first
                try
                {
                    var graphNotification = JsonConvert.DeserializeObject<GraphChangeNotification>(body);
                    
                    if (graphNotification?.Value != null && graphNotification.Value.Count > 0)
                    {
                        Console.WriteLine($"Received Graph notification with {graphNotification.Value.Count} items");
                        Console.WriteLine($"Notification body: {body.Substring(0, Math.Min(500, body.Length))}...");
                        
                        // Process each notification item
                        var processedCount = 0;
                        foreach (var notification in graphNotification.Value)
                        {
                            Console.WriteLine($"=== Processing Notification Item ===");
                            Console.WriteLine($"Subscription ID: {notification.SubscriptionId}");
                            Console.WriteLine($"Change Type: {notification.ChangeType}");
                            Console.WriteLine($"Resource: {notification.Resource}");
                            Console.WriteLine($"ResourceData is null: {notification.ResourceData == null}");
                            
                            if (notification.ResourceData == null)
                            {
                                Console.WriteLine($"⚠️ Notification has no resource data: {notification.SubscriptionId}");
                                Console.WriteLine($"Full notification JSON: {JsonConvert.SerializeObject(notification)}");
                                continue;
                            }
                            
                            // Process "updated" events but filter out duplicates using idempotency check
                            // Graph API only supports "updated" for drive subscriptions (not "created")
                            // We use metadata check + semaphore lock to prevent duplicate processing
                            if (notification.ChangeType != "updated")
                            {
                                Console.WriteLine($"⏭️ Skipping {notification.ChangeType} event - only processing 'updated' events");
                                continue;
                            }
                            
                            Console.WriteLine($"ResourceData ID: {notification.ResourceData.Id}");
                            Console.WriteLine($"ResourceData Name: {notification.ResourceData.Name}");
                            Console.WriteLine($"ResourceData DriveId: {notification.ResourceData.DriveId}");
                            
                            // Extract file details from Graph notification
                            string driveId = notification.ResourceData.DriveId ?? "";
                            string itemId = notification.ResourceData.Id ?? "";
                            string fileName = notification.ResourceData.Name ?? "";
                            // Note: Graph SDK v5 Identity doesn't have Email property
                            // Use DisplayName from our custom model if available, otherwise empty string
                            string uploaderEmail = "";
                            if (notification.ResourceData.CreatedBy?.User != null)
                            {
                                uploaderEmail = notification.ResourceData.CreatedBy.User.Email ?? notification.ResourceData.CreatedBy.User.DisplayName ?? "";
                            }
                            
                            // If resourceData is missing fields, extract from resource path
                            if (string.IsNullOrWhiteSpace(driveId) && !string.IsNullOrWhiteSpace(notification.Resource))
                            {
                                var resourceParts = notification.Resource.Split('/');
                                // Resource: /drives/b!xyz/root
                                // Parts: ["", "drives", "b!xyz", "root"]
                                if (resourceParts.Length >= 3 && resourceParts[1] == "drives")
                                {
                                    driveId = resourceParts[2];
                                    Console.WriteLine($"✅ Extracted driveId from resource: {driveId}");
                                }
                            }
                            
                            // If we still don't have file details, query Graph API for recent changes
                            // BUT: Check metadata FIRST before querying (to avoid processing same file multiple times)
                            if (string.IsNullOrWhiteSpace(itemId) && !string.IsNullOrWhiteSpace(driveId))
                            {
                                Console.WriteLine($"⚠️ No itemId in notification, querying Graph API for recent changes in drive {driveId}...");
                                try
                                {
                                    // Query for recent items in the drive root (only files, not folders)
                                    var recentItems = await _graph.GetRecentDriveItemsAsync(driveId, maxItems: 10);
                                    if (recentItems != null && recentItems.Count > 0)
                                    {
                                        // Find the first file that hasn't been processed yet
                                        DriveItem? candidateFile = null;
                                        
                                        foreach (var item in recentItems)
                                        {
                                            // Skip folders
                                            if (item.Folder != null) continue;
                                            
                                            // Check if this file was already processed
                                            var itemIdToCheck = item.Id ?? "";
                                            if (!string.IsNullOrWhiteSpace(itemIdToCheck))
                                            {
                                                var metadata = await _graph.GetListItemFieldsAsync(driveId, itemIdToCheck);
                                                if (metadata != null && metadata.ContainsKey("SMEPilot_Enriched"))
                                                {
                                                    var enrichedValue = metadata["SMEPilot_Enriched"]?.ToString();
                                                    var isEnriched = enrichedValue == "True" || enrichedValue == "true" || enrichedValue == "1";
                                                    if (isEnriched)
                                                    {
                                                        Console.WriteLine($"⏭️ Recent file {item.Name} already processed, checking next...");
                                                        continue; // Skip already processed files
                                                    }
                                                }
                                            }
                                            
                                            // Found a file that hasn't been processed
                                            candidateFile = item;
                                            break;
                                        }
                                        
                                        if (candidateFile == null)
                                        {
                                            Console.WriteLine("⚠️ All recent files have already been processed");
                                            continue;
                                        }
                                        
                                        itemId = candidateFile.Id ?? "";
                                        fileName = candidateFile.Name ?? "unknown";
                                        uploaderEmail = candidateFile.CreatedBy?.User?.DisplayName ?? candidateFile.CreatedBy?.Application?.DisplayName ?? "";
                                        Console.WriteLine($"✅ Found unprocessed file: {fileName} (ID: {itemId})");
                                    }
                                    else
                                    {
                                        Console.WriteLine("⚠️ No recent files found in drive");
                                        continue;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"❌ Error querying Graph API for recent items: {ex.Message}");
                                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                                    continue;
                                }
                            }

                            if (string.IsNullOrWhiteSpace(driveId) || string.IsNullOrWhiteSpace(itemId))
                            {
                                Console.WriteLine($"⚠️ Missing driveId or itemId. DriveId: {driveId}, ItemId: {itemId}");
                                Console.WriteLine($"Full notification JSON: {JsonConvert.SerializeObject(notification)}");
                                continue;
                            }

                            // Get tenant ID from resource path or use default
                            var tenantId = ExtractTenantIdFromResource(notification.Resource) ?? "default";

                            Console.WriteLine($"✅ Processing Graph notification: File {fileName} (ID: {itemId}) in Drive {driveId}, ChangeType: {notification.ChangeType}");

                            // Use in-memory semaphore to prevent concurrent processing of the same file
                            var lockKey = $"{driveId}:{itemId}";
                            var semaphore = _processingLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
                            
                            // Try to acquire lock (non-blocking check first)
                            if (!await semaphore.WaitAsync(0))
                            {
                                Console.WriteLine($"⏭️ [CONCURRENCY] File {fileName} is already being processed by another notification, skipping");
                                continue;
                            }
                            
                            try
                            {
                                // Check if file was already processed (idempotency) - INSIDE lock
                                Console.WriteLine($"🔍 [IDEMPOTENCY] Checking if file was already processed...");
                                try
                                {
                                    var existingMetadata = await _graph.GetListItemFieldsAsync(driveId, itemId);
                                    if (existingMetadata != null)
                                    {
                                        Console.WriteLine($"🔍 [IDEMPOTENCY] Retrieved metadata: {JsonConvert.SerializeObject(existingMetadata)}");
                                        
                                        // Check if already enriched
                                        if (existingMetadata.ContainsKey("SMEPilot_Enriched"))
                                        {
                                            var enrichedValue = existingMetadata["SMEPilot_Enriched"]?.ToString();
                                            Console.WriteLine($"🔍 [IDEMPOTENCY] SMEPilot_Enriched value: '{enrichedValue}'");
                                            
                                            var isEnriched = enrichedValue == "True" || enrichedValue == "true" || enrichedValue == "1";
                                            if (isEnriched)
                                            {
                                                Console.WriteLine($"⏭️ [IDEMPOTENCY] File {fileName} already processed (SMEPilot_Enriched={enrichedValue}), skipping");
                                                continue;
                                            }
                                        }
                                        
                                        // Check if currently processing (prevents race conditions)
                                        if (existingMetadata.ContainsKey("SMEPilot_Status"))
                                        {
                                            var statusValue = existingMetadata["SMEPilot_Status"]?.ToString();
                                            Console.WriteLine($"🔍 [IDEMPOTENCY] SMEPilot_Status value: '{statusValue}'");
                                            
                                            if (statusValue == "Processing")
                                            {
                                                Console.WriteLine($"⏭️ [IDEMPOTENCY] File {fileName} is currently being processed (SMEPilot_Status=Processing), skipping to avoid duplicate");
                                                continue;
                                            }
                                        }
                                        
                                        Console.WriteLine($"🔄 [IDEMPOTENCY] File {fileName} is ready to process (not enriched, not processing)");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"🔄 [IDEMPOTENCY] File {fileName} has no metadata, will process");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"⚠️ [IDEMPOTENCY] Error checking metadata: {ex.Message}");
                                    Console.WriteLine($"   Stack trace: {ex.StackTrace}");
                                    Console.WriteLine($"   Will proceed with processing to avoid missing files");
                                }

                                // Process the file
                                var result = await ProcessFileAsync(driveId, itemId, fileName, uploaderEmail, tenantId);
                                
                                if (result.Success)
                                {
                                    processedCount++;
                                    Console.WriteLine($"✅ Successfully processed {fileName}");
                                }
                                else
                                {
                                    Console.WriteLine($"❌ Failed to process {fileName}: {result.ErrorMessage}");
                                }
                            }
                            finally
                            {
                                semaphore.Release();
                                // Note: Semaphore cleanup not needed - lightweight and can be reused
                                // Dictionary will grow but only with unique file keys (driveId:itemId)
                            }
                        }

                        // Return 202 Accepted for Graph notifications (asynchronous processing)
                        var accepted = req.CreateResponse(HttpStatusCode.Accepted);
                        await accepted.WriteStringAsync(JsonConvert.SerializeObject(new
                        {
                            message = $"Processing {processedCount} file(s) from Graph notification",
                            processedCount = processedCount
                        }));
                        return accepted;
                    }
                }
                catch (JsonException)
                {
                    // Not a Graph notification format, try manual payload
                    Console.WriteLine("Not a Graph notification format, trying manual payload");
                }

                // Step 4: Try to parse as manual SharePointEvent payload (for testing)
                var evt = JsonConvert.DeserializeObject<SharePointEvent>(body);
                if (evt == null || string.IsNullOrWhiteSpace(evt.driveId) || string.IsNullOrWhiteSpace(evt.itemId))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync(JsonConvert.SerializeObject(new
                    {
                        error = "Invalid event payload",
                        received = body.Length > 0 ? "Non-empty body received" : "Empty body",
                        hint = "Expected Graph notification or SharePointEvent format"
                    }));
                    return bad;
                }

                // Process manual payload
                Console.WriteLine($"Processing manual payload: File {evt.fileName} (ID: {evt.itemId})");
                var manualResult = await ProcessFileAsync(evt.driveId, evt.itemId, evt.fileName, evt.uploaderEmail, evt.tenantId ?? "default");
                
                if (!manualResult.Success)
                {
                    var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await err.WriteStringAsync(JsonConvert.SerializeObject(new { error = manualResult.ErrorMessage }));
                    return err;
                }

                var ok = req.CreateResponse(HttpStatusCode.OK);
                await ok.WriteStringAsync(JsonConvert.SerializeObject(new { enrichedUrl = manualResult.EnrichedUrl }));
                return ok;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in ProcessSharePointFile: {ex}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                var res = req.CreateResponse(HttpStatusCode.InternalServerError);
                await res.WriteStringAsync(JsonConvert.SerializeObject(new
                {
                    error = ex.Message,
                    type = ex.GetType().Name,
                    message = "Check Visual Studio Output window for details"
                }));
                return res;
            }
        }

        private string? ExtractTenantIdFromResource(string resource)
        {
            // Resource format: /sites/{siteId}/drives/{driveId}/root/children
            // Site ID format: domain.sharepoint.com,TENANT-UUID,SITE-GUID
            if (string.IsNullOrWhiteSpace(resource)) return null;
            
            try
            {
                var parts = resource.Split('/');
                if (parts.Length >= 3 && parts[1] == "sites")
                {
                    var siteId = parts[2];
                    var siteIdParts = siteId.Split(',');
                    if (siteIdParts.Length >= 2)
                    {
                        return siteIdParts[1]; // Middle part is Tenant ID
                    }
                }
            }
            catch { }
            
            return null;
        }

        private async Task<(bool Success, string? EnrichedUrl, string? ErrorMessage)> ProcessFileAsync(
            string driveId, string itemId, string fileName, string uploaderEmail, string tenantId)
        {
            // Skip folders - only process files
            try
            {
                var driveItem = await _graph.GetDriveItemAsync(driveId, itemId);
                if (driveItem?.Folder != null)
                {
                    Console.WriteLine($"⏭️ Skipping folder: {fileName}");
                    return (false, null, "Item is a folder, not a file");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Warning: Could not verify item type (will proceed): {ex.Message}");
            }

            try
            {
                // 0. Mark file as "processing" immediately to prevent concurrent processing
                Console.WriteLine($"📝 [METADATA] Marking file as 'Processing' to prevent duplicate processing...");
                try
                {
                    await _graph.UpdateListItemFieldsAsync(driveId, itemId, new Dictionary<string, object>
                    {
                        {"SMEPilot_Status", "Processing"},
                        {"SMEPilot_Enriched", false}
                    });
                    Console.WriteLine($"✅ [METADATA] File marked as 'Processing'");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ [METADATA] Could not mark file as Processing (will continue): {ex.Message}");
                }

                // 1. Download file
                using var fileStream = await _graph.DownloadFileStreamAsync(driveId, itemId);

                // Check file size - if large, return 202 Accepted for async processing
                if (fileStream.Length > 4 * 1024 * 1024)
                {
                    Console.WriteLine($"File {fileName} is too large ({fileStream.Length} bytes), will process asynchronously");
                    return (false, null, "File too large for single-run processing");
                }

                // 2. Extract docx text & images
                var (text, imagesBytes) = await _extractor.ExtractDocxAsync(fileStream);

                // 3. Optionally OCR images here (skipped in POC)
                var imageOcrs = new List<string>();

                // 4. Call OpenAI to get sections JSON
                string fileId = Guid.NewGuid().ToString();
                string json;
                Console.WriteLine($"📄 [ENRICHMENT] Starting AI enrichment for file: {fileName}");
                Console.WriteLine($"   File ID: {fileId}");
                Console.WriteLine($"   Extracted text length: {text?.Length ?? 0} characters");
                Console.WriteLine($"   Images extracted: {imagesBytes?.Count ?? 0}");
                
                try
                {
                    json = await _openai.GenerateSectionsJsonAsync(text, imageOcrs, fileId);
                    Console.WriteLine($"✅ [ENRICHMENT] AI sectioning completed successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ [ENRICHMENT] AI sectioning failed: {ex.Message}");
                    Console.WriteLine($"   Exception type: {ex.GetType().Name}");
                    await _graph.UpdateListItemFieldsAsync(driveId, itemId, new Dictionary<string, object>
                    {
                        {"SMEPilot_Enriched", false},
                        {"SMEPilot_Status", "ManualReview"},
                        {"SMEPilot_EnrichedJobId", fileId}
                    });
                    return (false, null, $"OpenAI failed to return valid JSON: {ex.Message}");
                }

                var docModel = JsonConvert.DeserializeObject<DocumentModel>(json);
                if (docModel == null)
                {
                    return (false, null, "OpenAI returned invalid JSON (post-parse)");
                }

                // 5. Build enriched docx bytes
                var enrichedBytes = TemplateBuilder.BuildDocxBytes(docModel, imagesBytes);
                var enrichedName = Path.GetFileNameWithoutExtension(fileName) + "_enriched.docx";

                // 6. Upload to ProcessedDocs
                var uploaded = await _graph.UploadFileBytesAsync(driveId, _cfg.EnrichedFolderRelativePath, enrichedName, enrichedBytes);

                // 7. Create embeddings and store
                Console.WriteLine($"🔍 [ENRICHMENT] Creating embeddings for {docModel.Sections.Count} sections...");
                var embeddingCount = 0;
                foreach (var s in docModel.Sections)
                {
                    var textForEmb = string.IsNullOrWhiteSpace(s.Summary) ? s.Body : s.Summary;
                    Console.WriteLine($"   Creating embedding for section: {s.Heading} (text length: {textForEmb?.Length ?? 0})");
                    
                    try
                    {
                        var emb = await _openai.GetEmbeddingAsync(textForEmb);
                        embeddingCount++;

                        var embDoc = new EmbeddingDocument
                        {
                            id = Guid.NewGuid().ToString(),
                            TenantId = tenantId,
                            FileId = uploaded.Id,
                            FileUrl = uploaded.WebUrl,
                            SectionId = s.Id ?? Guid.NewGuid().ToString(),
                            Heading = s.Heading,
                            Summary = s.Summary,
                            Body = s.Body,
                            Embedding = emb,
                            CreatedAt = DateTime.UtcNow
                        };
                        await _cosmos.UpsertEmbeddingAsync(embDoc);
                        Console.WriteLine($"   ✅ Embedding created and stored for section: {s.Heading}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"   ⚠️ Failed to create embedding for section '{s.Heading}': {ex.Message}");
                        // Continue with other sections even if one fails
                    }
                }
                Console.WriteLine($"✅ [ENRICHMENT] Created {embeddingCount}/{docModel.Sections.Count} embeddings");

                // 8. Update original item metadata (mark as processed to prevent reprocessing)
                Console.WriteLine($"📝 [METADATA] Updating SharePoint metadata to mark file as processed...");
                var metadata = new Dictionary<string, object>
                {
                    {"SMEPilot_Enriched", true},
                    {"SMEPilot_Status", "Completed"},
                    {"SMEPilot_EnrichedFileUrl", uploaded.WebUrl},
                    {"SMEPilot_EnrichedJobId", fileId},
                    {"SMEPilot_Confidence", 0.0}
                };
                await _graph.UpdateListItemFieldsAsync(driveId, itemId, metadata);
                Console.WriteLine($"✅ [METADATA] Successfully marked file as processed (SMEPilot_Enriched=true)");

                Console.WriteLine($"Successfully processed {fileName}, enriched document: {uploaded.WebUrl}");
                return (true, uploaded.WebUrl, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file {fileName}: {ex.Message}");
                return (false, null, ex.Message);
            }
        }
    }
}
