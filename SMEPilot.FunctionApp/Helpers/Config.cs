using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SMEPilot.FunctionApp.Services;
using Microsoft.Extensions.Logging;

namespace SMEPilot.FunctionApp.Helpers
{
    public class Config
    {
        // SharePoint configuration cache (per site)
        private static readonly Dictionary<string, Dictionary<string, object>> _sharePointConfigCache = new Dictionary<string, Dictionary<string, object>>();
        private static readonly object _configCacheLock = new object();

        // Graph API credentials (always from environment variables)
        public string GraphTenantId => Environment.GetEnvironmentVariable("Graph_TenantId");
        public string GraphClientId => Environment.GetEnvironmentVariable("Graph_ClientId");
        public string GraphClientSecret => Environment.GetEnvironmentVariable("Graph_ClientSecret");
        
        // Enriched folder path - from SharePoint config or environment variable
        public string EnrichedFolderRelativePath => GetSharePointConfigValue("DestinationFolderPath") 
            ?? Environment.GetEnvironmentVariable("EnrichedFolderRelativePath") 
            ?? "/Shared Documents/SMEPilot Enriched Docs";
        
        // Source folder path - from SharePoint config
        public string SourceFolderPath => GetSharePointConfigValue("SourceFolderPath") ?? "";
        
        // Template configuration - from SharePoint config or environment variable
        public string TemplateFileUrl => GetSharePointConfigValue("TemplateFileUrl") ?? "";
        public string TemplateLibraryPath => GetSharePointConfigValue("TemplateLibraryPath") 
            ?? Environment.GetEnvironmentVariable("TemplateLibraryPath") 
            ?? "/Shared Documents/Templates";
        public string TemplateFileName => GetSharePointConfigValue("TemplateFileName") 
            ?? Environment.GetEnvironmentVariable("TemplateFileName") 
            ?? "UniversalOrgTemplate.dotx";
        
        // Azure Computer Vision OCR configuration (optional)
        public string AzureVisionEndpoint => Environment.GetEnvironmentVariable("AzureVision_Endpoint");
        public string AzureVisionKey => Environment.GetEnvironmentVariable("AzureVision_Key");
        
        // Spire license keys
        public string SpirePdfLicense => Environment.GetEnvironmentVariable("SpirePDFLicense");
        public string SpireDocLicense => Environment.GetEnvironmentVariable("SpireDOCLicense");
        
        // Retry configuration - from SharePoint config or environment variable
        public int MaxRetryAttempts => GetSharePointConfigIntValue("MaxRetryAttempts") 
            ?? (int.TryParse(Environment.GetEnvironmentVariable("MaxRetryAttempts"), out var retries) ? retries : 3);
        public int RetryDelaySeconds => int.TryParse(Environment.GetEnvironmentVariable("RetryDelaySeconds"), out var delay) ? delay : 2;
        public int MaxRetryDelaySeconds => int.TryParse(Environment.GetEnvironmentVariable("MaxRetryDelaySeconds"), out var maxDelay) ? maxDelay : 30;
        
        // File upload retry configuration
        public int MaxUploadRetries => int.TryParse(Environment.GetEnvironmentVariable("MaxUploadRetries"), out var uploadRetries) ? uploadRetries : 5;
        public int MaxMetadataRetries => int.TryParse(Environment.GetEnvironmentVariable("MaxMetadataRetries"), out var metadataRetries) ? metadataRetries : 5;
        public int FileLockWaitSeconds => int.TryParse(Environment.GetEnvironmentVariable("FileLockWaitSeconds"), out var waitSeconds) ? waitSeconds : 10;
        
        // File size limits - from SharePoint config or environment variable (default: 50MB)
        public long MaxFileSizeBytes
        {
            get
            {
                var maxSizeMB = GetSharePointConfigIntValue("MaxFileSizeMB");
                if (maxSizeMB.HasValue)
                {
                    return maxSizeMB.Value * 1024L * 1024L; // Convert MB to bytes
                }
                
                if (long.TryParse(Environment.GetEnvironmentVariable("MaxFileSizeBytes"), out var maxSize))
                {
                    return maxSize;
                }
                
                return 50 * 1024 * 1024; // 50MB default (updated from 4MB)
            }
        }
        
