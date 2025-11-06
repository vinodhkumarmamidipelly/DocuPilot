using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Authentication.Azure;
using Azure.Identity;
using Microsoft.Identity.Client;
using System.Collections.Generic;

namespace SMEPilot.FunctionApp.Helpers
{
    public class GraphHelper
    {
        private readonly GraphServiceClient? _client;
        private readonly Config _cfg;
        private readonly bool _hasCredentials;

        public GraphHelper(Config cfg)
        {
            _cfg = cfg;
            _hasCredentials = !string.IsNullOrWhiteSpace(cfg.GraphClientId) && !string.IsNullOrWhiteSpace(cfg.GraphClientSecret) && !string.IsNullOrWhiteSpace(cfg.GraphTenantId);

            if (_hasCredentials)
            {
                var cca = ConfidentialClientApplicationBuilder
                            .Create(_cfg.GraphClientId)
                            .WithClientSecret(_cfg.GraphClientSecret)
                            .WithTenantId(_cfg.GraphTenantId)
                            .Build();

                var tokenCredential = new ClientSecretCredential(
                    _cfg.GraphTenantId,
                    _cfg.GraphClientId,
                    _cfg.GraphClientSecret);

                var authProvider = new AzureIdentityAuthenticationProvider(tokenCredential, scopes: new[] { "https://graph.microsoft.com/.default" });
                _client = new GraphServiceClient(authProvider);
            }
            else
            {
                _client = null;
            }
        }

        public async Task<DriveItem?> GetDriveItemAsync(string driveId, string itemId)
        {
            if (!_hasCredentials)
            {
                return null; // Mock mode - can't verify
            }

            try
            {
                return await _client!.Drives[driveId].Items[itemId].GetAsync();
            }
            catch
            {
                return null;
            }
        }

        public async Task<Stream> DownloadFileStreamAsync(string driveId, string itemId)
        {
            if (!_hasCredentials)
            {
                // Try to find sample file
                var samplePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "samples", "sample1.docx");
                if (File.Exists(samplePath))
                    return File.OpenRead(samplePath);

                // If no sample file, try samples/input folder
                var inputPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "samples", "input", "sample1.docx");
                if (File.Exists(inputPath))
                    return File.OpenRead(inputPath);

                // If still no file, return a mock empty document stream (for testing)
                Console.WriteLine($"Mock mode: Sample file not found. Creating mock stream for {itemId}");
                var mockBytes = System.Text.Encoding.UTF8.GetBytes("Mock document content for testing");
                return new MemoryStream(mockBytes);
            }

