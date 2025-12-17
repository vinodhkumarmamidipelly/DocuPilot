namespace DocumentMergeApi.Models;

/// <summary>
/// Minimal, backend-agnostic representation of an enrichment run used to rebuild
/// Version History and Change Log tables deterministically on each merge.
/// </summary>
public sealed class RunHistoryEntry
{
    public string Version { get; init; } = "1.0";
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
    public string Author { get; init; } = "System Generated";
    public string SectionSummary { get; init; } = "Content";

    /// <summary>
    /// Human-readable description of what changed in this run. Used to populate:
    /// - Version History: "Changes"
    /// - Change Log: "Description"
    /// </summary>
    public string ChangeDescription { get; init; } = "Updated content based on latest raw document";

    /// <summary>
    /// Normalized heading -> content hash fingerprints for the raw doc sections of that run.
    /// Used to compute Added/Updated/Removed summaries on later runs.
    /// </summary>
    public IReadOnlyDictionary<string, string> SectionFingerprints { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Normalized heading -> display title for this run (used to render readable diffs).
    /// </summary>
    public IReadOnlyDictionary<string, string> SectionTitles { get; init; } = new Dictionary<string, string>();
}


