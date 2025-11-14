using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SMEPilot.FunctionApp.Helpers;
using Microsoft.Graph.Models;

namespace SMEPilot.FunctionApp.Services
{
    /// <summary>
    /// Service to read configuration from SharePoint SMEPilotConfig list
    /// Implements caching with 5-minute refresh interval
    /// </summary>
    public class ConfigService
    {
        private readonly GraphHelper _graph;
        private readonly ILogger<ConfigService>? _logger;
        private readonly string _siteId;
        private readonly string _listName;
        
        // Configuration cache
        private static Dictionary<string, object>? _cachedConfig;
        private static DateTime _cacheTimestamp = DateTime.MinValue;
        private static readonly TimeSpan CacheRefreshInterval = TimeSpan.FromMinutes(5);
        private static readonly object _cacheLock = new object();

        public ConfigService(GraphHelper graph, string siteId, string listName = "SMEPilotConfig", ILogger<ConfigService>? logger = null)
        {
            _graph = graph;
            _siteId = siteId;
            _listName = listName;
            _logger = logger;
        }

        /// <summary>
        /// Get configuration from SharePoint list with caching
        /// </summary>
        public async Task<Dictionary<string, object>> GetConfigurationAsync(bool forceRefresh = false)
        {
            lock (_cacheLock)
            {
                // Return cached config if still valid and not forcing refresh
                if (!forceRefresh && _cachedConfig != null && DateTime.UtcNow - _cacheTimestamp < CacheRefreshInterval)
                {
                    _logger?.LogDebug("📦 [ConfigService] Returning cached configuration (age: {Age} seconds)", 
                        (DateTime.UtcNow - _cacheTimestamp).TotalSeconds);
                    return _cachedConfig;
                }
            }

            try
            {
                _logger?.LogInformation("🔄 [ConfigService] Fetching configuration from SharePoint list '{ListName}' in site {SiteId}", 
                    _listName, _siteId);

                // Get list items
                var items = await _graph.GetListItemsByNameAsync(_siteId, _listName, top: 1);

                if (items == null || !items.Any())
                {
                    _logger?.LogWarning("⚠️ [ConfigService] No configuration items found in list '{ListName}'. Using defaults.", _listName);
                    return GetDefaultConfiguration();
                }

                // Get first item (should only be one configuration item)
                var configItem = items.First();
                var fields = configItem.Fields?.AdditionalData;

                if (fields == null || !fields.Any())
                {
                    _logger?.LogWarning("⚠️ [ConfigService] Configuration item has no fields. Using defaults.");
                    return GetDefaultConfiguration();
                }

                // Parse configuration values
                var config = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                // Source and Destination paths
                if (fields.TryGetValue("SourceFolderPath", out var sourcePath) && sourcePath != null)
                    config["SourceFolderPath"] = sourcePath.ToString() ?? "";
                if (fields.TryGetValue("DestinationFolderPath", out var destPath) && destPath != null)
                    config["DestinationFolderPath"] = destPath.ToString() ?? "";
                if (fields.TryGetValue("TargetLibraryUrl", out var targetUrl) && targetUrl != null)
                    config["DestinationFolderPath"] = targetUrl.ToString() ?? config.GetValueOrDefault("DestinationFolderPath", "").ToString() ?? "";

                // Template configuration
                if (fields.TryGetValue("TemplateFileUrl", out var templateUrl) && templateUrl != null)
                    config["TemplateFileUrl"] = templateUrl.ToString() ?? "";
                if (fields.TryGetValue("TemplateLibraryPath", out var templateLib) && templateLib != null)
                    config["TemplateLibraryPath"] = templateLib.ToString() ?? "";
                if (fields.TryGetValue("TemplateFileName", out var templateName) && templateName != null)
                    config["TemplateFileName"] = templateName.ToString() ?? "";

                // Processing settings
                if (fields.TryGetValue("MaxFileSizeMB", out var maxSize) && maxSize != null)
                {
                    if (int.TryParse(maxSize.ToString(), out var maxSizeMB))
                        config["MaxFileSizeMB"] = maxSizeMB;
                    else
                        config["MaxFileSizeMB"] = 50; // Default
                }
                else
                {
                    config["MaxFileSizeMB"] = 50; // Default
                }

                if (fields.TryGetValue("RetryAttempts", out var retries) && retries != null)
                {
                    if (int.TryParse(retries.ToString(), out var retryCount))
                        config["MaxRetryAttempts"] = retryCount;
                    else
                        config["MaxRetryAttempts"] = 3; // Default
                }
                else
                {
                    config["MaxRetryAttempts"] = 3; // Default
                }

                // Metadata change handling
                if (fields.TryGetValue("MetadataChangeHandling", out var metaHandling) && metaHandling != null)
                    config["MetadataChangeHandling"] = metaHandling.ToString() ?? "Skip";
                else
                    config["MetadataChangeHandling"] = "Skip"; // Default

                // Store all raw fields for future use
                config["_RawFields"] = fields;

                // Update cache
                lock (_cacheLock)
                {
                    _cachedConfig = config;
                    _cacheTimestamp = DateTime.UtcNow;
                }

                _logger?.LogInformation("✅ [ConfigService] Configuration loaded successfully. Keys: {Keys}", 
                    string.Join(", ", config.Keys.Where(k => !k.StartsWith("_"))));

                return config;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ [ConfigService] Error loading configuration from SharePoint. Using defaults.");
                
                // Return default configuration on error
                var defaults = GetDefaultConfiguration();
                
                // Update cache with defaults to prevent repeated failures
                lock (_cacheLock)
                {
                    _cachedConfig = defaults;
                    _cacheTimestamp = DateTime.UtcNow;
                }
                
                return defaults;
            }
        }

        /// <summary>
        /// Get default configuration values
        /// </summary>
        private Dictionary<string, object> GetDefaultConfiguration()
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["SourceFolderPath"] = "",
                ["DestinationFolderPath"] = "/Shared Documents/SMEPilot Enriched Docs",
                ["TemplateFileUrl"] = "",
                ["TemplateLibraryPath"] = "/Shared Documents/Templates",
                ["TemplateFileName"] = "UniversalOrgTemplate.dotx",
                ["MaxFileSizeMB"] = 50,
                ["MaxRetryAttempts"] = 3,
                ["MetadataChangeHandling"] = "Skip"
            };
        }

        /// <summary>
        /// Clear configuration cache (force refresh on next call)
        /// </summary>
        public static void ClearCache()
        {
            lock (_cacheLock)
            {
                _cachedConfig = null;
                _cacheTimestamp = DateTime.MinValue;
            }
        }
    }
}

