using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using SMEPilot.FunctionApp.Helpers;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

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
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            try
            {
                // Parse query parameters
                var query = req.Url.Query;
                var queryParams = string.IsNullOrEmpty(query) 
                    ? new System.Collections.Generic.Dictionary<string, string>()
                    : query.TrimStart('?').Split('&')
                        .Select(p => p.Split('='))
                        .Where(p => p.Length >= 1)
                        .ToDictionary(
                            p => p[0], 
                            p => p.Length > 1 ? Uri.UnescapeDataString(string.Join("=", p.Skip(1))) : "");

                string? siteId = queryParams.GetValueOrDefault("siteId");
                string? driveId = queryParams.GetValueOrDefault("driveId");
                string? notificationUrl = queryParams.GetValueOrDefault("notificationUrl");

                if (string.IsNullOrWhiteSpace(siteId) || string.IsNullOrWhiteSpace(driveId) || string.IsNullOrWhiteSpace(notificationUrl))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync(JsonConvert.SerializeObject(new
                    {
                        error = "Missing required parameters",
                        required = new[] { "siteId", "driveId", "notificationUrl" },
                        example = "/api/SetupSubscription?siteId=abc123&driveId=b!xyz&notificationUrl=https://your-function.azurewebsites.net/api/ProcessSharePointFile"
                    }));
                    return bad;
                }

                // Resource format: /drives/{driveId}/root
                // For drive subscriptions, use just the drive path (not the full site path)
                // This subscribes to changes in the root folder of the drive
                var resource = $"/drives/{driveId}/root";

                // Subscription expires in 3 days (Graph maximum for webhooks)
                var expiration = DateTimeOffset.UtcNow.AddDays(3);

                var subscription = await _graph.CreateSubscriptionAsync(resource, notificationUrl, expiration);

                var ok = req.CreateResponse(HttpStatusCode.OK);
                ok.Headers.Add("Content-Type", "application/json");
                await ok.WriteStringAsync(JsonConvert.SerializeObject(new
                {
                    success = true,
                    subscriptionId = subscription.Id,
                    resource = subscription.Resource,
                    expiration = subscription.ExpirationDateTime?.ToString("O"),
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
    }
}

