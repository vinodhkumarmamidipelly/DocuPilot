using System;

namespace SMEPilot.FunctionApp.Helpers
{
    public class Config
    {
        public string GraphTenantId => Environment.GetEnvironmentVariable("Graph_TenantId");
        public string GraphClientId => Environment.GetEnvironmentVariable("Graph_ClientId");
        public string GraphClientSecret => Environment.GetEnvironmentVariable("Graph_ClientSecret");
        
        public string EnrichedFolderRelativePath => Environment.GetEnvironmentVariable("EnrichedFolderRelativePath") ?? "/Shared Documents/ProcessedDocs";
        
        // Azure Computer Vision OCR configuration (optional)
        public string AzureVisionEndpoint => Environment.GetEnvironmentVariable("AzureVision_Endpoint");
        public string AzureVisionKey => Environment.GetEnvironmentVariable("AzureVision_Key");
        
        // Spire license keys
        public string SpirePdfLicense => Environment.GetEnvironmentVariable("SpirePDFLicense");
        public string SpireDocLicense => Environment.GetEnvironmentVariable("SpireDOCLicense");
        
        // Retry configuration
        public int MaxRetryAttempts => int.TryParse(Environment.GetEnvironmentVariable("MaxRetryAttempts"), out var retries) ? retries : 3;
        public int RetryDelaySeconds => int.TryParse(Environment.GetEnvironmentVariable("RetryDelaySeconds"), out var delay) ? delay : 2;
        public int MaxRetryDelaySeconds => int.TryParse(Environment.GetEnvironmentVariable("MaxRetryDelaySeconds"), out var maxDelay) ? maxDelay : 30;
        
        // File upload retry configuration
        public int MaxUploadRetries => int.TryParse(Environment.GetEnvironmentVariable("MaxUploadRetries"), out var uploadRetries) ? uploadRetries : 5;
        public int MaxMetadataRetries => int.TryParse(Environment.GetEnvironmentVariable("MaxMetadataRetries"), out var metadataRetries) ? metadataRetries : 5;
        public int FileLockWaitSeconds => int.TryParse(Environment.GetEnvironmentVariable("FileLockWaitSeconds"), out var waitSeconds) ? waitSeconds : 10;
        
        // File size limits
        public long MaxFileSizeBytes => long.TryParse(Environment.GetEnvironmentVariable("MaxFileSizeBytes"), out var maxSize) ? maxSize : 4 * 1024 * 1024; // 4MB default
        
        // Notification deduplication
        public int NotificationDedupWindowSeconds => int.TryParse(Environment.GetEnvironmentVariable("NotificationDedupWindowSeconds"), out var dedupWindow) ? dedupWindow : 30;
        
        // Retry state configuration
        public int RetryWaitMinutes => int.TryParse(Environment.GetEnvironmentVariable("RetryWaitMinutes"), out var retryWait) ? retryWait : 5;
        
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

