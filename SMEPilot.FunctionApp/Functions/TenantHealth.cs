using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SMEPilot.FunctionApp.Helpers;
using SMEPilot.FunctionApp.Services;

namespace SMEPilot.FunctionApp.Functions
{
    /// <summary>
    /// Lightweight health endpoint for support/ops to see per-tenant subscription status.
    /// Returns JSON only and is safe/read-only.
    /// </summary>
    public class TenantHealth
    {
        private readonly GraphHelper _graph;
        private readonly Config _cfg;
        private readonly ILogger<TenantHealth> _logger;
        private readonly TenantRegistryService _tenantRegistry;

        public TenantHealth(GraphHelper graph, Config cfg, ILogger<TenantHealth> logger, TenantRegistryService? tenantRegistry = null)
        {
            _graph = graph;
            _cfg = cfg;
            _logger = logger;
            _tenantRegistry = tenantRegistry ?? new TenantRegistryService();
        }


        [Function("TenantHealth")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "health/tenants")] HttpRequestData req)
        {
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var filterTenant = query["tenantId"];

            _logger.LogInformation("🔍 [TenantHealth] Starting health check. FilterTenant={TenantId}", filterTenant ?? "none");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");

            try
            {
                // Build tenant list from explicit filter or registry (with Graph_TenantId fallback)
                List<string> tenantIds;
                var tenantInfos = _tenantRegistry.GetAllTenants();
                var tenantInfoById = tenantInfos
                    .GroupBy(t => t.TenantId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                if (!string.IsNullOrWhiteSpace(filterTenant))
                {
                    tenantIds = new List<string> { filterTenant.Trim() };
                }
                else
                {
                    tenantIds = _tenantRegistry.GetActiveTenantIds().ToList();

                    if (tenantIds.Count == 0 && !string.IsNullOrWhiteSpace(_cfg.GraphTenantId))
                    {
                        _logger.LogWarning("⚠️ [TenantHealth] Tenant registry returned no tenants. Falling back to Graph_TenantId.");
                        tenantIds.Add(_cfg.GraphTenantId);
                    }
                }

                var tenantSummaries = new List<object>();

                foreach (var tenantId in tenantIds)
                {
                    try
                    {
                        tenantInfoById.TryGetValue(tenantId, out var info);
                        string? orgName = info?.DisplayName;
                        string? primarySiteUrl = info?.PrimarySiteUrl;

                        _logger.LogInformation("🔍 [TenantHealth] Checking subscriptions for tenant {TenantId}", tenantId);
                        var subscriptions = await _graph.GetSubscriptionsAsync(tenantId);

                        var now = DateTimeOffset.UtcNow;
                        var soonThreshold = now.AddHours(24);

                        var subscriptionSummaries = new List<object>();

                        foreach (var sub in subscriptions)
                        {
                            var exp = sub.ExpirationDateTime;
                            var isExpiringSoon = exp.HasValue && exp.Value <= soonThreshold;
                            var driveId = ExtractDriveIdFromResource(sub.Resource ?? string.Empty);
                            string? siteId = null;

                            if (!string.IsNullOrWhiteSpace(driveId))
                            {
                                try
                                {
                                    siteId = await _graph.GetSiteIdFromDriveAsync(driveId, tenantId);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "⚠️ [TenantHealth] Failed to resolve siteId from driveId {DriveId} for tenant {TenantId}", driveId, tenantId);
                                }
                            }

                            subscriptionSummaries.Add(new
                            {
                                subscriptionId = sub.Id,
                                resource = sub.Resource,
                                changeType = sub.ChangeType,
                                notificationUrl = sub.NotificationUrl,
                                clientState = sub.ClientState,
                                expiration = exp?.ToString("O"),
                                isExpiringSoon,
                                driveId,
                                siteId
                            });
                        }

                        tenantSummaries.Add(new
                        {
                            tenantId,
                            organizationName = orgName,
                            primarySiteUrl = primarySiteUrl,
                            subscriptionCount = subscriptionSummaries.Count,
                            subscriptions = subscriptionSummaries
                        });
                    }
                    catch (Exception exTenant)
                    {
                        _logger.LogError(exTenant, "❌ [TenantHealth] Error while checking tenant {TenantId}", tenantId);
                        tenantSummaries.Add(new
                        {
                            tenantId,
                            error = exTenant.Message
                        });
                    }
                }

                var payload = new
                {
                    generatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                    tenantCount = tenantSummaries.Count,
                    tenants = tenantSummaries
                };

                await response.WriteStringAsync(System.Text.Json.JsonSerializer.Serialize(payload,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [TenantHealth] Fatal error building health response");
                response.StatusCode = HttpStatusCode.InternalServerError;

                var errorPayload = new
                {
                    error = "Tenant health check failed",
                    message = ex.Message
                };

                await response.WriteStringAsync(System.Text.Json.JsonSerializer.Serialize(errorPayload,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
                return response;
            }
        }

        private static string? ExtractDriveIdFromResource(string resource)
        {
            // Expected: /drives/{driveId}/root or /drives/{driveId}/items/{id}/children
            try
            {
                var parts = resource.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var drivesIndex = Array.IndexOf(parts, "drives");
                if (drivesIndex >= 0 && drivesIndex + 1 < parts.Length)
                {
                    return parts[drivesIndex + 1];
                }
            }
            catch
            {
                // Ignore and return null
            }

            return null;
        }
    }
}


