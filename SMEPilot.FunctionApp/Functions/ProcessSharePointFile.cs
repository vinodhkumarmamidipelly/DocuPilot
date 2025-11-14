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
        private readonly RuleBasedFormatter? _ruleBasedFormatter;
        private readonly ILogger<ProcessSharePointFile> _logger;
        private readonly TelemetryService? _telemetry;
        private readonly NotificationService? _notifications;
        private readonly RateLimitingService? _rateLimiter;
        
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
            OcrHelper? ocrHelper = null,
            RuleBasedFormatter? ruleBasedFormatter = null,
            TelemetryService? telemetry = null,
            NotificationService? notifications = null,
            RateLimitingService? rateLimiter = null)
        {
            _graph = graph;
            _extractor = extractor;
            _cfg = cfg;
            _logger = logger;
            _hybridEnricher = hybridEnricher;
            _ocrHelper = ocrHelper;
            _ruleBasedFormatter = ruleBasedFormatter;
            _telemetry = telemetry;
            _notifications = notifications;
            _rateLimiter = rateLimiter;
        }

        [Function("ProcessSharePointFile")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "options")] HttpRequestData req)
        {
            // CRITICAL: Handle webhook validation FIRST - must respond within 10 seconds!
            // Graph API sends validation token via GET request with query parameter
            // This ONLY happens during initial subscription creation (one-time validation)
            // After that, all notifications come as POST with empty query string (data in body)
            var query = req.Url.Query;
            
            // Check for validation token IMMEDIATELY (before any other processing)
            if (!string.IsNullOrEmpty(query))
            {
                var queryString = query.TrimStart('?');
                var validationTokenMatch = System.Text.RegularExpressions.Regex.Match(queryString, @"validationToken=([^&]*)");

                if (validationTokenMatch.Success)
                {
                    var encodedToken = validationTokenMatch.Groups[1].Value;
                    _logger.LogInformation("=== VALIDATION REQUEST (Subscription Setup) ===");
                    _logger.LogInformation("Received validation token (length: {Length})", encodedToken.Length);

                    // Properly decode URL-encoded token (+ becomes space, %XX becomes characters)
                    var validationToken = encodedToken
                        .Replace("+", " ")
                        .Replace("%3a", ":", StringComparison.OrdinalIgnoreCase)
                        .Replace("%3A", ":")
                        .Replace("%2F", "/")
                        .Replace("%2f", "/");
                    validationToken = Uri.UnescapeDataString(validationToken);

                    // Return validation token as plain text (decoded) - REQUIRED by Graph API
                    // MUST respond within 10 seconds - this is the fastest possible response
                    var vresp = req.CreateResponse(HttpStatusCode.OK);
                    vresp.Headers.Add("Content-Type", "text/plain; charset=utf-8");
                    AddCorsHeaders(vresp, req);
                    await vresp.WriteStringAsync(validationToken);
                    _logger.LogInformation("✅ Validation token returned to Graph API (immediate response)");
                    _logger.LogInformation("=== END VALIDATION ===");
                    return vresp;
                }
            }

            // Handle CORS preflight requests
            if (req.Method == "OPTIONS")
            {
                var corsResp = req.CreateResponse(HttpStatusCode.OK);
                AddCorsHeaders(corsResp, req);
                return corsResp;
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

            // Rate limiting (by IP or source) - AFTER validation check
            var clientIdentifier = req.Headers.TryGetValues("X-Forwarded-For", out var forwardedFor) 
                ? forwardedFor.FirstOrDefault() 
                : req.Headers.TryGetValues("X-Real-IP", out var realIp) 
                    ? realIp.FirstOrDefault() 
                    : "unknown";
            
            if (_rateLimiter != null && _rateLimiter.IsRateLimited(clientIdentifier ?? "unknown", out var rateLimitReason))
            {
                _logger.LogWarning("🚫 [RateLimit] Request rate limited: {Reason}", rateLimitReason);
                var rateLimitResp = req.CreateResponse(HttpStatusCode.TooManyRequests);
                AddCorsHeaders(rateLimitResp, req);
                await rateLimitResp.WriteStringAsync($"Rate limit exceeded: {rateLimitReason}");
                return rateLimitResp;
            }

            try
            {
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

                            // CRITICAL: Capture site ID from file's DriveItem BEFORE loading config (needed for validation)
                            // This is the same approach used in ProcessFileAsync
                            string? siteId = null;
                            try
                            {
                                var driveItem = await _graph.GetDriveItemAsync(driveId, itemId);
                                if (driveItem != null)
                                {
                                    siteId = driveItem.ParentReference?.SiteId;
                                    if (!string.IsNullOrWhiteSpace(siteId))
                                    {
                                        _logger.LogInformation("✅ [SITE_ID] Captured site ID from source file: {SiteId}", siteId);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "⚠️ [SITE_ID] Could not get site ID from file DriveItem: {Error}", ex.Message);
                            }

                            // Fallback: Try to get site ID from drive if not captured from file
                            if (string.IsNullOrWhiteSpace(siteId))
                            {
                                try
                                {
                                    siteId = await _graph.GetSiteIdFromDriveAsync(driveId);
                                    if (!string.IsNullOrWhiteSpace(siteId))
                                    {
                                        _logger.LogInformation("✅ [SITE_ID] Got site ID from drive: {SiteId}", siteId);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "⚠️ [SITE_ID] Could not get site ID from drive: {Error}", ex.Message);
                                }
                            }

                            // Load SharePoint configuration for this site (now that we have siteId)
                            if (!string.IsNullOrWhiteSpace(siteId))
                            {
                                try
                                {
                                    _logger.LogInformation("🔄 [CONFIG] Loading SharePoint configuration for site {SiteId}", siteId);
                                    await _cfg.LoadSharePointConfigAsync(_graph, siteId, _logger);
                                    _logger.LogInformation("✅ [CONFIG] SharePoint configuration loaded. Source: {SourcePath}, Destination: {DestPath}, MaxSize: {MaxSize}MB", 
                                        _cfg.SourceFolderPath, _cfg.EnrichedFolderRelativePath, _cfg.MaxFileSizeBytes / 1024 / 1024);
                                }
                                catch (Exception configEx)
                                {
                                    _logger.LogWarning(configEx, "⚠️ [CONFIG] Failed to load SharePoint configuration. Using environment variables/defaults. Error: {Error}", configEx.Message);
                                    // Continue processing with defaults - don't fail the entire request
                                }
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ [CONFIG] Could not determine siteId. Using environment variables/defaults.");
                            }

                            // Validate that file is in the configured source folder (if configured)
                            if (!string.IsNullOrWhiteSpace(_cfg.SourceFolderPath))
                            {
                                _logger.LogInformation("🔍 [VALIDATION] Starting validation for file {FileName} (ItemId: {ItemId}) against source folder '{SourceFolder}'", 
                                    fileName, itemId, _cfg.SourceFolderPath);
                                try
                                {
                                    var isInSourceFolder = await _graph.IsFileInSourceFolderAsync(driveId, itemId, _cfg.SourceFolderPath, siteId);
                                    
                                    if (isInSourceFolder == false)
                                    {
                                        // File is definitively NOT in source folder - skip processing
                                        _logger.LogInformation("⏭️ [VALIDATION] File {FileName} (ItemId: {ItemId}) is NOT in configured source folder '{SourceFolder}'. Skipping processing.", 
                                            fileName, itemId, _cfg.SourceFolderPath);
                                        continue; // Skip this notification
                                    }
                                    else if (isInSourceFolder == true)
                                    {
                                        _logger.LogInformation("✅ [VALIDATION] File {FileName} (ItemId: {ItemId}) IS in configured source folder '{SourceFolder}'. Proceeding with processing.", 
                                            fileName, itemId, _cfg.SourceFolderPath);
                                    }
                                    else if (isInSourceFolder == null)
                                    {
                                        // Couldn't determine - fail open and allow processing
                                        _logger.LogWarning("⚠️ [VALIDATION] Could not determine if file {FileName} (ItemId: {ItemId}) is in source folder '{SourceFolder}'. Allowing processing (fail open).", 
                                            fileName, itemId, _cfg.SourceFolderPath);
                                    }
                                }
                                catch (Exception validationEx)
                                {
                                    _logger.LogWarning(validationEx, "⚠️ [VALIDATION] Error validating source folder for file {FileName} (ItemId: {ItemId}): {Error}. Allowing processing (fail open).", 
                                        fileName, itemId, validationEx.Message);
                                    // Fail open - if validation fails, allow processing to continue
                                }
                            }
                            else
                            {
                                _logger.LogInformation("ℹ️ [VALIDATION] Source folder path not configured, skipping validation for file {FileName} (ItemId: {ItemId})", fileName, itemId);
                            }

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
                                    
                                    // Check if already enriched - with versioning detection
                                    if (existingMetadata.ContainsKey("SMEPilot_Enriched"))
                                    {
                                        var enrichedValue = existingMetadata["SMEPilot_Enriched"]?.ToString();
                                        _logger.LogInformation("🔍 [IDEMPOTENCY] SMEPilot_Enriched value: '{EnrichedValue}' (Type: {Type})", enrichedValue, enrichedValue?.GetType().Name ?? "null");
                                        var isEnriched = enrichedValue == "True" || enrichedValue == "true" || enrichedValue == "1";
                                        
                                        if (isEnriched)
                                        {
                                            // Versioning detection: Check if file was modified after last enrichment
                                            var driveItem = await _graph.GetDriveItemAsync(driveId, itemId);
                                            if (driveItem?.LastModifiedDateTime != null)
                                            {
                                                var lastModified = driveItem.LastModifiedDateTime.Value.DateTime;
                                                
                                                // Get last enriched time from metadata
                                                DateTime? lastEnrichedTime = null;
                                                if (existingMetadata.ContainsKey("SMEPilot_LastEnrichedTime"))
                                                {
                                                    var lastEnrichedValue = existingMetadata["SMEPilot_LastEnrichedTime"];
                                                    if (lastEnrichedValue != null)
                                                    {
                                                        if (lastEnrichedValue is DateTime dt)
                                                        {
                                                            lastEnrichedTime = dt;
                                                        }
                                                        else if (DateTime.TryParse(lastEnrichedValue.ToString(), out var parsedTime))
                                                        {
                                                            lastEnrichedTime = parsedTime;
                                                        }
                                                    }
                                                }
                                                
                                                if (lastEnrichedTime.HasValue)
                                                {
                                                    if (lastModified > lastEnrichedTime.Value)
                                                    {
                                                        // File was modified after last enrichment - reprocess (new version)
                                                        _logger.LogInformation("🔄 [VERSIONING] File {FileName} was modified after last enrichment (LastModified: {LastModified}, LastEnriched: {LastEnriched}). Reprocessing as new version.", 
                                                            fileName, lastModified, lastEnrichedTime.Value);
                                                        shouldSkip = false; // Process the new version
                                                    }
                                                    else if (lastModified == lastEnrichedTime.Value || Math.Abs((lastModified - lastEnrichedTime.Value).TotalSeconds) < 5)
                                                    {
                                                        // File unchanged - skip (duplicate)
                                                        _logger.LogInformation("⏭️ [IDEMPOTENCY] File {FileName} already processed and unchanged (LastModified: {LastModified}, LastEnriched: {LastEnriched}). Skipping duplicate.", 
                                                            fileName, lastModified, lastEnrichedTime.Value);
                                                        shouldSkip = true;
                                                    }
                                                    else
                                                    {
                                                        // File modified before last enrichment (shouldn't happen, but handle gracefully)
                                                        _logger.LogWarning("⚠️ [VERSIONING] File {FileName} LastModified ({LastModified}) is before LastEnriched ({LastEnriched}). This is unexpected. Skipping.", 
                                                            fileName, lastModified, lastEnrichedTime.Value);
                                                        shouldSkip = true;
                                                    }
                                                }
                                                else
                                                {
                                                    // No LastEnrichedTime - treat as already processed (legacy file)
                                                    _logger.LogInformation("⏭️ [IDEMPOTENCY] File {FileName} already processed (SMEPilot_Enriched={EnrichedValue}) but no LastEnrichedTime. Skipping.", fileName, enrichedValue);
                                                    shouldSkip = true;
                                                }
                                            }
                                            else
                                            {
                                                // Can't get LastModified - skip to be safe
                                                _logger.LogInformation("⏭️ [IDEMPOTENCY] File {FileName} already processed (SMEPilot_Enriched={EnrichedValue}), skipping", fileName, enrichedValue);
                                                shouldSkip = true;
                                            }
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
                                        else if (statusValue == "Retrying" || statusValue == "Retry") // Support both for backward compatibility
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
                                        
                                        // Check if already enriched (another process might have completed) - with versioning detection
                                        if (existingMetadata.ContainsKey("SMEPilot_Enriched"))
                                        {
                                            var enrichedValue = existingMetadata["SMEPilot_Enriched"]?.ToString();
                                            _logger.LogInformation("🔍 [IDEMPOTENCY] Double-check SMEPilot_Enriched: '{EnrichedValue}' (Type: {Type})", enrichedValue, enrichedValue?.GetType().Name ?? "null");
                                            var isEnriched = enrichedValue == "True" || enrichedValue == "true" || enrichedValue == "1";
                                            if (isEnriched)
                                            {
                                                // Versioning detection: Check if file was modified after last enrichment
                                                var driveItem = await _graph.GetDriveItemAsync(driveId, itemId);
                                                if (driveItem?.LastModifiedDateTime != null)
                                                {
                                                    var lastModified = driveItem.LastModifiedDateTime.Value.DateTime;
                                                    
                                                    // Get last enriched time from metadata
                                                    DateTime? lastEnrichedTime = null;
                                                    if (existingMetadata.ContainsKey("SMEPilot_LastEnrichedTime"))
                                                    {
                                                        var lastEnrichedValue = existingMetadata["SMEPilot_LastEnrichedTime"];
                                                        if (lastEnrichedValue != null)
                                                        {
                                                            if (lastEnrichedValue is DateTime dt)
                                                            {
                                                                lastEnrichedTime = dt;
                                                            }
                                                            else if (DateTime.TryParse(lastEnrichedValue.ToString(), out var parsedTime))
                                                            {
                                                                lastEnrichedTime = parsedTime;
                                                            }
                                                        }
                                                    }
                                                    
                                                    if (lastEnrichedTime.HasValue && lastModified > lastEnrichedTime.Value)
                                                    {
                                                        // File was modified after last enrichment - reprocess (new version)
                                                        _logger.LogInformation("🔄 [VERSIONING] File {FileName} was modified after last enrichment (LastModified: {LastModified}, LastEnriched: {LastEnriched}). Reprocessing as new version.", 
                                                            fileName, lastModified, lastEnrichedTime.Value);
                                                        // Continue processing - don't skip
                                                    }
                                                    else
                                                    {
                                                        // File unchanged or no timestamp - skip (duplicate or legacy)
                                                        _logger.LogInformation("⏭️ [IDEMPOTENCY] File {FileName} was processed by another instance (SMEPilot_Enriched={EnrichedValue}), skipping", fileName, enrichedValue);
                                                        continue;
                                                    }
                                                }
                                                else
                                                {
                                                    // Can't get LastModified - skip to be safe
                                                    _logger.LogInformation("⏭️ [IDEMPOTENCY] File {FileName} was processed by another instance (SMEPilot_Enriched={EnrichedValue}), skipping", fileName, enrichedValue);
                                                    continue;
                                                }
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
                                            else if (statusValue == "Retrying" || statusValue == "Retry") // Support both for backward compatibility
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
                                    
                                    // Error notification already sent in ProcessFileAsync
                                    
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
                                                {"SMEPilot_Status", "Retrying"},
                                                {"SMEPilot_ErrorMessage", result.ErrorMessage ?? "Unknown error"},
                                                {"SMEPilot_LastErrorTime", DateTime.UtcNow.ToString("O")}
                                            };
                                            
                                            await _graph.UpdateListItemFieldsAsync(driveId, itemId, retryMetadata);
                                            _logger.LogInformation("✅ [METADATA] Successfully marked {FileName} (ItemId: {ItemId}) as Retrying - will attempt again on next notification", fileName, itemId);
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

                // Load SharePoint configuration for manual processing
                try
                {
                    var siteId = await _graph.GetSiteIdFromDriveAsync(evt.driveId);
                    if (!string.IsNullOrWhiteSpace(siteId))
                    {
                        _logger.LogInformation("🔄 [CONFIG] Loading SharePoint configuration for site {SiteId} (manual processing)", siteId);
                        await _cfg.LoadSharePointConfigAsync(_graph, siteId, _logger);
                        _logger.LogInformation("✅ [CONFIG] SharePoint configuration loaded. Source: {SourcePath}, Destination: {DestPath}, MaxSize: {MaxSize}MB", 
                            _cfg.SourceFolderPath, _cfg.EnrichedFolderRelativePath, _cfg.MaxFileSizeBytes / 1024 / 1024);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ [CONFIG] Could not determine siteId from drive {DriveId}. Using environment variables/defaults.", evt.driveId);
                    }
                }
                catch (Exception configEx)
                {
                    _logger.LogWarning(configEx, "⚠️ [CONFIG] Failed to load SharePoint configuration. Using environment variables/defaults. Error: {Error}", configEx.Message);
                    // Continue processing with defaults - don't fail the entire request
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
            var processingStartTime = DateTimeOffset.UtcNow;
            long fileSizeBytes = 0;
            string? enrichedUrl = null;
            
            try
            {
                // Input validation
                if (string.IsNullOrWhiteSpace(driveId))
                {
                    _logger.LogError("❌ [VALIDATION] driveId is null or empty");
                    _telemetry?.TrackProcessingFailure(itemId, fileName, "driveId is required");
                    return (false, null, "driveId is required");
                }
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    _logger.LogError("❌ [VALIDATION] itemId is null or empty");
                    _telemetry?.TrackProcessingFailure(itemId ?? "unknown", fileName ?? "unknown", "itemId is required");
                    return (false, null, "itemId is required");
                }
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    _logger.LogError("❌ [VALIDATION] fileName is null or empty");
                    _telemetry?.TrackProcessingFailure(itemId, "unknown", "fileName is required");
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
            // CRITICAL: Capture site ID from source file for destination folder resolution
            string? sourceSiteId = null;
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
                
                // Extract site ID from source file's drive item for destination folder resolution
                sourceSiteId = driveItem.ParentReference?.SiteId;
                if (!string.IsNullOrWhiteSpace(sourceSiteId))
                {
                    _logger.LogInformation("✅ [SITE_ID] Captured site ID from source file: {SiteId}", sourceSiteId);
                    
                    // PERMANENT FIX: Load SharePoint configuration using the captured site ID
                    // This ensures we get the correct DestinationFolderPath from SharePoint config
                    try
                    {
                        _logger.LogInformation("🔄 [CONFIG] Loading SharePoint configuration using captured site ID: {SiteId}", sourceSiteId);
                        await _cfg.LoadSharePointConfigAsync(_graph, sourceSiteId, _logger, forceRefresh: true);
                        _logger.LogInformation("✅ [CONFIG] SharePoint configuration loaded. Source: {SourcePath}, Destination: {DestPath}, MaxSize: {MaxSize}MB", 
                            _cfg.SourceFolderPath, _cfg.EnrichedFolderRelativePath, _cfg.MaxFileSizeBytes / 1024 / 1024);
                    }
                    catch (Exception configEx)
                    {
                        _logger.LogWarning(configEx, "⚠️ [CONFIG] Failed to load SharePoint configuration with captured site ID. Using cached/defaults. Error: {Error}", configEx.Message);
                        // Continue with cached/default config - don't fail processing
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️ [SITE_ID] Could not extract site ID from source file's ParentReference");
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
                var downloadStartTime = DateTimeOffset.UtcNow;
                using var fileStream = await _graph.DownloadFileStreamAsync(driveId, itemId);
                fileSizeBytes = fileStream.Length;
                var downloadDuration = DateTimeOffset.UtcNow - downloadStartTime;
                _telemetry?.TrackDependency("GraphAPI", "DownloadFile", driveId, downloadStartTime, downloadDuration, true);
                _logger.LogDebug("✅ [DOWNLOAD] File downloaded. Size: {Size} bytes", fileStream.Length);

                // Check file size - if large, reject
                if (fileStream.Length > _cfg.MaxFileSizeBytes)
                {
                    var errorMessage = $"File too large for processing (max: {_cfg.MaxFileSizeBytes / 1024 / 1024}MB)";
                    _logger.LogWarning("File {FileName} is too large ({Size} bytes, max: {MaxSize} bytes), rejecting", 
                        fileName, fileStream.Length, _cfg.MaxFileSizeBytes);
                    _telemetry?.TrackProcessingFailure(itemId, fileName, errorMessage);
                    // Fire-and-forget notification (don't block on email send)
                    if (_notifications != null)
                    {
                        var notificationService = _notifications; // Capture for closure
                        _ = Task.Run(async () => await notificationService.SendProcessingFailureNotificationAsync(fileName, itemId, errorMessage));
                    }
                    return (false, null, errorMessage);
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
                    // Sanitize filename to avoid issues with special characters in temp path
                    var safeTempFileName = string.Join("_", Path.GetFileName(fileName).Split(Path.GetInvalidFileNameChars()));
                    tempInputPath = Path.Combine(Path.GetTempPath(), $"input_{fileId}_{safeTempFileName}");
                    
                    _logger.LogDebug("💾 [EXTRACTION] Saving .docx to temp file: {TempPath} (original: {OriginalName})", tempInputPath, fileName);
                    
                    fileStream.Position = 0;
                    // Write file in binary mode to ensure no encoding issues
                    using (var tempFileStream = new FileStream(tempInputPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan))
                    {
                        await fileStream.CopyToAsync(tempFileStream);
                        await tempFileStream.FlushAsync();
                    }
                    
                    // Verify file was written correctly
                    var fileInfo = new FileInfo(tempInputPath);
                    if (!fileInfo.Exists || fileInfo.Length != fileStream.Length)
                    {
                        throw new InvalidOperationException($"Failed to save temp file correctly. Expected {fileStream.Length} bytes, got {fileInfo.Length} bytes.");
                    }
                    
                    _logger.LogDebug("✅ [EXTRACTION] Saved .docx to temp file. Size: {Size} bytes", fileInfo.Length);
                    
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

                // 4. Rule-based enrichment (NO AI, NO DATABASE)
                byte[] enrichedBytes;
                string enrichedName;
                DocumentModel? docModel = null; // Declare at higher scope for classification
                
                _logger.LogInformation("Starting rule-based enrichment (no AI, no DB).");
                
                // For .docx files, use RuleBasedFormatter (stream-based, no temp files)
                if (fileExtension == ".docx" && _ruleBasedFormatter != null)
                {
                    try
                    {
                        _logger.LogDebug("📋 [ENRICHMENT] Using DocumentEnricherService for .docx file...");
                        
                        // Paths (ensure these files exist in the function app deployment package)
                        var repoRoot = AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory();
                        var mappingJsonPath = Path.Combine(repoRoot, "Config", "mapping.json");
                        
                        // Try to get template from SharePoint config first
                        string? templatePath = null;
                        if (!string.IsNullOrWhiteSpace(sourceSiteId))
                        {
                            _logger.LogInformation("📥 [TEMPLATE] Attempting to download template from SharePoint config...");
                            templatePath = await _graph.DownloadTemplateFileAsync(
                                sourceSiteId,
                                _cfg.TemplateLibraryPath,
                                _cfg.TemplateFileName,
                                _cfg.TemplateFileUrl);
                            
                            if (!string.IsNullOrWhiteSpace(templatePath) && File.Exists(templatePath))
                            {
                                _logger.LogInformation("✅ [TEMPLATE] Using template from SharePoint: {TemplatePath}", templatePath);
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ [TEMPLATE] Could not download template from SharePoint, falling back to local files");
                                templatePath = null;
                            }
                        }
                        
                        // Fallback to local template files if SharePoint download failed
                        if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
                        {
                            _logger.LogInformation("📂 [TEMPLATE] Looking for local template files...");
                            var templateDir = Path.Combine(repoRoot, "Templates");
                            templatePath = Path.Combine(templateDir, "SMEPilot_OrgTemplate_RuleBased.dotx");
                            if (!File.Exists(templatePath))
                            {
                                templatePath = Path.Combine(templateDir, "UniversalOrgTemplate.dotx");
                            }
                            if (!File.Exists(templatePath))
                            {
                                // Try to find any .dotx file in Templates folder
                                var dotxFiles = Directory.GetFiles(templateDir, "*.dotx");
                                if (dotxFiles.Length > 0)
                                {
                                    templatePath = dotxFiles[0];
                                    _logger.LogInformation("📋 [ENRICHMENT] Using template file: {TemplatePath}", templatePath);
                                }
                            }
                        }
                        
                        // Verify template file exists
                        if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
                        {
                            _logger.LogError("❌ [ENRICHMENT] Template file not found: {TemplatePath}", templatePath ?? "null");
                            return (false, null, $"Template file not found. Please ensure template is configured in SharePoint or deployed locally.");
                        }
                        
                        // Verify mapping.json exists
                        if (!File.Exists(mappingJsonPath))
                        {
                            _logger.LogError("❌ [ENRICHMENT] Mapping file not found: {MappingPath}", mappingJsonPath);
                            return (false, null, $"Mapping file not found: {mappingJsonPath}. Please ensure mapping.json is deployed.");
                        }
                        
                        // Create DocumentEnricherService instance
                        var enricher = new DocumentEnricherService(
                            mappingJsonPath, 
                            templatePath);
                        
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

                    // 5. Fill template using UniversalOrgTemplate.dotx
                    _logger.LogDebug("📝 [TEMPLATE] Filling template with extracted content...");
                    
                    // Try to get template from SharePoint config first
                    string? templatePath = null;
                    if (!string.IsNullOrWhiteSpace(sourceSiteId))
                    {
                        _logger.LogInformation("📥 [TEMPLATE] Attempting to download template from SharePoint config...");
                        templatePath = await _graph.DownloadTemplateFileAsync(
                            sourceSiteId,
                            _cfg.TemplateLibraryPath,
                            _cfg.TemplateFileName,
                            _cfg.TemplateFileUrl);
                        
                        if (!string.IsNullOrWhiteSpace(templatePath) && File.Exists(templatePath))
                        {
                            _logger.LogInformation("✅ [TEMPLATE] Using template from SharePoint: {TemplatePath}", templatePath);
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ [TEMPLATE] Could not download template from SharePoint, falling back to local files");
                            templatePath = null;
                        }
                    }
                    
                    // Fallback to local template files if SharePoint download failed
                    if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
                    {
                        _logger.LogInformation("📂 [TEMPLATE] Looking for local template files...");
                        var repoRoot = AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory();
                        var templatesDir = Path.Combine(repoRoot, "Templates");
                        templatePath = Directory.GetFiles(templatesDir, "UniversalOrgTemplate*.dotx")
                            .FirstOrDefault() ?? Path.Combine(templatesDir, "UniversalOrgTemplate.dotx");
                    }
                    
                    if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
                    {
                        _logger.LogWarning("⚠️ [TEMPLATE] Template file not found at {TemplatePath}, falling back to TemplateBuilder", templatePath ?? "null");
                        // Fallback to old method if template not found
                        enrichedBytes = TemplateBuilder.BuildDocxBytes(docModel, imagesBytes);
                        enrichedName = Path.GetFileNameWithoutExtension(fileName) + "_enriched.docx";
                    }
                    else
                    {
                        // Get document type classification (use docModel sections if text not available)
                        string fullText = text ?? string.Join("\n\n", docModel.Sections?.Select(s => $"{s.Heading}\n{s.Body}") ?? Enumerable.Empty<string>());
                        var classification = _hybridEnricher?.ClassifyDocument(docModel.Title ?? "", fullText) ?? "Generic";
                        
                        // Create temp output path
                        var tempOutputPath = Path.Combine(Path.GetTempPath(), $"enriched_{fileId}_{Path.GetFileNameWithoutExtension(fileName)}_enriched.docx");
                        
                        // Inspect template first to see what tags are available
                        var availableTags = TemplateFiller.InspectTemplate(templatePath, _logger);
                        _logger.LogInformation("🔍 [TEMPLATE] Template inspection complete. Available tags: {Tags}", 
                            string.Join(", ", availableTags));
                        
                        // Build contentMap using simplified mapper
                        var contentMap = SimplifiedContentMapper.BuildContentMap(
                            docModel, 
                            classification, 
                            availableTags, 
                            _logger);
                        
                        // Build revisions list
                        var revisions = new List<(string version, string date, string author, string changes)>
                        {
                            ("1.0", DateTime.UtcNow.ToString("yyyy-MM-dd"), "SMEPilot", "Initial document enrichment")
                        };
                        
                        // Fill template using TemplateFiller
                        TemplateFiller.FillTemplate(
                            templatePath,
                            tempOutputPath,
                            contentMap,
                            imagesBytes,
                            revisions,
                            _logger);
                        
                        // Read filled document back to bytes
                        enrichedBytes = await File.ReadAllBytesAsync(tempOutputPath);
                        enrichedName = Path.GetFileNameWithoutExtension(fileName) + "_enriched.docx";
                        
                        // Clean up temp file
                        try
                        {
                            if (File.Exists(tempOutputPath))
                                File.Delete(tempOutputPath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "⚠️ [TEMPLATE] Failed to delete temp file: {Path}", tempOutputPath);
                        }
                    }
                    
                    _logger.LogDebug("✅ [TEMPLATE] Formatted document created. Size: {Size} bytes", enrichedBytes.Length);
                }
                
                // Skip embedding generation/storage in no-DB mode (log only)
                _logger.LogInformation("Skipping embeddings & DB storage (No DB mode).");

                // 6. Upload to destination folder (with retry for locked files)
                // IMPORTANT: Resolve destination folder path to get the correct drive ID
                // The source driveId is from the source library, but we need the destination library's drive ID
                
                // Normalize the destination folder path (remove duplicate folder names, normalize slashes)
                var rawDestinationPath = _cfg.EnrichedFolderRelativePath;
                _logger.LogInformation("📤 [UPLOAD] Raw destination folder path from config: '{RawPath}' (SiteId: {SiteId})", rawDestinationPath, sourceSiteId ?? "null");
                
                // Verify config was loaded correctly
                if (string.IsNullOrWhiteSpace(rawDestinationPath) || rawDestinationPath == "/Shared Documents/SMEPilot Enriched Docs")
                {
                    _logger.LogWarning("⚠️ [UPLOAD] Destination path appears to be default value. Config may not have loaded correctly. RawPath: '{RawPath}'", rawDestinationPath);
                }
                
                // Normalize path: remove /sites/SiteName/ prefix if present, and clean up duplicate folder names
                var normalizedDestinationPath = rawDestinationPath.TrimStart('/').TrimEnd('/');
                if (normalizedDestinationPath.StartsWith("sites/", StringComparison.OrdinalIgnoreCase))
                {
                    var pathParts = normalizedDestinationPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (pathParts.Length >= 3)
                    {
                        // Skip "sites" and site name, take the rest
                        normalizedDestinationPath = string.Join("/", pathParts.Skip(2));
                    }
                }
                
                // Remove duplicate consecutive folder names (e.g., "Shared Documents/Shared Documents" -> "Shared Documents")
                var folderParts = normalizedDestinationPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                for (int i = folderParts.Count - 1; i > 0; i--)
                {
                    if (folderParts[i] == folderParts[i - 1])
                    {
                        _logger.LogWarning("⚠️ [UPLOAD] Found duplicate folder name '{FolderName}' in path, removing duplicate", folderParts[i]);
                        folderParts.RemoveAt(i);
                    }
                }
                normalizedDestinationPath = string.Join("/", folderParts);
                
                _logger.LogInformation("📤 [UPLOAD] Normalized destination folder path: '{NormalizedPath}'", normalizedDestinationPath);
                
                // Get site ID (we need it to resolve the destination folder)
                // CRITICAL FIX: Use the site ID captured from the source file instead of trying to get it from drive
                string? siteId = sourceSiteId;
                if (string.IsNullOrWhiteSpace(siteId))
                {
                    // Fallback 1: Try to get site ID from drive
                    _logger.LogDebug("🔍 [UPLOAD] Site ID not captured from source file, trying to get from drive...");
                    siteId = await _graph.GetSiteIdFromDriveAsync(driveId);
                    if (!string.IsNullOrWhiteSpace(siteId))
                    {
                        _logger.LogInformation("✅ [UPLOAD] Got site ID from drive: {SiteId}", siteId);
                    }
                }
                
                if (string.IsNullOrWhiteSpace(siteId))
                {
                    // Fallback 2: Try to get site ID from drive item directly (one more attempt)
                    _logger.LogDebug("🔍 [UPLOAD] Site ID still not available, trying to get from drive item directly...");
                    try
                    {
                        var driveItemForSiteId = await _graph.GetDriveItemAsync(driveId, itemId);
                        siteId = driveItemForSiteId?.ParentReference?.SiteId;
                        if (!string.IsNullOrWhiteSpace(siteId))
                        {
                            _logger.LogInformation("✅ [UPLOAD] Got site ID from drive item: {SiteId}", siteId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ [UPLOAD] Error getting site ID from drive item: {Error}", ex.Message);
                    }
                }
                
                if (string.IsNullOrWhiteSpace(siteId))
                {
                    _logger.LogError("❌ [UPLOAD] Could not determine site ID. Cannot resolve destination folder. Source site ID: {SourceSiteId}", sourceSiteId ?? "null");
                    // This is a critical error - we cannot proceed without a valid site ID
                    throw new InvalidOperationException("Cannot resolve destination folder: Site ID is required but could not be determined from source file or drive");
                }
                
                _logger.LogInformation("✅ [UPLOAD] Using site ID for destination folder resolution: {SiteId}", siteId);
                
                // Resolve destination folder path to get its drive ID and correct path
                string? destinationDriveId = null;
                string? destinationItemId = null;
                string destinationUploadPath = normalizedDestinationPath;
                
                try
                {
                    _logger.LogInformation("🔍 [UPLOAD] Resolving destination folder path: '{FolderPath}' for site: {SiteId}", 
                        normalizedDestinationPath, siteId);
                    
                    var (resolvedDriveId, resolvedItemId) = await _graph.ResolveFolderPathAsync(siteId, normalizedDestinationPath);
                    if (!string.IsNullOrWhiteSpace(resolvedDriveId))
                    {
                        destinationDriveId = resolvedDriveId;
                        destinationItemId = resolvedItemId;
                        
                        // PERMANENT FIX: When we have a resolved itemId, we should use just the subfolder path
                        // The resolved driveId is for the library, and itemId is for the subfolder
                        // If itemId is null, it means we're uploading to the library root
                        // If itemId is not null, we need to extract just the subfolder name for the upload path
                        if (!string.IsNullOrWhiteSpace(resolvedItemId))
                        {
                            // Extract just the subfolder name from the path (e.g., "ProcessedDocs" from "Shared Documents/ProcessedDocs")
                            var pathParts = normalizedDestinationPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                            if (pathParts.Length > 1)
                            {
                                // Use just the last part (subfolder name) as the upload path
                                destinationUploadPath = pathParts[pathParts.Length - 1];
                                _logger.LogInformation("✅ [UPLOAD] Resolved destination folder. DriveId: {DriveId}, ItemId: {ItemId}, UploadPath: '{UploadPath}' (extracted subfolder)", 
                                    destinationDriveId, destinationItemId, destinationUploadPath);
                            }
                            else
                            {
                                // This shouldn't happen, but if it does, use empty path (upload to drive root)
                                destinationUploadPath = "";
                                _logger.LogWarning("⚠️ [UPLOAD] Unexpected path format, using empty path for upload");
                            }
                        }
                        else
                        {
                            // Uploading to library root - use empty path
                            destinationUploadPath = "";
                            _logger.LogInformation("✅ [UPLOAD] Resolved destination folder to library root. DriveId: {DriveId}, UploadPath: '' (library root)", 
                                destinationDriveId);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ [UPLOAD] Could not resolve destination folder path '{FolderPath}', using source driveId (may upload to wrong location)", 
                            normalizedDestinationPath);
                        destinationDriveId = driveId; // Fallback to source drive
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ [UPLOAD] Error resolving destination folder path '{FolderPath}', using source driveId (may upload to wrong location). Error: {Error}", 
                        normalizedDestinationPath, ex.Message);
                    destinationDriveId = driveId; // Fallback to source drive
                }
                
                _logger.LogInformation("📤 [UPLOAD] Uploading formatted document '{FileName}' to path: '{UploadPath}' in drive {DriveId}", 
                    enrichedName, destinationUploadPath, destinationDriveId);
                
                DriveItem? uploaded = null;
                int uploadRetries = 0;
                int maxUploadRetries = _cfg.MaxUploadRetries;
                while (uploadRetries < maxUploadRetries)
                {
                    try
                    {
                        uploaded = await _graph.UploadFileBytesAsync(destinationDriveId!, destinationUploadPath, enrichedName, enrichedBytes);
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
                                uploaded = await _graph.UploadFileBytesAsync(destinationDriveId!, destinationUploadPath, enrichedName, enrichedBytes);
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
                            _logger.LogDebug("⏳ [UPLOAD] File locked, retrying in 2 seconds... (Attempt {Retry}/{MaxRetries})", uploadRetries, maxUploadRetries);
                            await Task.Delay(2000);
                        }
                    }
                }
                
                if (uploaded == null)
                {
                    throw new InvalidOperationException("Failed to upload enriched document after retries");
                }

                // 7. Update original item metadata (mark as processed to prevent reprocessing)
                _logger.LogInformation("📝 [METADATA] Updating SharePoint metadata for {FileName} (ItemId: {ItemId}) to mark as processed...", fileName, itemId);
                var enrichedTime = DateTime.UtcNow;
                var metadata = new Dictionary<string, object>
                {
                    {"SMEPilot_Enriched", true},
                    {"SMEPilot_Status", "Succeeded"},
                    {"SMEPilot_EnrichedFileUrl", uploaded.WebUrl},
                    {"SMEPilot_LastEnrichedTime", enrichedTime}, // Required for versioning detection
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
                            {"SMEPilot_EnrichedFileUrl", uploaded.WebUrl ?? ""},
                            {"SMEPilot_LastEnrichedTime", enrichedTime} // Include timestamp even in fallback
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
                
                // Track successful processing
                var processingDuration = DateTimeOffset.UtcNow - processingStartTime;
                enrichedUrl = uploaded.WebUrl;
                _telemetry?.TrackDocumentProcessing(itemId, fileName, fileSizeBytes, "Succeeded", processingDuration);
                
                return (true, uploaded.WebUrl, null);
            }
            catch (Exception ex)
            {
                // Track processing failure
                var processingDuration = DateTimeOffset.UtcNow - processingStartTime;
                string errorMessage;
                
                // Enhanced error logging for Graph API errors
                if (ex is ODataError odataError)
                {
                    errorMessage = $"Graph API Error: {odataError.Error?.Code ?? "Unknown"} - {odataError.Error?.Message ?? ex.Message}";
                    _logger.LogError(odataError, "❌ [GRAPH ERROR] ODataError processing file {FileName}: Code={Code}, Message={Message}", 
                        fileName, odataError.Error?.Code ?? "Unknown", odataError.Error?.Message ?? ex.Message);
                    if (odataError.Error?.AdditionalData != null)
                    {
                        foreach (var kvp in odataError.Error.AdditionalData)
                        {
                            _logger.LogDebug("   Additional Data: {Key}={Value}", kvp.Key, kvp.Value);
                        }
                    }
                    
                    _telemetry?.TrackProcessingFailure(itemId, fileName, errorMessage, odataError);
                    // Fire-and-forget notification (don't block on email send)
                    if (_notifications != null)
                    {
                        var notificationService = _notifications; // Capture for closure
                        _ = Task.Run(async () => await notificationService.SendProcessingFailureNotificationAsync(fileName, itemId, errorMessage, odataError.StackTrace));
                    }
                    
                    return (false, null, errorMessage);
                }
                else
                {
                    errorMessage = ex.Message;
                    _logger.LogError(ex, "❌ Error processing file {FileName}: {ErrorType}: {Message}", fileName, ex.GetType().Name, ex.Message);
                    
                    _telemetry?.TrackProcessingFailure(itemId, fileName, errorMessage, ex);
                    // Fire-and-forget notification (don't block on email send)
                    if (_notifications != null)
                    {
                        var notificationService = _notifications; // Capture for closure
                        _ = Task.Run(async () => await notificationService.SendProcessingFailureNotificationAsync(fileName, itemId, errorMessage, ex.StackTrace));
                    }
                }
                
                return (false, null, errorMessage);
            }
            finally
            {
                // Track processing attempt (even if failed)
                var processingDuration = DateTimeOffset.UtcNow - processingStartTime;
                if (enrichedUrl == null) // Only track if not already tracked (failure case)
                {
                    // Already tracked in catch block
                }
            }
        }

        /// <summary>
        /// Builds a content map from DocumentModel for TemplateFiller
        /// Maps sections to template content control tags
        /// </summary>
        private Dictionary<string, string> BuildContentMapForTemplateFiller(DocumentModel docModel, string? documentType)
        {
            var contentMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var usedContent = new HashSet<string>(); // Track content to prevent duplicates

            // Document metadata
            if (!string.IsNullOrWhiteSpace(docModel.Title))
                contentMap["DocumentTitle"] = docModel.Title;

            if (!string.IsNullOrWhiteSpace(documentType))
                contentMap["DocumentType"] = documentType;

            if (docModel.Sections == null || docModel.Sections.Count == 0)
                return contentMap;

            // First pass: Map sections with explicit headings/keywords
            foreach (var section in docModel.Sections)
            {
                var heading = section.Heading ?? "";
                var body = section.Body ?? "";
                if (string.IsNullOrWhiteSpace(body)) continue;

                var headingLower = heading.ToLowerInvariant();
                var bodyLower = body.ToLowerInvariant();

                // Skip if this content was already mapped
                var contentHash = body.Trim().Substring(0, Math.Min(100, body.Trim().Length));
                if (usedContent.Contains(contentHash)) continue;

                string? targetTag = null;

                // Check for explicit section markers in content
                if (bodyLower.Contains("functional overview:") || bodyLower.Contains("functional details:"))
                {
                    targetTag = "Functional";
                    body = ExtractAfterMarker(body, new[] { "Functional Overview:", "Functional Details:" });
                }
                else if (bodyLower.Contains("technical implementation:") || bodyLower.Contains("technical details:"))
                {
                    targetTag = "Technical";
                    body = ExtractAfterMarker(body, new[] { "Technical Implementation:", "Technical Details:" });
                }
                else if (bodyLower.Contains("troubleshooting:") || bodyLower.Contains("known issues:"))
                {
                    targetTag = "Findings";
                }
                else if (bodyLower.Contains("references:") || bodyLower.Contains("reference:") || bodyLower.StartsWith("http"))
                {
                    targetTag = "References";
                }
                // Check heading keywords
                else if (headingLower.Contains("overview") || headingLower.Contains("summary") || headingLower.Contains("introduction"))
                {
                    targetTag = "Overview";
                }
                else if (headingLower.Contains("functional") && !headingLower.Contains("technical"))
                {
                    targetTag = "Functional";
                }
                else if (headingLower.Contains("technical") || headingLower.Contains("implementation") || headingLower.Contains("api") || headingLower.Contains("endpoint"))
                {
                    targetTag = "Technical";
                }
                else if (headingLower.Contains("reference") || headingLower.Contains("link") || headingLower.Contains("documentation"))
                {
                    targetTag = "References";
                }
                // Content-based analysis
                else if (bodyLower.Contains("api") || bodyLower.Contains("endpoint") || bodyLower.Contains("cron") || bodyLower.Contains("webhook") || bodyLower.Contains("microservice"))
                {
                    targetTag = "Technical";
                }
                else if (bodyLower.Contains("functional") || bodyLower.Contains("feature") || bodyLower.Contains("workflow"))
                {
                    targetTag = "Functional";
                }
                else if (bodyLower.Contains("http://") || bodyLower.Contains("https://") || bodyLower.Contains("intranet"))
                {
                    targetTag = "References";
                }

                // Map to target tag if found
                if (targetTag != null && !string.IsNullOrWhiteSpace(body))
                {
                    if (contentMap.ContainsKey(targetTag))
                    {
                        contentMap[targetTag] = contentMap[targetTag] + "\n\n" + body;
                    }
                    else
                    {
                        contentMap[targetTag] = body;
                    }
                    usedContent.Add(contentHash);
                }
            }

            // Second pass: Map unmapped sections to Overview if empty
            foreach (var section in docModel.Sections)
            {
                var body = section.Body ?? "";
                if (string.IsNullOrWhiteSpace(body)) continue;

                var contentHash = body.Trim().Substring(0, Math.Min(100, body.Trim().Length));
                if (usedContent.Contains(contentHash)) continue;

                if (!contentMap.ContainsKey("Overview") && body.Length > 50)
                {
                    contentMap["Overview"] = body;
                    usedContent.Add(contentHash);
                }
            }

            return contentMap;
        }

        /// <summary>
        /// Extracts content after a marker for TemplateFiller
        /// </summary>
        private string ExtractAfterMarker(string text, string[] markers)
        {
            var textLower = text.ToLowerInvariant();
            foreach (var marker in markers)
            {
                var markerLower = marker.ToLowerInvariant();
                var index = textLower.IndexOf(markerLower);
                if (index >= 0)
                {
                    var startIndex = index + marker.Length;
                    return text.Substring(startIndex).Trim();
                }
            }
            return text;
        }
    }
}
