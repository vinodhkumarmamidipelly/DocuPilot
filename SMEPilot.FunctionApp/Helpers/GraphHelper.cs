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
using Microsoft.Extensions.Logging;
using Polly.Retry;

namespace SMEPilot.FunctionApp.Helpers
{
    public class GraphHelper
    {
        private readonly GraphServiceClient? _client;
        private readonly Config _cfg;
        private readonly bool _hasCredentials;
        private readonly ILogger<GraphHelper>? _logger;
        private readonly AsyncRetryPolicy _retryPolicy;

        public GraphHelper(Config cfg, ILogger<GraphHelper>? logger = null)
        {
            _cfg = cfg;
            _logger = logger;
            _hasCredentials = !string.IsNullOrWhiteSpace(cfg.GraphClientId) && !string.IsNullOrWhiteSpace(cfg.GraphClientSecret) && !string.IsNullOrWhiteSpace(cfg.GraphTenantId);
            _retryPolicy = RetryPolicyHelper.CreateGraphRetryPolicy(cfg, logger);

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
                _logger?.LogDebug("Mock mode: Sample file not found. Creating mock stream for {ItemId}", itemId);
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
                _logger?.LogDebug("Mock: Would query recent items from drive {DriveId}", driveId);
                return new List<DriveItem>();
            }

            try
            {
                // Get root item first to get its ID
                var rootItem = await _client!.Drives[driveId].Root.GetAsync();
                if (rootItem == null || string.IsNullOrWhiteSpace(rootItem.Id))
                {
                    _logger?.LogError("Error: Could not get root item for drive {DriveId}", driveId);
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
                
                _logger?.LogDebug("Found {Count} recent files (excluding folders) in drive {DriveId}", itemList.Count, driveId);
                return itemList;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting recent drive items: {Error}", ex.Message);
                if (ex is Microsoft.Graph.Models.ODataErrors.ODataError oDataError)
                {
                    _logger?.LogError("OData Error Code: {Code}, Message: {Message}", oDataError.Error?.Code, oDataError.Error?.Message);
                }
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
                _logger?.LogInformation("🔍 [GetListItemFieldsAsync] Retrieving metadata for ItemId: {ItemId}", itemId);
                
                // CRITICAL FIX: Expand listItem to ensure it is populated
                // Note: parentReference is a complex property (not navigation) and is included by default - cannot be expanded
                var driveItem = await _client!.Drives[driveId].Items[itemId].GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.Expand = new[] { "listItem" };
                });
                
                if (driveItem == null)
                {
                    _logger?.LogWarning("⚠️ [GetListItemFieldsAsync] DriveItem is null for ItemId: {ItemId}", itemId);
                    return null;
                }
                
                if (driveItem.ListItem == null)
                {
                    _logger?.LogWarning("⚠️ [GetListItemFieldsAsync] ListItem is null for ItemId: {ItemId} (DriveItem exists but ListItem not expanded)", itemId);
                    return null;
                }

                var siteId = driveItem.ParentReference?.SiteId;
                if (siteId == null)
                {
                    _logger?.LogWarning("⚠️ [GetListItemFieldsAsync] SiteId is null for ItemId: {ItemId}", itemId);
                    return null;
                }

