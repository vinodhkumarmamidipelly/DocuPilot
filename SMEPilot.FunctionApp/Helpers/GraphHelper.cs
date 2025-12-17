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
using System.Net.Http;
using System.Net.Http.Headers;
using Azure.Core;
using Newtonsoft.Json.Linq;
using SMEPilot.FunctionApp.Models;

namespace SMEPilot.FunctionApp.Helpers
{
    public class GraphHelper
    {
        private readonly GraphServiceClient? _client;
        private readonly Config _cfg;
        private readonly bool _hasCredentials;
        private readonly ILogger<GraphHelper>? _logger;
        private readonly AsyncRetryPolicy _retryPolicy;
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _driveSiteCache = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();

        public GraphHelper(Config cfg, ILogger<GraphHelper>? logger = null)
        {
            _cfg = cfg;
            _logger = logger;
            _hasCredentials = !string.IsNullOrWhiteSpace(cfg.GraphClientId) && !string.IsNullOrWhiteSpace(cfg.GraphClientSecret) && !string.IsNullOrWhiteSpace(cfg.GraphTenantId);
            _retryPolicy = RetryPolicyHelper.CreateGraphRetryPolicy(cfg, logger);

            if (_hasCredentials)
            {
                // Default client bound to the configured Graph_TenantId.
                var tokenCredential = new ClientSecretCredential(
                    _cfg.GraphTenantId,
                    _cfg.GraphClientId,
                    _cfg.GraphClientSecret);

                var authProvider = new AzureIdentityAuthenticationProvider(
                    tokenCredential,
                    scopes: new[] { "https://graph.microsoft.com/.default" });

                _client = new GraphServiceClient(authProvider);
            }
            else
            {
                _client = null;
            }
        }

        /// <summary>
        /// Creates a GraphServiceClient for the specified tenant. If tenantId is null or empty,
        /// the default client (bound to Graph_TenantId) is returned.
        /// </summary>
        private GraphServiceClient GetClientForTenant(string? tenantId = null)
        {
            if (!_hasCredentials)
            {
                throw new InvalidOperationException("Graph credentials are not configured.");
            }

            // If no specific tenant requested or it matches the configured tenant, reuse the default client.
            if (string.IsNullOrWhiteSpace(tenantId) ||
                tenantId.Equals(_cfg.GraphTenantId, StringComparison.OrdinalIgnoreCase))
            {
                if (_client == null)
                {
                    throw new InvalidOperationException("Default Graph client is not initialized.");
                }

                return _client;
            }

            // Create a lightweight client for this tenant on demand.
            _logger?.LogInformation("🌐 [GraphHelper] Creating per-tenant Graph client for TenantId={TenantId}", tenantId);

            var tokenCredential = new ClientSecretCredential(
                tenantId,
                _cfg.GraphClientId,
                _cfg.GraphClientSecret);

            var authProvider = new AzureIdentityAuthenticationProvider(
                tokenCredential,
                scopes: new[] { "https://graph.microsoft.com/.default" });

            return new GraphServiceClient(authProvider);
        }

        public async Task<DriveItem?> GetDriveItemAsync(string driveId, string itemId, string? tenantId = null)
        {
            if (!_hasCredentials)
            {
                return null; // Mock mode - can't verify
            }

            try
            {
                var client = GetClientForTenant(tenantId);
                return await client.Drives[driveId].Items[itemId].GetAsync();
            }
            catch
            {
                return null;
            }
        }

        #region Tracking (SMEPilotRuns) – processing status & idempotency
        // NOTE:
        // - SharePoint has a built-in read-only field named "Author". Writing to fields["Author"] will fail.
        // - In some tenants, the app may not have permission to create custom columns on SMEPilotRuns.
        // To keep history working reliably, we store per-run author + section summary in the existing
        // ErrorMessage text field for *Succeeded* runs, using a recognizable prefix.
        private const string TrackingMetaPrefix = "SMEPilotMeta:";

        private static string ODataStringLiteral(string value) =>
            (value ?? string.Empty).Replace("'", "''");

        private static string BuildSucceededMeta(
            string? author,
            string? sectionSummary,
            string? changeDescription,
            string? rawUiVersion,
            DateTimeOffset? rawLastModifiedUtc,
            Dictionary<string, string>? sectionFingerprints,
            Dictionary<string, string>? sectionTitles)
        {
            var obj = new JObject();
            if (!string.IsNullOrWhiteSpace(author)) obj["author"] = author;
            if (!string.IsNullOrWhiteSpace(sectionSummary)) obj["sectionSummary"] = sectionSummary;
            if (!string.IsNullOrWhiteSpace(changeDescription)) obj["changeDescription"] = changeDescription;
            if (!string.IsNullOrWhiteSpace(rawUiVersion)) obj["rawUiVersion"] = rawUiVersion;
            if (rawLastModifiedUtc.HasValue) obj["rawLastModifiedUtc"] = rawLastModifiedUtc.Value.ToString("O");
            if (sectionFingerprints != null && sectionFingerprints.Count > 0)
            {
                // Store as JSON object: normalizedHeading -> hash
                obj["sectionFingerprints"] = JObject.FromObject(sectionFingerprints);
            }
            if (sectionTitles != null && sectionTitles.Count > 0)
            {
                // Store as JSON object: normalizedHeading -> display title
                obj["sectionTitles"] = JObject.FromObject(sectionTitles);
            }
            return TrackingMetaPrefix + obj.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static void TryExtractSucceededMeta(ProcessingRunRecord record)
        {
            if (!string.Equals(record.Status, "Succeeded", StringComparison.OrdinalIgnoreCase))
                return;
            if (string.IsNullOrWhiteSpace(record.ErrorMessage))
                return;
            if (!record.ErrorMessage.StartsWith(TrackingMetaPrefix, StringComparison.OrdinalIgnoreCase))
                return;

            var json = record.ErrorMessage.Substring(TrackingMetaPrefix.Length);
            try
            {
                var obj = JObject.Parse(json);
                record.Author = obj["author"]?.ToString();
                record.SectionSummary = obj["sectionSummary"]?.ToString();
                record.ChangeDescription = obj["changeDescription"]?.ToString();
                record.RawUiVersion = obj["rawUiVersion"]?.ToString();
                if (DateTimeOffset.TryParse(obj["rawLastModifiedUtc"]?.ToString(), out var lm))
                {
                    record.RawLastModifiedUtc = lm;
                }

                if (obj["sectionFingerprints"] is JObject fpObj)
                {
                    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var prop in fpObj.Properties())
                    {
                        var v = prop.Value?.ToString();
                        if (!string.IsNullOrWhiteSpace(prop.Name) && !string.IsNullOrWhiteSpace(v))
                        {
                            dict[prop.Name] = v!;
                        }
                    }
                    record.SectionFingerprints = dict;
                }

                if (obj["sectionTitles"] is JObject titleObj)
                {
                    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var prop in titleObj.Properties())
                    {
                        var v = prop.Value?.ToString();
                        if (!string.IsNullOrWhiteSpace(prop.Name) && !string.IsNullOrWhiteSpace(v))
                        {
                            dict[prop.Name] = v!;
                        }
                    }
                    record.SectionTitles = dict;
                }
                // Hide internal payload from consumers that treat ErrorMessage as an error string.
                record.ErrorMessage = null;
            }
            catch
            {
                // ignore
            }
        }


        /// <summary>
        /// Logical name of the SharePoint list used to track processing runs.
        /// </summary>
        private const string ProcessingRunsListName = "SMEPilotRuns";

        /// <summary>
        /// Retrieves the latest ProcessingRunRecord for a given raw file (by RawDriveId + RawItemId).
        /// If the tracking list is missing or cannot be read, this logs a warning and returns null
        /// so callers can fall back to existing behavior.
        /// </summary>
        public async Task<ProcessingRunRecord?> GetLatestProcessingRunAsync(string siteId, string rawDriveId, string rawItemId, string? tenantId = null)
        {
            if (!_hasCredentials)
            {
                _logger?.LogWarning("⚠️ [Tracking] Graph credentials not configured; cannot read {ListName} list. Falling back to legacy idempotency.", ProcessingRunsListName);
                return null;
            }

            try
            {
                var client = GetClientForTenant(tenantId);
                var listId = await EnsureProcessingRunsListExistsAsync(siteId, tenantId);
                if (string.IsNullOrWhiteSpace(listId))
                {
                    _logger?.LogWarning("⚠️ [Tracking] {ListName} list not available on site {SiteId} for tenant {TenantId}.", ProcessingRunsListName, siteId, tenantId ?? "default");
                    return null;
                }

                _logger?.LogInformation("🔍 [Tracking] Querying latest run for RawDriveId={DriveId}, RawItemId={ItemId} in tenant {TenantId}", rawDriveId, rawItemId, tenantId ?? "default");

                var d = ODataStringLiteral(rawDriveId);
                var i = ODataStringLiteral(rawItemId);
                var items = await client.Sites[siteId].Lists[listId].Items.GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.Expand = new[] { "fields" };
                    requestConfig.QueryParameters.Top = 1;
                    requestConfig.QueryParameters.Filter = $"fields/RawDriveId eq '{d}' and fields/RawItemId eq '{i}'";
                    requestConfig.QueryParameters.Orderby = new[] { "createdDateTime desc" };
                    // Graph requires this exact Prefer token when filtering by non-indexed fields.
                    requestConfig.Headers.Add("Prefer", "HonorNonIndexedQueriesWarningMayFailRandomly");
                });

                if (items?.Value == null || items.Value.Count == 0)
                {
                    _logger?.LogInformation("ℹ️ [Tracking] No tracking records found yet for RawDriveId={DriveId}, RawItemId={ItemId}", rawDriveId, rawItemId);
                    return null;
                }

                var first = items.Value.FirstOrDefault();
                var latest = first?.Fields?.AdditionalData != null ? MapFieldsToProcessingRunRecord(first.Fields.AdditionalData) : null;
                if (latest != null) TryExtractSucceededMeta(latest);

                if (latest == null)
                {
                    _logger?.LogInformation("ℹ️ [Tracking] No matching tracking records for RawDriveId={DriveId}, RawItemId={ItemId} in tenant {TenantId}", rawDriveId, rawItemId, tenantId ?? "default");
                }
                else
                {
                    _logger?.LogInformation("📋 [Tracking] Latest run for RawDriveId={DriveId}, RawItemId={ItemId} in tenant {TenantId}: Status={Status}, Hash={Hash}, LastUpdated={LastUpdated:o}",
                        latest.RawDriveId, latest.RawItemId, tenantId ?? "default", latest.Status, latest.ContentHash, latest.LastUpdatedUtc);
                }