        // Notification deduplication
        public int NotificationDedupWindowSeconds => int.TryParse(Environment.GetEnvironmentVariable("NotificationDedupWindowSeconds"), out var dedupWindow) ? dedupWindow : 30;
        
        // Retry state configuration
        public int RetryWaitMinutes => int.TryParse(Environment.GetEnvironmentVariable("RetryWaitMinutes"), out var retryWait) ? retryWait : 5;
        
        // Metadata change handling - from SharePoint config
        public string MetadataChangeHandling => GetSharePointConfigValue("MetadataChangeHandling") ?? "Skip";
        
        // Current site ID (set when loading SharePoint config)
        private string? _currentSiteId;
        
        /// <summary>
        /// Load configuration from SharePoint SMEPilotConfig list
        /// </summary>
        /// <param name="graph">GraphHelper instance</param>
        /// <param name="siteId">SharePoint site ID</param>
        /// <param name="logger">Optional logger</param>
        /// <param name="forceRefresh">Force refresh even if cached</param>
        public async Task LoadSharePointConfigAsync(GraphHelper graph, string siteId, ILogger? logger = null, bool forceRefresh = false)
        {
            _currentSiteId = siteId;
            
            lock (_configCacheLock)
            {
                // Check if we have cached config for this site
                if (!forceRefresh && _sharePointConfigCache.ContainsKey(siteId))
                {
                    logger?.LogDebug("📦 [Config] Using cached SharePoint configuration for site {SiteId}", siteId);
                    return;
                }
            }

            try
            {
                logger?.LogInformation("🔄 [Config] Loading configuration from SharePoint for site {SiteId}", siteId);
                
                var configService = new ConfigService(graph, siteId, "SMEPilotConfig", 
                    logger != null ? new LoggerFactory().CreateLogger<ConfigService>() : null);
                
                var config = await configService.GetConfigurationAsync(forceRefresh);
                
                lock (_configCacheLock)
                {
                    _sharePointConfigCache[siteId] = config;
                }
                
                logger?.LogInformation("✅ [Config] SharePoint configuration loaded successfully for site {SiteId}", siteId);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "❌ [Config] Failed to load SharePoint configuration for site {SiteId}. Using defaults/environment variables.", siteId);
                // Continue with defaults - don't throw
            }
        }
        
        /// <summary>
        /// Get SharePoint config value for current site
        /// </summary>
        private string? GetSharePointConfigValue(string key)
        {
            if (string.IsNullOrWhiteSpace(_currentSiteId))
                return null;
                
            lock (_configCacheLock)
            {
                if (_sharePointConfigCache.TryGetValue(_currentSiteId, out var config))
                {
                    if (config.TryGetValue(key, out var value) && value != null)
                    {
                        return value.ToString();
                    }
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Get SharePoint config int value for current site
        /// </summary>
        private int? GetSharePointConfigIntValue(string key)
        {
            var value = GetSharePointConfigValue(key);
            if (string.IsNullOrWhiteSpace(value))
                return null;
                
            if (int.TryParse(value, out var intValue))
                return intValue;
                
            return null;
        }
        
        /// <summary>
        /// Clear SharePoint config cache for a specific site or all sites
        /// </summary>
        public static void ClearSharePointConfigCache(string? siteId = null)
        {
            lock (_configCacheLock)
            {
                if (siteId != null)
                {
                    _sharePointConfigCache.Remove(siteId);
                }
                else
                {
                    _sharePointConfigCache.Clear();
                }
            }
        }
        
        // Validation
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(GraphTenantId))
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(GraphClientId))
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(GraphClientSecret))
            {
                return false;
            }
            return true;
        }
    }
}

