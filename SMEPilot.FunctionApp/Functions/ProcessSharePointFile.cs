using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SMEPilot.FunctionApp.Helpers;
using SMEPilot.FunctionApp.Models;
using SMEPilot.FunctionApp.Services;
using System.Collections.Generic;
using System.Text;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace SMEPilot.FunctionApp.Functions
{
    public class ProcessSharePointFile
    {
        private readonly GraphHelper _graph;
        private readonly SimpleExtractor _extractor; 
        private readonly Config _cfg;
        private readonly HybridEnricher? _hybridEnricher;
        private readonly OcrHelper? _ocrHelper;
        private readonly ILogger<ProcessSharePointFile> _logger;
        
        // In-memory semaphore to prevent concurrent processing of the same file
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _processingLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
        
        // Notification deduplication: Track processed notifications to avoid duplicate webhook triggers
        // Key: subscriptionId:resource:changeType, Value: timestamp
        private static readonly ConcurrentDictionary<string, DateTime> _processedNotifications = new ConcurrentDictionary<string, DateTime>();
        // Note: Dedup window is now configurable via Config.NotificationDedupWindowSeconds

        // Add CORS headers to allow requests from SharePoint
        private void AddCorsHeaders(HttpResponseData response, HttpRequestData request)
        {
            // Get origin from request header
            if (request.Headers.TryGetValues("Origin", out var origins))
            {
                var origin = origins.FirstOrDefault();
                if (!string.IsNullOrEmpty(origin))
                {
                    response.Headers.Add("Access-Control-Allow-Origin", origin);
                }
            }
            else
            {
                // Default: allow all origins (for development)
                response.Headers.Add("Access-Control-Allow-Origin", "*");
            }
            
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
            response.Headers.Add("Access-Control-Max-Age", "3600");
        }

        public ProcessSharePointFile(
            GraphHelper graph, 
            SimpleExtractor extractor, 
            Config cfg, 
            ILogger<ProcessSharePointFile> logger,
            HybridEnricher? hybridEnricher = null, 
            OcrHelper? ocrHelper = null)
        {
            _graph = graph;
            _extractor = extractor;
            _cfg = cfg;
            _logger = logger;
            _hybridEnricher = hybridEnricher;
            _ocrHelper = ocrHelper;
        }

        [Function("ProcessSharePointFile")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "options")] HttpRequestData req)
        {
            // Handle CORS preflight requests
            if (req.Method == "OPTIONS")
            {
                var corsResp = req.CreateResponse(HttpStatusCode.OK);
                AddCorsHeaders(corsResp, req);
                return corsResp;
            }

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
                        _logger.LogInformation("=== VALIDATION REQUEST (Subscription Setup) ===");

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
                        AddCorsHeaders(vresp, req);
                        await vresp.WriteStringAsync(validationToken);
                        _logger.LogInformation("✅ Validation token returned to Graph API");
                        _logger.LogInformation("=== END VALIDATION ===");
                        return vresp;
                    }
                }

                // If GET request without validation token, return OK (for Graph API health checks)
                if (req.Method == "GET")
                {
                    _logger.LogInformation("Health check request received");
                    var okResp = req.CreateResponse(HttpStatusCode.OK);
                    AddCorsHeaders(okResp, req);
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
                        _logger.LogDebug("Received Graph notification with {Count} items", graphNotification.Value.Count);
                        _logger.LogDebug("Notification body: {Body}", body.Substring(0, Math.Min(500, body.Length)));
                        
                        // Process each notification item
                        var processedCount = 0;
                        foreach (var notification in graphNotification.Value)
                        {
                            _logger.LogDebug("=== Processing Notification Item ===");
                            _logger.LogDebug("Subscription ID: {SubscriptionId}, Change Type: {ChangeType}, Resource: {Resource}", 
                                notification.SubscriptionId, notification.ChangeType, notification.Resource);
                            
                            if (notification.ResourceData == null)
                            {
                                _logger.LogWarning("⚠️ Notification has no resource data: {SubscriptionId}", notification.SubscriptionId);
                                _logger.LogDebug("Full notification JSON: {Json}", JsonConvert.SerializeObject(notification));
                                continue;
                            }
                            
                            // Process "updated" events but filter out duplicates using idempotency check
                            // Graph API only supports "updated" for drive subscriptions (not "created")
                            // We use metadata check + semaphore lock to prevent duplicate processing
                            if (notification.ChangeType != "updated")
                            {
                                _logger.LogInformation("⏭️ Skipping {ChangeType} event - only processing 'updated' events", notification.ChangeType);
                                continue;
                            }
                            
                            // Early deduplication: Check if we've seen this exact notification recently
                            // Include itemId in key if available to make deduplication more precise
                            var itemIdForDedup = notification.ResourceData?.Id ?? "";
                            var notificationKey = string.IsNullOrWhiteSpace(itemIdForDedup) 
                                ? $"{notification.SubscriptionId}:{notification.Resource}:{notification.ChangeType}"
                                : $"{notification.SubscriptionId}:{notification.Resource}:{notification.ChangeType}:{itemIdForDedup}";
                            
                            if (_processedNotifications.TryGetValue(notificationKey, out var lastProcessed))
                            {
                                var timeSinceLastProcessed = DateTime.UtcNow - lastProcessed;
                                var dedupWindow = TimeSpan.FromSeconds(_cfg.NotificationDedupWindowSeconds);
                                if (timeSinceLastProcessed < dedupWindow)
                                {
                                    _logger.LogInformation("⏭️ [DEDUP] Duplicate notification detected (processed {Seconds:F1}s ago, window: {WindowSeconds}s), skipping. Key: {Key}", 
                                        timeSinceLastProcessed.TotalSeconds, _cfg.NotificationDedupWindowSeconds, notificationKey);
                                    continue;
                                }
                            }
                            
                            // Mark notification as processed (with timestamp)
                            _processedNotifications.AddOrUpdate(notificationKey, DateTime.UtcNow, (key, oldValue) => DateTime.UtcNow);
                            
                            // Cleanup old entries (older than 5 minutes) to prevent memory leak
                            var cutoffTime = DateTime.UtcNow - TimeSpan.FromMinutes(5);
                            var keysToRemove = _processedNotifications
                                .Where(kvp => kvp.Value < cutoffTime)
                                .Select(kvp => kvp.Key)
                                .ToList();
                            foreach (var key in keysToRemove)
                            {
                                _processedNotifications.TryRemove(key, out _);
                            }
                            
                            _logger.LogDebug("ResourceData ID: {Id}, Name: {Name}, DriveId: {DriveId}", 
                                notification.ResourceData.Id, notification.ResourceData.Name, notification.ResourceData.DriveId);
                            
                            // Extract file details from Graph notification
                            string driveId = notification.ResourceData.DriveId ?? "";
                            string itemId = notification.ResourceData.Id ?? "";
                            string fileName = notification.ResourceData.Name ?? "";
                            
                            // CRITICAL FIX: Skip enriched files (files we created) - they end with "_enriched"
                            if (!string.IsNullOrWhiteSpace(fileName) && 
                                (fileName.EndsWith("_enriched.docx", StringComparison.OrdinalIgnoreCase) ||
                                 fileName.EndsWith("_enriched.pptx", StringComparison.OrdinalIgnoreCase) ||
                                 fileName.EndsWith("_enriched.xlsx", StringComparison.OrdinalIgnoreCase) ||
                                 fileName.EndsWith("_enriched.pdf", StringComparison.OrdinalIgnoreCase)))
                            {
                                _logger.LogDebug("⏭️ Skipping enriched file (output file): {FileName}", fileName);
                                continue;
                            }
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
                                    _logger.LogDebug("✅ Extracted driveId from resource: {DriveId}", driveId);
                                }
                            }
                            
                            // If we still don't have file details, query Graph API for recent changes
                            // BUT: Check metadata FIRST before querying (to avoid processing same file multiple times)
                            if (string.IsNullOrWhiteSpace(itemId) && !string.IsNullOrWhiteSpace(driveId))
                            {
                                _logger.LogDebug("⚠️ No itemId in notification, querying Graph API for recent changes in drive {DriveId}...", driveId);
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
                                            
                                            // CRITICAL FIX: Skip enriched files (files we created) - they end with "_enriched"
                                            var itemName = item.Name ?? "";
                                            if (itemName.EndsWith("_enriched.docx", StringComparison.OrdinalIgnoreCase) ||
                                                itemName.EndsWith("_enriched.pptx", StringComparison.OrdinalIgnoreCase) ||
                                                itemName.EndsWith("_enriched.xlsx", StringComparison.OrdinalIgnoreCase) ||
                                                itemName.EndsWith("_enriched.pdf", StringComparison.OrdinalIgnoreCase))
                                            {
                                                _logger.LogDebug("⏭️ Skipping enriched file (output file): {FileName}", itemName);
                                                continue;
                                            }
                                            
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
                                                        _logger.LogDebug("⏭️ Recent file {FileName} already processed, checking next...", item.Name);
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
                                            _logger.LogDebug("⚠️ All recent files have already been processed");
                                            continue;
                                        }
                                        
                                        itemId = candidateFile.Id ?? "";
                                        fileName = candidateFile.Name ?? "unknown";
                                        uploaderEmail = candidateFile.CreatedBy?.User?.DisplayName ?? candidateFile.CreatedBy?.Application?.DisplayName ?? "";
                                        _logger.LogDebug("✅ Found unprocessed file: {FileName} (ID: {ItemId})", fileName, itemId);
                                    }
                                    else
                                    {
                                        // No recent files found - this likely indicates a file deletion
                                        // When a file is deleted, SharePoint sends "updated" notification but file no longer exists
                                        _logger.LogDebug("🗑️ [DELETION] No recent files found in drive - likely file deletion event. Skipping processing.");
                                        continue;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "❌ Error querying Graph API for recent items: {Error}", ex.Message);
                                    continue;
                                }
                            }

                            if (string.IsNullOrWhiteSpace(driveId) || string.IsNullOrWhiteSpace(itemId))
                            {
                                _logger.LogWarning("⚠️ Missing driveId or itemId. DriveId: {DriveId}, ItemId: {ItemId}", driveId, itemId);
                                _logger.LogDebug("Full notification JSON: {Json}", JsonConvert.SerializeObject(notification));
                                continue;
                            }

                            // Get tenant ID from resource path or use default
                            var tenantId = ExtractTenantIdFromResource(notification.Resource) ?? "default";

                            _logger.LogDebug("✅ Processing Graph notification: File {FileName} (ID: {ItemId}) in Drive {DriveId}, ChangeType: {ChangeType}", 
                                fileName, itemId, driveId, notification.ChangeType);

                            // CRITICAL: Check metadata BEFORE acquiring lock (early idempotency check)
                            // This prevents unnecessary lock acquisition for already-processed files
                            // Use LogDebug to avoid console output (only in log files)
                            _logger.LogInformation("🔍 [IDEMPOTENCY] Early check for file {FileName} (ItemId: {ItemId})", fileName, itemId);
                            bool shouldSkip = false;
                            try
                            {
                                var existingMetadata = await _graph.GetListItemFieldsAsync(driveId, itemId);
                                if (existingMetadata != null)
                                {
                                    _logger.LogInformation("📋 [IDEMPOTENCY] Metadata found for {FileName}. Keys: {Keys}", fileName, string.Join(", ", existingMetadata.Keys));
                                    
                                    // Check if already enriched
                                    if (existingMetadata.ContainsKey("SMEPilot_Enriched"))
                                    {
                                        var enrichedValue = existingMetadata["SMEPilot_Enriched"]?.ToString();
                                        _logger.LogInformation("🔍 [IDEMPOTENCY] SMEPilot_Enriched value: '{EnrichedValue}' (Type: {Type})", enrichedValue, enrichedValue?.GetType().Name ?? "null");
                                        var isEnriched = enrichedValue == "True" || enrichedValue == "true" || enrichedValue == "1";
                                        if (isEnriched)
                                        {
                                            _logger.LogInformation("⏭️ [IDEMPOTENCY] File {FileName} already processed (SMEPilot_Enriched={EnrichedValue}), skipping", fileName, enrichedValue);
                                            shouldSkip = true;
                                        }
                                        else
                                        {
                                            _logger.LogInformation("🔄 [IDEMPOTENCY] File {FileName} not enriched yet (SMEPilot_Enriched={EnrichedValue}), will process", fileName, enrichedValue);
                                        }
                                    }
                                    else
                                    {
                                        _logger.LogInformation("🔄 [IDEMPOTENCY] File {FileName} has no SMEPilot_Enriched field, will process", fileName);
                                    }
                                    
                                    // Check if currently processing or failed (prevents race conditions and infinite retries)
                                    if (!shouldSkip && existingMetadata.ContainsKey("SMEPilot_Status"))
                                    {
                                        var statusValue = existingMetadata["SMEPilot_Status"]?.ToString();
                                        _logger.LogInformation("🔍 [IDEMPOTENCY] SMEPilot_Status value: '{StatusValue}'", statusValue);
                                        if (statusValue == "Processing")
                                        {
                                            _logger.LogInformation("⏭️ [IDEMPOTENCY] File {FileName} is currently being processed (SMEPilot_Status=Processing), skipping to avoid duplicate", fileName);
                                            shouldSkip = true;
                                        }
                                        else if (statusValue == "Failed")
                                        {
                                            var errorMessage = existingMetadata.ContainsKey("SMEPilot_ErrorMessage") 
                                                ? existingMetadata["SMEPilot_ErrorMessage"]?.ToString() 
                                                : "Unknown error";
                                            _logger.LogInformation("⏭️ [IDEMPOTENCY] File {FileName} previously failed (SMEPilot_Status=Failed, Error: {Error}), skipping to prevent infinite retries", fileName, errorMessage);
                                            shouldSkip = true;
                                        }
                                        else if (statusValue == "MetadataUpdateFailed")
                                        {
                                            _logger.LogInformation("⏭️ [IDEMPOTENCY] File {FileName} was enriched but metadata save failed (SMEPilot_Status=MetadataUpdateFailed), skipping to prevent reprocessing", fileName);
                                            shouldSkip = true;
                                        }
                                        else if (statusValue == "Retry")
                                        {
                                            // Transient failure - allow retry (but check if too many retries)
                                            var lastErrorTime = existingMetadata.ContainsKey("SMEPilot_LastErrorTime") 
                                                ? existingMetadata["SMEPilot_LastErrorTime"]?.ToString() 
                                                : null;
                                            if (!string.IsNullOrWhiteSpace(lastErrorTime) && DateTime.TryParse(lastErrorTime, out var errorTime))
                                            {
                                                var timeSinceError = DateTime.UtcNow - errorTime;
                                                if (timeSinceError.TotalMinutes < _cfg.RetryWaitMinutes)
                                                {
                                                    _logger.LogInformation("⏭️ [IDEMPOTENCY] File {FileName} in retry state (last error {Minutes:F1} minutes ago, wait: {WaitMinutes} min), waiting before retry...", 
                                                        fileName, timeSinceError.TotalMinutes, _cfg.RetryWaitMinutes);
                                                    shouldSkip = true;
                                                }
                                                else
                                                {
                                                    _logger.LogInformation("✅ [IDEMPOTENCY] File {FileName} in retry state, enough time has passed ({Minutes:F1} minutes), will retry", fileName, timeSinceError.TotalMinutes);
                                                }
                                            }
                                            else
                                            {
                                                _logger.LogInformation("✅ [IDEMPOTENCY] File {FileName} in retry state (no timestamp), will retry", fileName);
                                            }
                                        }
                                    }
                                    
                                    if (!shouldSkip)
                                    {
                                        _logger.LogInformation("✅ [IDEMPOTENCY] File {FileName} is ready to process (not enriched, not processing)", fileName);
                                    }
                                }
                                else
                                {
                                    _logger.LogInformation("🔄 [IDEMPOTENCY] File {FileName} has no metadata at all, will process", fileName);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "⚠️ [IDEMPOTENCY] Error checking metadata for {FileName}: {Error}. Will proceed with processing to avoid missing files", fileName, ex.Message);
                            }
                            
                            if (shouldSkip)
                            {
                                continue; // Skip this notification
                            }
                            
                            // Use in-memory semaphore to prevent concurrent processing of the same file
                            var lockKey = $"{driveId}:{itemId}";
                            var semaphore = _processingLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
                            
                            // Try to acquire lock (non-blocking check first)
                            if (!await semaphore.WaitAsync(0))
                            {
                                _logger.LogInformation("⏭️ [CONCURRENCY] File {FileName} is already being processed by another notification, skipping", fileName);
                                continue;
                            }
                            
                            try
                            {
                                // Double-check metadata INSIDE lock (race condition protection)
                                _logger.LogInformation("🔍 [IDEMPOTENCY] Double-checking metadata inside lock for {FileName} (ItemId: {ItemId})", fileName, itemId);
                                try
                                {
                                    var existingMetadata = await _graph.GetListItemFieldsAsync(driveId, itemId);
                                    if (existingMetadata != null)
                                    {
                                        _logger.LogInformation("📋 [IDEMPOTENCY] Double-check metadata found. Keys: {Keys}", string.Join(", ", existingMetadata.Keys));
                                        
                                        // Check if already enriched (another process might have completed)
                                        if (existingMetadata.ContainsKey("SMEPilot_Enriched"))
                                        {
                                            var enrichedValue = existingMetadata["SMEPilot_Enriched"]?.ToString();
                                            _logger.LogInformation("🔍 [IDEMPOTENCY] Double-check SMEPilot_Enriched: '{EnrichedValue}' (Type: {Type})", enrichedValue, enrichedValue?.GetType().Name ?? "null");
                                            var isEnriched = enrichedValue == "True" || enrichedValue == "true" || enrichedValue == "1";
                                            if (isEnriched)
                                            {
                                                _logger.LogInformation("⏭️ [IDEMPOTENCY] File {FileName} was processed by another instance (SMEPilot_Enriched={EnrichedValue}), skipping", fileName, enrichedValue);
                                                continue;
                                            }
                                        }
                                        
                                        // Check if currently processing or failed
                                        if (existingMetadata.ContainsKey("SMEPilot_Status"))
                                        {
                                            var statusValue = existingMetadata["SMEPilot_Status"]?.ToString();
                                            _logger.LogInformation("🔍 [IDEMPOTENCY] Double-check SMEPilot_Status: '{StatusValue}'", statusValue);
                                            if (statusValue == "Processing")
                                            {
                                                _logger.LogInformation("⏭️ [IDEMPOTENCY] File {FileName} is currently being processed (SMEPilot_Status=Processing), skipping", fileName);
                                                continue;
                                            }
                                            else if (statusValue == "Failed")
                                            {
                                                var errorMessage = existingMetadata.ContainsKey("SMEPilot_ErrorMessage") 
                                                    ? existingMetadata["SMEPilot_ErrorMessage"]?.ToString() 
                                                    : "Unknown error";
                                                _logger.LogInformation("⏭️ [IDEMPOTENCY] File {FileName} previously failed (SMEPilot_Status=Failed, Error: {Error}), skipping to prevent infinite retries", fileName, errorMessage);
                                                continue;
                                            }
                                            else if (statusValue == "MetadataUpdateFailed")
                                            {
                                                _logger.LogInformation("⏭️ [IDEMPOTENCY] File {FileName} was enriched but metadata save failed (SMEPilot_Status=MetadataUpdateFailed), skipping to prevent reprocessing", fileName);
                                                continue;
                                            }
                                            else if (statusValue == "Retry")
                                            {
                                                // Transient failure - allow retry (but check if too many retries)
                                                var lastErrorTime = existingMetadata.ContainsKey("SMEPilot_LastErrorTime") 
                                                    ? existingMetadata["SMEPilot_LastErrorTime"]?.ToString() 
                                                    : null;
                                                if (!string.IsNullOrWhiteSpace(lastErrorTime) && DateTime.TryParse(lastErrorTime, out var errorTime))
                                                {
                                                    var timeSinceError = DateTime.UtcNow - errorTime;
                                                    if (timeSinceError.TotalMinutes < _cfg.RetryWaitMinutes)
                                                    {
                                                        _logger.LogInformation("⏭️ [IDEMPOTENCY] File {FileName} in retry state (last error {Minutes:F1} minutes ago, wait: {WaitMinutes} min), waiting before retry...", 
                                                            fileName, timeSinceError.TotalMinutes, _cfg.RetryWaitMinutes);
                                                        continue;
                                                    }
                                                    else
                                                    {
                                                        _logger.LogInformation("✅ [IDEMPOTENCY] File {FileName} in retry state, enough time has passed ({Minutes:F1} minutes), will retry", fileName, timeSinceError.TotalMinutes);
                                                    }
                                                }
                                                else
                                                {
                                                    _logger.LogInformation("✅ [IDEMPOTENCY] File {FileName} in retry state (no timestamp), will retry", fileName);
                                                }
                                            }
                                        }
                                        
                                        _logger.LogInformation("✅ [IDEMPOTENCY] Double-check passed - file {FileName} is ready to process", fileName);
                                    }
                                    else
                                    {
                                        _logger.LogInformation("🔄 [IDEMPOTENCY] Double-check: No metadata found for {FileName}, will process", fileName);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "⚠️ [IDEMPOTENCY] Error in double-check for {FileName}: {Error}. Will proceed with processing", fileName, ex.Message);
                                }

                                // Process the file
                                var result = await ProcessFileAsync(driveId, itemId, fileName, uploaderEmail, tenantId);
                                
                                if (result.Success)
                                {
                                    processedCount++;
                                    _logger.LogInformation("✅ Successfully processed {FileName}", fileName);
                                }
                                else
                                {
                                    _logger.LogError("❌ Failed to process {FileName}: {ErrorMessage}", fileName, result.ErrorMessage);
                                    
                                    // Determine if this is a permanent failure (should not retry) or transient (can retry)
                                    bool isPermanentFailure = result.ErrorMessage != null && (
                                        result.ErrorMessage.Contains("Unsupported file type") ||
                                        result.ErrorMessage.Contains("not supported") ||
                                        result.ErrorMessage.Contains("Old format") ||
                                        result.ErrorMessage.Contains("convert to")
                                    );
                                    
                                    if (isPermanentFailure)
                                    {
                                        // Permanent failure (e.g., unsupported file type) - mark as Failed to prevent retries
                                        try
                                        {
                                            _logger.LogInformation("📝 [METADATA] Setting permanent failure metadata for {FileName} (ItemId: {ItemId}) to prevent reprocessing...", fileName, itemId);
                                            var failureMetadata = new Dictionary<string, object>
                                            {
                                                {"SMEPilot_Enriched", false},
                                                {"SMEPilot_Status", "Failed"},
                                                {"SMEPilot_ErrorMessage", result.ErrorMessage ?? "Unknown error"}
                                            };
                                            
                                            await _graph.UpdateListItemFieldsAsync(driveId, itemId, failureMetadata);
                                            _logger.LogInformation("✅ [METADATA] Successfully marked {FileName} (ItemId: {ItemId}) as Failed (permanent) to prevent reprocessing", fileName, itemId);
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogWarning(ex, "⚠️ [METADATA] Could not set failure metadata for {FileName} (ItemId: {ItemId}): {Error}. File may be reprocessed.", fileName, itemId, ex.Message);
                                        }
                                    }
                                    else
                                    {
                                        // Transient failure (e.g., network error) - mark as "Retry" so it can be retried later
                                        try
                                        {
                                            _logger.LogInformation("📝 [METADATA] Setting transient failure metadata for {FileName} (ItemId: {ItemId}) - will retry on next notification...", fileName, itemId);
                                            var retryMetadata = new Dictionary<string, object>
                                            {
                                                {"SMEPilot_Enriched", false},
                                                {"SMEPilot_Status", "Retry"},
                                                {"SMEPilot_ErrorMessage", result.ErrorMessage ?? "Unknown error"},
                                                {"SMEPilot_LastErrorTime", DateTime.UtcNow.ToString("O")}
                                            };
                                            
                                            await _graph.UpdateListItemFieldsAsync(driveId, itemId, retryMetadata);
                                            _logger.LogInformation("✅ [METADATA] Successfully marked {FileName} (ItemId: {ItemId}) as Retry - will attempt again on next notification", fileName, itemId);
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogWarning(ex, "⚠️ [METADATA] Could not set retry metadata for {FileName} (ItemId: {ItemId}): {Error}. File may be reprocessed.", fileName, itemId, ex.Message);
                                        }
                                    }
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
                        AddCorsHeaders(accepted, req);
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
                    _logger.LogDebug("Not a Graph notification format, trying manual payload");
                }

                // Step 4: Try to parse as manual SharePointEvent payload (for testing)
                var evt = JsonConvert.DeserializeObject<SharePointEvent>(body);
                if (evt == null || string.IsNullOrWhiteSpace(evt.driveId) || string.IsNullOrWhiteSpace(evt.itemId))
                {
                    _logger.LogWarning("Invalid event payload received. Body length: {Length}", body.Length);
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    AddCorsHeaders(bad, req);
                    await bad.WriteStringAsync(JsonConvert.SerializeObject(new
                    {
                        error = "Invalid event payload",
                        received = body.Length > 0 ? "Non-empty body received" : "Empty body",
                        hint = "Expected Graph notification or SharePointEvent format"
                    }));
                    return bad;
                }

                // Process manual payload
                _logger.LogInformation("Processing manual payload: File {FileName} (ID: {ItemId})", evt.fileName, evt.itemId);
                var manualResult = await ProcessFileAsync(evt.driveId, evt.itemId, evt.fileName, evt.uploaderEmail, evt.tenantId ?? "default");
                
                if (!manualResult.Success)
                {
                    _logger.LogError("Failed to process manual payload: {FileName} - {ErrorMessage}", evt.fileName, manualResult.ErrorMessage);
                    var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                    AddCorsHeaders(err, req);
                    await err.WriteStringAsync(JsonConvert.SerializeObject(new { error = manualResult.ErrorMessage }));
                    return err;
                }

                _logger.LogInformation("Successfully processed manual payload: {FileName}", evt.fileName);
                var ok = req.CreateResponse(HttpStatusCode.OK);
                AddCorsHeaders(ok, req);
                await ok.WriteStringAsync(JsonConvert.SerializeObject(new { enrichedUrl = manualResult.EnrichedUrl }));
                return ok;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR in ProcessSharePointFile: {Error}", ex.Message);
                var res = req.CreateResponse(HttpStatusCode.InternalServerError);
                AddCorsHeaders(res, req);
                await res.WriteStringAsync(JsonConvert.SerializeObject(new
                {
                    error = ex.Message,
                    type = ex.GetType().Name,
                    message = "Check log files for details"
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
            // Input validation
            if (string.IsNullOrWhiteSpace(driveId))
            {
                _logger.LogError("❌ [VALIDATION] driveId is null or empty");
                return (false, null, "driveId is required");
            }
            if (string.IsNullOrWhiteSpace(itemId))
            {
                _logger.LogError("❌ [VALIDATION] itemId is null or empty");
                return (false, null, "itemId is required");
            }
            if (string.IsNullOrWhiteSpace(fileName))
            {
                _logger.LogError("❌ [VALIDATION] fileName is null or empty");
                return (false, null, "fileName is required");
            }
            
            // Sanitize file name (remove path traversal attempts)
            var sanitizedFileName = Path.GetFileName(fileName);
            if (sanitizedFileName != fileName)
            {
                _logger.LogWarning("⚠️ [VALIDATION] File name contains path separators, sanitized: {Original} -> {Sanitized}", fileName, sanitizedFileName);
                fileName = sanitizedFileName;
            }
            
            // Validate driveId and itemId format (basic check)
            if (driveId.Length > 100 || itemId.Length > 100)
            {
                _logger.LogError("❌ [VALIDATION] driveId or itemId exceeds maximum length");
                return (false, null, "Invalid driveId or itemId format");
            }
            // Skip folders - only process files
            // Also check if file was deleted (item no longer exists)
            try
            {
                var driveItem = await _graph.GetDriveItemAsync(driveId, itemId);
                if (driveItem == null)
                {
                    _logger.LogDebug("🗑️ [DELETION] File {FileName} (ID: {ItemId}) no longer exists - likely deleted. Skipping processing.", fileName, itemId);
                    return (false, null, "File was deleted and no longer exists");
                }
                if (driveItem.Folder != null)
                {
                    _logger.LogDebug("⏭️ Skipping folder: {FileName}", fileName);
                    return (false, null, "Item is a folder, not a file");
                }
            }
            catch (Exception ex)
            {
                // If we get a 404 or item not found error, it's likely a deletion
                if (ex is ODataError odataError && (odataError.Error?.Code == "itemNotFound" || odataError.Error?.Code == "NotFound"))
                {
                    _logger.LogDebug("🗑️ [DELETION] File {FileName} (ID: {ItemId}) not found - likely deleted. Skipping processing.", fileName, itemId);
                    return (false, null, "File was deleted and no longer exists");
                }
                _logger.LogWarning(ex, "⚠️ Warning: Could not verify item type (will proceed): {Error}", ex.Message);
            }

            try
            {
                // 0. Mark file as "processing" immediately to prevent concurrent processing
                _logger.LogDebug("📝 [METADATA] Marking file as 'Processing' to prevent duplicate processing...");
                try
                {
                    await _graph.UpdateListItemFieldsAsync(driveId, itemId, new Dictionary<string, object>
                    {
                        {"SMEPilot_Status", "Processing"},
                        {"SMEPilot_Enriched", false}
                    });
                    _logger.LogDebug("✅ [METADATA] File marked as 'Processing'");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ [METADATA] Could not mark file as Processing (will continue): {Error}", ex.Message);
                }

                // 1. Download file
                _logger.LogDebug("📥 [DOWNLOAD] Downloading file: {FileName}", fileName);
                using var fileStream = await _graph.DownloadFileStreamAsync(driveId, itemId);
                _logger.LogDebug("✅ [DOWNLOAD] File downloaded. Size: {Size} bytes", fileStream.Length);

                // Check file size - if large, return 202 Accepted for async processing
                if (fileStream.Length > _cfg.MaxFileSizeBytes)
                {
                    _logger.LogWarning("File {FileName} is too large ({Size} bytes, max: {MaxSize} bytes), will process asynchronously", 
                        fileName, fileStream.Length, _cfg.MaxFileSizeBytes);
                    return (false, null, $"File too large for single-run processing (max: {_cfg.MaxFileSizeBytes / 1024 / 1024}MB)");
                }

                // 2. Extract text & images based on file type
                var fileExtension = Path.GetExtension(fileName).ToLower();
                string text;
                List<byte[]> imagesBytes;
                string tempInputPath = null; // For .docx files, we'll save to temp for DocumentEnricherService
                string fileId = Guid.NewGuid().ToString(); // Generate file ID early for temp file naming
                
                _logger.LogDebug("📄 [EXTRACTION] Detected file type: {FileExtension}", fileExtension);
                
                // For .docx files, save to temp file first (needed for DocumentEnricherService)
                if (fileExtension == ".docx")
                {
                    tempInputPath = Path.Combine(Path.GetTempPath(), $"input_{fileId}_{Path.GetFileName(fileName)}");
                    fileStream.Position = 0;
                    using (var tempFileStream = File.Create(tempInputPath))
                    {
                        await fileStream.CopyToAsync(tempFileStream);
                    }
                    _logger.LogDebug("💾 [EXTRACTION] Saved .docx to temp file for processing: {TempPath}", tempInputPath);
                    
                    // Reset stream position and extract from stream
                    fileStream.Position = 0;
                    (text, imagesBytes) = await _extractor.ExtractDocxAsync(fileStream);
                    _logger.LogDebug("✅ [EXTRACTION] Extracted {TextLength} characters and {ImageCount} images from DOCX", text?.Length ?? 0, imagesBytes?.Count ?? 0);
                }
                else
                {
                    switch (fileExtension)
                    {
                    case ".doc":
                        // Old Word format (.doc) is binary and not supported by OpenXML
                        _logger.LogError("❌ [EXTRACTION] .doc format (old Word format) is not supported. Please convert to .docx format.");
                        return (false, null, "Old Word format (.doc) is not supported. Please convert to .docx format. You can open the file in Word and save as .docx.");
                    case ".pptx":
                        _logger.LogDebug("📊 [EXTRACTION] Extracting from PowerPoint presentation...");
                        (text, imagesBytes) = await _extractor.ExtractPptxAsync(fileStream);
                        _logger.LogDebug("✅ [EXTRACTION] Extracted {TextLength} characters and {ImageCount} images from PPTX", text?.Length ?? 0, imagesBytes?.Count ?? 0);
                        break;
                    case ".ppt":
                        // Old PowerPoint format (.ppt) is binary and not supported by OpenXML
                        _logger.LogError("❌ [EXTRACTION] .ppt format (old PowerPoint format) is not supported. Please convert to .pptx format.");
                        return (false, null, "Old PowerPoint format (.ppt) is not supported. Please convert to .pptx format. You can open the file in PowerPoint and save as .pptx.");
                    case ".pdf":
                        _logger.LogDebug("📄 [EXTRACTION] Extracting from PDF document...");
                        (text, imagesBytes) = await _extractor.ExtractPdfAsync(fileStream);
                        _logger.LogDebug("✅ [EXTRACTION] Extracted {TextLength} characters and {ImageCount} images from PDF", text?.Length ?? 0, imagesBytes?.Count ?? 0);
                        break;
                    case ".xlsx":
                        _logger.LogDebug("📊 [EXTRACTION] Extracting from Excel spreadsheet...");
                        (text, imagesBytes) = await _extractor.ExtractXlsxAsync(fileStream);
                        _logger.LogDebug("✅ [EXTRACTION] Extracted {TextLength} characters and {ImageCount} images from XLSX", text?.Length ?? 0, imagesBytes?.Count ?? 0);
                        break;
                    case ".xls":
                        // Old Excel format (.xls) is binary and not supported by OpenXML
                        _logger.LogError("❌ [EXTRACTION] .xls format (old Excel format) is not supported. Please convert to .xlsx format.");
                        return (false, null, "Old Excel format (.xls) is not supported. Please convert to .xlsx format. You can open the file in Excel and save as .xlsx.");
                    case ".png":
                    case ".jpg":
                    case ".jpeg":
                    case ".gif":
                    case ".bmp":
                    case ".tiff":
                    case ".tif":
                        _logger.LogDebug("🖼️ [EXTRACTION] Processing image file...");
                        (text, imagesBytes) = await _extractor.ExtractImageAsync(fileStream, _ocrHelper);
                        if (_ocrHelper != null && !string.IsNullOrWhiteSpace(text) && !text.Contains("[Image file - OCR not configured"))
                        {
                            _logger.LogDebug("✅ [EXTRACTION] Processed image file with OCR - extracted {TextLength} characters", text.Length);
                        }
                        else
                        {
                            _logger.LogDebug("✅ [EXTRACTION] Processed image file (OCR not configured or no text detected)");
                        }
                        break;
                    default:
                        _logger.LogError("❌ [EXTRACTION] Unsupported file type: {FileExtension}", fileExtension);
                        return (false, null, $"Unsupported file type: {fileExtension}. Supported formats: DOCX, PPTX, XLSX, PDF, Images (PNG, JPG, JPEG, GIF, BMP, TIFF). Note: Old formats (.doc, .ppt, .xls) are not supported - please convert to new formats (.docx, .pptx, .xlsx).");
                    }
                }

                // 3. Optionally OCR images here (skipped in POC)
                var imageOcrs = new List<string>();

                // 4. Rule-based sectioning and template formatting (NO AI, NO DATABASE)
                byte[] enrichedBytes;
                string enrichedName;
                DocumentModel? docModel = null; // Declare at higher scope for classification
                
                _logger.LogDebug("📄 [TEMPLATE] Starting template formatting for file: {FileName}", fileName);
                _logger.LogDebug("   File ID: {FileId}", fileId);
                _logger.LogDebug("   Extracted text length: {TextLength} characters", text?.Length ?? 0);
                _logger.LogDebug("   Images extracted: {ImageCount}", imagesBytes?.Count ?? 0);
                
                // For .docx files, use DocumentEnricherService (production-ready rule-based enrichment)
                if (fileExtension == ".docx")
                {
                    try
                    {
                        _logger.LogDebug("📋 [ENRICHMENT] Using DocumentEnricherService for .docx file...");
                        
                        // Paths (ensure these files exist in the function app deployment package)
                        var repoRoot = AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory();
                        var mappingJsonPath = Path.Combine(repoRoot, "Config", "mapping.json");
                        var templatePath = Path.Combine(repoRoot, "Templates", "SMEPilot_OrgTemplate_RuleBased.dotx");
                        
                        // Create DocumentEnricherService instance
                        var enricher = new DocumentEnricherService(
                            mappingJsonPath, 
                            File.Exists(templatePath) ? templatePath : null);
                        
                        // Use the temp file we already saved during extraction
                        var tempOutputPath = Path.Combine(Path.GetTempPath(), $"enriched_{fileId}_{Path.GetFileNameWithoutExtension(fileName)}_enriched.docx");
                        
                        try
                        {
                            _logger.LogDebug("💾 [ENRICHMENT] Using temp input file: {TempPath}", tempInputPath);
                            
                            // Enrich the document
                            var result = enricher.EnrichFile(tempInputPath, tempOutputPath, uploaderEmail ?? "AutomatedEnricher");
                            
                            if (!result.Success)
                            {
                                _logger.LogError("❌ [ENRICHMENT] DocumentEnricherService failed: {Error}", result.ErrorMessage);
                                await _graph.UpdateListItemFieldsAsync(driveId, itemId, new Dictionary<string, object>
                                {
                                    {"SMEPilot_Enriched", false},
                                    {"SMEPilot_Status", "ManualReview"},
                                    {"SMEPilot_EnrichedJobId", fileId}
                                });
                                return (false, null, $"Document enrichment failed: {result.ErrorMessage}");
                            }
                            
                            // Read enriched file back to bytes
                            enrichedBytes = await File.ReadAllBytesAsync(tempOutputPath);
                            enrichedName = Path.GetFileNameWithoutExtension(fileName) + "_enriched.docx";
                            
                            _logger.LogDebug("✅ [ENRICHMENT] Document enriched successfully. Size: {Size} bytes", enrichedBytes.Length);
                            _logger.LogDebug("   Document Type: {DocType}, Status: {Status}", result.DocumentType, result.Status);
                            
                            // Clean up temp files
                            try
                            {
                                if (File.Exists(tempInputPath)) File.Delete(tempInputPath);
                                if (File.Exists(tempOutputPath)) File.Delete(tempOutputPath);
                            }
                            catch (Exception cleanupEx)
                            {
                                _logger.LogWarning(cleanupEx, "⚠️ [ENRICHMENT] Could not clean up temp files: {Error}", cleanupEx.Message);
                            }
                        }
                        finally
                        {
                            // Ensure cleanup even on error
                            try
                            {
                                if (tempInputPath != null && File.Exists(tempInputPath)) File.Delete(tempInputPath);
                                if (File.Exists(tempOutputPath)) File.Delete(tempOutputPath);
                            }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ [ENRICHMENT] DocumentEnricherService exception: {Error}", ex.Message);
                        await _graph.UpdateListItemFieldsAsync(driveId, itemId, new Dictionary<string, object>
                        {
                            {"SMEPilot_Enriched", false},
                            {"SMEPilot_Status", "ManualReview"},
                            {"SMEPilot_EnrichedJobId", fileId}
                        });
                        return (false, null, $"Document enrichment failed: {ex.Message}");
                    }
                }
                else
                {
                    // For non-.docx files, use existing HybridEnricher + TemplateBuilder flow
                    
                    try
                    {
                        // Step 1: Rule-based sectioning (no AI)
                        if (_hybridEnricher != null)
                        {
                            _logger.LogDebug("📋 [TEMPLATE] Step 1: Rule-based sectioning...");
                            docModel = _hybridEnricher.SectionDocument(text, fileName);
                            _logger.LogDebug("✅ [TEMPLATE] Created {SectionCount} sections using rule-based parsing", docModel.Sections.Count);
                            
                            // Step 2: Classify document (keyword-based, no AI)
                            var classification = _hybridEnricher.ClassifyDocument(docModel.Title, text);
                            _logger.LogDebug("📂 [TEMPLATE] Document classified as: {Classification}", classification);
                        }
                        else
                        {
                            // Fallback: Create simple document model
                            docModel = new DocumentModel
                            {
                                Title = Path.GetFileNameWithoutExtension(fileName),
                                Sections = new List<Section>
                                {
                                    new Section
                                    {
                                        Id = "s1",
                                        Heading = "Content",
                                        Summary = "Document content",
                                        Body = text ?? ""
                                    }
                                },
                                Images = new List<ImageData>()
                            };
                            _logger.LogDebug("✅ [TEMPLATE] Created simple document structure");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ [TEMPLATE] Sectioning failed: {Error}", ex.Message);
                        await _graph.UpdateListItemFieldsAsync(driveId, itemId, new Dictionary<string, object>
                        {
                            {"SMEPilot_Enriched", false},
                            {"SMEPilot_Status", "ManualReview"},
                            {"SMEPilot_EnrichedJobId", fileId}
                        });
                        return (false, null, $"Template formatting failed: {ex.Message}");
                    }

                    // 5. Build enriched docx bytes
                    _logger.LogDebug("📝 [TEMPLATE] Building formatted document...");
                    enrichedBytes = TemplateBuilder.BuildDocxBytes(docModel, imagesBytes);
                    enrichedName = Path.GetFileNameWithoutExtension(fileName) + "_enriched.docx";
                    _logger.LogDebug("✅ [TEMPLATE] Formatted document created. Size: {Size} bytes", enrichedBytes.Length);
                }

                // 6. Upload to ProcessedDocs (with retry for locked files)
                _logger.LogDebug("📤 [UPLOAD] Uploading formatted document to: {FolderPath}", _cfg.EnrichedFolderRelativePath);
                DriveItem? uploaded = null;
                int uploadRetries = 0;
                int maxUploadRetries = _cfg.MaxUploadRetries;
                while (uploadRetries < maxUploadRetries)
                {
                    try
                    {
                        uploaded = await _graph.UploadFileBytesAsync(driveId, _cfg.EnrichedFolderRelativePath, enrichedName, enrichedBytes);
                        _logger.LogDebug("✅ [UPLOAD] File uploaded successfully: {EnrichedName}", enrichedName);
                        break; // Success!
                    }
                    catch (ODataError ex) when (ex.Error?.Code == "notAllowed" && ex.Error?.Message?.Contains("locked") == true)
                    {
                        uploadRetries++;
                        if (uploadRetries >= maxUploadRetries)
                        {
                            _logger.LogWarning("❌ [UPLOAD] File still locked after {Retries} retries. Waiting {WaitSeconds} seconds before final attempt...", 
                                maxUploadRetries, _cfg.FileLockWaitSeconds);
                            await Task.Delay(_cfg.FileLockWaitSeconds * 1000);
                            try
                            {
                                uploaded = await _graph.UploadFileBytesAsync(driveId, _cfg.EnrichedFolderRelativePath, enrichedName, enrichedBytes);
                                _logger.LogDebug("✅ [UPLOAD] File uploaded successfully on final attempt");
                            }
                            catch (Exception finalEx)
                            {
                                _logger.LogError(finalEx, "❌ [UPLOAD] Failed to upload after {TotalAttempts} attempts due to file lock", maxUploadRetries + 1);
                                throw new InvalidOperationException($"Failed to upload after {maxUploadRetries + 1} attempts due to file lock");
                            }
                        }
                        else
                        {
                            var waitTime = (int)Math.Pow(2, uploadRetries) * 1000; // Exponential backoff: 2s, 4s, 8s, 16s
                            _logger.LogWarning("⚠️ [UPLOAD] File locked (attempt {Attempt}/{MaxRetries}). Waiting {WaitTime}s before retry...", uploadRetries, maxUploadRetries, waitTime/1000);
                            await Task.Delay(waitTime);
                        }
                    }
                }
                
                if (uploaded == null)
                {
                    throw new InvalidOperationException("Failed to upload enriched document after retries");
                }

                // 7. Update original item metadata (mark as processed to prevent reprocessing)
                _logger.LogInformation("📝 [METADATA] Updating SharePoint metadata for {FileName} (ItemId: {ItemId}) to mark as processed...", fileName, itemId);
                var metadata = new Dictionary<string, object>
                {
                    {"SMEPilot_Enriched", true},
                    {"SMEPilot_Status", "Completed"},
                    {"SMEPilot_EnrichedFileUrl", uploaded.WebUrl},
                    {"SMEPilot_EnrichedJobId", fileId},
                    {"SMEPilot_Confidence", 0.0}
                };
                
                // Add classification if available (only for non-.docx files where docModel was created)
                if (_hybridEnricher != null && docModel != null)
                {
                    var classification = _hybridEnricher.ClassifyDocument(docModel.Title, text);
                    metadata["SMEPilot_Classification"] = classification;
                    _logger.LogInformation("📂 [METADATA] Document classified as: {Classification}", classification);
                }
                
                _logger.LogInformation("📋 [METADATA] Setting metadata values: SMEPilot_Enriched={Enriched}, SMEPilot_Status={Status}, SMEPilot_EnrichedFileUrl={Url}", 
                    metadata["SMEPilot_Enriched"], metadata["SMEPilot_Status"], metadata["SMEPilot_EnrichedFileUrl"]);
                
                // Update metadata with retry for locked files
                int metadataRetries = 0;
                int maxMetadataRetries = _cfg.MaxMetadataRetries;
                bool metadataUpdateSuccess = false;
                while (metadataRetries < maxMetadataRetries)
                {
                    try
                    {
                        _logger.LogInformation("🔄 [METADATA] Attempting to update metadata (attempt {Attempt}/{MaxRetries}) for {FileName} (ItemId: {ItemId})", 
                            metadataRetries + 1, maxMetadataRetries, fileName, itemId);
                        await _graph.UpdateListItemFieldsAsync(driveId, itemId, metadata);
                        _logger.LogInformation("✅ [METADATA] Successfully marked file {FileName} (ItemId: {ItemId}) as processed (SMEPilot_Enriched=true)", fileName, itemId);
                        metadataUpdateSuccess = true;
                        break; // Success!
                    }
                    catch (ODataError ex) when (ex.Error?.Code == "notAllowed" && ex.Error?.Message?.Contains("locked") == true)
                    {
                        metadataRetries++;
                        _logger.LogWarning("⚠️ [METADATA] File {FileName} (ItemId: {ItemId}) locked during metadata update. Error: {ErrorCode} - {ErrorMessage}", 
                            fileName, itemId, ex.Error?.Code, ex.Error?.Message);
                        if (metadataRetries >= maxMetadataRetries)
                        {
                            _logger.LogWarning("❌ [METADATA] File {FileName} (ItemId: {ItemId}) still locked after {Retries} retries. Document was enriched but metadata update failed.", 
                                fileName, itemId, maxMetadataRetries);
                            _logger.LogWarning("   Enriched document URL: {Url}", uploaded.WebUrl);
                            _logger.LogWarning("   ⚠️ File may be reprocessed on next webhook notification");
                            // Don't throw - document was successfully enriched, just metadata update failed
                            break;
                        }
                        else
                        {
                            var waitTime = (int)Math.Pow(2, metadataRetries) * 1000; // Exponential backoff: 2s, 4s, 8s, 16s
                            _logger.LogWarning("⚠️ [METADATA] File locked (attempt {Attempt}/{MaxRetries}). Waiting {WaitTime}s before retry...", metadataRetries, maxMetadataRetries, waitTime/1000);
                            await Task.Delay(waitTime);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ [METADATA] Unexpected error updating metadata for {FileName} (ItemId: {ItemId}): {Error}", fileName, itemId, ex.Message);
                        metadataRetries++;
                        if (metadataRetries >= maxMetadataRetries)
                        {
                            _logger.LogError("❌ [METADATA] Failed to update metadata after {Retries} retries for {FileName} (ItemId: {ItemId})", maxMetadataRetries, fileName, itemId);
                            break;
                        }
                        await Task.Delay(1000 * metadataRetries); // Simple backoff for other errors
                    }
                }
                
                // Verify metadata was set correctly
                if (metadataUpdateSuccess)
                {
                    try
                    {
                        _logger.LogInformation("🔍 [METADATA] Verifying metadata was set correctly for {FileName} (ItemId: {ItemId})...", fileName, itemId);
                        await Task.Delay(500); // Small delay to allow SharePoint to commit
                        var verifyMetadata = await _graph.GetListItemFieldsAsync(driveId, itemId);
                        if (verifyMetadata != null && verifyMetadata.ContainsKey("SMEPilot_Enriched"))
                        {
                            var verifyValue = verifyMetadata["SMEPilot_Enriched"]?.ToString();
                            _logger.LogInformation("✅ [METADATA] Verification: SMEPilot_Enriched={EnrichedValue} for {FileName} (ItemId: {ItemId})", verifyValue, fileName, itemId);
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ [METADATA] Verification failed: Could not find SMEPilot_Enriched field for {FileName} (ItemId: {ItemId})", fileName, itemId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ [METADATA] Could not verify metadata for {FileName} (ItemId: {ItemId}): {Error}", fileName, itemId, ex.Message);
                    }
                }
                else
                {
                    _logger.LogError("❌ [METADATA] Metadata update FAILED for {FileName} (ItemId: {ItemId}) - file may be reprocessed!", fileName, itemId);
                    
                    // CRITICAL: Try to set a minimal metadata flag to prevent infinite reprocessing
                    // Even if full metadata update failed, try to set at least SMEPilot_Status to prevent reprocessing
                    try
                    {
                        _logger.LogInformation("🔄 [METADATA] Attempting fallback: Setting minimal metadata flag to prevent reprocessing...");
                        var fallbackMetadata = new Dictionary<string, object>
                        {
                            {"SMEPilot_Status", "MetadataUpdateFailed"},
                            {"SMEPilot_Enriched", true}, // Document was enriched, just metadata save failed
                            {"SMEPilot_EnrichedFileUrl", uploaded.WebUrl ?? ""}
                        };
                        await _graph.UpdateListItemFieldsAsync(driveId, itemId, fallbackMetadata);
                        _logger.LogInformation("✅ [METADATA] Fallback metadata set successfully - file will NOT be reprocessed");
                    }
                    catch (Exception fallbackEx)
                    {
                        _logger.LogError(fallbackEx, "❌ [METADATA] Fallback metadata update also failed for {FileName} (ItemId: {ItemId}) - file WILL be reprocessed on next notification", fileName, itemId);
                    }
                }

                _logger.LogDebug("✅ Successfully processed {FileName}, enriched document: {Url}", fileName, uploaded.WebUrl);
                return (true, uploaded.WebUrl, null);
            }
            catch (Exception ex)
            {
                // Enhanced error logging for Graph API errors
                if (ex is ODataError odataError)
                {
                    _logger.LogError(odataError, "❌ [GRAPH ERROR] ODataError processing file {FileName}: Code={Code}, Message={Message}", 
                        fileName, odataError.Error?.Code ?? "Unknown", odataError.Error?.Message ?? ex.Message);
                    if (odataError.Error?.AdditionalData != null)
                    {
                        foreach (var kvp in odataError.Error.AdditionalData)
                        {
                            _logger.LogDebug("   Additional Data: {Key}={Value}", kvp.Key, kvp.Value);
                        }
                    }
                    return (false, null, $"Graph API Error: {odataError.Error?.Code ?? "Unknown"} - {odataError.Error?.Message ?? ex.Message}");
                }
                else
                {
                    _logger.LogError(ex, "❌ Error processing file {FileName}: {ErrorType}: {Message}", fileName, ex.GetType().Name, ex.Message);
                }
                return (false, null, ex.Message);
            }
        }
    }
}
