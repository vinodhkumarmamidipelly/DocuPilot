using System;

namespace SMEPilot.FunctionApp.Models
{
    /// <summary>
    /// Tracking record for enrichment runs, keyed by RawDriveId + RawItemId.
    /// This will eventually be backed by a SharePoint list (e.g. SMEPilotRuns),
    /// but introducing the model is a no-op for current behavior.
    /// </summary>
    public class ProcessingRunRecord
    {
        public string RawDriveId { get; set; } = string.Empty;
        public string RawItemId { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public string? Version { get; set; }
        public string Status { get; set; } = string.Empty; // e.g. Processing, Succeeded, Failed
        public string? ErrorMessage { get; set; }
        public string? EnrichedUrl { get; set; }
        public DateTimeOffset LastUpdatedUtc { get; set; }
    }
}


