using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using SMEPilot.FunctionApp.Helpers;
using SMEPilot.FunctionApp.Services;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Collections.Generic;

namespace SMEPilot.FunctionApp.Functions
{
    public class SetupSubscription
    {
        private readonly GraphHelper _graph;
        private readonly Config _cfg;
        private readonly ILogger<SetupSubscription> _logger;

        public SetupSubscription(GraphHelper graph, Config cfg, ILogger<SetupSubscription> logger)
        {
            _graph = graph;
            _cfg = cfg;
            _logger = logger;
        }

        [Function("SetupSubscription")]
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
                string? siteId = null;
                string? driveId = null;
                string? notificationUrl = null;
                string? sourceFolderPath = null;
                string? existingSubscriptionId = null;
                string? tenantId = null;

                // Handle POST request (from SPFx) - read from body
                string? functionAppUrl = null;
                if (req.Method == "POST")
                {
                    var body = await new StreamReader(req.Body).ReadToEndAsync();
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        try
                        {
                            var requestData = JsonConvert.DeserializeObject<Dictionary<string, string>>(body);
                            siteId = requestData?.GetValueOrDefault("siteId");
                            driveId = requestData?.GetValueOrDefault("driveId");
                            notificationUrl = requestData?.GetValueOrDefault("notificationUrl");
                            sourceFolderPath = requestData?.GetValueOrDefault("sourceFolderPath");
                            functionAppUrl = requestData?.GetValueOrDefault("functionAppUrl");
                            existingSubscriptionId = requestData?.GetValueOrDefault("subscriptionId");
                            tenantId = requestData?.GetValueOrDefault("tenantId");
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse POST body as JSON, trying query parameters");
                        }
                    }
                }

                // Fallback to query parameters (for GET or if POST body parsing failed)
                var query = req.Url.Query;
                var queryParams = string.IsNullOrEmpty(query) 
                    ? new Dictionary<string, string>()
                    : query.TrimStart('?').Split('&')
                        .Select(p => p.Split('='))
                        .Where(p => p.Length >= 1)
                        .ToDictionary(
                            p => p[0], 
                            p => p.Length > 1 ? Uri.UnescapeDataString(string.Join("=", p.Skip(1))) : "");

                // Fill in missing parameters from query string
                siteId = siteId ?? queryParams.GetValueOrDefault("siteId");
                driveId = driveId ?? queryParams.GetValueOrDefault("driveId");
                notificationUrl = notificationUrl ?? queryParams.GetValueOrDefault("notificationUrl");
                sourceFolderPath = sourceFolderPath ?? queryParams.GetValueOrDefault("sourceFolderPath");
                functionAppUrl = functionAppUrl ?? queryParams.GetValueOrDefault("functionAppUrl");
                tenantId = tenantId ?? queryParams.GetValueOrDefault("tenantId");

                // If notificationUrl not provided, construct from Function App URL
                if (string.IsNullOrWhiteSpace(notificationUrl))
                {
                    if (!string.IsNullOrWhiteSpace(functionAppUrl))
                    {
                        notificationUrl = $"{functionAppUrl.TrimEnd('/')}/api/ProcessSharePointFile";
                        _logger.LogInformation("🔍 [SetupSubscription] Constructed notificationUrl from functionAppUrl: {NotificationUrl}", notificationUrl);
                    }
                    else
                    {
                        // Try to get from environment variable
                        var envFunctionAppUrl = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
                        if (!string.IsNullOrWhiteSpace(envFunctionAppUrl))
                        {
                            notificationUrl = $"https://{envFunctionAppUrl}/api/ProcessSharePointFile";
                        }
                    }
                }

                // If driveId not provided but siteId and sourceFolderPath are, resolve folder path using Graph API
                string? folderItemId = null;
                string? libraryName = null;
                if (string.IsNullOrWhiteSpace(driveId) && !string.IsNullOrWhiteSpace(siteId) && !string.IsNullOrWhiteSpace(sourceFolderPath))
                {
                    _logger.LogInformation("🔍 [SetupSubscription] DriveId not provided, resolving folder path using Graph API");
                    
                    // Use Graph API to resolve the folder path - this is the most reliable method
                    var (resolvedDriveId, resolvedItemId) = await _graph.ResolveFolderPathAsync(siteId, sourceFolderPath);
                    
                    if (!string.IsNullOrWhiteSpace(resolvedDriveId))
                    {
                        driveId = resolvedDriveId;
                        folderItemId = resolvedItemId; // This might be null if it's a library root
                        _logger.LogInformation("✅ [SetupSubscription] Successfully resolved folder path. DriveId: {DriveId}, ItemId: {ItemId}", driveId, folderItemId ?? "null (library root)");
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ [SetupSubscription] Could not resolve folder path: {SourceFolderPath} in site {SiteId}", sourceFolderPath, siteId);
                    }
                }

