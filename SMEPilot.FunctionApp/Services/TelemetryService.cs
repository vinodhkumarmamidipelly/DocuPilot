using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;

namespace SMEPilot.FunctionApp.Services
{
    /// <summary>
    /// Service for Application Insights telemetry and custom metrics
    /// </summary>
    public class TelemetryService
    {
        private readonly TelemetryClient _telemetryClient;
        private readonly ILogger<TelemetryService>? _logger;

        public TelemetryService(TelemetryClient telemetryClient, ILogger<TelemetryService>? logger = null)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            _logger = logger;
        }

        /// <summary>
        /// Track document processing event
        /// </summary>
        public void TrackDocumentProcessing(string itemId, string fileName, long fileSizeBytes, string status, TimeSpan processingTime)
        {
            var properties = new Dictionary<string, string>
            {
                { "ItemId", itemId },
                { "FileName", fileName },
                { "FileSizeMB", (fileSizeBytes / 1024.0 / 1024.0).ToString("F2") },
                { "Status", status }
            };

            var metrics = new Dictionary<string, double>
            {
                { "ProcessingTimeSeconds", processingTime.TotalSeconds },
                { "FileSizeMB", fileSizeBytes / 1024.0 / 1024.0 }
            };

            _telemetryClient.TrackEvent("DocumentProcessed", properties, metrics);
            _logger?.LogInformation("📊 [Telemetry] Tracked document processing: {FileName}, Status: {Status}, Time: {Time}s", 
                fileName, status, processingTime.TotalSeconds);
        }

        /// <summary>
        /// Track processing failure
        /// </summary>
        public void TrackProcessingFailure(string itemId, string fileName, string errorMessage, Exception? exception = null)
        {
            var properties = new Dictionary<string, string>
            {
                { "ItemId", itemId },
                { "FileName", fileName },
                { "ErrorMessage", errorMessage }
            };

            if (exception != null)
            {
                _telemetryClient.TrackException(exception, properties);
            }
            else
            {
                _telemetryClient.TrackEvent("ProcessingFailed", properties);
            }

            _logger?.LogError("📊 [Telemetry] Tracked processing failure: {FileName}, Error: {Error}", fileName, errorMessage);
        }

        /// <summary>
        /// Track webhook subscription event
        /// </summary>
        public void TrackWebhookSubscription(string subscriptionId, string action, bool success, string? errorMessage = null)
        {
            var properties = new Dictionary<string, string>
            {
                { "SubscriptionId", subscriptionId },
                { "Action", action },
                { "Success", success.ToString() }
            };

            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                properties["ErrorMessage"] = errorMessage;
            }

            _telemetryClient.TrackEvent("WebhookSubscription", properties);
            _logger?.LogInformation("📊 [Telemetry] Tracked webhook subscription: {Action}, Success: {Success}", action, success);
        }

        /// <summary>
        /// Track configuration loading
        /// </summary>
        public void TrackConfigurationLoad(string siteId, bool fromCache, int configItemCount)
        {
            var properties = new Dictionary<string, string>
            {
                { "SiteId", siteId },
                { "FromCache", fromCache.ToString() },
                { "ConfigItemCount", configItemCount.ToString() }
            };

            _telemetryClient.TrackEvent("ConfigurationLoaded", properties);
            _logger?.LogDebug("📊 [Telemetry] Tracked configuration load: SiteId: {SiteId}, FromCache: {FromCache}", siteId, fromCache);
        }

        /// <summary>
        /// Track custom metric
        /// </summary>
        public void TrackMetric(string metricName, double value, Dictionary<string, string>? properties = null)
        {
            _telemetryClient.TrackMetric(metricName, value, properties);
            _logger?.LogDebug("📊 [Telemetry] Tracked metric: {MetricName} = {Value}", metricName, value);
        }

        /// <summary>
        /// Track dependency (e.g., Graph API call)
        /// </summary>
        public void TrackDependency(string dependencyType, string dependencyName, string data, DateTimeOffset startTime, TimeSpan duration, bool success)
        {
            _telemetryClient.TrackDependency(dependencyType, dependencyName, data, startTime, duration, success);
            _logger?.LogDebug("📊 [Telemetry] Tracked dependency: {DependencyName}, Duration: {Duration}ms, Success: {Success}", 
                dependencyName, duration.TotalMilliseconds, success);
        }

        /// <summary>
        /// Track performance counter
        /// </summary>
        public void TrackPerformanceCounter(string counterName, double value)
        {
            var metric = new MetricTelemetry(counterName, value);
            _telemetryClient.TrackMetric(metric);
        }

        /// <summary>
        /// Flush telemetry (call before shutdown)
        /// </summary>
        public void Flush()
        {
            _telemetryClient.Flush();
            _logger?.LogDebug("📊 [Telemetry] Flushed telemetry");
        }
    }
}

