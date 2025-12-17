namespace DocumentMergeApi.Models;

public sealed class MergeResult
{
    public MergeResult(
        byte[] content,
        string fileName,
        string version,
        string? sectionSummary = null,
        string? changeDescription = null,
        IReadOnlyDictionary<string, string>? sectionFingerprints = null,
        IReadOnlyDictionary<string, string>? sectionTitles = null)
    {
        Content = content;
        FileName = fileName;
        Version = version;
        SectionSummary = sectionSummary ?? "Content";
        ChangeDescription = changeDescription ?? "Updated content based on latest raw document";
        SectionFingerprints = sectionFingerprints ?? new Dictionary<string, string>();
        SectionTitles = sectionTitles ?? new Dictionary<string, string>();
    }

    public byte[] Content { get; }
    public string FileName { get; }
    public string Version { get; }
    public string SectionSummary { get; }
    public string ChangeDescription { get; }
    public IReadOnlyDictionary<string, string> SectionFingerprints { get; }
    public IReadOnlyDictionary<string, string> SectionTitles { get; }
}