                // CRITICAL FIX: Expand list to ensure it is populated
                var drive = await _client!.Drives[driveId].GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.Expand = new[] { "list" };
                });
                
                if (drive == null)
                {
                    _logger?.LogWarning("⚠️ [GetListItemFieldsAsync] Drive is null for DriveId: {DriveId}", driveId);
                    return null;
                }
                
                if (drive.List == null)
                {
                    _logger?.LogWarning("⚠️ [GetListItemFieldsAsync] List is null for DriveId: {DriveId} (Drive exists but List not expanded)", driveId);
                    return null;
                }

                var listId = drive.List.Id;
                var listItemId = driveItem.ListItem.Id;
                
                _logger?.LogInformation("📋 [GetListItemFieldsAsync] Querying metadata - SiteId: {SiteId}, ListId: {ListId}, ListItemId: {ListItemId}", 
                    siteId, listId, listItemId);
                
                // Use retry policy for Graph API call
                var fields = await RetryPolicyHelper.ExecuteWithRetryAsync(
                    _retryPolicy,
                    async () => await _client.Sites[siteId].Lists[listId].Items[listItemId].Fields.GetAsync(),
                    $"GetListItemFieldsAsync for ItemId: {itemId}",
                    _logger);
                
                if (fields?.AdditionalData != null && fields.AdditionalData.Count > 0)
                {
                    var result = new Dictionary<string, object>(fields.AdditionalData);
                    _logger?.LogInformation("✅ [GetListItemFieldsAsync] Retrieved {Count} fields for item {ItemId}. Keys: {Keys}", 
                        result.Count, itemId, string.Join(", ", result.Keys));
                    return result;
                }
                else
                {
                    _logger?.LogInformation("⚠️ [GetListItemFieldsAsync] Fields API returned null or empty AdditionalData for item {ItemId}. Fields object: {Fields}", 
                        itemId, fields != null ? "exists but AdditionalData is null/empty" : "null");
                    // Custom columns might not exist yet - this is OK for new files
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "⚠️ [GetListItemFieldsAsync] Error retrieving metadata for ItemId: {ItemId}: {Error}", itemId, ex.Message);
                if (ex is Microsoft.Graph.Models.ODataErrors.ODataError odataError)
                {
                    _logger?.LogWarning("   OData Error Code: {Code}, Message: {Message}", odataError.Error?.Code, odataError.Error?.Message);
                }
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

            try
            {
                _logger?.LogInformation("🔄 [UpdateListItemFieldsAsync] Starting metadata update for ItemId: {ItemId}, Fields: {FieldNames}", 
                    itemId, string.Join(", ", fields.Keys));
                
                // CRITICAL FIX: Expand listItem to ensure it is populated
                // Note: parentReference is a complex property (not navigation) and is included by default - cannot be expanded
                var driveItem = await _client!.Drives[driveId].Items[itemId].GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.Expand = new[] { "listItem" };
                });
                
                if (driveItem == null)
                {
                    _logger?.LogWarning("⚠️ [UpdateListItemFieldsAsync] DriveItem is null for ItemId: {ItemId}", itemId);
                    throw new InvalidOperationException($"DriveItem is null for ItemId: {itemId}");
                }
                
                if (driveItem.ListItem == null)
                {
                    _logger?.LogWarning("⚠️ [UpdateListItemFieldsAsync] ListItem is null for ItemId: {ItemId} (DriveItem exists but ListItem not expanded)", itemId);
                    throw new InvalidOperationException($"ListItem is null for ItemId: {itemId}");
                }

                var siteId = driveItem.ParentReference?.SiteId;
                if (siteId == null)
                {
                    _logger?.LogWarning("⚠️ [UpdateListItemFieldsAsync] SiteId is null for ItemId: {ItemId}", itemId);
                    throw new InvalidOperationException($"SiteId is null for ItemId: {itemId}");
                }

                // Get list info from driveItem's drive which contains the list
                // CRITICAL FIX: Expand list to ensure it is populated
                var drive = await _client!.Drives[driveId].GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.Expand = new[] { "list" };
                });
                
                if (drive == null)
                {
                    _logger?.LogWarning("⚠️ [UpdateListItemFieldsAsync] Drive is null for DriveId: {DriveId}", driveId);
                    throw new InvalidOperationException($"Drive is null for DriveId: {driveId}");
                }
                
                if (drive.List == null)
                {
                    _logger?.LogWarning("⚠️ [UpdateListItemFieldsAsync] List is null for DriveId: {DriveId} (Drive exists but List not expanded)", driveId);
                    throw new InvalidOperationException($"List is null for DriveId: {driveId}");
                }

                var listId = drive.List.Id;
                var listItemId = driveItem.ListItem.Id;
                
                _logger?.LogInformation("📋 [UpdateListItemFieldsAsync] Updating metadata - SiteId: {SiteId}, ListId: {ListId}, ListItemId: {ListItemId}", 
                    siteId, listId, listItemId);
                _logger?.LogInformation("📋 [UpdateListItemFieldsAsync] Fields to update: {Fields}", 
                    System.Text.Json.JsonSerializer.Serialize(fields));
                
                var fieldValueSet = new FieldValueSet { AdditionalData = fields };
                
                try
                {
                    await _client.Sites[siteId].Lists[listId].Items[listItemId].Fields.PatchAsync(fieldValueSet);
                    _logger?.LogInformation("✅ [UpdateListItemFieldsAsync] PatchAsync call completed successfully for ItemId: {ItemId}", itemId);
                }
                catch (Exception ex)
                {
                    // Check if this is a "field not recognized" error - this means custom fields don't exist in SharePoint
                    if (ex is Microsoft.Graph.Models.ODataErrors.ODataError odataError)
                    {
                        var errorCode = odataError.Error?.Code ?? "";
                        var errorMessage = odataError.Error?.Message ?? "";
                        
                        _logger?.LogError(ex, "❌ [UpdateListItemFieldsAsync] PatchAsync failed for ItemId: {ItemId}: {Error}", itemId, ex.Message);
                        _logger?.LogError("   OData Error Code: {Code}, Message: {Message}", errorCode, errorMessage);
                        
                        // CRITICAL: If fields don't exist, try to create them automatically
                        if (errorCode == "invalidRequest" && errorMessage.Contains("is not recognized"))
                        {
                            _logger?.LogWarning("⚠️ [UpdateListItemFieldsAsync] Custom metadata fields do not exist in SharePoint list!");
                            _logger?.LogWarning("   Attempting to create columns automatically...");
                            
                            // Try to create missing columns automatically
                            try
                            {
                                var columnsCreated = await EnsureColumnsExistAsync(siteId, listId);
                                
                                if (columnsCreated > 0)
                                {
                                    _logger?.LogInformation("✅ [UpdateListItemFieldsAsync] {Count} columns created successfully! Retrying metadata update...", columnsCreated);
                                    
                                    // Wait a moment for SharePoint to sync the new columns
                                    await Task.Delay(2000);
                                    
                                    // Retry the metadata update after creating columns
                                    await _client.Sites[siteId].Lists[listId].Items[listItemId].Fields.PatchAsync(fieldValueSet);
                                    _logger?.LogInformation("✅ [UpdateListItemFieldsAsync] Metadata update succeeded after creating columns!");
                                    return; // Success!
                                }
                                else
                                {
                                    _logger?.LogWarning("⚠️ [UpdateListItemFieldsAsync] No columns were created (may already exist or creation failed). Cannot retry metadata update.");
                                    _logger?.LogWarning("   Required fields: {Fields}", string.Join(", ", fields.Keys));
                                    _logger?.LogWarning("   Action required: Check logs above for column creation errors");
                                    _logger?.LogWarning("   📖 See: CREATE_SHAREPOINT_COLUMNS.md for manual setup instructions");
                                    // Don't throw - allow processing to continue
                                    return;
                                }
                            }
                            catch (Exception createEx)
                            {
                                _logger?.LogError(createEx, "❌ [UpdateListItemFieldsAsync] Failed to create columns automatically: {Error}", createEx.Message);
                                _logger?.LogWarning("   Required fields: {Fields}", string.Join(", ", fields.Keys));
                                _logger?.LogWarning("   Action required: Create these columns in SharePoint list '{ListId}' manually", listId);
                                _logger?.LogWarning("   📖 See: CREATE_SHAREPOINT_COLUMNS.md for step-by-step instructions");
                                _logger?.LogWarning("   For now, processing will continue without metadata tracking");
                                _logger?.LogWarning("   ⚠️ WARNING: Files will be reprocessed on each webhook until columns are created!");
                                // Don't throw - allow processing to continue
                                return;
                            }
                        }
                        
                        if (odataError.Error?.AdditionalData != null)
                        {
                            foreach (var kvp in odataError.Error.AdditionalData)
                            {
                                _logger?.LogError("   Additional Data: {Key}={Value}", kvp.Key, kvp.Value);
                            }
                        }
                    }
                    else
                    {
                        _logger?.LogError(ex, "❌ [UpdateListItemFieldsAsync] PatchAsync failed for ItemId: {ItemId}: {Error}", itemId, ex.Message);
                    }
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ [UpdateListItemFieldsAsync] Error updating metadata for ItemId: {ItemId}: {Error}", itemId, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Ensures all required SMEPilot columns exist in the SharePoint list.
        /// Creates missing columns automatically.
        /// </summary>
        /// <returns>The number of columns that were successfully created.</returns>
        public async Task<int> EnsureColumnsExistAsync(string siteId, string listId)
        {
            if (!_hasCredentials)
            {
                _logger?.LogWarning("⚠️ [EnsureColumnsExistAsync] Graph credentials not configured - cannot create columns");
                return 0;
            }

            try
            {
                _logger?.LogInformation("🔍 [EnsureColumnsExistAsync] Checking for required columns in list {ListId}...", listId);

                // Get existing columns
                var existingColumns = await _client!.Sites[siteId].Lists[listId].Columns.GetAsync();
                var existingColumnNames = existingColumns?.Value?.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

                _logger?.LogInformation("📋 [EnsureColumnsExistAsync] Found {Count} existing columns", existingColumnNames.Count);

                // Define required columns
                var requiredColumns = new[]
                {
                    new { Name = "SMEPilot_Enriched", Type = "boolean", DisplayName = "SMEPilot Enriched", Description = "Indicates if document has been processed by SMEPilot" },
                    new { Name = "SMEPilot_Status", Type = "text", DisplayName = "SMEPilot Status", Description = "Processing status: Processing, Completed, Failed, Retry, MetadataUpdateFailed" },
                    new { Name = "SMEPilot_EnrichedFileUrl", Type = "url", DisplayName = "SMEPilot Enriched File URL", Description = "URL to the enriched document" },
                    new { Name = "SMEPilot_EnrichedJobId", Type = "text", DisplayName = "SMEPilot Enriched Job ID", Description = "Unique job ID for this processing run" },
                    new { Name = "SMEPilot_Confidence", Type = "number", DisplayName = "SMEPilot Confidence", Description = "Confidence score (0-100)" },
                    new { Name = "SMEPilot_Classification", Type = "text", DisplayName = "SMEPilot Classification", Description = "Document classification: Functional, Technical, etc." },
                    new { Name = "SMEPilot_ErrorMessage", Type = "note", DisplayName = "SMEPilot Error Message", Description = "Error message if processing failed" },
                    new { Name = "SMEPilot_LastErrorTime", Type = "dateTime", DisplayName = "SMEPilot Last Error Time", Description = "Timestamp of last error (for retry logic)" }
                };

                int createdCount = 0;
                foreach (var column in requiredColumns)
                {
                    // Check if column already exists (case-insensitive)
                    if (existingColumnNames.Contains(column.Name))
                    {
                        _logger?.LogDebug("✅ [EnsureColumnsExistAsync] Column '{ColumnName}' already exists, skipping", column.Name);
                        continue;
                    }

                    try
                    {
                        _logger?.LogInformation("➕ [EnsureColumnsExistAsync] Creating column '{ColumnName}' (Type: {Type})...", column.Name, column.Type);

                        // Create column definition based on type
                        ColumnDefinition columnDefinition = column.Type switch
                        {
                            "boolean" => new ColumnDefinition
                            {
                                Name = column.Name,
                                DisplayName = column.DisplayName,
                                Description = column.Description,
                                Boolean = new BooleanColumn()
                            },
                            "text" => new ColumnDefinition
                            {
                                Name = column.Name,
                                DisplayName = column.DisplayName,
                                Description = column.Description,
                                Text = new TextColumn()
                            },
                            "url" => new ColumnDefinition
                            {
                                Name = column.Name,
                                DisplayName = column.DisplayName,
                                Description = column.Description,
                                HyperlinkOrPicture = new HyperlinkOrPictureColumn()
                            },
                            "number" => new ColumnDefinition
                            {
                                Name = column.Name,
                                DisplayName = column.DisplayName,
                                Description = column.Description,
                                Number = new NumberColumn()
                            },
                            "note" => new ColumnDefinition
                            {
                                Name = column.Name,
                                DisplayName = column.DisplayName,
                                Description = column.Description,
                                Text = new TextColumn() // Note columns use Text in Graph API
                            },
                            "dateTime" => new ColumnDefinition
                            {
                                Name = column.Name,
                                DisplayName = column.DisplayName,
                                Description = column.Description,
                                DateTime = new DateTimeColumn()
                            },
                            _ => throw new NotSupportedException($"Column type '{column.Type}' is not supported")
                        };

                        // Create the column
                        var createdColumn = await _client.Sites[siteId].Lists[listId].Columns.PostAsync(columnDefinition);
                        _logger?.LogInformation("✅ [EnsureColumnsExistAsync] Successfully created column '{ColumnName}' (ID: {ColumnId})", column.Name, createdColumn.Id);
                        createdCount++;

                        // Add to existing set to avoid duplicate creation attempts
                        existingColumnNames.Add(column.Name);
                    }
                    catch (Exception ex)
                    {
                        // Log detailed error information
                        if (ex is ODataError odataError)
                        {
                            var errorCode = odataError.Error?.Code ?? "";
                            var errorMessage = odataError.Error?.Message ?? "";
                            
                            _logger?.LogError(ex, "❌ [EnsureColumnsExistAsync] Failed to create column '{ColumnName}': {Error}", column.Name, ex.Message);
                            _logger?.LogError("   OData Error Code: {Code}, Message: {Message}", errorCode, errorMessage);
                            
                            if (odataError.Error?.AdditionalData != null)
                            {
                                foreach (var kvp in odataError.Error.AdditionalData)
                                {
                                    _logger?.LogError("   Additional Data: {Key}={Value}", kvp.Key, kvp.Value);
                                }
                            }
                            
                            // Check if column was created by another process (race condition)
                            if (errorCode == "invalidRequest" && 
                                (errorMessage.Contains("already exists") || errorMessage.Contains("duplicate")))
                            {
                                _logger?.LogWarning("⚠️ [EnsureColumnsExistAsync] Column '{ColumnName}' already exists (created by another process), skipping", column.Name);
                                existingColumnNames.Add(column.Name);
                                continue; // Skip to next column
                            }
                            
                            // Check for permission errors
                            if (errorCode == "Forbidden" || errorCode == "Unauthorized" || errorCode == "accessDenied" || 
                                errorMessage.Contains("permission", StringComparison.OrdinalIgnoreCase) || 
                                errorMessage.Contains("access denied", StringComparison.OrdinalIgnoreCase))
                            {
                                _logger?.LogError("❌ [EnsureColumnsExistAsync] PERMISSION ERROR: App does not have permission to create columns!");
                                _logger?.LogError("   Error Code: {Code}, Message: {Message}", errorCode, errorMessage);
                                _logger?.LogError("   ⚠️ IMPORTANT: Sites.ReadWrite.All may NOT be sufficient for column creation!");
                                _logger?.LogError("   Required permission: Sites.Manage.All (Application permission) - REQUIRED for column creation");
                                _logger?.LogError("   Sites.ReadWrite.All only allows reading/writing items, NOT schema changes (columns)");
                                _logger?.LogError("   Admin consent: REQUIRED");
                                _logger?.LogError("   📖 See: PERMISSIONS_SETUP.md for step-by-step permission setup instructions");
                                _logger?.LogError("   ⚠️ After adding Sites.Manage.All, RESTART the Function App to clear cached tokens!");
                                _logger?.LogError("   ⚠️ Without this permission, columns must be created manually (see CREATE_SHAREPOINT_COLUMNS.md)");
                                // Don't continue - permission errors affect all columns
                                throw;
                            }
                        }
                        else
                        {
                            _logger?.LogError(ex, "❌ [EnsureColumnsExistAsync] Failed to create column '{ColumnName}': {Error}", column.Name, ex.Message);
                        }
                        // Continue with other columns even if one fails (unless it's a permission error)
                    }
                }

                _logger?.LogInformation("✅ [EnsureColumnsExistAsync] Column check complete. Created {CreatedCount} new columns", createdCount);
                return createdCount;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ [EnsureColumnsExistAsync] Error ensuring columns exist: {Error}", ex.Message);
                throw;
            }
        }

        public async Task<Subscription> CreateSubscriptionAsync(string resource, string notificationUrl, DateTimeOffset expiration)
        {
            if (!_hasCredentials) throw new InvalidOperationException("Graph credentials are not configured.");
            
            try
            {
                _logger?.LogInformation("=== CREATING SUBSCRIPTION ===");
                _logger?.LogInformation("Resource: {Resource}", resource);
                _logger?.LogInformation("Notification URL: {NotificationUrl}", notificationUrl);
                _logger?.LogInformation("Expiration: {Expiration}", expiration);
                _logger?.LogInformation("Tenant ID: {TenantId}", _cfg.GraphTenantId);
                _logger?.LogInformation("Client ID: {ClientId}", _cfg.GraphClientId);
                _logger?.LogInformation("Client Secret: {Status}", string.IsNullOrEmpty(_cfg.GraphClientSecret) ? "EMPTY" : "SET");
                
                // Try to get an access token first to verify authentication
                try
                {
                    var tokenCredential = new ClientSecretCredential(
                        _cfg.GraphTenantId,
                        _cfg.GraphClientId,
                        _cfg.GraphClientSecret);
                    
                    var tokenRequestContext = new Azure.Core.TokenRequestContext(new[] { "https://graph.microsoft.com/.default" });
                    var token = await tokenCredential.GetTokenAsync(tokenRequestContext, default);
                    _logger?.LogInformation("Access token obtained successfully (length: {Length})", token.Token.Length);
                    _logger?.LogInformation("Token expires: {ExpiresOn}", token.ExpiresOn);
                }
                catch (Exception authEx)
                {
                    _logger?.LogError(authEx, "ERROR: Failed to get access token: {Error}", authEx.Message);
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
                
                _logger?.LogInformation("Calling Graph API to create subscription...");
                var result = await _client!.Subscriptions.PostAsync(subscription);
                _logger?.LogInformation("✅ Subscription created successfully! ID: {SubscriptionId}", result.Id);
                return result;
            }
            catch (ODataError odataError)
            {
                var errorDetails = $"Graph API Error: {odataError.Error?.Code} - {odataError.Error?.Message}";
                _logger?.LogError(odataError, "=== GRAPH API ERROR ===");
                _logger?.LogError("Error Code: {Code}", odataError.Error?.Code);
                _logger?.LogError("Error Message: {Message}", odataError.Error?.Message);
                if (odataError.Error?.AdditionalData != null)
                {
                    foreach (var kvp in odataError.Error.AdditionalData)
                    {
                        _logger?.LogError("Additional Data: {Key}={Value}", kvp.Key, kvp.Value);
                    }
                }
                if (odataError.Error?.InnerError != null)
                {
                    _logger?.LogError("Inner Error: {InnerError}", odataError.Error.InnerError);
                    // InnerError is a Dictionary<string, object> in Graph SDK
                    if (odataError.Error.InnerError is System.Collections.Generic.IDictionary<string, object> innerDict)
                    {
                        foreach (var kvp in innerDict)
                        {
                            _logger?.LogError("  {Key}={Value}", kvp.Key, kvp.Value);
                        }
                    }
                }
                _logger?.LogError("=== END ERROR ===");
                throw new InvalidOperationException(errorDetails, odataError);
            }
        }
    }
}



