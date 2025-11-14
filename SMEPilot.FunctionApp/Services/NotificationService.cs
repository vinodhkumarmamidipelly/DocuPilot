using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Azure.Communication.Email;
using Azure;

namespace SMEPilot.FunctionApp.Services
{
    /// <summary>
    /// Service for sending email notifications for errors and alerts
    /// </summary>
    public class NotificationService
    {
        private readonly EmailClient? _emailClient;
        private readonly string? _adminEmail;
        private readonly ILogger<NotificationService>? _logger;
        private readonly bool _isEnabled;

        public NotificationService(string? connectionString, string? adminEmail, ILogger<NotificationService>? logger = null)
        {
            _logger = logger;
            _adminEmail = adminEmail;

            if (!string.IsNullOrWhiteSpace(connectionString) && !string.IsNullOrWhiteSpace(adminEmail))
            {
                try
                {
                    _emailClient = new EmailClient(connectionString);
                    _isEnabled = true;
                    _logger?.LogInformation("✅ [NotificationService] Email notifications enabled. Admin email: {Email}", adminEmail);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "❌ [NotificationService] Failed to initialize email client: {Error}", ex.Message);
                    _isEnabled = false;
                }
            }
            else
            {
                _isEnabled = false;
                _logger?.LogWarning("⚠️ [NotificationService] Email notifications disabled. ConnectionString or AdminEmail not configured.");
            }
        }

        /// <summary>
        /// Send error notification
        /// </summary>
        public async Task SendErrorNotificationAsync(string subject, string errorMessage, Dictionary<string, string>? context = null)
        {
            if (!_isEnabled || _emailClient == null || string.IsNullOrWhiteSpace(_adminEmail))
            {
                _logger?.LogDebug("📧 [NotificationService] Email notification skipped (not configured)");
                return;
            }

            try
            {
                var body = BuildErrorEmailBody(errorMessage, context);
                
                var emailContent = new EmailContent(subject)
                {
                    PlainText = body
                };

                // Create email message with recipients and content
                var emailRecipients = new EmailRecipients(new List<EmailAddress> 
                { 
                    new EmailAddress(_adminEmail) 
                });
                
                var emailMessage = new EmailMessage(
                    senderAddress: "DoNotReply@azurecomm.net", // Azure Communication Services sender
                    emailRecipients, 
                    emailContent);

                var emailSendOperation = await _emailClient.SendAsync(
                    WaitUntil.Started, 
                    emailMessage);
                
                _logger?.LogInformation("✅ [NotificationService] Error notification sent to {Email}. OperationId: {OperationId}", 
                    _adminEmail, emailSendOperation.Id);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ [NotificationService] Failed to send error notification: {Error}", ex.Message);
                // Don't throw - email failure shouldn't break processing
            }
        }

        /// <summary>
        /// Send processing failure notification
        /// </summary>
        public async Task SendProcessingFailureNotificationAsync(string fileName, string itemId, string errorMessage, string? stackTrace = null)
        {
            var context = new Dictionary<string, string>
            {
                { "FileName", fileName },
                { "ItemId", itemId },
                { "Timestamp", DateTimeOffset.UtcNow.ToString("O") }
            };

            if (!string.IsNullOrWhiteSpace(stackTrace))
            {
                context["StackTrace"] = stackTrace;
            }

            await SendErrorNotificationAsync(
                $"SMEPilot: Processing Failed - {fileName}",
                $"Document processing failed for file: {fileName}\n\nError: {errorMessage}",
                context
            );
        }

        /// <summary>
        /// Send webhook expiration warning
        /// </summary>
        public async Task SendWebhookExpirationWarningAsync(string subscriptionId, DateTimeOffset expirationDate)
        {
            var context = new Dictionary<string, string>
            {
                { "SubscriptionId", subscriptionId },
                { "ExpirationDate", expirationDate.ToString("O") },
                { "DaysUntilExpiration", ((expirationDate - DateTimeOffset.UtcNow).TotalDays).ToString("F1") }
            };

            await SendErrorNotificationAsync(
                "SMEPilot: Webhook Subscription Expiring Soon",
                $"Webhook subscription {subscriptionId} will expire on {expirationDate:yyyy-MM-dd HH:mm:ss} UTC.\n\n" +
                $"Days until expiration: {context["DaysUntilExpiration"]}\n\n" +
                "The system will attempt to renew automatically, but please verify renewal was successful.",
                context
            );
        }

        /// <summary>
        /// Send configuration error notification
        /// </summary>
        public async Task SendConfigurationErrorNotificationAsync(string siteId, string errorMessage)
        {
            var context = new Dictionary<string, string>
            {
                { "SiteId", siteId },
                { "Timestamp", DateTimeOffset.UtcNow.ToString("O") }
            };

            await SendErrorNotificationAsync(
                "SMEPilot: Configuration Error",
                $"Failed to load configuration for site: {siteId}\n\nError: {errorMessage}\n\n" +
                "The system will use default values, but this may cause processing issues.",
                context
            );
        }

        private string BuildErrorEmailBody(string errorMessage, Dictionary<string, string>? context)
        {
            var body = "SMEPilot Error Notification\n";
            body += "==========================\n\n";
            body += $"Time: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n\n";
            body += $"Error:\n{errorMessage}\n\n";

            if (context != null && context.Count > 0)
            {
                body += "Context:\n";
                foreach (var kvp in context)
                {
                    body += $"  {kvp.Key}: {kvp.Value}\n";
                }
                body += "\n";
            }

            body += "---\n";
            body += "This is an automated notification from SMEPilot.\n";
            body += "Please check Application Insights for detailed logs.";

            return body;
        }
    }
}