                // If siteId not provided, try to get it from driveId
                if (string.IsNullOrWhiteSpace(siteId) && !string.IsNullOrWhiteSpace(driveId))
                {
                    _logger.LogInformation("🔍 [SetupSubscription] SiteId not provided, extracting from drive {DriveId}", driveId);
                    siteId = await _graph.GetSiteIdFromDriveAsync(driveId);
                    if (string.IsNullOrWhiteSpace(siteId))
                    {
                        _logger.LogWarning("⚠️ [SetupSubscription] Could not determine siteId from drive {DriveId}", driveId);
                    }
                }

                // Derive the library (document library) name from the sourceFolderPath so we can
                // subscribe at the LIST level (recommended, works for all current and future subfolders).
                if (!string.IsNullOrWhiteSpace(sourceFolderPath))
                {
                    var pathParts = sourceFolderPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (pathParts.Length >= 3 && pathParts[0].Equals("sites", StringComparison.OrdinalIgnoreCase))
                    {
                        libraryName = pathParts[2];
                    }
                    else if (pathParts.Length >= 1)
                    {
                        libraryName = pathParts[0];
                    }
                }

                string? listId = null;
                string? normalizedSiteId = null;

                // 1) Best-effort: resolve listId directly from driveId (document library drive)
                if (!string.IsNullOrWhiteSpace(driveId))
                {
                    listId = await _graph.GetListIdFromDriveAsync(driveId);
                    if (!string.IsNullOrWhiteSpace(listId))
                    {
                        _logger.LogInformation("✅ [SetupSubscription] Resolved listId {ListId} from driveId {DriveId}", listId, driveId);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ [SetupSubscription] Could not resolve listId from driveId {DriveId}. Will fall back to name-based lookup if possible.", driveId);
                    }
                }

                // 2) Fallback: resolve listId by library (list) display name using siteId
                if (string.IsNullOrWhiteSpace(listId) && !string.IsNullOrWhiteSpace(siteId) && !string.IsNullOrWhiteSpace(libraryName))
                {
                    normalizedSiteId = _graph.NormalizeSiteIdForResource(siteId, sourceFolderPath);
                    _logger.LogInformation("🔍 [SetupSubscription] Resolving list ID for library '{LibraryName}' in site {SiteId}", libraryName, normalizedSiteId);
                    listId = await _graph.GetListIdByNameAsync(siteId, libraryName, sourceFolderPath, tenantId);
                    if (!string.IsNullOrWhiteSpace(listId))
                    {
                        _logger.LogInformation("✅ [SetupSubscription] Resolved library '{LibraryName}' to listId {ListId} via name lookup", libraryName, listId);
                    }
                    else
                    {
                        _logger.LogError("❌ [SetupSubscription] Failed to resolve listId for library '{LibraryName}' in site {SiteId} via name lookup", libraryName, siteId);
                    }
                }

                // Ensure we have a normalized siteId string for the /sites/{id}/lists/{listId}/items resource
                if (normalizedSiteId == null && !string.IsNullOrWhiteSpace(siteId))
                {
                    normalizedSiteId = _graph.NormalizeSiteIdForResource(siteId, sourceFolderPath);
                }

                // Log received parameters for debugging
                _logger.LogInformation("📋 [SetupSubscription] Final parameters after resolution - tenantId: {TenantId}, siteId: {SiteId}, driveId: {DriveId}, folderItemId: {FolderItemId}, libraryName: {LibraryName}, listId: {ListId}, sourceFolderPath: {SourceFolderPath}, notificationUrl: {NotificationUrl}, functionAppUrl: {FunctionAppUrl}",
                    tenantId ?? "null", siteId ?? "null", driveId ?? "null", folderItemId ?? "null", libraryName ?? "null", listId ?? "null", sourceFolderPath ?? "null", notificationUrl ?? "null", functionAppUrl ?? "null");