                return latest;
            }
            catch (ODataError odataError)
            {
                _logger?.LogWarning(odataError, "⚠️ [Tracking] ODataError while reading {ListName} on site {SiteId}: Code={Code}, Message={Message}",
                    ProcessingRunsListName, siteId, odataError.Error?.Code ?? "Unknown", odataError.Error?.Message ?? "Unknown");
                if (odataError.Error?.AdditionalData != null)
                {
                    foreach (var kvp in odataError.Error.AdditionalData)
                    {
                        _logger?.LogDebug("   [Tracking] Additional Data: {Key}={Value}", kvp.Key, kvp.Value);
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "⚠️ [Tracking] Unexpected error reading {ListName} on site {SiteId}: {Error}. Falling back to legacy idempotency.",
                    ProcessingRunsListName, siteId, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Retrieves the latest *Succeeded* ProcessingRunRecord for a given raw file.
        /// This intentionally ignores newer runs that are still in Processing/Failed state
        /// so content-hash idempotency can compare against the last known-good run.
        /// </summary>
        public async Task<ProcessingRunRecord?> GetLatestSucceededProcessingRunAsync(string siteId, string rawDriveId, string rawItemId, string? tenantId = null)
        {
            if (!_hasCredentials)
            {
                _logger?.LogWarning("⚠️ [Tracking] Graph credentials not configured; cannot read {ListName} list. Falling back to legacy idempotency.", ProcessingRunsListName);
                return null;
            }

            try
            {
                var client = GetClientForTenant(tenantId);
                var listId = await EnsureProcessingRunsListExistsAsync(siteId, tenantId);
                if (string.IsNullOrWhiteSpace(listId))
                {
                    _logger?.LogWarning("⚠️ [Tracking] {ListName} list not available on site {SiteId} for tenant {TenantId}.", ProcessingRunsListName, siteId, tenantId ?? "default");
                    return null;
                }

                _logger?.LogInformation("🔍 [Tracking] Querying latest *succeeded* run for RawDriveId={DriveId}, RawItemId={ItemId} in tenant {TenantId}", rawDriveId, rawItemId, tenantId ?? "default");

                var d = ODataStringLiteral(rawDriveId);
                var i = ODataStringLiteral(rawItemId);
                var itemsResponse = await client.Sites[siteId].Lists[listId].Items.GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.Expand = new[] { "fields" };
                    requestConfig.QueryParameters.Top = 1;
                    requestConfig.QueryParameters.Filter = $"fields/RawDriveId eq '{d}' and fields/RawItemId eq '{i}' and fields/Status eq 'Succeeded'";
                    requestConfig.QueryParameters.Orderby = new[] { "createdDateTime desc" };
                    requestConfig.Headers.Add("Prefer", "HonorNonIndexedQueriesWarningMayFailRandomly");
                });

                var first = itemsResponse?.Value?.FirstOrDefault();
                if (first?.Fields?.AdditionalData == null)
                {
                    _logger?.LogInformation("ℹ️ [Tracking] No succeeded tracking records for RawDriveId={DriveId}, RawItemId={ItemId} in tenant {TenantId}", rawDriveId, rawItemId, tenantId ?? "default");
                    return null;
                }

                var latestSucceeded = MapFieldsToProcessingRunRecord(first.Fields.AdditionalData);
                TryExtractSucceededMeta(latestSucceeded);
                _logger?.LogInformation("📋 [Tracking] Latest *succeeded* run for RawDriveId={DriveId}, RawItemId={ItemId} in tenant {TenantId}: Status={Status}, Hash={Hash}, LastUpdated={LastUpdated:o}",
                    latestSucceeded.RawDriveId, latestSucceeded.RawItemId, tenantId ?? "default", latestSucceeded.Status, latestSucceeded.ContentHash, latestSucceeded.LastUpdatedUtc);
                return latestSucceeded;
            }
            catch (ODataError odataError)
            {
                _logger?.LogWarning(odataError, "⚠️ [Tracking] ODataError querying latest succeeded run for RawDriveId={DriveId}, RawItemId={ItemId}: Code={Code}, Message={Message}",
                    rawDriveId, rawItemId, odataError.Error?.Code ?? "Unknown", odataError.Error?.Message ?? "Unknown");
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "⚠️ [Tracking] Unexpected error querying latest succeeded run for RawDriveId={DriveId}, RawItemId={ItemId}: {Error}", rawDriveId, rawItemId, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Retrieves all *Succeeded* ProcessingRunRecord entries for a given raw file (by RawDriveId + RawItemId),
        /// ordered by LastUpdatedUtc ascending. This is used to rebuild Version History and Change Log tables
        /// deterministically on every enrichment run.
        /// </summary>
        public async Task<List<ProcessingRunRecord>> GetSucceededProcessingRunsAsync(
            string siteId,
            string rawDriveId,
            string rawItemId,
            string? tenantId = null)
        {
            var results = new List<ProcessingRunRecord>();

            if (!_hasCredentials)
            {
                _logger?.LogWarning("⚠️ [Tracking] Graph credentials not configured; cannot read {ListName} list.", ProcessingRunsListName);
                return results;
            }

            try
            {
                var client = GetClientForTenant(tenantId);
                var listId = await EnsureProcessingRunsListExistsAsync(siteId, tenantId);
                if (string.IsNullOrWhiteSpace(listId))
                {
                    return results;
                }

                var d = ODataStringLiteral(rawDriveId);
                var i = ODataStringLiteral(rawItemId);
                var itemsResponse = await client.Sites[siteId].Lists[listId].Items.GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.Expand = new[] { "fields" };
                    requestConfig.QueryParameters.Top = 200;
                    requestConfig.QueryParameters.Filter = $"fields/RawDriveId eq '{d}' and fields/RawItemId eq '{i}' and fields/Status eq 'Succeeded'";
                    requestConfig.QueryParameters.Orderby = new[] { "createdDateTime asc" };
                    requestConfig.Headers.Add("Prefer", "HonorNonIndexedQueriesWarningMayFailRandomly");
                });

                if (itemsResponse?.Value == null)
                {
                    return results;
                }

                foreach (var item in itemsResponse.Value)
                {
                    var fields = item.Fields?.AdditionalData;
                    if (fields == null || fields.Count == 0)
                        continue;

                    var r = MapFieldsToProcessingRunRecord(fields);
                    TryExtractSucceededMeta(r);
                    results.Add(r);
                }

                return results.OrderBy(r => r.LastUpdatedUtc).ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "⚠️ [Tracking] Failed to query succeeded run history for RawDriveId={DriveId}, RawItemId={ItemId}: {Error}",
                    rawDriveId, rawItemId, ex.Message);
                return results;
            }
        }

        /// <summary>
        /// Cache a mapping between driveId and siteId when we successfully discover it from a file context.
        /// This allows later deletion events (which may not include siteId) to reuse the cached mapping
        /// without needing to call Graph again.
        /// </summary>
        public void CacheDriveSiteMapping(string driveId, string siteId)
        {
            if (string.IsNullOrWhiteSpace(driveId) || string.IsNullOrWhiteSpace(siteId))
                return;

            _driveSiteCache[driveId] = siteId;
            _logger?.LogDebug("🧠 [DriveSiteCache] Cached mapping DriveId={DriveId} -> SiteId={SiteId}", driveId, siteId);
        }

        /// <summary>
        /// Try to get a cached siteId for a given driveId.
        /// Returns null if no mapping is known.
        /// </summary>
        public string? TryGetCachedSiteIdForDrive(string driveId)
        {
            if (string.IsNullOrWhiteSpace(driveId))
                return null;

            return _driveSiteCache.TryGetValue(driveId, out var siteId) ? siteId : null;
        }

        /// <summary>
        /// Shared helper to compute the latest *Succeeded* run from a list items sequence,
        /// optionally filtering by RawItemId when provided.
        /// </summary>
        private ProcessingRunRecord? FilterLatestSucceededRun(IEnumerable<ListItem>? items, string siteId, string rawDriveId, string? rawItemId, string? tenantId)
        {
            if (items == null)
            {
                if (!string.IsNullOrWhiteSpace(rawItemId))
                {
                    _logger?.LogInformation("ℹ️ [Tracking] No tracking records found yet for RawDriveId={DriveId}, RawItemId={ItemId}", rawDriveId, rawItemId);
                }
                else
                {
                    _logger?.LogInformation("ℹ️ [Tracking] No tracking records found yet for RawDriveId={DriveId} on site {SiteId}", rawDriveId, siteId);
                }
                return null;
            }

            ProcessingRunRecord? latestSucceeded = null;
            foreach (var item in items)
            {
                var fields = item.Fields?.AdditionalData;
                if (fields == null || fields.Count == 0)
                    continue;

                if (!fields.TryGetValue("RawDriveId", out var dObj))
                {
                    continue;
                }

                var d = dObj?.ToString();
                if (!string.Equals(d, rawDriveId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(rawItemId))
                {
                    if (!fields.TryGetValue("RawItemId", out var iObj))
                        continue;

                    var i = iObj?.ToString();
                    if (!string.Equals(i, rawItemId, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                var record = MapFieldsToProcessingRunRecord(fields);
                if (!string.Equals(record.Status, "Succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (latestSucceeded == null || record.LastUpdatedUtc > latestSucceeded.LastUpdatedUtc)
                {
                    latestSucceeded = record;
                }
            }

            if (latestSucceeded == null)
            {
                if (!string.IsNullOrWhiteSpace(rawItemId))
                {
                    _logger?.LogInformation("ℹ️ [Tracking] No succeeded tracking records for RawDriveId={DriveId}, RawItemId={ItemId} in tenant {TenantId}", rawDriveId, rawItemId, tenantId ?? "default");
                }
                else
                {
                    _logger?.LogInformation("ℹ️ [Tracking] No succeeded tracking records for RawDriveId={DriveId} on site {SiteId} in tenant {TenantId}", rawDriveId, siteId, tenantId ?? "default");
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(rawItemId))
                {
                    _logger?.LogInformation("📋 [Tracking] Latest *succeeded* run for RawDriveId={DriveId}, RawItemId={ItemId} in tenant {TenantId}: Status={Status}, Hash={Hash}, LastUpdated={LastUpdated:o}",
                        latestSucceeded.RawDriveId, latestSucceeded.RawItemId, tenantId ?? "default", latestSucceeded.Status, latestSucceeded.ContentHash, latestSucceeded.LastUpdatedUtc);
                }
                else
                {
                    _logger?.LogInformation("📋 [Tracking] Latest *succeeded* run for RawDriveId={DriveId} on site {SiteId} in tenant {TenantId}: Status={Status}, Hash={Hash}, LastUpdated={LastUpdated:o}",
                        latestSucceeded.RawDriveId, siteId, tenantId ?? "default", latestSucceeded.Status, latestSucceeded.ContentHash, latestSucceeded.LastUpdatedUtc);
                }
            }

            return latestSucceeded;
        }


        /// <summary>
        /// Upserts a ProcessingRunRecord entry for a given raw file.
        /// For now we always append a new list item and rely on LastUpdatedUtc when
        /// querying the "latest" record. If the list or columns are missing, this logs
        /// detailed errors but does not throw, so enrichment can still succeed.
        /// </summary>
        public async Task UpsertProcessingRunAsync(string siteId, ProcessingRunRecord record, string? tenantId = null)
        {
            if (!_hasCredentials)
            {
                _logger?.LogWarning("⚠️ [Tracking] Graph credentials not configured; cannot write to {ListName}.", ProcessingRunsListName);
                return;
            }

            try
            {
                var client = GetClientForTenant(tenantId);
                var listId = await EnsureProcessingRunsListExistsAsync(siteId, tenantId);
                if (string.IsNullOrWhiteSpace(listId))
                {
                    _logger?.LogWarning("⚠️ [Tracking] {ListName} list not available on site {SiteId} for tenant {TenantId}; skipping tracking write.", ProcessingRunsListName, siteId, tenantId ?? "default");
                    return;
                }

                var fields = new FieldValueSet
                {
                    AdditionalData = new Dictionary<string, object>
                    {
                        ["Title"] = record.Title ?? string.Empty,
                        ["RawDriveId"] = record.RawDriveId,
                        ["RawItemId"] = record.RawItemId,
                        ["ContentHash"] = record.ContentHash,
                        ["Version"] = record.Version ?? string.Empty,
                        ["EnrichedUrl"] = record.EnrichedUrl ?? string.Empty,
                        ["EnrichedDriveId"] = record.EnrichedDriveId ?? string.Empty,
                        ["EnrichedItemId"] = record.EnrichedItemId ?? string.Empty,
                        ["Status"] = record.Status,
                        ["ErrorMessage"] = string.Equals(record.Status, "Succeeded", StringComparison.OrdinalIgnoreCase)
                            ? BuildSucceededMeta(record.Author, record.SectionSummary, record.ChangeDescription, record.RawUiVersion, record.RawLastModifiedUtc, record.SectionFingerprints, record.SectionTitles)
                            : (record.ErrorMessage ?? string.Empty),
                        ["LastUpdatedUtc"] = record.LastUpdatedUtc.ToString("O")
                    }
                };

                // IMPORTANT: We always append a new row per run, so that we can reconstruct
                // Version History and Change Log tables deterministically from SMEPilotRuns.
                // Idempotency uses "latest succeeded" and in-memory locks; history is derived
                // from all succeeded records.
                var listItem = new ListItem { Fields = fields };

                _logger?.LogInformation("📝 [Tracking] Writing new run record to {ListName} for RawDriveId={DriveId}, RawItemId={ItemId}, Status={Status} in tenant {TenantId}",
                    ProcessingRunsListName, record.RawDriveId, record.RawItemId, record.Status, tenantId ?? "default");

                await client.Sites[siteId].Lists[listId].Items.PostAsync(listItem);

                _logger?.LogInformation("✅ [Tracking] Tracking record written/updated successfully in {ListName} for tenant {TenantId}.", ProcessingRunsListName, tenantId ?? "default");
            }
            catch (ODataError odataError)
            {
                _logger?.LogWarning(odataError, "⚠️ [Tracking] ODataError writing to {ListName} on site {SiteId}: Code={Code}, Message={Message}",
                    ProcessingRunsListName, siteId, odataError.Error?.Code ?? "Unknown", odataError.Error?.Message ?? "Unknown");
                if (odataError.Error?.AdditionalData != null)
                {
                    foreach (var kvp in odataError.Error.AdditionalData)
                    {
                        _logger?.LogDebug("   [Tracking] Additional Data: {Key}={Value}", kvp.Key, kvp.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "⚠️ [Tracking] Unexpected error writing to {ListName} on site {SiteId}: {Error}. Skipping tracking write.",
                    ProcessingRunsListName, siteId, ex.Message);
            }
        }

        #endregion

        private async Task<string?> EnsureProcessingRunsListExistsAsync(string siteId, string? tenantId = null)
        {
            if (!_hasCredentials)
            {
                _logger?.LogWarning("⚠️ [Tracking] Graph credentials not configured; cannot ensure {ListName} list.", ProcessingRunsListName);
                return null;
            }

            try
            {
                _logger?.LogInformation("🔍 [Tracking] Ensuring {ListName} list exists on site {SiteId} for tenant {TenantId}...", ProcessingRunsListName, siteId, tenantId ?? "default");

                var client = GetClientForTenant(tenantId);

                // Try to find existing list by display name
                var lists = await client.Sites[siteId].Lists.GetAsync();
                var existing = lists?.Value?.FirstOrDefault(l =>
                    string.Equals(l.DisplayName, ProcessingRunsListName, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    _logger?.LogInformation("✅ [Tracking] Found existing list {ListName} (Id={ListId}) for tenant {TenantId}", ProcessingRunsListName, existing.Id, tenantId ?? "default");
                    await EnsureProcessingRunsColumnsExistAsync(siteId, existing.Id!, tenantId);
                    return existing.Id;
                }

                // Create list if not found
                var newList = new List
                {
                    DisplayName = ProcessingRunsListName,
                    Description = "Tracks SMEPilot processing runs for idempotency and diagnostics."
                    // Rely on SharePoint's default template (generic list); no explicit ListInfo property in this SDK.
                };

                var created = await client.Sites[siteId].Lists.PostAsync(newList);
                if (created == null || string.IsNullOrWhiteSpace(created.Id))
                {
                    _logger?.LogWarning("⚠️ [Tracking] Failed to create list {ListName} on site {SiteId} for tenant {TenantId}.", ProcessingRunsListName, siteId, tenantId ?? "default");
                    return null;
                }

                _logger?.LogInformation("✅ [Tracking] Created list {ListName} (Id={ListId}) on site {SiteId} for tenant {TenantId}.", ProcessingRunsListName, created.Id, siteId, tenantId ?? "default");
                await EnsureProcessingRunsColumnsExistAsync(siteId, created.Id, tenantId);
                return created.Id;
            }
            catch (ODataError odataError)
            {
                _logger?.LogWarning(odataError, "⚠️ [Tracking] ODataError ensuring {ListName} list on site {SiteId} for tenant {TenantId}: Code={Code}, Message={Message}",
                    ProcessingRunsListName, siteId, tenantId ?? "default", odataError.Error?.Code ?? "Unknown", odataError.Error?.Message ?? "Unknown");
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "⚠️ [Tracking] Unexpected error ensuring {ListName} list on site {SiteId} for tenant {TenantId}: {Error}", ProcessingRunsListName, siteId, tenantId ?? "default", ex.Message);
                return null;
            }
        }

        private async Task EnsureProcessingRunsColumnsExistAsync(string siteId, string listId, string? tenantId = null)
        {
            try
            {
                _logger?.LogInformation("🔍 [Tracking] Ensuring required columns exist on {ListName} (ListId={ListId}) for tenant {TenantId}", ProcessingRunsListName, listId, tenantId ?? "default");

                var client = GetClientForTenant(tenantId);
                var existingColumns = await client.Sites[siteId].Lists[listId].Columns.GetAsync();
                var existingNames = existingColumns?.Value?.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
                                   ?? new HashSet<string>();

                // Keep schema simple and robust across tenants: all columns are text,
                // including LastUpdatedUtc (stored as an ISO 8601 string). This avoids
                // issues with more complex column types and reduces the chances of
                // invalidRequest errors when creating columns.
                var requiredColumns = new[]
                {
                    new { Name = "RawDriveId", Type = "text", DisplayName = "Raw Drive Id", Description = "DriveId of source document" },
                    new { Name = "RawItemId", Type = "text", DisplayName = "Raw Item Id", Description = "ItemId of source document" },
                    new { Name = "ContentHash", Type = "text", DisplayName = "Content Hash", Description = "SHA256 hash of raw content" },
                    new { Name = "Version", Type = "text", DisplayName = "Version", Description = "Logical document version used for this enriched run" },
                    new { Name = "EnrichedUrl", Type = "text", DisplayName = "Enriched Url", Description = "Url of the enriched document" },
                    new { Name = "EnrichedDriveId", Type = "text", DisplayName = "Enriched Drive Id", Description = "DriveId of enriched (destination) document" },
                    new { Name = "EnrichedItemId", Type = "text", DisplayName = "Enriched Item Id", Description = "ItemId of enriched (destination) document" },
                    new { Name = "Status", Type = "text", DisplayName = "Status", Description = "Processing status (Processing, Succeeded, Failed, etc.)" },
                    new { Name = "ErrorMessage", Type = "text", DisplayName = "Error Message", Description = "Error details if processing failed" },
                    new { Name = "LastUpdatedUtc", Type = "text", DisplayName = "Last Updated (UTC)", Description = "Last time this record was updated (UTC, ISO 8601 string)" }
                };

                foreach (var col in requiredColumns)
                {
                    if (existingNames.Contains(col.Name))
                    {
                        _logger?.LogDebug("✅ [Tracking] Column '{ColumnName}' already exists on {ListName}", col.Name, ProcessingRunsListName);
                        continue;
                    }

                    ColumnDefinition def = new ColumnDefinition
                    {
                        Name = col.Name,
                        DisplayName = col.DisplayName,
                        Description = col.Description,
                        Text = new TextColumn()
                    };

                    var created = await client.Sites[siteId].Lists[listId].Columns.PostAsync(def);
                    _logger?.LogInformation("✅ [Tracking] Created column '{ColumnName}' (Id={ColumnId}) on {ListName} for tenant {TenantId}", col.Name, created?.Id ?? "null", ProcessingRunsListName, tenantId ?? "default");
                    existingNames.Add(col.Name);
                }
            }
            catch (ODataError odataError)
            {
                _logger?.LogWarning(odataError, "⚠️ [Tracking] ODataError ensuring columns on {ListName} (ListId={ListId}) for tenant {TenantId}: Code={Code}, Message={Message}",
                    ProcessingRunsListName, listId, tenantId ?? "default", odataError.Error?.Code ?? "Unknown", odataError.Error?.Message ?? "Unknown");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "⚠️ [Tracking] Unexpected error ensuring columns on {ListName} (ListId={ListId}) for tenant {TenantId}: {Error}", ProcessingRunsListName, listId, tenantId ?? "default", ex.Message);
            }
        }

        private static ProcessingRunRecord MapFieldsToProcessingRunRecord(IDictionary<string, object> fields)
        {
            var record = new ProcessingRunRecord
            {
                Title = fields.TryGetValue("Title", out var t) ? t?.ToString() : null,
                RawDriveId = fields.TryGetValue("RawDriveId", out var d) ? d?.ToString() ?? string.Empty : string.Empty,
                RawItemId = fields.TryGetValue("RawItemId", out var i) ? i?.ToString() ?? string.Empty : string.Empty,
                ContentHash = fields.TryGetValue("ContentHash", out var h) ? h?.ToString() ?? string.Empty : string.Empty,
                Version = fields.TryGetValue("Version", out var v) ? v?.ToString() : null,
                Status = fields.TryGetValue("Status", out var s) ? s?.ToString() ?? string.Empty : string.Empty,
                ErrorMessage = fields.TryGetValue("ErrorMessage", out var e) ? e?.ToString() : null,
                EnrichedUrl = fields.TryGetValue("EnrichedUrl", out var url) ? url?.ToString() : null,
                EnrichedDriveId = fields.TryGetValue("EnrichedDriveId", out var ed) ? ed?.ToString() : null,
                EnrichedItemId = fields.TryGetValue("EnrichedItemId", out var ei) ? ei?.ToString() : null,
                LastUpdatedUtc = DateTimeOffset.UtcNow
            };

            if (fields.TryGetValue("LastUpdatedUtc", out var lu) && lu != null)
            {
                if (DateTimeOffset.TryParse(lu.ToString(), out var parsed))
                {
                    record.LastUpdatedUtc = parsed;
                }
            }

            // Extract per-run metadata (author + section summary) from our safe meta payload if present.
            TryExtractSucceededMeta(record);

            return record;
        }

        /// <summary>
        /// Gets a drive item by file path within a drive
        /// </summary>
        public async Task<DriveItem?> GetDriveItemByPathAsync(string driveId, string filePath)
        {
            if (!_hasCredentials)
            {
                return null; // Mock mode - can't verify
            }

            try
            {
                return await _client!.Drives[driveId].Root.ItemWithPath(filePath).GetAsync();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Checks if a file is in the configured source folder
        /// Returns true if file is in source folder, false if not, null if cannot determine (fail open)
        /// </summary>
        public async Task<bool?> IsFileInSourceFolderAsync(string driveId, string itemId, string? sourceFolderPath, string? siteId = null)
        {
            _logger?.LogInformation("🔍 [IsFileInSourceFolderAsync] Checking if file {ItemId} (DriveId: {DriveId}) is in source folder '{SourceFolder}' (SiteId: {SiteId})", 
                itemId, driveId, sourceFolderPath ?? "null", siteId ?? "null");
            
            if (!_hasCredentials)
            {
                _logger?.LogWarning("⚠️ [IsFileInSourceFolderAsync] No credentials available, allowing processing (mock mode)");
                return null; // Mock mode - fail open
            }

            // If source folder path is not configured, allow processing (fail open)
            if (string.IsNullOrWhiteSpace(sourceFolderPath))
            {
                _logger?.LogWarning("⚠️ [IsFileInSourceFolderAsync] Source folder path not configured, allowing processing");
                return null; // Fail open - don't block if config is missing
            }

            try
            {
                // Get the file's DriveItem to access its parent reference
                _logger?.LogInformation("🔍 [IsFileInSourceFolderAsync] Getting file item {ItemId} from drive {DriveId}", itemId, driveId);
                var fileItem = await GetDriveItemAsync(driveId, itemId);
                if (fileItem == null)
                {
                    _logger?.LogWarning("⚠️ [IsFileInSourceFolderAsync] Could not get file item {ItemId}, allowing processing", itemId);
                    return null; // Fail open - if we can't get the file, allow processing
                }

                // Get the file's parent folder ID
                var fileParentId = fileItem.ParentReference?.Id;
                var fileParentPath = fileItem.ParentReference?.Path;
                _logger?.LogInformation("🔍 [IsFileInSourceFolderAsync] File {ItemId} - ParentId: {ParentId}, ParentPath: {ParentPath}", 
                    itemId, fileParentId ?? "null", fileParentPath ?? "null");
                
                if (string.IsNullOrWhiteSpace(fileParentId))
                {
                    _logger?.LogWarning("⚠️ [IsFileInSourceFolderAsync] File {ItemId} has no parent ID, allowing processing", itemId);
                    return null; // Fail open - if we can't determine parent, allow processing
                }

                // Get site ID if not provided
                if (string.IsNullOrWhiteSpace(siteId))
                {
                    siteId = fileItem.ParentReference?.SiteId;
                    _logger?.LogInformation("🔍 [IsFileInSourceFolderAsync] SiteId from file: {SiteId}", siteId ?? "null");
                    if (string.IsNullOrWhiteSpace(siteId))
                    {
                        _logger?.LogWarning("⚠️ [IsFileInSourceFolderAsync] Could not determine site ID, allowing processing");
                        return null; // Fail open
                    }
                }

                // Resolve the source folder path to get its item ID
                _logger?.LogInformation("🔍 [IsFileInSourceFolderAsync] Resolving source folder path '{SourceFolder}' for site {SiteId}", sourceFolderPath, siteId);
                var (sourceDriveId, sourceFolderItemId) = await ResolveFolderPathAsync(siteId, sourceFolderPath);
                _logger?.LogInformation("🔍 [IsFileInSourceFolderAsync] Resolved source folder - DriveId: {SourceDriveId}, FolderItemId: {SourceFolderItemId}", 
                    sourceDriveId ?? "null", sourceFolderItemId ?? "null");
                
                // If source folder is library root (sourceFolderItemId is null), check if file is in the same drive
                if (string.IsNullOrWhiteSpace(sourceFolderItemId))
                {
                    // Source folder is library root - check if file is in the same drive
                    if (driveId == sourceDriveId)
                    {
                        _logger?.LogInformation("✅ [IsFileInSourceFolderAsync] File {ItemId} is in source folder (library root) of drive {DriveId}", itemId, driveId);
                        return true;
                    }
                    else
                    {
                        _logger?.LogInformation("⏭️ [IsFileInSourceFolderAsync] File {ItemId} is NOT in source folder. File drive: {FileDriveId}, Source drive: {SourceDriveId}", 
                            itemId, driveId, sourceDriveId);
                        return false;
                    }
                }

                // Source folder is a subfolder - check if file's parent matches source folder
                // We need to check if the file's parent folder is the source folder or a descendant
                // For simplicity, we'll check if the parent ID matches or if we can traverse up
                if (fileParentId == sourceFolderItemId)
                {
                    _logger?.LogInformation("✅ [IsFileInSourceFolderAsync] File {ItemId} is directly in source folder (ItemId: {SourceFolderId})", itemId, sourceFolderItemId);
                    return true;
                }

                // Check if file's parent is a descendant of source folder by checking parent chain
                // This is a simplified check - we'll verify the parent is in the source folder's drive
                if (driveId == sourceDriveId)
                {
                    // File is in the same drive as source folder
                    // For now, we'll allow it if it's in the same drive (fail open for subfolders)
                    // A more precise check would require traversing the parent chain
                    _logger?.LogDebug("🔍 [IsFileInSourceFolderAsync] File {ItemId} is in same drive as source folder, allowing processing (parent: {ParentId}, source: {SourceId})", 
                        itemId, fileParentId, sourceFolderItemId);
                    return null; // Fail open - if in same drive but parent doesn't match, allow (could be in subfolder)
                }
                else
                {
                    _logger?.LogInformation("⏭️ [IsFileInSourceFolderAsync] File {ItemId} is NOT in source folder. File drive: {FileDriveId}, Source drive: {SourceDriveId}", 
                        itemId, driveId, sourceDriveId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "⚠️ [IsFileInSourceFolderAsync] Error checking if file {ItemId} is in source folder: {Error}. Allowing processing.", itemId, ex.Message);
                return null; // Fail open - if there's an error, allow processing
            }
        }

        /// <summary>
        /// Feedback2: Download file stream with retry policy
        /// </summary>
        public async Task<Stream> DownloadFileStreamAsync(string driveId, string itemId, string? tenantId = null)
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

            // Feedback2: Use retry policy for download
            var client = GetClientForTenant(tenantId);
            var stream = await RetryPolicyHelper.ExecuteWithRetryAsync(
                _retryPolicy,
                async () => await client.Drives[driveId].Items[itemId].Content.GetAsync(),
                $"DownloadFileStreamAsync for ItemId: {itemId}",
                _logger);
            
            var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;
            return ms;
        }

        /// <summary>
        /// Export a file in a drive to PDF using Microsoft Graph's format=pdf support.
        /// Returns a memory stream positioned at 0, or null if credentials are not configured.
        /// </summary>
        public async Task<Stream?> DownloadFileAsPdfStreamAsync(string driveId, string itemId, string? tenantId = null)
        {
            if (!_hasCredentials)
            {
                _logger?.LogWarning("⚠️ [PDF] Graph credentials not configured; cannot export item {ItemId} to PDF", itemId);
                return null;
            }

            // Use raw HTTP with ?format=pdf so the service renders the document (including TOC/page numbers)
            var requestUrl = $"https://graph.microsoft.com/v1.0/drives/{driveId}/items/{itemId}/content?format=pdf";

            var stream = await RetryPolicyHelper.ExecuteWithRetryAsync(
                _retryPolicy,
                async () =>
                {
                    var effectiveTenant = string.IsNullOrWhiteSpace(tenantId) ? _cfg.GraphTenantId : tenantId;
                    var tokenCredential = new ClientSecretCredential(
                        effectiveTenant,
                        _cfg.GraphClientId,
                        _cfg.GraphClientSecret);

                    var token = await tokenCredential.GetTokenAsync(
                        new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" }));

                    using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();

                    return await response.Content.ReadAsStreamAsync();
                },
                $"DownloadFileAsPdfStreamAsync for ItemId: {itemId}",
                _logger);

            if (stream == null)
            {
                _logger?.LogWarning("⚠️ [PDF] Export to PDF returned null stream for item {ItemId}", itemId);
                return null;
            }

            var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;
            return ms;
        }

        /// <summary>
        /// Deletes a file (driveItem) by driveId + itemId.
        /// Best-effort helper used for cleanup scenarios (e.g., when only PDF output is desired).
        /// </summary>
        public async Task DeleteFileAsync(string driveId, string itemId, string? tenantId = null)
        {
            if (!_hasCredentials)
            {
                _logger?.LogWarning("⚠️ [DELETE] Graph credentials not configured; cannot delete item {ItemId} from drive {DriveId}", itemId, driveId);
                return;
            }

            try
            {
                var client = GetClientForTenant(tenantId);
                await RetryPolicyHelper.ExecuteWithRetryAsync(
                    _retryPolicy,
                    async () =>
                    {
                        await client.Drives[driveId].Items[itemId].DeleteAsync();
                        return true;
                    },
                    $"DeleteFileAsync for ItemId: {itemId}",
                    _logger);

                _logger?.LogInformation("🗑️ [DELETE] Deleted file {ItemId} from drive {DriveId}", itemId, driveId);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "⚠️ [DELETE] Failed to delete file {ItemId} from drive {DriveId}: {Error}", itemId, driveId, ex.Message);
            }
        }

        /// <summary>
        /// Downloads a template file from SharePoint using the configured template path or file URL
        /// </summary>
        public async Task<string?> DownloadTemplateFileAsync(string siteId, string templateLibraryPath, string templateFileName, string? templateFileUrl = null, string? tenantId = null)
        {
            if (!_hasCredentials)
            {
                _logger?.LogWarning("⚠️ [DownloadTemplateFileAsync] No credentials available, cannot download from SharePoint");
                return null;
            }

            try
            {
                string? templateFilePath = null;
                string? targetDriveId = null;
                string? targetItemId = null;

                // Option 1: If TemplateFileUrl is provided, parse and use it (including subfolders within the library)
                if (!string.IsNullOrWhiteSpace(templateFileUrl))
                {
                    _logger?.LogInformation("📥 [TEMPLATE] Attempting to download template from URL: {Url}", templateFileUrl);
                    
                    // Parse the URL format:
                    // - /sites/SiteName/Library[/Subfolder...]/FileName
                    // - /Library[/Subfolder...]/FileName
                    var normalizedUrl = templateFileUrl.TrimStart('/').TrimEnd('/');
                    var urlParts = normalizedUrl.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    string? parsedLibraryName = null;
                    string? parsedFileName = null;
                    string? parsedSubfolderPath = null; // path inside the library, e.g. "Templates" or "Sub1/Sub2"
                    
                    if (urlParts.Length >= 3 && urlParts[0].Equals("sites", StringComparison.OrdinalIgnoreCase))
                    {
                        // Example: sites/SMEPilot/Shared Documents/Templates/Basic_Template.dotx
                        if (urlParts.Length >= 4)
                        {
                            var afterSite = urlParts.Skip(2).ToArray(); // [Library, (Subfolder... ), FileName]
                            parsedLibraryName = afterSite[0];
                            parsedFileName = afterSite[^1];
                            if (afterSite.Length > 2)
                            {
                                parsedSubfolderPath = string.Join("/", afterSite.Skip(1).Take(afterSite.Length - 2));
                            }

                            _logger?.LogInformation("📥 [TEMPLATE] Parsed URL - Library: '{Library}', Subfolder: '{Subfolder}', File: '{File}'",
                                parsedLibraryName, parsedSubfolderPath ?? "<root>", parsedFileName);
                        }
                        else if (urlParts.Length == 3)
                        {
                            _logger?.LogWarning("⚠️ [TEMPLATE] URL format invalid - missing file name: {Url}", templateFileUrl);
                        }
                    }
                    else if (urlParts.Length >= 2)
                    {
                        // Example: Shared Documents/Templates/Basic_Template.dotx
                        parsedLibraryName = urlParts[0];
                        parsedFileName = urlParts[^1];
                        if (urlParts.Length > 2)
                        {
                            parsedSubfolderPath = string.Join("/", urlParts.Skip(1).Take(urlParts.Length - 2));
                        }

                        _logger?.LogInformation("📥 [TEMPLATE] Parsed URL - Library: '{Library}', Subfolder: '{Subfolder}', File: '{File}'",
                            parsedLibraryName, parsedSubfolderPath ?? "<root>", parsedFileName);
                    }
                    else if (urlParts.Length == 1)
                    {
                        _logger?.LogWarning("⚠️ [TEMPLATE] URL format invalid - missing library name: {Url}", templateFileUrl);
                    }
                    
                    // If we successfully parsed the URL, try to resolve and download
                    if (!string.IsNullOrWhiteSpace(parsedLibraryName) && !string.IsNullOrWhiteSpace(parsedFileName))
                    {
                        // Build a folder path for ResolveFolderPathAsync (library + optional subfolders)
                        var resolvePath = string.IsNullOrWhiteSpace(parsedSubfolderPath)
                            ? parsedLibraryName
                            : $"{parsedLibraryName}/{parsedSubfolderPath}";

                        var (driveId, folderItemId) = await ResolveFolderPathAsync(siteId, resolvePath, tenantId);
                        if (!string.IsNullOrWhiteSpace(driveId))
                        {
                            targetDriveId = driveId;
                            
                            // Build the file path within the drive (subfolders + filename)
                            var filePath = string.IsNullOrWhiteSpace(parsedSubfolderPath)
                                ? parsedFileName
                                : $"{parsedSubfolderPath}/{parsedFileName}";
                            
                            try
                            {
                                var client = GetClientForTenant(tenantId);
                                var fileItem = await client.Drives[targetDriveId].Root.ItemWithPath(filePath).GetAsync();
                                targetItemId = fileItem.Id;
                                _logger?.LogInformation("✅ [TEMPLATE] Found template file in SharePoint from URL: {LibraryPath}/{File} (ItemId: {ItemId})", 
                                    resolvePath, parsedFileName, targetItemId);
                                
                                // Update templateFileName for download path
                                templateFileName = parsedFileName;
                            }
                            catch (ODataError odataError) when (odataError.Error?.Code == "itemNotFound" || odataError.Error?.Code == "NotFound")
                            {
                                _logger?.LogWarning("⚠️ [TEMPLATE] Template file not found at parsed URL path: {LibraryPath}/{File}", 
                                    resolvePath, parsedFileName);
                                // Continue to fallback option 2
                            }
                        }
                        else
                        {
                            _logger?.LogWarning("⚠️ [TEMPLATE] Could not resolve library path '{LibraryPath}' from URL, falling back to TemplateLibraryPath", 
                                resolvePath);
                            // Continue to fallback option 2
                        }
                    }
                    else
                    {
                        _logger?.LogWarning("⚠️ [TEMPLATE] Could not parse TemplateFileUrl, falling back to TemplateLibraryPath + TemplateFileName");
                        // Continue to fallback option 2
                    }
                }

                // Option 2: Resolve template path using TemplateLibraryPath and TemplateFileName (fallback if Option 1 didn't work)
                if (string.IsNullOrWhiteSpace(targetDriveId) || string.IsNullOrWhiteSpace(targetItemId))
                {
                    if (!string.IsNullOrWhiteSpace(templateLibraryPath) && !string.IsNullOrWhiteSpace(templateFileName))
                    {
                        _logger?.LogInformation("📥 [TEMPLATE] Resolving template path: {LibraryPath}/{FileName}", templateLibraryPath, templateFileName);
                    
                        // Resolve the library path to get drive ID
                        var (driveId, folderItemId) = await ResolveFolderPathAsync(siteId, templateLibraryPath, tenantId);
                        if (string.IsNullOrWhiteSpace(driveId))
                        {
                            _logger?.LogWarning("⚠️ [TEMPLATE] Could not resolve template library path: {Path}", templateLibraryPath);
                            return null;
                        }

                        targetDriveId = driveId;
                        
                        // Normalize the library path (remove /sites/SiteName/ prefix if present)
                        var normalizedLibPath = templateLibraryPath.TrimStart('/').TrimEnd('/');
                        if (normalizedLibPath.StartsWith("sites/", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = normalizedLibPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 3)
                            {
                                normalizedLibPath = string.Join("/", parts.Skip(2));
                            }
                        }
                        
                        // Construct the full path to the template file
                        // If folderItemId is null, template is in library root, otherwise it's in a subfolder
                        string normalizedPath;
                        if (string.IsNullOrWhiteSpace(folderItemId))
                        {
                            // Template is in library root
                            normalizedPath = templateFileName;
                        }
                        else
                        {
                            // Template is in a subfolder - need to extract subfolder path from templateLibraryPath
                            var pathParts = normalizedLibPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                            if (pathParts.Length > 1)
                            {
                                // Library name is first part, rest is subfolder path
                                var subfolderPath = string.Join("/", pathParts.Skip(1));
                                normalizedPath = $"{subfolderPath}/{templateFileName}";
                            }
                            else
                            {
                                // Just library name, template should be in root
                                normalizedPath = templateFileName;
                            }
                        }

                        try
                        {
                            // Try to get the file using ItemWithPath
                            var client = GetClientForTenant(tenantId);
                            var fileItem = await client.Drives[targetDriveId].Root.ItemWithPath(normalizedPath).GetAsync();
                            targetItemId = fileItem.Id;
                            _logger?.LogInformation("✅ [TEMPLATE] Found template file in SharePoint: {Path} (ItemId: {ItemId})", normalizedPath, targetItemId);
                        }
                        catch (ODataError odataError) when (odataError.Error?.Code == "itemNotFound" || odataError.Error?.Code == "NotFound")
                        {
                            _logger?.LogWarning("⚠️ [TEMPLATE] Template file not found at path: {Path}", normalizedPath);
                            return null;
                        }
                    }
                } // End of Option 2 (fallback)

                if (string.IsNullOrWhiteSpace(targetDriveId) || string.IsNullOrWhiteSpace(targetItemId))
                {
                    _logger?.LogWarning("⚠️ [TEMPLATE] Could not determine drive/item IDs for template download");
                    return null;
                }

                // Download the template file to a temp location
                var tempDir = Path.Combine(Path.GetTempPath(), "SMEPilot_Templates");
                Directory.CreateDirectory(tempDir);
                templateFilePath = Path.Combine(tempDir, templateFileName);

                _logger?.LogInformation("📥 [TEMPLATE] Downloading template file to: {TempPath}", templateFilePath);
                
                using (var templateStream = await DownloadFileStreamAsync(targetDriveId, targetItemId))
                using (var fileStream = File.Create(templateFilePath))
                {
                    await templateStream.CopyToAsync(fileStream);
                }

                _logger?.LogInformation("✅ [TEMPLATE] Template file downloaded successfully: {Path}", templateFilePath);
                return templateFilePath;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ [TEMPLATE] Failed to download template from SharePoint: {Error}", ex.Message);
                return null;
            }
        }

        public async Task<DriveItem> UploadFileBytesAsync(string driveId, string folderPath, string fileName, byte[] bytes, string? tenantId = null)
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

            // Normalize the path - remove leading slash if present (Graph API expects relative path)
            var normalizedPath = folderPath.TrimStart('/').TrimEnd('/');
            
            // If the path contains "Shared Documents" or a library name, we need to handle it correctly
            // Graph API paths are relative to the drive root
            // If folderPath is like "/Shared Documents/ProcessedDocs", normalize to "Shared Documents/ProcessedDocs"
            // If folderPath is like "ProcessedDocs" and we're in "Shared Documents" drive, use "ProcessedDocs"
            
            var fullPath = normalizedPath;
            if (!string.IsNullOrEmpty(fileName))
            {
                fullPath = normalizedPath + "/" + fileName;
            }
            
            _logger?.LogInformation("📤 [UploadFileBytesAsync] Uploading to path: '{FullPath}' in drive {DriveId} (normalized from: '{OriginalPath}')", 
                fullPath, driveId, folderPath);
            
            // Feedback2: Use retry policy for upload
            using var ms = new MemoryStream(bytes);
            var client = GetClientForTenant(tenantId);
            var item = await RetryPolicyHelper.ExecuteWithRetryAsync(
                _retryPolicy,
                async () => await client.Drives[driveId].Root.ItemWithPath(fullPath).Content.PutAsync(ms),
                $"UploadFileBytesAsync to {fullPath}",
                _logger);
            
            _logger?.LogInformation("✅ [UploadFileBytesAsync] File uploaded successfully. WebUrl: {WebUrl}, Id: {ItemId}, Name: {Name}", 
                item.WebUrl, item.Id, item.Name);
            
            return item;
        }

        public async Task<List<DriveItem>> GetRecentDriveItemsAsync(string driveId, int maxItems = 10, string? tenantId = null)
        {
            if (!_hasCredentials)
            {
                _logger?.LogDebug("Mock: Would query recent items from drive {DriveId}", driveId);
                return new List<DriveItem>();
            }

            try
            {
                var client = GetClientForTenant(tenantId);
                // Get root item first to get its ID
                var rootItem = await client.Drives[driveId].Root.GetAsync();
                if (rootItem == null || string.IsNullOrWhiteSpace(rootItem.Id))
                {
                    _logger?.LogError("Error: Could not get root item for drive {DriveId}", driveId);
                    return new List<DriveItem>();
                }

                // Get children of root using the root item ID
                var items = await client.Drives[driveId].Items[rootItem.Id].Children.GetAsync(config =>
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

        /// <summary>
        /// Uses the Graph delta API to find recently changed files in a drive,
        /// filtered by parent folder path (relative to drive root) and a lookback window.
        /// This is more accurate for nested folders than GetRecentDriveItemsAsync, which only inspects root children.
        /// </summary>
        public async Task<List<DriveItem>> GetRecentItemsFromDeltaAsync(
            string driveId,
            string? sourceFolderPath,
            TimeSpan lookbackWindow,
            int maxItems = 20,
            string? tenantId = null)
        {
            var results = new List<DriveItem>();

            if (!_hasCredentials)
            {
                _logger?.LogDebug("Mock: Would query delta for drive {DriveId}", driveId);
                return results;
            }

            try
            {
                // Normalize source folder path to drive-relative *subfolder* form.
                // - If SourceFolderPath points to the library root (e.g. "/sites/Site/Raw Documents"),
                //   we treat the entire drive as in-scope (no path filter).
                // - If it points to a subfolder (e.g. "/sites/Site/Raw Documents/Team1"),
                //   we use only the subfolder part ("Team1") to filter delta items.
                string? normalizedSource = null;
                if (!string.IsNullOrWhiteSpace(sourceFolderPath))
                {
                    var normalizedPath = sourceFolderPath.Trim('/');
                    if (normalizedPath.StartsWith("sites/", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = normalizedPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        // parts: ["sites", "SiteName", "Library", "Sub1", "Sub2", ...]
                        if (parts.Length >= 4)
                        {
                            // Library root + at least one subfolder → filter by subfolder path only
                            normalizedSource = string.Join("/", parts.Skip(3)); // "Sub1/Sub2"
                        }
                        else
                        {
                            // Library root only -> no path filter (entire drive is source)
                            normalizedSource = null;
                        }
                    }
                }

                var useTimeFilter = lookbackWindow > TimeSpan.Zero;
                var cutoff = useTimeFilter ? DateTimeOffset.UtcNow - lookbackWindow : (DateTimeOffset?)null;

                // High-level diagnostic so we can see exactly how delta is being queried.
                _logger?.LogInformation(
                    "🔍 [Delta] Starting GetRecentItemsFromDeltaAsync for DriveId={DriveId}, SourceFolderPath={SourceFolderPath}, NormalizedSource={NormalizedSource}, Lookback={Lookback}, MaxItems={MaxItems}",
                    driveId,
                    sourceFolderPath ?? "null",
                    normalizedSource ?? "<none>",
                    lookbackWindow,
                    maxItems);

                // Acquire an access token for calling Graph directly (tenant-aware)
                var effectiveTenant = string.IsNullOrWhiteSpace(tenantId) ? _cfg.GraphTenantId : tenantId;
                var tokenCredential = new ClientSecretCredential(
                    effectiveTenant,
                    _cfg.GraphClientId,
                    _cfg.GraphClientSecret);

                var tokenContext = new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" });
                var token = await tokenCredential.GetTokenAsync(tokenContext, default);

                var requestUrl = $"https://graph.microsoft.com/v1.0/drives/{driveId}/root/delta?$top=50";
                int pageCount = 0;

                while (!string.IsNullOrWhiteSpace(requestUrl) &&
                       results.Count < maxItems &&
                       pageCount < 5) // safety limit
                {
                    pageCount++;
                    var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

                    var response = await _httpClient.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorText = await response.Content.ReadAsStringAsync();
                        _logger?.LogWarning("⚠️ [Delta] Request to {Url} failed with {Status}: {Error}", requestUrl, response.StatusCode, errorText);
                        break;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var jo = JObject.Parse(json);
                    var values = jo["value"] as JArray;
                    if (values == null || values.Count == 0)
                    {
                        break;
                    }

                    foreach (var v in values)
                    {
                        if (results.Count >= maxItems)
                        {
                            break;
                        }

                        // Skip folders
                        if (v["folder"] != null)
                        {
                            continue;
                        }

                        var id = (string?)v["id"];
                        var name = (string?)v["name"];
                        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                        {
                            continue;
                        }

                        // Last modified filter
                        DateTimeOffset? lastModified = null;
                        var lmStr = (string?)v["lastModifiedDateTime"];
                        if (!string.IsNullOrWhiteSpace(lmStr) && DateTimeOffset.TryParse(lmStr, out var lm))
                        {
                            lastModified = lm;
                        }

                        // Log raw delta candidate so we can see what Graph returned before filters.
                        var parentPathForLog = (string?)v["parentReference"]?["path"];
                        _logger?.LogDebug(
                            "🔍 [Delta] Candidate from delta: Id={Id}, Name={Name}, ParentPath={ParentPath}, LastModified={LastModified:o}",
                            id,
                            name,
                            parentPathForLog ?? "<null>",
                            lastModified);

                        if (useTimeFilter && cutoff.HasValue && lastModified.HasValue && lastModified.Value < cutoff.Value)
                        {
                            // Older than lookback window – skip
                            continue;
                        }

                        // Path filter based on parentReference.path
                        if (!string.IsNullOrWhiteSpace(normalizedSource))
                        {
                            var parentPath = parentPathForLog;
                            if (string.IsNullOrWhiteSpace(parentPath))
                            {
                                continue;
                            }

                            // Example parentPath: "/drives/{id}/root:/Raw Documents/Team1/Employee1"
                            var marker = "root:";
                            var idx = parentPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                            var relative = idx >= 0
                                ? parentPath.Substring(idx + marker.Length).TrimStart('/')
                                : parentPath.Trim('/');

                            if (!relative.StartsWith(normalizedSource, StringComparison.OrdinalIgnoreCase))
                            {
                                // Not under the configured source folder
                                continue;
                            }
                        }

                // Build a minimal DriveItem so callers can reuse existing code paths
                var driveItem = new DriveItem
                {
                    Id = id,
                    Name = name,
                    LastModifiedDateTime = lastModified
                };

                results.Add(driveItem);
                    }

                    // Follow nextLink if present
                    var nextLink = (string?)jo["@odata.nextLink"];
                    if (string.IsNullOrWhiteSpace(nextLink))
                    {
                        break;
                    }

                    requestUrl = nextLink;
                }

            // Order candidates by most-recent modification time so callers see the freshest items first.
            var orderedResults = results
                .OrderByDescending(r => r.LastModifiedDateTime ?? DateTimeOffset.MinValue)
                .ToList();

            if (orderedResults.Count > 0)
            {
                var previewNames = string.Join(
                    ", ",
                    orderedResults
                        .Take(Math.Min(orderedResults.Count, 5))
                        .Select(r => $"{r.Name} @ {r.LastModifiedDateTime:O}"));

                _logger?.LogInformation(
                    "✅ [Delta] Retrieved {Count} recent candidate files from delta for drive {DriveId}. Top candidates (most recent first): {Candidates}",
                    orderedResults.Count,
                    driveId,
                    previewNames);
            }
            else
            {
                _logger?.LogInformation("✅ [Delta] Retrieved 0 recent candidate files from delta for drive {DriveId}", driveId);
            }

            return orderedResults;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ [Delta] Error getting recent items from delta for drive {DriveId}: {Error}", driveId, ex.Message);
                return new List<DriveItem>();
            }
        }

        public async Task<Dictionary<string, object>?> GetListItemFieldsAsync(string driveId, string itemId, string? tenantId = null)
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
                var client = GetClientForTenant(tenantId);
                
                // CRITICAL FIX: Expand listItem to ensure it is populated
                // Note: parentReference is a complex property (not navigation) and is included by default - cannot be expanded
                var driveItem = await client.Drives[driveId].Items[itemId].GetAsync(requestConfig =>
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
                var drive = await client.Drives[driveId].GetAsync(requestConfig =>
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
                    async () => await client.Sites[siteId].Lists[listId].Items[listItemId].Fields.GetAsync(),
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

        /// <summary>
        /// Resolve a list item's Author / Editor display name using person fields.
        /// This is a higher-level helper used by the Function to populate document Author(s).
        /// </summary>
        public async Task<string?> ResolveListItemAuthorDisplayNameAsync(string driveId, string itemId, string? tenantId = null)
        {
            if (!_hasCredentials)
            {
                return null;
            }

            try
            {
                _logger?.LogInformation("🔍 [AUTHOR] Resolving list item author for DriveId={DriveId}, ItemId={ItemId}", driveId, itemId);
                var client = GetClientForTenant(tenantId);

                DriveItem? driveItem = null;

                // 1) Try DriveItem with listItem expanded (createdBy/lastModifiedBy are simple facets and
                //    do NOT support $expand; selecting them is enough to read uploader/editor info).
                try
                {
                    driveItem = await client.Drives[driveId].Items[itemId].GetAsync(requestConfig =>
                    {
                        requestConfig.QueryParameters.Expand = new[] { "listItem" };
                    });
                }
                catch (ODataError odataError)
                {
                    _logger?.LogWarning(odataError, "⚠️ [AUTHOR] DriveItem.GetAsync with createdBy/lastModifiedBy failed for ItemId: {ItemId}. Code={Code}, Message={Message}",
                        itemId, odataError.Error?.Code, odataError.Error?.Message);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "⚠️ [AUTHOR] DriveItem.GetAsync with createdBy/lastModifiedBy threw unexpected error for ItemId: {ItemId}", itemId);
                }

                // Helper to read a friendly name from an IdentitySet
                string? GetUserNameFromIdentity(IdentitySet? identity)
                {
                    var name = identity?.User?.DisplayName;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        return name;
                    }

                    // Some tenants expose user info only via AdditionalData
                    var data = identity?.User?.AdditionalData;
                    if (data != null)
                    {
                        if (data.TryGetValue("displayName", out var raw) && raw != null)
                        {
                            var v = raw.ToString();
                            if (!string.IsNullOrWhiteSpace(v)) return v;
                        }
                        if (data.TryGetValue("email", out var rawEmail) && rawEmail != null)
                        {
                            var v = rawEmail.ToString();
                            if (!string.IsNullOrWhiteSpace(v)) return v;
                        }
                    }

                    return null;
                }

                if (driveItem != null)
                {
                    var createdByName = GetUserNameFromIdentity(driveItem.CreatedBy);
                    var modifiedByName = GetUserNameFromIdentity(driveItem.LastModifiedBy);

                    _logger?.LogInformation("📋 [AUTHOR] DriveItem.CreatedBy raw: DisplayName={DisplayName}, Email={Email}",
                        driveItem.CreatedBy?.User?.DisplayName ?? "null",
                        driveItem.CreatedBy?.User?.AdditionalData != null && driveItem.CreatedBy.User.AdditionalData.TryGetValue("email", out var cEmail) ? cEmail?.ToString() : "null");
                    _logger?.LogInformation("📋 [AUTHOR] DriveItem.LastModifiedBy raw: DisplayName={DisplayName}, Email={Email}",
                        driveItem.LastModifiedBy?.User?.DisplayName ?? "null",
                        driveItem.LastModifiedBy?.User?.AdditionalData != null && driveItem.LastModifiedBy.User.AdditionalData.TryGetValue("email", out var mEmail) ? mEmail?.ToString() : "null");

                    // Prefer the real human uploader:
                    // - If LastModifiedBy looks like a system account (e.g. "SharePoint App"), ignore it
                    // - Otherwise prefer LastModifiedBy, then fall back to CreatedBy.
                    bool IsSystemAccount(string? name) =>
                        string.IsNullOrWhiteSpace(name) ||
                        name.Equals("SharePoint App", StringComparison.OrdinalIgnoreCase);

                    if (!IsSystemAccount(modifiedByName))
                    {
                        _logger?.LogInformation("👤 [AUTHOR] Resolved from DriveItem.LastModifiedBy (human): {Author}", modifiedByName);
                        return modifiedByName;
                    }

                    if (!string.IsNullOrWhiteSpace(createdByName))
                    {
                        _logger?.LogInformation("👤 [AUTHOR] Resolved from DriveItem.CreatedBy: {Author}", createdByName);
                        return createdByName;
                    }

                    // If both are empty or system accounts, fall through to listItem-based resolution.
                }

                // 2) Fall back to listItem path (which we know works from GetListItemFieldsAsync) to find site/list/listItem ids.
                if (driveItem == null)
                {
                    try
                    {
                        driveItem = await client.Drives[driveId].Items[itemId].GetAsync(requestConfig =>
                        {
                            requestConfig.QueryParameters.Expand = new[] { "listItem" };
                        });
                    }
                    catch (ODataError odataError)
                    {
                        _logger?.LogWarning(odataError, "⚠️ [AUTHOR] DriveItem.GetAsync with listItem expand failed for ItemId: {ItemId}. Code={Code}, Message={Message}",
                            itemId, odataError.Error?.Code, odataError.Error?.Message);
                        driveItem = null;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "⚠️ [AUTHOR] DriveItem.GetAsync with listItem expand threw unexpected error for ItemId: {ItemId}", itemId);
                        driveItem = null;
                    }
                }

                if (driveItem == null || driveItem.ListItem == null)
                {
                    _logger?.LogWarning("⚠️ [AUTHOR] DriveItem or ListItem is null while resolving author for ItemId: {ItemId}", itemId);
                    return null;
                }

                var siteId = driveItem.ParentReference?.SiteId;
                if (siteId == null)
                {
                    _logger?.LogWarning("⚠️ [AUTHOR] SiteId is null while resolving author for ItemId: {ItemId}", itemId);
                    return null;
                }

                var drive = await _client.Drives[driveId].GetAsync(requestConfig =>
                {
                    requestConfig.QueryParameters.Expand = new[] { "list" };
                });

                if (drive == null || drive.List == null)
                {
                    _logger?.LogWarning("⚠️ [AUTHOR] Drive or List is null while resolving author for DriveId: {DriveId}", driveId);
                    return null;
                }

                var listId = drive.List.Id;
                var listItemId = driveItem.ListItem.Id;

                // Expand fields and person navigation props so we can inspect Author/Editor person fields
                var listItem = await RetryPolicyHelper.ExecuteWithRetryAsync(
                    _retryPolicy,
                    async () => await _client.Sites[siteId].Lists[listId].Items[listItemId].GetAsync(requestConfig =>
                    {
                        requestConfig.QueryParameters.Expand = new[] { "fields", "createdByUser", "lastModifiedByUser" };
                    }),
                    $"ResolveListItemAuthorDisplayNameAsync for ItemId: {itemId}",
                    _logger);

                if (listItem == null)
                {
                    _logger?.LogWarning("⚠️ [AUTHOR] ListItem is null while resolving author for ItemId: {ItemId}", itemId);
                    return null;
                }

                // Probe createdByUser / lastModifiedByUser which should mirror SharePoint UI "Created by / Modified"
                var createdByUser = listItem.CreatedByUser;
                var modifiedByUser = listItem.LastModifiedByUser;

                _logger?.LogInformation("📋 [AUTHOR] ListItem.CreatedByUser: DisplayName={DisplayName}, UPN={Upn}",
                    createdByUser?.DisplayName ?? "null",
                    createdByUser?.UserPrincipalName ?? "null");
                _logger?.LogInformation("📋 [AUTHOR] ListItem.LastModifiedByUser: DisplayName={DisplayName}, UPN={Upn}",
                    modifiedByUser?.DisplayName ?? "null",
                    modifiedByUser?.UserPrincipalName ?? "null");

                if (!string.IsNullOrWhiteSpace(modifiedByUser?.DisplayName))
                {
                    _logger?.LogInformation("👤 [AUTHOR] Resolved from ListItem.LastModifiedByUser: {Author}", modifiedByUser.DisplayName);
                    return modifiedByUser.DisplayName;
                }

                if (!string.IsNullOrWhiteSpace(createdByUser?.DisplayName))
                {
                    _logger?.LogInformation("👤 [AUTHOR] Resolved from ListItem.CreatedByUser: {Author}", createdByUser.DisplayName);
                    return createdByUser.DisplayName;
                }

                if (listItem.Fields?.AdditionalData == null || listItem.Fields.AdditionalData.Count == 0)
                {
                    _logger?.LogWarning("⚠️ [AUTHOR] No fields.AdditionalData returned while resolving author for ItemId: {ItemId}", itemId);
                    return null;
                }

                // Try to get a friendly Author/Editor string; different tenants may expose different shapes,
                // so we probe a few common keys in order.
                string? TryExtractPerson(IDictionary<string, object> data, string prefix)
                {
                    if (data.TryGetValue(prefix, out var raw) && raw != null)
                    {
                        var value = raw.ToString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            return value;
                        }
                    }

                    if (data.TryGetValue(prefix + "LookupValue", out raw) && raw != null)
                    {
                        var value = raw.ToString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            return value;
                        }
                    }

                    if (data.TryGetValue(prefix + "StringId", out raw) && raw != null)
                    {
                        var value = raw.ToString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            return value;
                        }
                    }

                    return null;
                }

                var data = listItem.Fields.AdditionalData;

                var authorName = TryExtractPerson(data, "Author");
                if (!string.IsNullOrWhiteSpace(authorName))
                {
                    _logger?.LogInformation("👤 [AUTHOR] Resolved from list item fields (Author*): {Author}", authorName);
                    return authorName;
                }

                var editorName = TryExtractPerson(data, "Editor");
                if (!string.IsNullOrWhiteSpace(editorName))
                {
                    _logger?.LogInformation("👤 [AUTHOR] Resolved from list item fields (Editor*): {Author}", editorName);
                    return editorName;
                }

                _logger?.LogWarning("⚠️ [AUTHOR] Could not resolve Author/Editor text fields for ItemId: {ItemId}", itemId);
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "⚠️ [AUTHOR] Failed to resolve list item author for ItemId: {ItemId}. Error: {Error}", itemId, ex.Message);
                return null;
            }
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

                        // If request is invalid for other reasons (e.g., URL field payload quirks),
                        // treat this as a non-fatal metadata failure so the enrichment flow still succeeds.
                        if (errorCode == "invalidRequest")
                        {
                            _logger?.LogWarning("⚠️ [UpdateListItemFieldsAsync] InvalidRequest for ItemId: {ItemId}. Skipping metadata update but keeping document enrichment.", itemId);
                            return;
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
                    new { Name = "SMEPilot_ContentHash", Type = "text", DisplayName = "SMEPilot Content Hash", Description = "SHA256 hash of file content for idempotency checks" },
                    new { Name = "SMEPilot_LastEnrichedTime", Type = "dateTime", DisplayName = "SMEPilot Last Enriched Time", Description = "Timestamp when document was last enriched" },
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
                                _logger?.LogWarning("⚠️ [EnsureColumnsExistAsync] PERMISSION ERROR: App is not allowed to create columns via Graph (app-only).");
                                _logger?.LogWarning("   Error Code: {Code}, Message: {Message}", errorCode, errorMessage);
                                _logger?.LogWarning("   Columns must be provisioned using SharePoint/SPFx under an admin user context (e.g., via the Admin Panel).");
                                _logger?.LogWarning("   For now, tracking metadata will be limited until the SMEPilotRuns list and its columns exist.");
                                // Do not throw; simply stop trying to create more columns.
                                break;
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

        /// <summary>
        /// Get all active subscriptions
        /// </summary>
        /// <returns>List of active subscriptions</returns>
        public async Task<IEnumerable<Subscription>> GetSubscriptionsAsync(string? tenantId = null)
        {
            if (!_hasCredentials)
            {
                _logger?.LogDebug("Mock: Would get all subscriptions");
                return new List<Subscription>();
            }

            try
            {
                _logger?.LogInformation("📋 [GetSubscriptionsAsync] Getting all active subscriptions (tenant={TenantId})", tenantId ?? _cfg.GraphTenantId);

                var client = GetClientForTenant(tenantId);
                var subscriptions = await _retryPolicy.ExecuteAsync(async () =>
                {
                    return await client.Subscriptions.GetAsync();
                });

                var subscriptionList = subscriptions?.Value?.ToList() ?? new List<Subscription>();
                _logger?.LogInformation("✅ [GetSubscriptionsAsync] Found {Count} active subscriptions", subscriptionList.Count);
                return subscriptionList;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ [GetSubscriptionsAsync] Error getting subscriptions: {Error}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Delete a subscription by ID
        /// </summary>
        /// <param name="subscriptionId">Subscription ID</param>
        public async Task DeleteSubscriptionAsync(string subscriptionId, string? tenantId = null)
        {
            if (!_hasCredentials)
            {
                _logger?.LogDebug("Mock: Would delete subscription {SubscriptionId}", subscriptionId);
                return;
            }

            try
            {
                _logger?.LogInformation("🗑️ [DeleteSubscriptionAsync] Deleting subscription {SubscriptionId} for tenant {TenantId}", subscriptionId, tenantId ?? "default");

                var client = GetClientForTenant(tenantId);
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    await client.Subscriptions[subscriptionId].DeleteAsync();
                });

                _logger?.LogInformation("✅ [DeleteSubscriptionAsync] Successfully deleted subscription {SubscriptionId} for tenant {TenantId}", subscriptionId, tenantId ?? "default");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ [DeleteSubscriptionAsync] Error deleting subscription {SubscriptionId}: {Error}", subscriptionId, ex.Message);
                throw;
            }
        }

        public async Task<Subscription> CreateSubscriptionAsync(string resource, string notificationUrl, DateTimeOffset expiration, string? tenantId = null, string? clientState = null)
        {
            if (!_hasCredentials) throw new InvalidOperationException("Graph credentials are not configured.");
            
            try
            {
                _logger?.LogInformation("=== CREATING SUBSCRIPTION ===");
                _logger?.LogInformation("Resource: {Resource}", resource);
                _logger?.LogInformation("Notification URL: {NotificationUrl}", notificationUrl);
                _logger?.LogInformation("Expiration: {Expiration}", expiration);
                _logger?.LogInformation("Configured Tenant ID: {TenantId}", _cfg.GraphTenantId);
                if (!string.IsNullOrWhiteSpace(tenantId))
                {
                    _logger?.LogInformation("Effective Tenant ID (per-call): {TenantId}", tenantId);
                }
                _logger?.LogInformation("Client ID: {ClientId}", _cfg.GraphClientId);
                _logger?.LogInformation("Client Secret: {Status}", string.IsNullOrEmpty(_cfg.GraphClientSecret) ? "EMPTY" : "SET");
                
                // Try to get an access token first to verify authentication
                try
                {
                    var effectiveTenant = string.IsNullOrWhiteSpace(tenantId) ? _cfg.GraphTenantId : tenantId;
                    var tokenCredential = new ClientSecretCredential(
                        effectiveTenant,
                        _cfg.GraphClientId,
                        _cfg.GraphClientSecret);
                    
                    var tokenRequestContext = new Azure.Core.TokenRequestContext(new[] { "https://graph.microsoft.com/.default" });
                    var token = await tokenCredential.GetTokenAsync(tokenRequestContext, default);
                    _logger?.LogInformation("Access token obtained successfully for Tenant={Tenant} (length: {Length})", effectiveTenant, token.Token.Length);
                    _logger?.LogInformation("Token expires: {ExpiresOn}", token.ExpiresOn);
                }
                catch (Exception authEx)
                {
                    _logger?.LogError(authEx, "ERROR: Failed to get access token: {Error}", authEx.Message);
                    throw new InvalidOperationException($"Authentication failed: {authEx.Message}", authEx);
                }
                
                var effectiveClientState = string.IsNullOrWhiteSpace(clientState)
                    ? "SMEPilotState"
                    : clientState;

                var subscription = new Subscription
                {
                    // NOTE: For the resources we are using (drive and list item), Graph currently rejects
                    // 'deleted' in the changeType (see Invalid 'changeType' attribute: 'deleted').
                    // To keep subscriptions working reliably across tenants, we request only 'updated'
                    // and derive deletions via 404/delta where possible instead of asking for 'deleted'.
                    ChangeType = "updated",
                    NotificationUrl = notificationUrl,
                    Resource = resource,
                    ExpirationDateTime = expiration,
                    ClientState = effectiveClientState
                };
                
                _logger?.LogInformation("Calling Graph API to create subscription...");
                var client = GetClientForTenant(tenantId);
                var result = await client.Subscriptions.PostAsync(subscription);
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

        /// <summary>
        /// Get site ID from drive ID
        /// </summary>
        /// <param name="driveId">Drive ID</param>
        /// <returns>Site ID or null if not found</returns>
        public async Task<string?> GetSiteIdFromDriveAsync(string driveId, string? tenantId = null)
        {
            if (!_hasCredentials)
            {
                _logger?.LogDebug("Mock: Would get site ID from drive {DriveId}", driveId);
                return null;
            }

            try
            {
                _logger?.LogDebug("🔍 [GetSiteIdFromDriveAsync] Getting site ID for drive {DriveId}", driveId);

                // Get drive root item to extract siteId from ParentReference
                var client = GetClientForTenant(tenantId);
                var rootItem = await client.Drives[driveId].Root.GetAsync();
                if (rootItem?.ParentReference?.SiteId != null)
                {
                    var siteId = rootItem.ParentReference.SiteId;
                    _logger?.LogDebug("✅ [GetSiteIdFromDriveAsync] Found site ID: {SiteId}", siteId);
                    return siteId;
                }

                // This is not a fatal condition in our flow because we also capture
                // SiteId from the file/list context. Log at Debug to avoid noisy
                // warnings once SiteId is reliably available elsewhere.
                _logger?.LogDebug("ℹ️ [GetSiteIdFromDriveAsync] SiteId not present in drive root for drive {DriveId}", driveId);
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ [GetSiteIdFromDriveAsync] Error getting site ID from drive {DriveId}: {Error}", driveId, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Normalize site ID for Graph API - Graph API requires specific formats
        /// Options:
        /// 1. Use full siteId as-is: sites/{hostname},{tenantId},{siteId}
        /// 2. Use hostname and path: sites/{hostname}:/sites/{sitePath}
        /// 3. Use just the site ID part: sites/{siteId}
        /// </summary>
        private string NormalizeSiteIdForGraph(string siteId, string? sourceFolderPath = null)
        {
            // If siteId is in SharePoint REST chunk format (hostname,tenantId,siteId)
            if (!string.IsNullOrWhiteSpace(siteId) && siteId.Contains(','))
            {
                var parts = siteId.Split(',');
                if (parts.Length >= 3)
                {
                    var hostname = parts[0];

                    // If a usable sourceFolderPath is available and includes '/sites/<sitePath>',
                    // prefer the hostname:/sites/<sitePath> format which is accepted by Graph.
                    if (!string.IsNullOrWhiteSpace(sourceFolderPath))
                    {
                        var pathParts = sourceFolderPath.TrimStart('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        if (pathParts.Length >= 2 && pathParts[0].Equals("sites", StringComparison.OrdinalIgnoreCase))
                        {
                            var sitePath = pathParts[1]; // e.g. "DocEnricher-PoC"
                            var hostnamePathFormat = $"{hostname}:/sites/{sitePath}";
                            _logger?.LogDebug("🔍 [NormalizeSiteIdForGraph] Using hostname:path format: {HostnamePathFormat}", hostnamePathFormat);
                            return hostnamePathFormat; // Graph likes this format
                        }
                    }

                    // fallback: use just the siteId guid portion as sites/<siteGuid>
                    var siteGuid = parts[2];
                    var guidFormat = siteGuid;
                    _logger?.LogDebug("🔍 [NormalizeSiteIdForGraph] Using site GUID format: {SiteGuid}", guidFormat);
                    return guidFormat;
                }
            }

            // If already in a friendly format, return as-is
            return siteId;
        }

        /// <summary>
        /// Public wrapper around NormalizeSiteIdForGraph so other components (e.g. functions)
        /// can build correct /sites/{siteId}/... resource paths for Graph.
        /// </summary>
        public string NormalizeSiteIdForResource(string siteId, string? sourceFolderPath = null)
        {
            return NormalizeSiteIdForGraph(siteId, sourceFolderPath);
        }

        public async Task<(string? driveId, string? itemId)> ResolveFolderPathAsync(string siteId, string folderPath, string? tenantId = null)
        {
            if (!_hasCredentials)
            {
                _logger?.LogDebug("Mock: Would resolve folder path for site {SiteId}, path {FolderPath}", siteId, folderPath);
                return (null, null);
            }

            try
            {
                _logger?.LogDebug("🔍 [ResolveFolderPathAsync] Resolving folder path for site {SiteId}, path {FolderPath}, tenant={TenantId}", siteId, folderPath, tenantId ?? "default");
                
                // Try multiple siteId formats
                var siteIdFormats = new List<string>();
                
                // Format 1: Use normalized format (hostname:/sites/sitePath if available)
                var initialNormalizedSiteId = NormalizeSiteIdForGraph(siteId, folderPath);
                siteIdFormats.Add(initialNormalizedSiteId);
                
                // Format 2: If siteId contains commas, try extracting just the site ID part
                if (siteId.Contains(','))
                {
                    var parts = siteId.Split(',');
                    if (parts.Length >= 3)
                    {
                        // Try just the site ID (GUID)
                        siteIdFormats.Add(parts[2]);
                        // Try hostname:/sites/siteId format
                        siteIdFormats.Add($"{parts[0]}:/sites/{parts[2]}");
                    }
                }
                
                // Format 3: Original siteId as-is
                if (!siteIdFormats.Contains(siteId))
                {
                    siteIdFormats.Add(siteId);
                }
                
                _logger?.LogDebug("🔍 [ResolveFolderPathAsync] Will try {Count} site ID formats", siteIdFormats.Count);

                // Normalize the path - remove leading/trailing slashes and handle URL encoding
                var normalizedPath = folderPath.TrimStart('/').TrimEnd('/');
                _logger?.LogInformation("🔍 [ResolveFolderPathAsync] Input path: '{InputPath}' -> After trim: '{TrimmedPath}'", folderPath, normalizedPath);
                
                // If path starts with "sites/", remove it (Graph API expects path relative to site root)
                if (normalizedPath.StartsWith("sites/", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract path after site name (e.g., "sites/SiteName/Library/Folder" -> "Library/Folder")
                    var parts = normalizedPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    _logger?.LogInformation("🔍 [ResolveFolderPathAsync] Path starts with 'sites/', parts: [{Parts}]", string.Join(", ", parts));
                    if (parts.Length >= 3)
                    {
                        // Skip "sites" and site name, take the rest
                        var beforeNormalize = normalizedPath;
                        normalizedPath = string.Join("/", parts.Skip(2));
                        _logger?.LogInformation("🔍 [ResolveFolderPathAsync] Removed site prefix: '{Before}' -> '{After}'", beforeNormalize, normalizedPath);
                    }
                    else if (parts.Length == 2)
                    {
                        // Just "sites/SiteName" - no library specified, this is invalid
                        _logger?.LogWarning("⚠️ [ResolveFolderPathAsync] Path 'sites/SiteName' format but no library name found");
                    }
                }

                _logger?.LogInformation("🔍 [ResolveFolderPathAsync] Final normalized path: '{NormalizedPath}'", normalizedPath);

                // PERMANENT FIX: Parse the path to extract library name and subfolder path
                // Path format: "LibraryName" or "LibraryName/Subfolder" or "LibraryName/Subfolder/Subfolder2"
                var pathParts = normalizedPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                string? libraryName = null;
                string? subfolderPath = null;
                
                if (pathParts.Length == 0)
                {
                    _logger?.LogWarning("⚠️ [ResolveFolderPathAsync] Empty path after normalization");
                    return (null, null);
                }
                else if (pathParts.Length == 1)
                {
                    // Just a library name (e.g., "Shared Documents")
                    libraryName = pathParts[0];
                    subfolderPath = null;
                    _logger?.LogDebug("🔍 [ResolveFolderPathAsync] Path is just a library name: '{LibraryName}'", libraryName);
                }
                else
                {
                    // Library name + subfolder path (e.g., "Shared Documents/ProcessedDocs")
                    libraryName = pathParts[0];
                    subfolderPath = string.Join("/", pathParts.Skip(1));
                    _logger?.LogDebug("🔍 [ResolveFolderPathAsync] Parsed path - Library: '{LibraryName}', Subfolder: '{SubfolderPath}'", libraryName, subfolderPath);
                }
                
                // Get all drives for the site and find the matching library
                string? targetDriveId = null;
                string? normalizedSiteId = null;
                
                // Try each siteId format until one works
                foreach (var siteIdFormat in siteIdFormats.Distinct())
                {
                    try
                    {
                        _logger?.LogDebug("🔍 [ResolveFolderPathAsync] Trying site ID format: {SiteIdFormat}", siteIdFormat);
                        var client = GetClientForTenant(tenantId);
                        var allDrives = await client.Sites[siteIdFormat].Drives.GetAsync();
                        if (allDrives?.Value != null)
                        {
                            _logger?.LogInformation("🔍 [ResolveFolderPathAsync] Found {Count} drives in site {SiteIdFormat}", allDrives.Value.Count(), siteIdFormat);
                            
                            // Log all drive names for debugging
                            var driveNames = string.Join(", ", allDrives.Value.Select(d => $"'{d.Name}'"));
                            _logger?.LogInformation("📋 [ResolveFolderPathAsync] Available drives: {DriveNames}", driveNames);
                            
                            // PERMANENT FIX: Handle common library name variations
                            // SharePoint often uses different names in Graph API vs user-facing names
                            var libraryNameVariations = new List<string> { libraryName };
                            
                            // Add common variations for standard libraries
                            if (libraryName.Equals("Shared Documents", StringComparison.OrdinalIgnoreCase))
                            {
                                libraryNameVariations.Add("Documents");
                                libraryNameVariations.Add("Document Library");
                            }
                            else if (libraryName.Equals("Documents", StringComparison.OrdinalIgnoreCase))
                            {
                                libraryNameVariations.Add("Shared Documents");
                                libraryNameVariations.Add("Document Library");
                            }
                            // Handle "Enriched documents" variations (common naming patterns)
                            else if (libraryName.IndexOf("Enriched", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                // Try variations: "Enriched documents", "Enriched Documents", "EnrichedDocs", etc.
                                libraryNameVariations.Add(libraryName.Replace(" ", "")); // "Enricheddocuments"
                                // Capitalize "Documents" if present (case-insensitive replace)
                                var lowerLibraryName = libraryName.ToLowerInvariant();
                                if (lowerLibraryName.Contains(" documents"))
                                {
                                    var idx = lowerLibraryName.IndexOf(" documents");
                                    libraryNameVariations.Add(libraryName.Substring(0, idx) + " Documents" + libraryName.Substring(idx + " documents".Length));
                                }
                                libraryNameVariations.Add("EnrichedDocs");
                                libraryNameVariations.Add("Enriched Documents");
                                // Also try with different casing
                                libraryNameVariations.Add("enriched documents");
                            }
                            
                            Drive? matchingDrive = null;
                            
                            // Try each variation
                            foreach (var variation in libraryNameVariations)
                            {
                                // Try exact match first
                                matchingDrive = allDrives.Value.FirstOrDefault(d => 
                                    string.Equals(d.Name, variation, StringComparison.OrdinalIgnoreCase));
                                
                                if (matchingDrive != null)
                                {
                                    _logger?.LogInformation("✅ [ResolveFolderPathAsync] Found exact match: '{Variation}' -> Drive '{DriveName}' (ID: {DriveId})", 
                                        variation, matchingDrive.Name, matchingDrive.Id);
                                    break;
                                }
                                
                                // Try normalized comparison (remove spaces, case-insensitive)
                                var normalizedVariation = variation.Replace(" ", "").ToLowerInvariant();
                                matchingDrive = allDrives.Value.FirstOrDefault(d =>
                                {
                                    var driveName = d.Name?.Replace(" ", "").ToLowerInvariant() ?? "";
                                    return driveName == normalizedVariation;
                                });
                                
                                if (matchingDrive != null)
                                {
                                    _logger?.LogInformation("✅ [ResolveFolderPathAsync] Found normalized match: '{Variation}' -> Drive '{DriveName}' (ID: {DriveId})", 
                                        variation, matchingDrive.Name, matchingDrive.Id);
                                    break;
                                }
                            }
                            
                            if (matchingDrive != null && !string.IsNullOrWhiteSpace(matchingDrive.Id))
                            {
                                targetDriveId = matchingDrive.Id;
                                normalizedSiteId = siteIdFormat;
                                _logger?.LogInformation("✅ [ResolveFolderPathAsync] Successfully matched library '{LibraryName}' to drive '{DriveName}' (ID: {DriveId})", 
                                    libraryName, matchingDrive.Name, targetDriveId);
                                break; // Found the drive, exit the siteId format loop
                            }
                            
                            _logger?.LogWarning("⚠️ [ResolveFolderPathAsync] No matching drive found for library '{LibraryName}' (tried variations: {Variations}) among {Count} drives: {DriveNames}", 
                                libraryName, string.Join(", ", libraryNameVariations), allDrives.Value.Count(), driveNames);
                        }
                        else
                        {
                            _logger?.LogWarning("⚠️ [ResolveFolderPathAsync] No drives found for site {SiteIdFormat}", siteIdFormat);
                        }
                        
                        // If we got here, we successfully accessed the site, so break and use this format
                        normalizedSiteId = siteIdFormat;
                        break;
                }
                catch (ODataError odataError)
                {
                    _logger?.LogWarning(odataError, "⚠️ [ResolveFolderPathAsync] ODataError getting drives for site {SiteIdFormat}: {Code} - {Message}. Trying next format...", 
                        siteIdFormat, odataError.Error?.Code, odataError.Error?.Message);
                    // Try next format
                    continue;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "⚠️ [ResolveFolderPathAsync] Error getting drives for site {SiteIdFormat}: {Error}. Trying next format...", 
                        siteIdFormat, ex.Message);
                    // Try next format
                    continue;
                }
                }

                // If we couldn't find the library drive, return null
                if (string.IsNullOrWhiteSpace(targetDriveId))
                {
                    _logger?.LogError("❌ [ResolveFolderPathAsync] Could not find drive for library '{LibraryName}' in site", libraryName);
                    return (null, null);
                }

                // If there's no subfolder path, return the library root
                if (string.IsNullOrWhiteSpace(subfolderPath))
                {
                    _logger?.LogInformation("✅ [ResolveFolderPathAsync] Resolved to library root. DriveId: {DriveId}", targetDriveId);
                    return (targetDriveId, null);
                }

                // Resolve the subfolder path within the found drive
                _logger?.LogInformation("🔍 [ResolveFolderPathAsync] Resolving subfolder path '{SubfolderPath}' within drive {DriveId}...", subfolderPath, targetDriveId);
                DriveItem? folderItem = null;
                try
                {
                    // Use the drive ID to access root and resolve the subfolder path
                    folderItem = await _client!.Drives[targetDriveId].Root.ItemWithPath(subfolderPath).GetAsync();
                    _logger?.LogInformation("✅ [ResolveFolderPathAsync] Successfully resolved subfolder '{SubfolderPath}' in drive {DriveId}", subfolderPath, targetDriveId);
                }
                catch (ODataError odataError) when (odataError.Error?.Code == "itemNotFound" || odataError.Error?.Code == "NotFound")
                {
                    // PERMANENT FIX: If folder doesn't exist, create it
                    _logger?.LogWarning("⚠️ [ResolveFolderPathAsync] Subfolder '{SubfolderPath}' not found in drive {DriveId}. Attempting to create it...", subfolderPath, targetDriveId);
                    
                    try
                    {
                        // Split the path into parts to create nested folders if needed
                        var folderParts = subfolderPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        DriveItem? currentFolder = null;
                        string currentPath = "";
                        
                        foreach (var folderPart in folderParts)
                        {
                            currentPath = string.IsNullOrEmpty(currentPath) ? folderPart : $"{currentPath}/{folderPart}";
                            
                            try
                            {
                                // Try to get the folder
                                if (currentFolder == null)
                                {
                                    currentFolder = await _client!.Drives[targetDriveId].Root.ItemWithPath(currentPath).GetAsync();
                                }
                                else
                                {
                                    currentFolder = await _client!.Drives[targetDriveId].Items[currentFolder.Id].ItemWithPath(folderPart).GetAsync();
                                }
                                _logger?.LogDebug("✅ [ResolveFolderPathAsync] Folder '{FolderPart}' already exists at path '{CurrentPath}'", folderPart, currentPath);
                            }
                            catch (ODataError ex) when (ex.Error?.Code == "itemNotFound" || ex.Error?.Code == "NotFound")
                            {
                                // Folder doesn't exist, create it
                                _logger?.LogInformation("📁 [ResolveFolderPathAsync] Creating folder '{FolderPart}' at path '{CurrentPath}'...", folderPart, currentPath);
                                
                                var newFolder = new DriveItem
                                {
                                    Name = folderPart,
                                    Folder = new Folder()
                                };
                                
                                if (currentFolder == null)
                                {
                                    // Create in drive root - need to get root item first
                                    var rootItem = await _client!.Drives[targetDriveId].Root.GetAsync();
                                    if (rootItem?.Id != null)
                                    {
                                        currentFolder = await _client!.Drives[targetDriveId].Items[rootItem.Id].Children.PostAsync(newFolder);
                                    }
                                    else
                                    {
                                        throw new InvalidOperationException($"Could not get root item for drive {targetDriveId}");
                                    }
                                }
                                else
                                {
                                    // Create in current folder
                                    currentFolder = await _client!.Drives[targetDriveId].Items[currentFolder.Id].Children.PostAsync(newFolder);
                                }
                                
                                _logger?.LogInformation("✅ [ResolveFolderPathAsync] Successfully created folder '{FolderPart}' (ID: {ItemId})", folderPart, currentFolder.Id);
                            }
                        }
                        
                        folderItem = currentFolder;
                        _logger?.LogInformation("✅ [ResolveFolderPathAsync] Successfully created/verified subfolder path '{SubfolderPath}' in drive {DriveId}", subfolderPath, targetDriveId);
                    }
                    catch (Exception createEx)
                    {
                        _logger?.LogError(createEx, "❌ [ResolveFolderPathAsync] Failed to create subfolder '{SubfolderPath}' in drive {DriveId}. Error: {Error}", 
                            subfolderPath, targetDriveId, createEx.Message);
                        return (null, null);
                    }
                }
                catch (ODataError odataError)
                {
                    _logger?.LogError(odataError, "❌ [ResolveFolderPathAsync] ODataError resolving subfolder path '{SubfolderPath}' in drive {DriveId}. Error Code: {Code}, Message: {Message}", 
                        subfolderPath, targetDriveId, odataError.Error?.Code, odataError.Error?.Message);
                    if (odataError.Error?.InnerError != null)
                    {
                        _logger?.LogError("Inner Error: {InnerError}", odataError.Error.InnerError);
                    }
                    return (null, null);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "⚠️ [ResolveFolderPathAsync] Error resolving subfolder path '{SubfolderPath}' in drive {DriveId}. Error: {Error}", subfolderPath, targetDriveId, ex.Message);
                    return (null, null);
                }
                
                if (folderItem == null)
                {
                    _logger?.LogWarning("⚠️ [ResolveFolderPathAsync] Subfolder not found at path: {SubfolderPath} in drive {DriveId}", subfolderPath, targetDriveId);
                    return (null, null);
                }

                var resolvedDriveId = folderItem.ParentReference?.DriveId;
                var itemId = folderItem.Id;

                if (string.IsNullOrWhiteSpace(resolvedDriveId) || string.IsNullOrWhiteSpace(itemId))
                {
                    _logger?.LogWarning("⚠️ [ResolveFolderPathAsync] Subfolder found but missing driveId or itemId. DriveId: {DriveId}, ItemId: {ItemId}", resolvedDriveId, itemId);
                    return (null, null);
                }

                _logger?.LogInformation("✅ [ResolveFolderPathAsync] Successfully resolved folder. DriveId: {DriveId}, ItemId: {ItemId}, Path: {Path}", resolvedDriveId, itemId, normalizedPath);
                return (resolvedDriveId, itemId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ [ResolveFolderPathAsync] Error resolving folder path for site {SiteId}, path {FolderPath}: {Error}", siteId, folderPath, ex.Message);
                return (null, null);
            }
        }

        /// <summary>
        /// Get drive ID from site ID and library name (or folder path)
        /// </summary>
        /// <param name="siteId">Site ID</param>
        /// <param name="libraryName">Library name (e.g., "Shared Documents", "Documents")</param>
        /// <param name="sourceFolderPath">Optional source folder path to help normalize siteId</param>
        /// <returns>Drive ID or null if not found</returns>
        public async Task<string?> GetDriveIdFromSiteAndLibraryAsync(string siteId, string libraryName, string? sourceFolderPath = null)
        {
            if (!_hasCredentials)
            {
                _logger?.LogDebug("Mock: Would get drive ID for site {SiteId}, library {LibraryName}", siteId, libraryName);
                return null;
            }

            // Try multiple siteId formats if the first one fails
            var siteIdFormats = new List<string>();
            
            // Format 1: Use normalized format (hostname:/sites/sitePath if available)
            var normalizedSiteId = NormalizeSiteIdForGraph(siteId, sourceFolderPath);
            siteIdFormats.Add(normalizedSiteId);
            
            // Format 2: If siteId contains commas, try extracting just the site ID part
            if (siteId.Contains(','))
            {
                var parts = siteId.Split(',');
                if (parts.Length >= 3)
                {
                    // Try just the site ID (GUID)
                    siteIdFormats.Add(parts[2]);
                    // Try hostname:/sites/siteId format
                    siteIdFormats.Add($"{parts[0]}:/sites/{parts[2]}");
                }
            }

            // Format 3: Original siteId as-is
            if (!siteIdFormats.Contains(siteId))
            {
                siteIdFormats.Add(siteId);
            }

            foreach (var siteIdFormat in siteIdFormats.Distinct())
            {
                try
                {
                    _logger?.LogDebug("🔍 [GetDriveIdFromSiteAndLibraryAsync] Trying site ID format: {SiteIdFormat} for library {LibraryName}", siteIdFormat, libraryName);

                    // Get all drives for the site
                    var drives = await _client!.Sites[siteIdFormat].Drives.GetAsync();
                    if (drives?.Value == null || !drives.Value.Any())
                    {
                        _logger?.LogWarning("⚠️ [GetDriveIdFromSiteAndLibraryAsync] No drives found for site {SiteIdFormat}", siteIdFormat);
                        continue; // Try next format
                    }

                    _logger?.LogDebug("🔍 [GetDriveIdFromSiteAndLibraryAsync] Found {Count} drives. Drive names: {DriveNames}", 
                        drives.Value.Count(), string.Join(", ", drives.Value.Select(d => d.Name ?? "Unknown")));

                    // Find drive by name (case-insensitive, exact match)
                    var matchingDrive = drives.Value.FirstOrDefault(d => 
                        string.Equals(d.Name, libraryName, StringComparison.OrdinalIgnoreCase));

                    if (matchingDrive != null && !string.IsNullOrWhiteSpace(matchingDrive.Id))
                    {
                        _logger?.LogInformation("✅ [GetDriveIdFromSiteAndLibraryAsync] Found drive ID: {DriveId} for library '{LibraryName}' (exact match)", matchingDrive.Id, libraryName);
                        return matchingDrive.Id;
                    }

                    // If exact match not found, try normalized comparison (remove spaces, case-insensitive)
                    var normalizedLibraryName = libraryName.Replace(" ", "").ToLowerInvariant();
                    matchingDrive = drives.Value.FirstOrDefault(d =>
                    {
                        var normalizedDriveName = d.Name?.Replace(" ", "").ToLowerInvariant() ?? "";
                        return normalizedDriveName == normalizedLibraryName;
                    });

                    if (matchingDrive != null && !string.IsNullOrWhiteSpace(matchingDrive.Id))
                    {
                        _logger?.LogInformation("✅ [GetDriveIdFromSiteAndLibraryAsync] Found drive ID: {DriveId} for library '{LibraryName}' (normalized match, actual name: '{ActualName}')", 
                            matchingDrive.Id, libraryName, matchingDrive.Name);
                        return matchingDrive.Id;
                    }

                    // Try partial match (contains)
                    matchingDrive = drives.Value.FirstOrDefault(d =>
                        d.Name?.Contains(libraryName, StringComparison.OrdinalIgnoreCase) == true ||
                        libraryName.Contains(d.Name ?? "", StringComparison.OrdinalIgnoreCase));

                    if (matchingDrive != null && !string.IsNullOrWhiteSpace(matchingDrive.Id))
                    {
                        _logger?.LogInformation("✅ [GetDriveIdFromSiteAndLibraryAsync] Found drive ID: {DriveId} for library '{LibraryName}' (partial match, actual name: '{ActualName}')", 
                            matchingDrive.Id, libraryName, matchingDrive.Name);
                        return matchingDrive.Id;
                    }

                    _logger?.LogWarning("⚠️ [GetDriveIdFromSiteAndLibraryAsync] Drive not found for library '{LibraryName}' in site {SiteIdFormat}. Available drives: {Drives}", 
                        libraryName, siteIdFormat, string.Join(", ", drives.Value.Select(d => $"'{d.Name}'")));
                }
                catch (ODataError odataError)
                {
                    _logger?.LogWarning(odataError, "⚠️ [GetDriveIdFromSiteAndLibraryAsync] ODataError with site ID format '{SiteIdFormat}': {Code} - {Message}. Trying next format...", 
                        siteIdFormat, odataError.Error?.Code, odataError.Error?.Message);
                    continue; // Try next format
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "⚠️ [GetDriveIdFromSiteAndLibraryAsync] Error with site ID format '{SiteIdFormat}': {Error}. Trying next format...", 
                        siteIdFormat, ex.Message);
                    continue; // Try next format
                }
            }

            _logger?.LogError("❌ [GetDriveIdFromSiteAndLibraryAsync] Failed to get drive ID for library '{LibraryName}' in site {SiteId} after trying {Count} formats", 
                libraryName, siteId, siteIdFormats.Count);
            return null;
        }

        /// <summary>
        /// Get the underlying list ID for a document library drive.
        /// This is used to build list-based webhook subscriptions from a known driveId.
        /// </summary>
        public async Task<string?> GetListIdFromDriveAsync(string driveId)
        {
            if (!_hasCredentials)
            {
                _logger?.LogDebug("Mock: Would get list ID for drive {DriveId}", driveId);
                return null;
            }

            try
            {
                _logger?.LogInformation("🔍 [GetListIdFromDriveAsync] Getting list ID for drive {DriveId}", driveId);

                var list = await _retryPolicy.ExecuteAsync(async () =>
                    await _client!.Drives[driveId].List.GetAsync());

                var listId = list?.Id;
                if (string.IsNullOrWhiteSpace(listId))
                {
                    _logger?.LogWarning("⚠️ [GetListIdFromDriveAsync] List is null or has no ID for drive {DriveId}", driveId);
                    return null;
                }

                _logger?.LogInformation("✅ [GetListIdFromDriveAsync] Found list ID {ListId} for drive {DriveId}", listId, driveId);
                return listId;
            }
            catch (ODataError odataError)
            {
                _logger?.LogError(odataError, "❌ [GetListIdFromDriveAsync] ODataError getting list ID for drive {DriveId}: {Code} - {Message}", 
                    driveId, odataError.Error?.Code, odataError.Error?.Message);
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ [GetListIdFromDriveAsync] Error getting list ID for drive {DriveId}: {Error}", driveId, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Resolves a SharePoint list item (by siteId + listId + listItemId) to its underlying DriveItem.
        /// This is used for list-based webhooks where notifications are raised on the list, not the drive.
        /// </summary>
        public async Task<DriveItem?> GetDriveItemForListItemAsync(string siteId, string listId, string listItemId, string? sourceFolderPath = null)
        {
            if (!_hasCredentials)
            {
                _logger?.LogDebug("Mock: Would resolve list item {ListItemId} in list {ListId} at site {SiteId} to a driveItem", listItemId, listId, siteId);
                return null;
            }

            try
            {
                var normalizedSiteId = NormalizeSiteIdForGraph(siteId, sourceFolderPath);

                _logger?.LogInformation("🔍 [GetDriveItemForListItemAsync] Resolving driveItem for SiteId={SiteId}, ListId={ListId}, ListItemId={ListItemId}", normalizedSiteId, listId, listItemId);

                var listItem = await _retryPolicy.ExecuteAsync(async () =>
                    await _client!.Sites[normalizedSiteId].Lists[listId].Items[listItemId].GetAsync(config =>
                    {
                        // Expand driveItem so we can get the underlying file
                        config.QueryParameters.Expand = new[] { "driveItem" };
                    }));

                if (listItem == null)
                {
                    _logger?.LogWarning("⚠️ [GetDriveItemForListItemAsync] ListItem is null for SiteId={SiteId}, ListId={ListId}, ListItemId={ListItemId}", normalizedSiteId, listId, listItemId);
                    return null;
                }

                if (listItem.DriveItem == null)
                {
                    _logger?.LogWarning("⚠️ [GetDriveItemForListItemAsync] DriveItem is null on list item for SiteId={SiteId}, ListId={ListId}, ListItemId={ListItemId}", normalizedSiteId, listId, listItemId);
                    return null;
                }

                _logger?.LogInformation("✅ [GetDriveItemForListItemAsync] Resolved DriveItem {ItemId} (Name={Name}) for list item {ListItemId}", listItem.DriveItem.Id ?? "null", listItem.DriveItem.Name ?? "null", listItemId);
                return listItem.DriveItem;
            }
            catch (ODataError odataError)
            {
                _logger?.LogError(odataError, "❌ [GetDriveItemForListItemAsync] ODataError resolving driveItem for SiteId={SiteId}, ListId={ListId}, ListItemId={ListItemId}: {Code} - {Message}", 
                    siteId, listId, listItemId, odataError.Error?.Code, odataError.Error?.Message);
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ [GetDriveItemForListItemAsync] Error resolving driveItem for SiteId={SiteId}, ListId={ListId}, ListItemId={ListItemId}: {Error}", 
                    siteId, listId, listItemId, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Update list item fields by site ID, list ID, and list item ID
        /// </summary>
        /// <param name="siteId">Site ID</param>
        /// <param name="listId">List ID</param>
        /// <param name="listItemId">List item ID</param>
        /// <param name="fields">Fields to update</param>
        public async Task UpdateListItemFieldsByListIdAsync(string siteId, string listId, string listItemId, Dictionary<string, object> fields, string? sourceFolderPath = null)
        {
            if (!_hasCredentials)
            {
                _logger?.LogDebug("Mock: Would update list item fields for site {SiteId}, list {ListId}, item {ListItemId}", siteId, listId, listItemId);
                return;
            }

            // Try multiple siteId formats if the first one fails
            var siteIdFormats = new List<string>();
            
            // Format 1: Use normalized format (hostname:/sites/sitePath if available)
            var normalizedSiteId = NormalizeSiteIdForGraph(siteId, sourceFolderPath);
            siteIdFormats.Add(normalizedSiteId);
            
            // Format 2: If siteId contains commas, try extracting just the site ID part
            if (siteId.Contains(','))
            {
                var parts = siteId.Split(',');
                if (parts.Length >= 3)
                {
                    // Try just the site ID (GUID)
                    siteIdFormats.Add(parts[2]);
                    // Try hostname:/sites/siteId format
                    siteIdFormats.Add($"{parts[0]}:/sites/{parts[2]}");
                }
            }
            
            // Format 3: Original siteId as-is
            if (!siteIdFormats.Contains(siteId))
            {
                siteIdFormats.Add(siteId);
            }

            Exception? lastException = null;

            // Try each siteId format until one works
            foreach (var siteIdFormat in siteIdFormats.Distinct())
            {
                try
                {
                    _logger?.LogInformation("🔄 [UpdateListItemFieldsByListIdAsync] Updating fields for site {SiteId}, list {ListId}, item {ListItemId} (trying format: {Format})", 
                        siteId, listId, listItemId, siteIdFormat);

                    var fieldValues = new Microsoft.Graph.Models.FieldValueSet();
                    if (fieldValues.AdditionalData == null)
                    {
                        fieldValues.AdditionalData = new Dictionary<string, object>();
                    }

                    foreach (var field in fields)
                    {
                        fieldValues.AdditionalData[field.Key] = field.Value;
                    }

                    await _retryPolicy.ExecuteAsync(async () =>
                    {
                        await _client!.Sites[siteIdFormat].Lists[listId].Items[listItemId].Fields.PatchAsync(fieldValues);
                    });

                    _logger?.LogInformation("✅ [UpdateListItemFieldsByListIdAsync] Successfully updated fields for list item {ListItemId} using site ID format: {SiteIdFormat}", 
                        listItemId, siteIdFormat);
                    return; // Success, exit
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger?.LogWarning("⚠️ [UpdateListItemFieldsByListIdAsync] Failed with site ID format {SiteIdFormat}: {Error}", 
                        siteIdFormat, ex.Message);
                    if (ex is ODataError odataError)
                    {
                        _logger?.LogWarning("   OData Error Code: {Code}, Message: {Message}", odataError.Error?.Code, odataError.Error?.Message);
                        // If it's itemNotFound, try next format
                        if (odataError.Error?.Code == "itemNotFound" || odataError.Error?.Code == "NotFound")
                        {
                            continue; // Try next format
                        }
                    }
                    // For other errors, continue trying other formats
                }
            }

            // If we get here, all formats failed
            _logger?.LogError(lastException, "❌ [UpdateListItemFieldsByListIdAsync] Error updating fields for site {SiteId}, list {ListId}, item {ListItemId} after trying all formats: {Error}", 
                siteId, listId, listItemId, lastException?.Message ?? "Unknown error");
            throw lastException ?? new InvalidOperationException($"Could not update list item fields in site {siteId} using any site ID format");
        }

        /// <summary>
        /// Get list ID by list name
        /// </summary>
        /// <param name="siteId">Site ID</param>
        /// <param name="listName">List name (e.g., "SMEPilotConfig")</param>
        /// <returns>List ID or null if not found</returns>
        public async Task<string?> GetListIdByNameAsync(string siteId, string listName, string? sourceFolderPath = null, string? tenantId = null)
        {
            if (!_hasCredentials)
            {
                _logger?.LogDebug("Mock: Would get list ID for list {ListName} in site {SiteId}", listName, siteId);
                return null;
            }

            // Try multiple siteId formats if the first one fails
            var siteIdFormats = new List<string>();
            
            // Format 1: Use normalized format (hostname:/sites/sitePath if available)
            var normalizedSiteId = NormalizeSiteIdForGraph(siteId, sourceFolderPath);
            siteIdFormats.Add(normalizedSiteId);
            
            // Format 2: If siteId contains commas, try extracting just the site ID part
            if (siteId.Contains(','))
            {
                var parts = siteId.Split(',');
                if (parts.Length >= 3)
                {
                    // Try just the site ID (GUID)
                    siteIdFormats.Add(parts[2]);
                    // Try hostname:/sites/siteId format
                    siteIdFormats.Add($"{parts[0]}:/sites/{parts[2]}");
                }
            }
            
            // Format 3: Original siteId as-is
            if (!siteIdFormats.Contains(siteId))
            {
                siteIdFormats.Add(siteId);
            }

            // Try each siteId format until one works
            foreach (var siteIdFormat in siteIdFormats.Distinct())
            {
                try
                {
                    _logger?.LogInformation("🔍 [GetListIdByNameAsync] Getting list ID for list '{ListName}' in site {SiteId} (trying format: {Format})", 
                        listName, siteId, siteIdFormat);

                    var client = GetClientForTenant(tenantId);

                    var lists = await _retryPolicy.ExecuteAsync(async () =>
                    {
                        return await client.Sites[siteIdFormat].Lists.GetAsync(config =>
                        {
                            config.QueryParameters.Filter = $"displayName eq '{listName}'";
                            config.QueryParameters.Top = 1;
                        });
                    });

                    if (lists?.Value == null || !lists.Value.Any())
                    {
                        _logger?.LogWarning("⚠️ [GetListIdByNameAsync] List '{ListName}' not found in site {SiteIdFormat}", listName, siteIdFormat);
                        continue; // Try next format
                    }

                    var list = lists.Value.First();
                    var listId = list.Id;
                    _logger?.LogInformation("✅ [GetListIdByNameAsync] Found list '{ListName}' with ID {ListId} using site ID format: {SiteIdFormat}", 
                        listName, listId, siteIdFormat);
                    return listId;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning("⚠️ [GetListIdByNameAsync] Failed with site ID format {SiteIdFormat}: {Error}", 
                        siteIdFormat, ex.Message);
                    if (ex is ODataError odataError)
                    {
                        _logger?.LogWarning("   OData Error Code: {Code}, Message: {Message}", odataError.Error?.Code, odataError.Error?.Message);
                        // If it's itemNotFound, try next format
                        if (odataError.Error?.Code == "itemNotFound" || odataError.Error?.Code == "NotFound")
                        {
                            continue; // Try next format
                        }
                    }
                    // For other errors, continue trying other formats
                }
            }

            // If we get here, all formats failed
            _logger?.LogError("❌ [GetListIdByNameAsync] Error getting list ID for list '{ListName}' in site {SiteId} after trying all formats", listName, siteId);
            return null;
        }

        /// <summary>
        /// Get list items from a SharePoint list by list name
        /// </summary>
        /// <param name="siteId">Site ID</param>
        /// <param name="listName">List name (e.g., "SMEPilotConfig")</param>
        /// <param name="top">Maximum number of items to return (default: 100)</param>
        /// <returns>List of list items with their fields</returns>
        public async Task<List<Microsoft.Graph.Models.ListItem>> GetListItemsByNameAsync(string siteId, string listName, int top = 100, string? sourceFolderPath = null, string? tenantId = null)
        {
            if (!_hasCredentials)
            {
                _logger?.LogDebug("Mock: Would query list items from list {ListName} in site {SiteId}", listName, siteId);
                return new List<Microsoft.Graph.Models.ListItem>();
            }

            // Try multiple siteId formats if the first one fails
            var siteIdFormats = new List<string>();
            
            // Format 1: Use normalized format (hostname:/sites/sitePath if available)
            var normalizedSiteId = NormalizeSiteIdForGraph(siteId, sourceFolderPath);
            siteIdFormats.Add(normalizedSiteId);
            
            // Format 2: If siteId contains commas, try extracting just the site ID part
            if (siteId.Contains(','))
            {
                var parts = siteId.Split(',');
                if (parts.Length >= 3)
                {
                    // Try just the site ID (GUID)
                    siteIdFormats.Add(parts[2]);
                    // Try hostname:/sites/siteId format
                    siteIdFormats.Add($"{parts[0]}:/sites/{parts[2]}");
                }
            }
            
            // Format 3: Original siteId as-is
            if (!siteIdFormats.Contains(siteId))
            {
                siteIdFormats.Add(siteId);
            }

            Exception? lastException = null;
            
            // Try each siteId format until one works
            foreach (var siteIdFormat in siteIdFormats.Distinct())
            {
                try
                {
                    _logger?.LogInformation("📋 [GetListItemsByNameAsync] Querying list items from list '{ListName}' in site {SiteId} (trying format: {Format})", 
                    listName, siteId, siteIdFormat);

                    var client = GetClientForTenant(tenantId);

                    // First, get the list by name
                    var lists = await _retryPolicy.ExecuteAsync(async () =>
                    {
                        return await client.Sites[siteIdFormat].Lists.GetAsync(config =>
                        {
                            config.QueryParameters.Filter = $"displayName eq '{listName}'";
                            config.QueryParameters.Top = 1;
                        });
                    });

                    if (lists?.Value == null || !lists.Value.Any())
                    {
                        _logger?.LogWarning("⚠️ [GetListItemsByNameAsync] List '{ListName}' not found in site {SiteIdFormat}", listName, siteIdFormat);
                        continue; // Try next format
                    }

                    var list = lists.Value.First();
                    var listId = list.Id;

                    _logger?.LogInformation("✅ [GetListItemsByNameAsync] Found list '{ListName}' with ID {ListId} using site ID format: {SiteIdFormat}", 
                        listName, listId, siteIdFormat);

                    // Get list items
                    var items = await _retryPolicy.ExecuteAsync(async () =>
                    {
                        return await client.Sites[siteIdFormat].Lists[listId].Items.GetAsync(config =>
                        {
                            config.QueryParameters.Top = top;
                            config.QueryParameters.Expand = new[] { "fields" };
                        });
                    });

                    var itemList = items?.Value?.ToList() ?? new List<Microsoft.Graph.Models.ListItem>();
                    _logger?.LogInformation("✅ [GetListItemsByNameAsync] Retrieved {Count} items from list '{ListName}'", itemList.Count, listName);

                    return itemList;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger?.LogWarning("⚠️ [GetListItemsByNameAsync] Failed with site ID format {SiteIdFormat}: {Error}", 
                        siteIdFormat, ex.Message);
                    if (ex is ODataError odataError)
                    {
                        _logger?.LogWarning("   OData Error Code: {Code}, Message: {Message}", odataError.Error?.Code, odataError.Error?.Message);
                        // If it's itemNotFound, try next format
                        if (odataError.Error?.Code == "itemNotFound" || odataError.Error?.Code == "NotFound")
                        {
                            continue; // Try next format
                        }
                    }
                    // For other errors, continue trying other formats
                }
            }

            // If we get here, all formats failed
            _logger?.LogError(lastException, "❌ [GetListItemsByNameAsync] Error retrieving list items from list '{ListName}' in site {SiteId} after trying all formats: {Error}", 
                listName, siteId, lastException?.Message ?? "Unknown error");
            if (lastException is ODataError odataError2)
            {
                _logger?.LogError("   OData Error Code: {Code}, Message: {Message}", odataError2.Error?.Code, odataError2.Error?.Message);
            }
            throw lastException ?? new InvalidOperationException($"Could not retrieve list items from '{listName}' in site {siteId} using any site ID format");
        }
    }
}



