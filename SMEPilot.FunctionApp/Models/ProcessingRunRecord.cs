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
        /// <summary>
        /// Optional title/label for the run (typically the file name). Mapped to the
        /// SharePoint list's built-in Title column so that rows are readable in the UI.
        /// </summary>
        public string? Title { get; set; }

        public string RawDriveId { get; set; } = string.Empty;
        public string RawItemId { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public string? Version { get; set; }
        public string Status { get; set; } = string.Empty; // e.g. Processing, Succeeded, Failed
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Best-effort human author name for this run (typically the SharePoint uploader / last modifier).
        /// Used to render Version History and Change Log tables deterministically from SMEPilotRuns.
        /// </summary>
        public string? Author { get; set; }

        /// <summary>
        /// Summary of which sections were updated/filled for this run.
        /// Used to render Change Log "Section" and "Description" fields.
        /// </summary>
        public string? SectionSummary { get; set; }

        /// <summary>
        /// Human-readable description of what changed in this run (used for Version History "Changes"
        /// and Change Log "Description"). Stored in tracking meta for succeeded runs.
        /// </summary>
        public string? ChangeDescription { get; set; }

        /// <summary>
        /// SharePoint list item UI version string for the raw source at the time we processed it.
        /// Used for persistent dedup across multiple Graph webhook notifications for the same edit.
        /// Stored inside tracking meta for succeeded runs.
        /// </summary>
        public string? RawUiVersion { get; set; }

        /// <summary>
        /// LastModifiedDateTime (UTC) of the raw DriveItem at the time we processed it.
        /// Used as a fallback dedup signal when UI version is unavailable.
        /// Stored inside tracking meta for succeeded runs.
        /// </summary>
        public DateTimeOffset? RawLastModifiedUtc { get; set; }

        /// <summary>
        /// Fingerprints of the raw document's sections for this run (normalized heading -> content hash).
        /// Used to compute a simple Added/Updated/Removed summary for future runs.
        /// Persisted inside tracking meta for succeeded runs.
        /// </summary>
        public Dictionary<string, string>? SectionFingerprints { get; set; }

        /// <summary>
        /// Normalized heading -> display title for this run.
        /// Persisted inside tracking meta for succeeded runs.
        /// </summary>
        public Dictionary<string, string>? SectionTitles { get; set; }
        public string? EnrichedUrl { get; set; }
        /// <summary>
        /// Optional driveId of the enriched file (destination library), if known.
        /// Used so we can clean up enriched copies when the source file is deleted.
        /// </summary>
        public string? EnrichedDriveId { get; set; }
        /// <summary>
        /// Optional itemId of the enriched file in the destination drive.
        /// </summary>
        public string? EnrichedItemId { get; set; }
        public DateTimeOffset LastUpdatedUtc { get; set; }
    }
}