                // For drive-based subscriptions we just require driveId and notificationUrl.
                if (string.IsNullOrWhiteSpace(driveId) || string.IsNullOrWhiteSpace(notificationUrl))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    AddCorsHeaders(bad, req);
                    var errorDetails = new
                    {
                        error = "Missing required parameters",
                        received = new
                        {
                            siteId = siteId ?? "null",
                            listId = listId ?? "null",
                            libraryName = libraryName ?? "null",
                            driveId = driveId ?? "null",
                            folderItemId = folderItemId ?? "null",
                            sourceFolderPath = sourceFolderPath ?? "null",
                            tenantId = tenantId ?? "null",
                            notificationUrl = notificationUrl ?? "null",
                            functionAppUrl = functionAppUrl ?? "null"
                        },
                        resolutionAttempted = !string.IsNullOrWhiteSpace(siteId) && !string.IsNullOrWhiteSpace(sourceFolderPath),
                        required = new[] { "driveId (or siteId + sourceFolderPath)", "notificationUrl (or functionAppUrl)" },
                        optional = new[] { "siteId", "sourceFolderPath", "tenantId" },
                        troubleshooting = new
                        {
                            message = "If driveId is null after resolution, the folder path might be incorrect or the folder/library doesn't exist.",
                            suggestions = new[]
                            {
                                "Verify the sourceFolderPath exists in SharePoint",
                                "Check if the path includes the library name (e.g., '/sites/SiteName/Shared Documents/FolderName')",
                                "If the path points to a library root, use the library name directly",
                                "Ensure the siteId format is correct: 'hostname,tenantId,siteId'"
                            }
                        },
                        example = "/api/SetupSubscription?driveId=b!xyz&notificationUrl=https://your-function.azurewebsites.net/api/ProcessSharePointFile",
                        alternative = "/api/SetupSubscription?siteId=siteId&sourceFolderPath=/sites/SiteName/LibraryName/Folder&notificationUrl=https://your-function.azurewebsites.net/api/ProcessSharePointFile"
                    };
                    _logger.LogWarning("❌ [SetupSubscription] Missing required parameters: {ErrorDetails}", JsonConvert.SerializeObject(errorDetails, Formatting.Indented));
                    await bad.WriteStringAsync(JsonConvert.SerializeObject(errorDetails, Formatting.Indented));
                    return bad;
                }

                // Clean up any existing SMEPilot subscriptions for this tenant before creating a new one.
                // Product rule: at any time, there should be at most ONE SMEPilot webhook subscription per org (tenant).
                if (!string.IsNullOrWhiteSpace(driveId))
                {
                    try
                    {
                        _logger.LogInformation("🔍 [SetupSubscription] Looking for existing SMEPilot subscriptions to clean up for tenant {TenantId}", tenantId ?? "default");
                        var allSubscriptions = await _graph.GetSubscriptionsAsync(tenantId);
                        var toDelete = allSubscriptions
                            .Where(s => IsSmepilotSubscription(s))
                            .ToList();

                        if (toDelete.Count > 0)
                        {
                            _logger.LogInformation("🗑️ [SetupSubscription] Found {Count} existing SMEPilot subscriptions for this tenant. Deleting them before creating a new one.", toDelete.Count);
                            foreach (var sub in toDelete)
                            {
                                if (string.IsNullOrWhiteSpace(sub.Id))
                                    continue;

                                try
                                {
                                    await _graph.DeleteSubscriptionAsync(sub.Id, tenantId);
                                    _logger.LogInformation("🗑️ [SetupSubscription] Deleted old subscription {SubscriptionId} for tenant {TenantId}", sub.Id, tenantId ?? "default");
                                }
                                catch (Exception exDel)
                                {
                                    _logger.LogWarning(exDel, "⚠️ [SetupSubscription] Failed to delete old subscription {SubscriptionId}. Continuing with setup.", sub.Id);
                                }
                            }
                        }
                        else
                        {
                            _logger.LogInformation("ℹ️ [SetupSubscription] No existing SMEPilot subscriptions found for this drive/tenant.");
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogWarning(cleanupEx, "⚠️ [SetupSubscription] Failed during existing subscription cleanup. Proceeding to create a new subscription.");
                    }
                }

                // Resource format (drive-based, known to be supported and stable across tenants):
                // - If folderItemId is available: /drives/{driveId}/items/{folderItemId}/children (monitors changes inside the folder)
                // - Otherwise: /drives/{driveId}/root (monitors changes in the root folder of the drive)
                // NOTE: We previously attempted a list-based resource (/sites/.../lists/{listId}/items) to get per-item delete IDs,
                // but Graph returned InvalidRequest: "resource '.../items' is not supported" in this tenant. To keep the
                // subscription creation reliable, we stick with the proven drive-based resources here.
                var resource = !string.IsNullOrWhiteSpace(folderItemId)
                    ? $"/drives/{driveId}/items/{folderItemId}/children"
                    : $"/drives/{driveId}/root";
                
                _logger.LogInformation("📁 [SetupSubscription] Using resource path: {Resource} (folderItemId: {FolderItemId})", resource, folderItemId ?? "null");

                // Subscription expires in 3 days (Graph maximum for webhooks)
                var expiration = DateTimeOffset.UtcNow.AddDays(3);

                // Generate a per-site clientState secret for webhook validation
                // This will be stored in SMEPilotConfig so ProcessSharePointFile can validate notifications.
                var clientStateSecret = Guid.NewGuid().ToString("N");

                _logger.LogInformation("🔄 [SetupSubscription] Creating webhook subscription for drive {DriveId}, resource: {Resource}, notificationUrl: {NotificationUrl}", 
                    driveId, resource, notificationUrl);

                // IMPORTANT: Verify notificationUrl is accessible before creating subscription
                // Graph API will send a validation request immediately, and it must respond within 10 seconds
                _logger.LogInformation("🔍 [SetupSubscription] Verifying notificationUrl is accessible...");
                try
                {
                    using (var httpClient = new System.Net.Http.HttpClient())
                    {
                        httpClient.Timeout = TimeSpan.FromSeconds(5); // Quick health check
                        var healthCheckResponse = await httpClient.GetAsync(notificationUrl);
                        _logger.LogInformation("✅ [SetupSubscription] NotificationUrl is accessible (Status: {Status})", healthCheckResponse.StatusCode);
                    }
                }
                catch (Exception healthEx)
                {
                    _logger.LogWarning(healthEx, "⚠️ [SetupSubscription] NotificationUrl health check failed: {Error}", healthEx.Message);
                    _logger.LogWarning("⚠️ [SetupSubscription] This may cause validation timeout. Ensure Function App is running and accessible.");
                    // Continue anyway - sometimes health check fails but validation works
                }

                var subscription = await _graph.CreateSubscriptionAsync(resource, notificationUrl, expiration, tenantId, clientStateSecret);

                _logger.LogInformation("✅ [SetupSubscription] Subscription created successfully! ID: {SubscriptionId}, Expires: {Expiration}", 
                    subscription.Id, subscription.ExpirationDateTime);

                // Store subscription ID in SMEPilotConfig list if we can determine a reliable siteId
                // Prefer resolving siteId from the drive so we use the same GUID-style ID that
                // ProcessSharePointFile uses (this is known to work well with GetListItemsByNameAsync).
                if (!string.IsNullOrWhiteSpace(driveId) || !string.IsNullOrWhiteSpace(siteId))
                {
                    try
                    {
                        // Derive a stable siteId for config operations
                        string? siteIdForConfig = siteId;
                        if (!string.IsNullOrWhiteSpace(driveId))
                        {
                            var driveSiteId = await _graph.GetSiteIdFromDriveAsync(driveId, tenantId);
                            if (!string.IsNullOrWhiteSpace(driveSiteId))
                            {
                                // If driveSiteId is in hostname,tenantId,siteGuid format, prefer the GUID part
                                var parts = driveSiteId.Split(',');
                                if (parts.Length >= 3)
                                {
                                    siteIdForConfig = parts[2];
                                }
                                else
                                {
                                    siteIdForConfig = driveSiteId;
                                }
                            }
                        }

                        if (string.IsNullOrWhiteSpace(siteIdForConfig))
                        {
                            _logger.LogWarning("⚠️ [SetupSubscription] Could not determine a reliable siteId for SMEPilotConfig. Subscription ID will not be stored.");
                        }
                        else
                        {
                            _logger.LogInformation("💾 [SetupSubscription] Storing subscription ID in SMEPilotConfig for site {SiteId}", siteIdForConfig);
                        
                            // Load configuration to get ConfigService
                            await _cfg.LoadSharePointConfigAsync(_graph, siteIdForConfig, _logger, tenantId: tenantId);
                        
                            // Get list items from SMEPilotConfig (pass sourceFolderPath and tenantId to help normalize site ID)
                            var configItems = await _graph.GetListItemsByNameAsync(siteIdForConfig, "SMEPilotConfig", top: 1, sourceFolderPath, tenantId);
                            if (configItems != null && configItems.Any())
                            {
                                var configItem = configItems.First();
                                var listItemId = configItem.Id;
                            
                                // Get the list ID for SMEPilotConfig (pass sourceFolderPath and tenantId to help normalize site ID)
                                var configListId = await _graph.GetListIdByNameAsync(siteIdForConfig, "SMEPilotConfig", sourceFolderPath, tenantId);
                                if (!string.IsNullOrWhiteSpace(configListId))
                                {
                                    // Update the subscription ID in the config item
                                    var updateFields = new Dictionary<string, object>
                                    {
                                        {"SubscriptionId", subscription.Id ?? ""},
                                        {"SubscriptionExpiration", subscription.ExpirationDateTime?.ToString("O") ?? ""},
                                        {"ClientStateSecret", clientStateSecret}
                                    };
                                
                                    await _graph.UpdateListItemFieldsByListIdAsync(siteIdForConfig, configListId, listItemId, updateFields, sourceFolderPath);
                                    _logger.LogInformation("✅ [SetupSubscription] Successfully stored subscription ID {SubscriptionId} in SMEPilotConfig", subscription.Id);
                                }
                                else
                                {
                                    _logger.LogWarning("⚠️ [SetupSubscription] Could not get list ID for SMEPilotConfig. Subscription ID will not be stored.");
                                }
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ [SetupSubscription] SMEPilotConfig list not found or empty. Subscription ID will not be stored in SharePoint.");
                            }
                        }
                    }
                    catch (Exception storeEx)
                    {
                        _logger.LogWarning(storeEx, "⚠️ [SetupSubscription] Failed to store subscription ID in SMEPilotConfig: {Error}. Subscription was created successfully.", storeEx.Message);
                        // Don't fail - subscription was created, just storage failed
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️ [SetupSubscription] SiteId not available. Subscription ID will not be stored in SharePoint.");
                }

                var ok = req.CreateResponse(HttpStatusCode.OK);
                ok.Headers.Add("Content-Type", "application/json");
                AddCorsHeaders(ok, req);
                await ok.WriteStringAsync(JsonConvert.SerializeObject(new
                {
                    success = true,
                    subscriptionId = subscription.Id,
                    resource = subscription.Resource,
                    expiration = subscription.ExpirationDateTime?.ToString("O"),
                    expirationDateTime = subscription.ExpirationDateTime?.ToString("O"), // Alias for SPFx compatibility
                    notificationUrl = subscription.NotificationUrl,
                    changeType = subscription.ChangeType,
                    message = "Subscription created successfully. It will expire in 3 days and needs renewal.",
                    renewBefore = subscription.ExpirationDateTime?.Subtract(TimeSpan.FromHours(1)).ToString("O")
                }, Formatting.Indented));
                return ok;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR in SetupSubscription: {Error}", ex.Message);
                
                // Check if credentials are configured
                var hasCredentials = !string.IsNullOrWhiteSpace(_cfg.GraphTenantId) 
                    && !string.IsNullOrWhiteSpace(_cfg.GraphClientId) 
                    && !string.IsNullOrWhiteSpace(_cfg.GraphClientSecret);
                
                _logger.LogDebug("Credentials configured: {HasCredentials}, Tenant ID: {HasTenantId}, Client ID: {HasClientId}, Client Secret: {HasClientSecret}",
                    hasCredentials,
                    !string.IsNullOrWhiteSpace(_cfg.GraphTenantId),
                    !string.IsNullOrWhiteSpace(_cfg.GraphClientId),
                    !string.IsNullOrWhiteSpace(_cfg.GraphClientSecret));
                
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                AddCorsHeaders(err, req);
                await err.WriteStringAsync(JsonConvert.SerializeObject(new
                {
                    error = ex.Message,
                    type = ex.GetType().Name,
                    innerException = ex.InnerException?.Message,
                    stackTrace = ex.StackTrace,
                    credentialsConfigured = hasCredentials,
                    message = "Check log files for full details"
                }, Formatting.Indented));
                return err;
            }
        }

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

        /// <summary>
        /// Returns true if the subscription looks like one created by SMEPilot.
        /// We currently treat any subscription whose NotificationUrl points at
        /// our ProcessSharePointFile endpoint as "ours".
        /// </summary>
        private bool IsSmepilotSubscription(Microsoft.Graph.Models.Subscription? subscription)
        {
            if (subscription == null)
                return false;

            var url = subscription.NotificationUrl ?? string.Empty;
            if (string.IsNullOrWhiteSpace(url))
                return false;

            // All SMEPilot webhooks are pointed at /api/ProcessSharePointFile
            return url.IndexOf("/api/ProcessSharePointFile", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string? ExtractDriveIdFromResource(string resource)
        {
            // Resource format: /drives/{driveId}/root or /drives/{driveId}/items/{itemId}/children
            try
            {
                var parts = resource.Split('/');
                if (parts.Length >= 3 && parts[1] == "drives")
                {
                    var driveId = parts[2];
                    return driveId;
                }
            }
            catch
            {
                // ignored
            }

            return null;
        }
    }
}

