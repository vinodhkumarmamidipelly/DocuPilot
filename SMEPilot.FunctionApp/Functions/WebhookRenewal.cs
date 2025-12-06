using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SMEPilot.FunctionApp.Helpers;
using SMEPilot.FunctionApp.Services;

namespace SMEPilot.FunctionApp.Functions
{
    /// <summary>
        /// Timer function to renew webhook subscriptions before they expire.
        /// Runs every 2 hours (cron: 0 0 */2 * * *) to check and renew subscriptions
        /// that are expiring within a configurable renewal window.
    /// </summary>
    public class WebhookRenewal
    {
        private readonly GraphHelper _graph;
        private readonly Config _cfg;
        private readonly ILogger<WebhookRenewal> _logger;
        private readonly TelemetryService? _telemetry;
        private readonly TenantRegistryService _tenantRegistry;

        public WebhookRenewal(GraphHelper graph, Config cfg, ILogger<WebhookRenewal> logger, 
            TelemetryService? telemetry = null,
            TenantRegistryService? tenantRegistry = null)
        {
            _graph = graph;
            _cfg = cfg;
            _logger = logger;
            _telemetry = telemetry;
            _tenantRegistry = tenantRegistry ?? new TenantRegistryService();
        }

        [Function("WebhookRenewal")]
        public async Task Run([TimerTrigger("0 0 */2 * * *")] TimerInfo timerInfo) // Every 2 hours
        {
            _logger.LogInformation("🔄 [WebhookRenewal] Starting webhook renewal check at {Time}", DateTime.UtcNow);

            try
            {
                // Multi-tenant: get active tenants from registry.
                var tenantIds = _tenantRegistry.GetActiveTenantIds();

                // Fallback: if registry has no tenants, use the configured Graph_TenantId (single-tenant mode)
                if (tenantIds == null || tenantIds.Count == 0)
                {
                    _logger.LogWarning("⚠️ [WebhookRenewal] Tenant registry returned no tenants. Falling back to Graph_TenantId.");
                    if (!string.IsNullOrWhiteSpace(_cfg.GraphTenantId))
                    {
                        tenantIds = new List<string> { _cfg.GraphTenantId };
                    }
                }

                int totalRenewed = 0;
                int totalErrors = 0;

                foreach (var tenantId in tenantIds)
                {
                    _logger.LogInformation("🔍 [WebhookRenewal] Checking subscriptions for tenant {TenantId}", tenantId);

                    // Get all active subscriptions from Graph API for this tenant
                    var subscriptions = await _graph.GetSubscriptionsAsync(tenantId);

                    if (subscriptions == null || !subscriptions.Any())
                    {
                        _logger.LogInformation("ℹ️ [WebhookRenewal] No active subscriptions found for tenant {TenantId}", tenantId);
                        continue;
                    }

                    _logger.LogInformation("📋 [WebhookRenewal] Found {Count} active subscriptions for tenant {TenantId}", subscriptions.Count(), tenantId);

                    // Filter to only SMEPilot subscriptions (those pointing to our ProcessSharePointFile endpoint)
                    subscriptions = subscriptions
                        .Where(s => IsSmepilotSubscription(s))
                        .ToList();

                    if (subscriptions == null || !subscriptions.Any())
                    {
                        _logger.LogInformation("ℹ️ [WebhookRenewal] No SMEPilot subscriptions found for tenant {TenantId} after filtering by notification URL", tenantId);
                        continue;
                    }

                    var renewalWindowHours = _cfg.SubscriptionRenewalWindowHours;
                    var renewalThreshold = DateTimeOffset.UtcNow.AddHours(renewalWindowHours); // Renew if expires within window
                    int renewedCount = 0;
                    int errorCount = 0;

                    foreach (var subscription in subscriptions)
                {
                    try
                    {
                        if (subscription.ExpirationDateTime == null)
                        {
                            _logger.LogWarning("⚠️ [WebhookRenewal] Subscription {SubscriptionId} has no expiration date, skipping", subscription.Id);
                            continue;
                        }

                        var expiration = subscription.ExpirationDateTime.Value;

                        // Check if subscription expires within the configured renewal window
                        if (expiration <= renewalThreshold)
                        {
                            _logger.LogInformation("🔄 [WebhookRenewal] Subscription {SubscriptionId} expires at {Expiration}, renewing...", 
                                subscription.Id, expiration);

                            // Create new subscription with same configuration
                            var newExpiration = DateTimeOffset.UtcNow.AddDays(3); // Graph maximum
                            // Try to reuse existing ClientState if available; otherwise generate a new one
                            var clientStateSecret = subscription.ClientState;
                            if (string.IsNullOrWhiteSpace(clientStateSecret))
                            {
                                clientStateSecret = Guid.NewGuid().ToString("N");
                            }

                            var newSubscription = await _graph.CreateSubscriptionAsync(
                                subscription.Resource ?? "",
                                subscription.NotificationUrl ?? "",
                                newExpiration,
                                tenantId,
                                clientStateSecret);

                            _logger.LogInformation("✅ [WebhookRenewal] Created new subscription {NewSubscriptionId} (expires: {NewExpiration})", 
                                newSubscription.Id, newSubscription.ExpirationDateTime);

                            // Delete old subscription
                            await _graph.DeleteSubscriptionAsync(subscription.Id ?? "", tenantId);
                            _logger.LogInformation("🗑️ [WebhookRenewal] Deleted old subscription {OldSubscriptionId} for tenant {TenantId}", subscription.Id, tenantId);

                            // Update subscription ID in SMEPilotConfig if we can determine siteId from resource
                            var driveId = ExtractDriveIdFromResource(subscription.Resource ?? "");
                            if (!string.IsNullOrWhiteSpace(driveId))
                            {
                                try
                                {
                                    var siteId = await _graph.GetSiteIdFromDriveAsync(driveId, tenantId);
                                    if (!string.IsNullOrWhiteSpace(siteId))
                                    {
                                        await UpdateSubscriptionIdInConfig(siteId, newSubscription.Id ?? "", newSubscription.ExpirationDateTime, tenantId, clientStateSecret);
                                    }
                                    else
                                    {
                                        _logger.LogWarning("⚠️ [WebhookRenewal] Could not determine siteId from drive {DriveId}", driveId);
                                    }
                                }
                                catch (Exception configEx)
                                {
                                    _logger.LogWarning(configEx, "⚠️ [WebhookRenewal] Failed to update subscription ID in config: {Error}", configEx.Message);
                                    // Don't fail - subscription was renewed successfully
                                }
                            }

                            renewedCount++;
                        }
                        else
                        {
                            _logger.LogDebug("✓ [WebhookRenewal] Subscription {SubscriptionId} expires at {Expiration}, no renewal needed yet", 
                                subscription.Id, expiration);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ [WebhookRenewal] Error processing subscription {SubscriptionId}: {Error}", 
                            subscription.Id, ex.Message);
                        errorCount++;
                    }
                    }

                    _logger.LogInformation("✅ [WebhookRenewal] Renewal check for tenant {TenantId} completed. Renewed: {RenewedCount}, Errors: {ErrorCount}",
                        tenantId, renewedCount, errorCount);

                    totalRenewed += renewedCount;
                    totalErrors += errorCount;
                }

                _logger.LogInformation("✅ [WebhookRenewal] Global renewal summary. Total Renewed: {RenewedCount}, Total Errors: {ErrorCount}",
                    totalRenewed, totalErrors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [WebhookRenewal] Fatal error in webhook renewal: {Error}", ex.Message);
                throw; // Re-throw to trigger Function App alert
            }
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
            // Resource format: /drives/{driveId}/root
            try
            {
                var parts = resource.Split('/');
                if (parts.Length >= 3 && parts[1] == "drives")
                {
                    var driveId = parts[2];
                    return driveId;
                }
            }
            catch { }
            
            return null;
        }

        private async Task UpdateSubscriptionIdInConfig(string siteId, string subscriptionId, DateTimeOffset? expiration, string? tenantId, string? clientStateSecret)
        {
            try
            {
                var configItems = await _graph.GetListItemsByNameAsync(siteId, "SMEPilotConfig", top: 1, sourceFolderPath: null, tenantId: tenantId);
                if (configItems != null && configItems.Any())
                {
                    var configItem = configItems.First();
                    var listItemId = configItem.Id;
                    
                    var listId = await _graph.GetListIdByNameAsync(siteId, "SMEPilotConfig", sourceFolderPath: null, tenantId: tenantId);
                    if (!string.IsNullOrWhiteSpace(listId))
                    {
                        var updateFields = new Dictionary<string, object>
                        {
                            {"SubscriptionId", subscriptionId},
                            {"SubscriptionExpiration", expiration?.ToString("O") ?? ""}
                        };

                        if (!string.IsNullOrWhiteSpace(clientStateSecret))
                        {
                            updateFields["ClientStateSecret"] = clientStateSecret;
                        }
                        
                        await _graph.UpdateListItemFieldsByListIdAsync(siteId, listId, listItemId, updateFields);
                        _logger.LogInformation("✅ [WebhookRenewal] Updated subscription ID in SMEPilotConfig");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ [WebhookRenewal] Failed to update subscription ID in config: {Error}", ex.Message);
                throw;
            }
        }
    }
}

