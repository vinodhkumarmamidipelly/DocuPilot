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
                        
                        // Fallback: Try to extract library name and get drive ID (for backward compatibility)
                        _logger.LogInformation("🔄 [SetupSubscription] Attempting fallback: extracting library name from path");
                        var pathParts = sourceFolderPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        string? libraryName = null;
                        
                        if (pathParts.Length >= 3 && pathParts[0].Equals("sites", StringComparison.OrdinalIgnoreCase))
                        {
                            libraryName = pathParts[2];
                        }
                        else if (pathParts.Length >= 1)
                        {
                            libraryName = pathParts[0];
                        }
                        
                        if (!string.IsNullOrWhiteSpace(libraryName))
                        {
                            _logger.LogInformation("🔄 [SetupSubscription] Extracted library name: '{LibraryName}' from path: {SourceFolderPath}", libraryName, sourceFolderPath);
                            driveId = await _graph.GetDriveIdFromSiteAndLibraryAsync(siteId, libraryName, sourceFolderPath);
                            if (!string.IsNullOrWhiteSpace(driveId))
                            {
                                _logger.LogInformation("✅ [SetupSubscription] Fallback successful: Got driveId: {DriveId} from siteId and library", driveId);
                            }
                            else
                            {
                                _logger.LogError("❌ [SetupSubscription] Fallback also failed: Could not get driveId for library '{LibraryName}' in site {SiteId}", libraryName, siteId);
                            }
                        }
                        else
                        {
                            _logger.LogError("❌ [SetupSubscription] Could not extract library name from path: {SourceFolderPath}", sourceFolderPath);
                        }
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

                // Log received parameters for debugging
                _logger.LogInformation("📋 [SetupSubscription] Final parameters after resolution - siteId: {SiteId}, driveId: {DriveId}, folderItemId: {FolderItemId}, sourceFolderPath: {SourceFolderPath}, notificationUrl: {NotificationUrl}, functionAppUrl: {FunctionAppUrl}",
                    siteId ?? "null", driveId ?? "null", folderItemId ?? "null", sourceFolderPath ?? "null", notificationUrl ?? "null", functionAppUrl ?? "null");

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
                            driveId = driveId ?? "null",
                            folderItemId = folderItemId ?? "null",
                            sourceFolderPath = sourceFolderPath ?? "null",
                            notificationUrl = notificationUrl ?? "null",
                            functionAppUrl = functionAppUrl ?? "null"
                        },
                        resolutionAttempted = !string.IsNullOrWhiteSpace(siteId) && !string.IsNullOrWhiteSpace(sourceFolderPath),
                        required = new[] { "driveId (or siteId + sourceFolderPath)", "notificationUrl (or functionAppUrl)" },
                        optional = new[] { "siteId", "sourceFolderPath" },
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

                // Resource format: 
                // - If folderItemId is available: /drives/{driveId}/items/{folderItemId}/children (monitors changes inside the folder)
                // - Otherwise: /drives/{driveId}/root (monitors changes in the root folder of the drive)
                var resource = !string.IsNullOrWhiteSpace(folderItemId)
                    ? $"/drives/{driveId}/items/{folderItemId}/children"
                    : $"/drives/{driveId}/root";
                
                _logger.LogInformation("📁 [SetupSubscription] Using resource path: {Resource} (folderItemId: {FolderItemId})", resource, folderItemId ?? "null");

                // Subscription expires in 3 days (Graph maximum for webhooks)
                var expiration = DateTimeOffset.UtcNow.AddDays(3);

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

                var subscription = await _graph.CreateSubscriptionAsync(resource, notificationUrl, expiration);

                _logger.LogInformation("✅ [SetupSubscription] Subscription created successfully! ID: {SubscriptionId}, Expires: {Expiration}", 
                    subscription.Id, subscription.ExpirationDateTime);

                // Store subscription ID in SMEPilotConfig list if siteId is available
                if (!string.IsNullOrWhiteSpace(siteId))
                {
                    try
                    {
                        _logger.LogInformation("💾 [SetupSubscription] Storing subscription ID in SMEPilotConfig for site {SiteId}", siteId);
                        
                        // Load configuration to get ConfigService
                        await _cfg.LoadSharePointConfigAsync(_graph, siteId, _logger);
                        
                        // Get list items from SMEPilotConfig (pass sourceFolderPath to help normalize site ID)
                        var configItems = await _graph.GetListItemsByNameAsync(siteId, "SMEPilotConfig", top: 1, sourceFolderPath);
                        if (configItems != null && configItems.Any())
                        {
                            var configItem = configItems.First();
                            var listItemId = configItem.Id;
                            
                            // Get the list ID by name (pass sourceFolderPath to help normalize site ID)
                            var listId = await _graph.GetListIdByNameAsync(siteId, "SMEPilotConfig", sourceFolderPath);
                            if (!string.IsNullOrWhiteSpace(listId))
                            {
                                // Update the subscription ID in the config item
                                var updateFields = new Dictionary<string, object>
                                {
                                    {"SubscriptionId", subscription.Id ?? ""},
                                    {"SubscriptionExpiration", subscription.ExpirationDateTime?.ToString("O") ?? ""}
                                };
                                
                                await _graph.UpdateListItemFieldsByListIdAsync(siteId, listId, listItemId, updateFields, sourceFolderPath);
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
    }
}