            var stream = await _client!.Drives[driveId].Items[itemId].Content.GetAsync();
            var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;
            return ms;
        }

        public async Task<DriveItem> UploadFileBytesAsync(string driveId, string folderPath, string fileName, byte[] bytes)
        {
            if (!_hasCredentials)
            {
                var outDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "samples", "output");
                Directory.CreateDirectory(outDir);
                var outPath = Path.Combine(outDir, fileName);
                await File.WriteAllBytesAsync(outPath, bytes);

                var meta = new Dictionary<string, object>
                {
                    {"SMEPilot_Enriched", true},
                    {"SMEPilot_Status", "Completed"},
                    {"SMEPilot_EnrichedFileUrl", outPath},
                    {"SMEPilot_EnrichedJobId", Guid.NewGuid().ToString()}
                };
                var metaPath = Path.ChangeExtension(outPath, ".metadata.json");
                await File.WriteAllTextAsync(metaPath, System.Text.Json.JsonSerializer.Serialize(meta));

                return new DriveItem { Id = Guid.NewGuid().ToString(), WebUrl = outPath };
            }

            var path = folderPath.TrimEnd('/') + "/" + fileName;
            using var ms = new MemoryStream(bytes);
            var item = await _client!.Drives[driveId].Root.ItemWithPath(path).Content.PutAsync(ms);
            return item;
        }

        public async Task<List<DriveItem>> GetRecentDriveItemsAsync(string driveId, int maxItems = 10)
        {
            if (!_hasCredentials)
            {
                Console.WriteLine($"Mock: Would query recent items from drive {driveId}");
                return new List<DriveItem>();
            }

            try
            {
                // Get root item first to get its ID
                var rootItem = await _client!.Drives[driveId].Root.GetAsync();
                if (rootItem == null || string.IsNullOrWhiteSpace(rootItem.Id))
                {
                    Console.WriteLine($"Error: Could not get root item for drive {driveId}");
                    return new List<DriveItem>();
                }

                // Get children of root using the root item ID
                var items = await _client!.Drives[driveId].Items[rootItem.Id].Children.GetAsync(config =>
                {
                    config.QueryParameters.Top = maxItems * 2; // Get more items, then sort
                });
                
                var itemList = items?.Value?.ToList() ?? new List<DriveItem>();
                
                // Filter out folders - only process files
                itemList = itemList
                    .Where(item => item.Folder == null) // Folders have Folder property, files don't
                    .ToList();
                
                // Sort by lastModifiedDateTime descending (most recent first)
                if (itemList.Any())
                {
                    itemList = itemList
                        .OrderByDescending(item => item.LastModifiedDateTime ?? item.CreatedDateTime ?? DateTimeOffset.MinValue)
                        .Take(maxItems)
                        .ToList();
                }
                
                Console.WriteLine($"Found {itemList.Count} recent files (excluding folders) in drive {driveId}");
                return itemList;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting recent drive items: {ex.Message}");
                Console.WriteLine($"Exception type: {ex.GetType().Name}");
                if (ex is Microsoft.Graph.Models.ODataErrors.ODataError oDataError)
                {
                    Console.WriteLine($"OData Error Code: {oDataError.Error?.Code}");
                    Console.WriteLine($"OData Error Message: {oDataError.Error?.Message}");
                }
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new List<DriveItem>();
            }
        }

        public async Task<Dictionary<string, object>?> GetListItemFieldsAsync(string driveId, string itemId)
        {
            if (!_hasCredentials)
            {
                // Mock: Check local metadata file
                var metaPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "samples", "output", $"{itemId}.metadata.json");
                if (File.Exists(metaPath))
                {
                    var json = await File.ReadAllTextAsync(metaPath);
                    return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                }
                return null;
            }

            try
            {
                var driveItem = await _client!.Drives[driveId].Items[itemId].GetAsync();
                if (driveItem?.ListItem != null)
                {
                    var siteId = driveItem.ParentReference?.SiteId;
                    var drive = await _client!.Drives[driveId].GetAsync();
                    if (drive?.List != null && siteId != null)
                    {
                        var listId = drive.List.Id;
                        var listItemId = driveItem.ListItem.Id;
                        
                        var fields = await _client.Sites[siteId].Lists[listId].Items[listItemId].Fields.GetAsync();
                        if (fields?.AdditionalData != null && fields.AdditionalData.Count > 0)
                        {
                            var result = new Dictionary<string, object>(fields.AdditionalData);
                            Console.WriteLine($"✅ [GetListItemFieldsAsync] Retrieved {result.Count} fields for item {itemId}");
                            return result;
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ [GetListItemFieldsAsync] Fields API returned null or empty AdditionalData for item {itemId}");
                            // Custom columns might not exist yet - this is OK for new files
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ [GetListItemFieldsAsync] Error retrieving metadata: {ex.Message}");
                Console.WriteLine($"   Exception type: {ex.GetType().Name}");
                // Don't throw - return null so processing can continue (file might be new)
            }
            
            return null;
        }

        public async Task UpdateListItemFieldsAsync(string driveId, string itemId, Dictionary<string, object> fields)
        {
            if (!_hasCredentials)
            {
                var outDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "samples", "output");
                Directory.CreateDirectory(outDir);
                var metaPath = Path.Combine(outDir, $"{itemId}.metadata.json");
                await File.WriteAllTextAsync(metaPath, System.Text.Json.JsonSerializer.Serialize(fields));
                return;
            }

            var driveItem = await _client!.Drives[driveId].Items[itemId].GetAsync();
            if (driveItem?.ListItem != null)
            {
                var siteId = driveItem.ParentReference?.SiteId;
                // Get list info from driveItem's drive which contains the list
                var drive = await _client!.Drives[driveId].GetAsync();
                if (drive?.List != null && siteId != null)
                {
                    var listId = drive.List.Id;
                    var listItemId = driveItem.ListItem.Id;
                    var fieldValueSet = new FieldValueSet { AdditionalData = fields };
                    await _client.Sites[siteId].Lists[listId].Items[listItemId].Fields.PatchAsync(fieldValueSet);
                }
            }
        }

        public async Task<Subscription> CreateSubscriptionAsync(string resource, string notificationUrl, DateTimeOffset expiration)
        {
            if (!_hasCredentials) throw new InvalidOperationException("Graph credentials are not configured.");
            
            try
            {
                Console.WriteLine($"=== CREATING SUBSCRIPTION ===");
                Console.WriteLine($"Resource: {resource}");
                Console.WriteLine($"Notification URL: {notificationUrl}");
                Console.WriteLine($"Expiration: {expiration}");
                Console.WriteLine($"Tenant ID: {_cfg.GraphTenantId}");
                Console.WriteLine($"Client ID: {_cfg.GraphClientId}");
                Console.WriteLine($"Client Secret: {(string.IsNullOrEmpty(_cfg.GraphClientSecret) ? "EMPTY" : "SET")}");
                
                // Try to get an access token first to verify authentication
                try
                {
                    var tokenCredential = new ClientSecretCredential(
                        _cfg.GraphTenantId,
                        _cfg.GraphClientId,
                        _cfg.GraphClientSecret);
                    
                    var tokenRequestContext = new Azure.Core.TokenRequestContext(new[] { "https://graph.microsoft.com/.default" });
                    var token = await tokenCredential.GetTokenAsync(tokenRequestContext, default);
                    Console.WriteLine($"Access token obtained successfully (length: {token.Token.Length})");
                    Console.WriteLine($"Token expires: {token.ExpiresOn}");
                }
                catch (Exception authEx)
                {
                    Console.WriteLine($"ERROR: Failed to get access token: {authEx.Message}");
                    Console.WriteLine($"Auth error type: {authEx.GetType().Name}");
                    throw new InvalidOperationException($"Authentication failed: {authEx.Message}", authEx);
                }
                
                var subscription = new Subscription
                {
                    ChangeType = "updated",  // Graph API only supports "updated" for drive subscriptions (not "created")
                    NotificationUrl = notificationUrl,
                    Resource = resource,
                    ExpirationDateTime = expiration,
                    ClientState = "SMEPilotState"
                };
                
                Console.WriteLine($"Calling Graph API to create subscription...");
                var result = await _client!.Subscriptions.PostAsync(subscription);
                Console.WriteLine($"✅ Subscription created successfully! ID: {result.Id}");
                return result;
            }
            catch (ODataError odataError)
            {
                var errorDetails = $"Graph API Error: {odataError.Error?.Code} - {odataError.Error?.Message}";
                Console.WriteLine($"=== GRAPH API ERROR ===");
                Console.WriteLine($"Error Code: {odataError.Error?.Code}");
                Console.WriteLine($"Error Message: {odataError.Error?.Message}");
                if (odataError.Error?.AdditionalData != null)
                {
                    Console.WriteLine($"Additional Data:");
                    foreach (var kvp in odataError.Error.AdditionalData)
                    {
                        Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                    }
                }
                if (odataError.Error?.InnerError != null)
                {
                    Console.WriteLine($"Inner Error:");
                    Console.WriteLine($"  InnerError: {odataError.Error.InnerError}");
                    // InnerError is a Dictionary<string, object> in Graph SDK
                    if (odataError.Error.InnerError is System.Collections.Generic.IDictionary<string, object> innerDict)
                    {
                        foreach (var kvp in innerDict)
                        {
                            Console.WriteLine($"    {kvp.Key}: {kvp.Value}");
                        }
                    }
                }
                Console.WriteLine($"=== END ERROR ===");
                throw new InvalidOperationException(errorDetails, odataError);
            }
        }
    }
}



