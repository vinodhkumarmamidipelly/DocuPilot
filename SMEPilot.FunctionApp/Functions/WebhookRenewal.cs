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
    /// Timer function to renew webhook subscriptions before they expire
    /// Runs every 2 days to check and renew subscriptions expiring within 24 hours
    /// </summary>
    public class WebhookRenewal
    {
        private readonly GraphHelper _graph;
        private readonly Config _cfg;
        private readonly ILogger<WebhookRenewal> _logger;
        private readonly TelemetryService? _telemetry;
        private readonly NotificationService? _notifications;

        public WebhookRenewal(GraphHelper graph, Config cfg, ILogger<WebhookRenewal> logger, 
            TelemetryService? telemetry = null, NotificationService? notifications = null)
        {
            _graph = graph;
            _cfg = cfg;
            _logger = logger;
            _telemetry = telemetry;
            _notifications = notifications;
        }

        [Function("WebhookRenewal")]
        public async Task Run([TimerTrigger("0 0 */2 * * *")] TimerInfo timerInfo) // Every 2 days at midnight
        {
            _logger.LogInformation("🔄 [WebhookRenewal] Starting webhook renewal check at {Time}", DateTime.UtcNow);

            try
            {
                // Get all active subscriptions from Graph API
                var subscriptions = await _graph.GetSubscriptionsAsync();
                
                if (subscriptions == null || !subscriptions.Any())
                {
                    _logger.LogInformation("ℹ️ [WebhookRenewal] No active subscriptions found");
                    return;
                }

                _logger.LogInformation("📋 [WebhookRenewal] Found {Count} active subscriptions", subscriptions.Count());

                var renewalThreshold = DateTimeOffset.UtcNow.AddHours(24); // Renew if expires within 24 hours
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
                        
                        // Check if subscription expires within 24 hours
                        if (expiration <= renewalThreshold)
                        {
                            _logger.LogInformation("🔄 [WebhookRenewal] Subscription {SubscriptionId} expires at {Expiration}, renewing...", 
                                subscription.Id, expiration);

                            // Create new subscription with same configuration
                            var newExpiration = DateTimeOffset.UtcNow.AddDays(3); // Graph maximum
                            var newSubscription = await _graph.CreateSubscriptionAsync(
                                subscription.Resource ?? "",
                                subscription.NotificationUrl ?? "",
                                newExpiration);

                            _logger.LogInformation("✅ [WebhookRenewal] Created new subscription {NewSubscriptionId} (expires: {NewExpiration})", 
                                newSubscription.Id, newSubscription.ExpirationDateTime);

                            // Delete old subscription
                            await _graph.DeleteSubscriptionAsync(subscription.Id ?? "");
                            _logger.LogInformation("🗑️ [WebhookRenewal] Deleted old subscription {OldSubscriptionId}", subscription.Id);

                            // Update subscription ID in SMEPilotConfig if we can determine siteId from resource
                            var driveId = ExtractDriveIdFromResource(subscription.Resource ?? "");
                            if (!string.IsNullOrWhiteSpace(driveId))
                            {
                                try
                                {
                                    var siteId = await _graph.GetSiteIdFromDriveAsync(driveId);
                                    if (!string.IsNullOrWhiteSpace(siteId))
                                    {
                                        await UpdateSubscriptionIdInConfig(siteId, newSubscription.Id ?? "", newSubscription.ExpirationDateTime);
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

                _logger.LogInformation("✅ [WebhookRenewal] Renewal check completed. Renewed: {RenewedCount}, Errors: {ErrorCount}", 
                    renewedCount, errorCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [WebhookRenewal] Fatal error in webhook renewal: {Error}", ex.Message);
                throw; // Re-throw to trigger Function App alert
            }
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

        private async Task UpdateSubscriptionIdInConfig(string siteId, string subscriptionId, DateTimeOffset? expiration)
        {
            try
            {
                var configItems = await _graph.GetListItemsByNameAsync(siteId, "SMEPilotConfig", top: 1);
                if (configItems != null && configItems.Any())
                {
                    var configItem = configItems.First();
                    var listItemId = configItem.Id;
                    
                    var listId = await _graph.GetListIdByNameAsync(siteId, "SMEPilotConfig");
                    if (!string.IsNullOrWhiteSpace(listId))
                    {
                        var updateFields = new Dictionary<string, object>
                        {
                            {"SubscriptionId", subscriptionId},
                            {"SubscriptionExpiration", expiration?.ToString("O") ?? ""}
                        };
                        
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

