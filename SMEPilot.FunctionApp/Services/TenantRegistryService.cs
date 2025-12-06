using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SMEPilot.FunctionApp.Services
{
    /// <summary>
    /// Lightweight tenant registry abstraction.
    ///
    /// First version is backed by app settings (MultiTenant_TenantIds) so we have
    /// a single place to reason about which customer tenants are considered "active".
    /// Later this can be moved to Azure Table, SQL, or a SharePoint list without
    /// changing the callers.
    /// </summary>
    public class TenantRegistryService
    {
        private readonly ILogger<TenantRegistryService>? _logger;

        public TenantRegistryService(ILogger<TenantRegistryService>? logger = null)
        {
            _logger = logger;
        }

        public IReadOnlyList<TenantInfo> GetAllTenants()
        {
            var tenants = new List<TenantInfo>();

            // Primary source: MultiTenant_TenantInfo app setting (JSON mapping)
            // {
            //   "tenantId-guid": { "name": "Org", "primarySiteUrl": "https://...", "isEnabled": true },
            //   ...
            // }
            var infoJson = Environment.GetEnvironmentVariable("MultiTenant_TenantInfo");
            if (!string.IsNullOrWhiteSpace(infoJson))
            {
                try
                {
                    var document = JsonSerializer.Deserialize<Dictionary<string, TenantInfoConfig>>(infoJson);
                    if (document != null)
                    {
                        foreach (var kvp in document)
                        {
                            var id = kvp.Key?.Trim();
                            if (string.IsNullOrWhiteSpace(id))
                                continue;

                            var cfg = kvp.Value ?? new TenantInfoConfig();
                            tenants.Add(new TenantInfo
                            {
                                TenantId = id,
                                DisplayName = cfg.name,
                                PrimarySiteUrl = cfg.primarySiteUrl,
                                IsEnabled = cfg.isEnabled ?? true
                            });
                        }
                    }

                    _logger?.LogInformation("🔍 [TenantRegistry] Loaded {Count} tenants from MultiTenant_TenantInfo", tenants.Count);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "⚠️ [TenantRegistry] Failed to parse MultiTenant_TenantInfo. Falling back to MultiTenant_TenantIds.");
                    tenants.Clear();
                }
            }

            // Fallback: MultiTenant_TenantIds app setting (comma/semicolon separated GUIDs)
            if (tenants.Count == 0)
            {
                var raw = Environment.GetEnvironmentVariable("MultiTenant_TenantIds");
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    var ids = raw
                        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    foreach (var id in ids)
                    {
                        tenants.Add(new TenantInfo
                        {
                            TenantId = id,
                            DisplayName = null,
                            PrimarySiteUrl = null,
                            IsEnabled = true
                        });
                    }

                    _logger?.LogInformation("🔍 [TenantRegistry] Loaded {Count} tenants from MultiTenant_TenantIds", tenants.Count);
                }
            }

            // If still empty, expose empty list (caller will handle fallback to Graph_TenantId).
            return tenants;
        }

        public IReadOnlyList<string> GetActiveTenantIds()
        {
            return GetAllTenants()
                .Where(t => t.IsEnabled)
                .Select(t => t.TenantId)
                .ToList();
        }
    }

    public class TenantInfo
    {
        public string TenantId { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? PrimarySiteUrl { get; set; }
        public bool IsEnabled { get; set; } = true;
    }

    internal class TenantInfoConfig
    {
        public string? name { get; set; }
        public string? primarySiteUrl { get; set; }
        public bool? isEnabled { get; set; }
    }
}


