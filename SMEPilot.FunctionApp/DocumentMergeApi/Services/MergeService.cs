using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentMergeApi.Models;
using System.Text.RegularExpressions;
using System.Text;
using System.Security.Cryptography;

namespace DocumentMergeApi.Services;

public sealed class MergeService
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
    private static readonly IReadOnlyDictionary<string, Func<IDictionary<string, string>, string?>> TokenFallbackResolvers =
        new Dictionary<string, Func<IDictionary<string, string>, string?>>(StringComparer.OrdinalIgnoreCase)
        {
            ["DOCUMENT_NAME"] = metadata => GetMetadataOrNull(metadata, "DOCUMENT_NAME") ?? GetMetadataOrNull(metadata, "PROJECT_NAME"),
            ["DOCUMENT_ID"] = metadata => BuildDocumentId(metadata),
            ["AUTHER"] = metadata => GetMetadataOrNull(metadata, "AUTHOR_NAME"),
            ["AUTHORS"] = metadata => GetMetadataOrNull(metadata, "AUTHOR_NAME"),
            ["AUTHOR_NAME"] = metadata => GetMetadataOrNull(metadata, "AUTHOR_NAME"),
            ["REVIEWER_NAME"] = metadata => GetMetadataOrNull(metadata, "REVIEWER_NAME"),
            ["APPROVER_NAME"] = metadata => GetMetadataOrNull(metadata, "APPROVER_NAME"),
            ["STATUS"] = metadata => GetMetadataOrNull(metadata, "STATUS") ?? "Draft",
            ["DATE"] = metadata => GetMetadataOrNull(metadata, "DATE") ?? DateTime.UtcNow.ToString("MMMM yyyy")
        };
    private static readonly HashSet<string> ReservedMetadataTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "DOCUMENT_NAME",
        "PROJECT_NAME",
        "VERSION_NUMBER",
        "DATE",
        "STATUS",
        "AUTHOR_NAME",
        "REVIEWER_NAME",
        "APPROVER_NAME",
        "DOCUMENT_ID",
        "DOCUMENT_TYPE",
        "CLASSIFICATION",
        "AUTHER"
    };
    private static readonly HashSet<string> LineBreakTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "PROJECT_NAME",
        "VERSION_NUMBER",
        "DATE",
        "STATUS",
        "AUTHOR_NAME",
        "REVIEWER_NAME",
        "APPROVER_NAME"
    };

    private readonly TokenDetector _tokens;
    private readonly RawSectionExtractor _extractor;
    private readonly MatcherEngine _matcher;
    private readonly MetadataExtractor _metadataExtractor;
    private readonly SectionInserter _sectionInserter;
    private readonly AltChunkInserter _altChunkInserter;
    private readonly TocManager _toc;
    private readonly TableUpdater _tables;

    public MergeService(TokenDetector tokens, RawSectionExtractor extractor, MatcherEngine matcher,
                        MetadataExtractor metadataExtractor, AltChunkInserter altChunkInserter,
                        SectionInserter sectionInserter, TocManager toc, TableUpdater tables)
    {
        _tokens = tokens;
        _extractor = extractor;
        _matcher = matcher;
        _metadataExtractor = metadataExtractor;
        _altChunkInserter = altChunkInserter;
        _sectionInserter = sectionInserter;
        _toc = toc;
        _tables = tables;
    }

    public MergeResult Merge(
        Stream templateStream,
        Stream rawStream,
        string? author,
        string? previousVersion = null,
        IReadOnlyList<RunHistoryEntry>? succeededHistory = null,
        string? rawUiVersion = null,
        DateTimeOffset? rawLastModifiedUtc = null)
    {
        // Use temporary file to ensure proper document saving
        var tempFile = Path.Combine(Path.GetTempPath(), $"merge_{Guid.NewGuid():N}.docx");
        var rawMemoryStream = new MemoryStream();
        IDictionary<string, string> metadataValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        string effectiveVersion = "1.0";
        string sectionSummary = "Content";
        string changeDescription = "Updated content based on latest raw document";
        Dictionary<string, string> currentFingerprints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> currentTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            // Copy template to temp file
            using (var fileStream = File.Create(tempFile))
            {
                templateStream.CopyTo(fileStream);
            }
            
            // Copy raw to memory stream
            rawStream.CopyTo(rawMemoryStream);
            rawMemoryStream.Position = 0;

            using (var templateDoc = WordprocessingDocument.Open(tempFile, true))
            using (var rawDoc = WordprocessingDocument.Open(rawMemoryStream, false))
            {
                // If the source template was a DOTX, the package may still be marked as a template
                // even though the file extension is .docx. Convert it to a standard document so
                // Word (and especially Word Online) no longer treats it as a template.
                if (templateDoc.DocumentType == WordprocessingDocumentType.Template)
                {
                    templateDoc.ChangeDocumentType(WordprocessingDocumentType.Document);
                }

                metadataValues = _metadataExtractor.Extract(rawDoc);

                // Determine the effective VERSION_NUMBER for this run, supporting auto-bump
                // behavior across enrichments. We combine the version detected from the raw
                // document (metadataValues["VERSION_NUMBER"]) with any previously-used
                // If we have run history, compute the next version deterministically from it.
                // This avoids relying on "latest succeeded" tracking reads in tenants where filtering is constrained.
                if (succeededHistory != null && succeededHistory.Any())
                {
                    effectiveVersion = ComputeNextVersionFromHistory(succeededHistory);
                    metadataValues["VERSION_NUMBER"] = effectiveVersion;
                }
                else
                {
                    // Otherwise, use the classic behavior: version from raw doc metadata + previousVersion (tracking).
                    effectiveVersion = ComputeEffectiveVersion(metadataValues, previousVersion);
                }

                var tokens = _tokens.GetTokens(templateDoc);
                var sections = _extractor.Extract(rawDoc);
                var matches = _matcher.Match(tokens, sections);
                EnsureMetadataTokensPreferMetadata(matches);

                Console.WriteLine($"Found {tokens.Count} tokens in template");
                Console.WriteLine($"Found {sections.Count} sections in raw document");
                Console.WriteLine($"Matched {matches.Values.Count(v => v != null)} out of {matches.Count} tokens");

                OpenXmlElement? lastInsertLocation = null;
                var usedSections = new HashSet<RawSection>();

                foreach (var kvp in matches.OrderBy(k => k.Key.ParagraphIndex))
                {
                    if (kvp.Value is not RawSection section)
                        continue;

                    if (!usedSections.Add(section))
                    {
                        matches[kvp.Key] = null;
                        continue;
                    }

                    var placeholder = kvp.Key.PlaceholderParagraph;
                    var headingParagraph = FindTemplateHeadingParagraph(kvp.Key);
                    var clonedSection = CloneSection(section);
                    Console.WriteLine($"Token '{kvp.Key.Token}' (idx: {kvp.Key.ParagraphIndex}) headingPlaceholder={headingParagraph != null}, sectionKey={section.Key}, elements={clonedSection.Elements.Count}");

                    try
                    {
                        var insertAfter = placeholder;
                        var insertedElement = InsertSectionPreservingFormatting(templateDoc, rawDoc, insertAfter, clonedSection);
                        lastInsertLocation = insertedElement;
                        Console.WriteLine($"Inserted section '{section.HeadingText}' for token '{kvp.Key.Token}'");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error inserting section for token '{kvp.Key.Token}': {ex.Message}");
                    }

                    RemoveParagraphIfExists(placeholder);
                    if (headingParagraph != null && headingParagraph != placeholder)
                    {
                        RemoveParagraphIfExists(headingParagraph);
                    }
                }

                var matchedSet = usedSections;
                var remaining = sections.Where(s => !matchedSet.Contains(s)).ToList();
                var body = templateDoc.MainDocumentPart!.Document.Body!;
                
                Console.WriteLine($"Remaining unmatched sections: {remaining.Count}");

                // Determine where to append any remaining sections that were not matched to
                // explicit tokens in the template. If the template defines an explicit content
                // window using:
                //   [DOCUMENT_CONTENT_STARTS_HERE]
                //   ...
                //   [DOCUMENT_CONTENT_ENDS_HERE]
                // then append unmatched sections *inside* that window, just before the END
                // marker. Otherwise, fall back to appending at the bottom of the document
                // (or after the last inserted section, if any).
                var (contentStart, contentEnd) = FindContentRegion(body);
                OpenXmlElement anchor;
                if (contentStart != null && contentEnd != null)
                {
                    // Insert before the END marker by anchoring at the element immediately
                    // preceding it. If there is no previous sibling (rare), fall back to body.
                    anchor = contentEnd.PreviousSibling<OpenXmlElement>() ?? (OpenXmlElement)body;
                }
                else
                {
                    anchor = lastInsertLocation ?? (OpenXmlElement)body;
                }

                foreach (var s in remaining)
                {
                    try
                    {
                        anchor = InsertSectionPreservingFormatting(templateDoc, rawDoc, anchor, s);
                        Console.WriteLine($"Appended section '{s.HeadingText}' (Key: {s.Key})");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error appending section '{s.HeadingText}': {ex.Message}");
                    }
                }

                ApplyMetadata(tokens, matches, metadataValues);

                // After generic token replacement, normalize the header metadata table so
                // key fields (Version, Date, Author, etc.) each have clean values in their
                // own cells rather than concatenated inline tokens.
                _tables.UpdateHeaderMetadataTable(templateDoc, metadataValues, author);

                var tocToken = tokens.FirstOrDefault(t => string.Equals(t.Token, "TOC", StringComparison.OrdinalIgnoreCase));
                var tocInserted = false;
                if (tocToken != null)
                {
                    _toc.InsertAutoTocAtPlaceholder(templateDoc, tocToken.PlaceholderParagraph);
                    tocInserted = true;
                }
                else if (!_toc.RawHasToc(rawDoc))
                {
                    // If the template already contains a "Table of Contents" heading,
                    // treat that paragraph as the TOC placeholder so the inserted TOC
                    // replaces it. Otherwise, insert a new placeholder at the top.
                    Paragraph? tocHeadingPara = body
                        .Descendants<Paragraph>()
                        .FirstOrDefault(p =>
                            string.Concat(p.Descendants<Text>().Select(t => t.Text ?? string.Empty))
                                  .Trim()
                                  .Equals("Table of Contents", StringComparison.OrdinalIgnoreCase));

                    Paragraph placeholderPara;
                    if (tocHeadingPara != null)
                    {
                        placeholderPara = tocHeadingPara;
                    }
                    else
                    {
                        placeholderPara = new Paragraph(new Run(new Text(string.Empty)));
                        var firstPara = body.Elements<Paragraph>().FirstOrDefault();
                        if (firstPara != null)
                        {
                            body.InsertBefore(placeholderPara, firstPara);
                        }
                        else
                        {
                            body.AppendChild(placeholderPara);
                        }
                    }

                    _toc.InsertAutoTocAtPlaceholder(templateDoc, placeholderPara);
                    tocInserted = true;
                }

                if (tocInserted)
                {
                    _toc.EnsureFieldsUpdateOnOpen(templateDoc);
                }

                // Build section summary for Change Log (use headings of matched sections)
                var sectionNames = matchedSet
                    .Select(s => s.HeadingText)
                    .Where(h => !string.IsNullOrWhiteSpace(h))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                sectionSummary = sectionNames.Count == 0
                    ? "Content"
                    : string.Join(", ", sectionNames);

                // Keep this short so it fits comfortably in tables and tracking.
                var shortSection = sectionNames.Count == 0 ? "Content" : string.Join(", ", sectionNames.Take(3));
                if (sectionNames.Count > 3) shortSection += $" (+{sectionNames.Count - 3})";

                // Compute fingerprints for raw sections and generate a simple Added/Updated/Removed summary
                // relative to the previous succeeded run (if available).
                (currentFingerprints, currentTitles) = ComputeSectionFingerprintsAndTitles(sections);
                var prevFingerprints = succeededHistory?
                    .OrderBy(h => h.TimestampUtc)
                    .LastOrDefault()?
                    .SectionFingerprints;
                var prevTitles = succeededHistory?
                    .OrderBy(h => h.TimestampUtc)
                    .LastOrDefault()?
                    .SectionTitles;

                var (humanChangeDescription, humanSectionSummary) = BuildHumanChangeSummary(prevFingerprints, prevTitles, currentFingerprints, currentTitles);
                var effectiveSectionSummary = string.IsNullOrWhiteSpace(humanSectionSummary) ? shortSection : humanSectionSummary;

                // Keep the description simple and human-friendly (like the example tables),
                // and avoid embedding raw UI versions or timestamps in the description.
                changeDescription = string.IsNullOrWhiteSpace(humanChangeDescription)
                    ? BuildChangeDescription(effectiveSectionSummary, rawUiVersion, rawLastModifiedUtc, diffSummary: null)
                    : humanChangeDescription;

                // Rebuild history tables deterministically from persisted SMEPilotRuns history (preferred),
                // falling back to legacy append behavior if history wasn't provided.
                if (succeededHistory != null)
                {
                    var runs = succeededHistory
                        .Where(r => r != null)
                        .OrderBy(r => r.TimestampUtc)
                        .ToList();

                    // Append "this run" at merge time so the output document always includes the latest run.
                    runs.Add(new RunHistoryEntry
                    {
                        Version = effectiveVersion,
                        TimestampUtc = DateTimeOffset.UtcNow,
                        Author = string.IsNullOrWhiteSpace(author) ? "System Generated" : author!,
                        SectionSummary = string.IsNullOrWhiteSpace(effectiveSectionSummary) ? "Content" : effectiveSectionSummary,
                        ChangeDescription = changeDescription,
                        SectionFingerprints = currentFingerprints,
                        SectionTitles = currentTitles
                    });

                    _tables.RebuildVersionHistoryFromRuns(templateDoc, runs);
                    _tables.RebuildChangeLogFromRuns(templateDoc, runs);
                }
                else
                {
                    _tables.AppendVersionHistory(templateDoc, author, effectiveVersion);
                    _tables.AppendChangeLog(templateDoc, author, sectionSummary);
                }

                // Ensure that the standard confidentiality disclaimer, if present,
                // always appears as the last paragraph in the document regardless
                // of where content was inserted.
                MoveDisclaimerToDocumentEnd(body);

                templateDoc.MainDocumentPart!.Document.Save();
                templateDoc.Save();
            }
            
            // Validate the file can be opened before proceeding
            try
            {
                using (var validationDoc = WordprocessingDocument.Open(tempFile, false))
                {
                    // If we can open it, it's valid
                    var validationBody = validationDoc.MainDocumentPart?.Document?.Body;
                    if (validationBody == null)
                    {
                        throw new InvalidOperationException("Document body is null after merge");
                    }
                    Console.WriteLine("Document validation passed - structure is valid");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Document validation failed. The merged document structure is invalid: {ex.Message}", ex);
            }
            
            // Read the saved file after document is closed
            var mergedBytes = File.ReadAllBytes(tempFile);
            var outputFileName = BuildOutputFileName(metadataValues);
            
            // Validate document size and basic structure (DOCX files start with PK - ZIP signature)
            if (mergedBytes.Length < 4 || mergedBytes[0] != 0x50 || mergedBytes[1] != 0x4B)
            {
                throw new InvalidOperationException($"Invalid document format. Expected DOCX (ZIP) format. Document size: {mergedBytes.Length} bytes");
            }
            
            Console.WriteLine($"Document size: {mergedBytes.Length} bytes");
            
            return new MergeResult(mergedBytes, outputFileName, effectiveVersion, sectionSummary, changeDescription, currentFingerprints, currentTitles);
        }
        finally
        {
            // Clean up temporary file
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
            
            rawMemoryStream?.Dispose();
        }
    }

    /// <summary>
    /// Computes the effective VERSION_NUMBER for the current merge run.
    ///
    /// Rules:
    /// - If there is no previousVersion, keep whatever was detected from the raw
    ///   document (falling back to 1.0 via MetadataExtractor).
    /// - If both previousVersion and current metadata version parse as X.Y, then:
    ///     * If the current metadata version is greater than the previous version,
    ///       prefer the current metadata (author explicitly bumped it).
    ///     * Otherwise, auto-bump the minor component of the previous version.
    /// - If only one parses, prefer the parsed one (bumping previous when only it parses).
    /// - If neither parses, fall back to 1.0.
    ///
    /// The chosen version is written back into metadata["VERSION_NUMBER"] and returned.
    /// </summary>
    private static string ComputeEffectiveVersion(IDictionary<string, string> metadata, string? previousVersion)
    {
        if (!metadata.TryGetValue("VERSION_NUMBER", out var currentRaw) || string.IsNullOrWhiteSpace(currentRaw))
        {
            currentRaw = "1.0";
        }

        static bool TryParseVersion(string? input, out int major, out int minor)
        {
            major = 0;
            minor = 0;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var parts = input.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !int.TryParse(parts[0], out major))
            {
                return false;
            }

            if (parts.Length > 1 && int.TryParse(parts[1], out var parsedMinor))
            {
                minor = parsedMinor;
            }

            return true;
        }

        var hasPrev = TryParseVersion(previousVersion, out var prevMajor, out var prevMinor);
        var hasCurr = TryParseVersion(currentRaw, out var currMajor, out var currMinor);

        string chosen;
        if (!hasPrev && !hasCurr)
        {
            chosen = "1.0";
        }
        else if (!hasPrev && hasCurr)
        {
            chosen = $"{currMajor}.{currMinor}";
        }
        else if (hasPrev && !hasCurr)
        {
            chosen = $"{prevMajor}.{prevMinor + 1}";
        }
        else
        {
            // Both parsed; honor explicit bumps in the raw metadata if they move
            // the version forward, otherwise auto-bump the previous version.
            if (currMajor > prevMajor || (currMajor == prevMajor && currMinor > prevMinor))
            {
                chosen = $"{currMajor}.{currMinor}";
            }
            else
            {
                chosen = $"{prevMajor}.{prevMinor + 1}";
            }
        }

        metadata["VERSION_NUMBER"] = chosen;
        return chosen;
    }

    private static string ComputeNextVersionFromHistory(IReadOnlyList<RunHistoryEntry> history)
    {
        static bool TryParseVersion(string? input, out int major, out int minor)
        {
            major = 0;
            minor = 0;
            if (string.IsNullOrWhiteSpace(input)) return false;
            var parts = input.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return false;
            return int.TryParse(parts[0], out major) && int.TryParse(parts[1], out minor);
        }

        // Prefer max parsed version; fallback to (1, count-1).
        int maxMajor = 1, maxMinor = 0;
        var any = false;
        foreach (var r in history.Where(h => h != null))
        {
            if (TryParseVersion(r.Version, out var maj, out var min))
            {
                if (!any || maj > maxMajor || (maj == maxMajor && min > maxMinor))
                {
                    maxMajor = maj;
                    maxMinor = min;
                    any = true;
                }
            }
        }

        if (any)
        {
            return $"{maxMajor}.{maxMinor + 1}";
        }

        var count = history.Count(h => h != null);
        if (count <= 0) return "1.0";
        return $"1.{Math.Max(0, count)}";
    }

    private static string BuildChangeDescription(string sectionSummary, string? rawUiVersion, DateTimeOffset? rawLastModifiedUtc, string? diffSummary)
    {
        // Fallback when we don't have diff information.
        // Keep it short and table-friendly.
        if (string.IsNullOrWhiteSpace(sectionSummary) || sectionSummary.Equals("Content", StringComparison.OrdinalIgnoreCase))
        {
            return "Updated content";
        }

        return $"Updated {sectionSummary} section";
    }

    private static (Dictionary<string, string> Fingerprints, Dictionary<string, string> Titles) ComputeSectionFingerprintsAndTitles(IList<RawSection> sections)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in sections)
        {
            var key = NormalizeHeadingText(s.HeadingText);
            if (string.IsNullOrWhiteSpace(key))
            {
                key = $"section_{s.Key}";
            }

            var displayTitle = string.IsNullOrWhiteSpace(s.HeadingText) ? key : s.HeadingText.Trim();
            titles[key] = displayTitle;

            var text = string.Concat(s.Elements.SelectMany(e => e.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>()).Select(t => t.Text ?? string.Empty));
            text = NormalizeContentText(text);

            dict[key] = Sha256Hex(text);
        }
        return (dict, titles);
    }

    private static string NormalizeContentText(string text) =>
        string.Join(' ', (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string Sha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input ?? string.Empty);
        var hash = SHA256.HashData(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static (string? ChangeDescription, string? SectionSummary) BuildHumanChangeSummary(
        IReadOnlyDictionary<string, string>? previous,
        IReadOnlyDictionary<string, string>? previousTitles,
        IReadOnlyDictionary<string, string> current,
        IReadOnlyDictionary<string, string> currentTitles)
    {
        if (previous == null || previous.Count == 0)
        {
            return (null, null);
        }

        var numberToTextCurrent = BuildNumberToTextMap(currentTitles);
        var numberToTextPrevious = previousTitles != null ? BuildNumberToTextMap(previousTitles) : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // IMPORTANT:
        // Fingerprint keys have evolved over time (previous versions normalized headings differently).
        // To keep diffs stable across runs, match sections using a canonical title key derived from the title,
        // rather than relying on the stored fingerprint key.
        var prevCanon = BuildCanonicalTitleIndex(previous, previousTitles);
        var curCanon = BuildCanonicalTitleIndex(current, currentTitles);

        var added = new List<string>();   // keys from current
        var removed = new List<string>(); // keys from previous
        var updated = new List<string>(); // keys from current

        foreach (var kv in curCanon)
        {
            if (!prevCanon.TryGetValue(kv.Key, out var prevEntry))
            {
                added.Add(kv.Value.Key);
                continue;
            }
            if (!string.Equals(prevEntry.Hash, kv.Value.Hash, StringComparison.OrdinalIgnoreCase))
            {
                updated.Add(kv.Value.Key);
            }
        }

        foreach (var kv in prevCanon)
        {
            if (!curCanon.ContainsKey(kv.Key))
            {
                removed.Add(kv.Value.Key);
            }
        }

        string ResolveTitle(string key, bool fromCurrent)
        {
            string? rawTitle = null;
            if (fromCurrent && currentTitles.TryGetValue(key, out var ct) && !string.IsNullOrWhiteSpace(ct)) rawTitle = ct;
            else if (!fromCurrent && previousTitles != null && previousTitles.TryGetValue(key, out var pt) && !string.IsNullOrWhiteSpace(pt)) rawTitle = pt;
            else if (currentTitles.TryGetValue(key, out var ct2) && !string.IsNullOrWhiteSpace(ct2)) rawTitle = ct2;
            else if (previousTitles != null && previousTitles.TryGetValue(key, out var pt2) && !string.IsNullOrWhiteSpace(pt2)) rawTitle = pt2;

            if (string.IsNullOrWhiteSpace(rawTitle)) return key;

            var map = fromCurrent ? numberToTextCurrent : (numberToTextPrevious.Count > 0 ? numberToTextPrevious : numberToTextCurrent);
            return BuildHeadingPath(rawTitle!, map) ?? rawTitle!;
        }

        static List<string> FilterMostSpecificPaths(List<string> items)
        {
            // If we have both "A" and "A - B", keep only the most specific ("A - B").
            // This removes noisy parent headings when a child heading is also present.
            var normalized = items
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            bool IsPrefix(string a, string b)
            {
                if (a.Equals(b, StringComparison.OrdinalIgnoreCase)) return false;
                if (!b.StartsWith(a, StringComparison.OrdinalIgnoreCase)) return false;
                // Must be a proper hierarchy delimiter.
                return b.Length > a.Length && b.Substring(a.Length).StartsWith(" - ", StringComparison.Ordinal);
            }

            return normalized
                .Where(a => !normalized.Any(b => IsPrefix(a, b)))
                .ToList();
        }

        string FormatList(string label, List<string> items, bool fromCurrent)
        {
            if (items.Count == 0) return string.Empty;
            var resolved = items.Select(k => ResolveTitle(k, fromCurrent)).ToList();
            resolved = FilterMostSpecificPaths(resolved);
            var take = resolved.Take(3).ToList();
            var suffix = resolved.Count > take.Count ? $" (+{resolved.Count - take.Count})" : string.Empty;
            return $"{label}: {string.Join(", ", take)}{suffix}";
        }

        var updatedResolved = FilterMostSpecificPaths(updated.Select(k => ResolveTitle(k, true)).ToList());
        var addedResolved = FilterMostSpecificPaths(added.Select(k => ResolveTitle(k, true)).ToList());
        var removedResolved = FilterMostSpecificPaths(removed.Select(k => ResolveTitle(k, false)).ToList());

        static string LeafOf(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            var parts = path.Split(" - ", StringSplitOptions.None);
            return (parts.Length > 0 ? parts[^1] : path).Trim();
        }

        // Section column: show the full hierarchy path of the primary change.
        var primaryPath = updatedResolved.FirstOrDefault() ?? addedResolved.FirstOrDefault() ?? removedResolved.FirstOrDefault();
        var sectionSummary = string.IsNullOrWhiteSpace(primaryPath) ? "Content" : primaryPath.Trim();

        // Description/Changes column: keep it similar to the example (short, human friendly).
        // Prefer leaf nodes to avoid repeating parents in text.
        var updatedLeafs = updatedResolved.Select(LeafOf).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var addedLeafs = addedResolved.Select(LeafOf).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var removedLeafs = removedResolved.Select(LeafOf).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        string JoinLeafs(List<string> leafs)
        {
            if (leafs.Count == 0) return sectionSummary;
            var take = leafs.Take(2).ToList();
            if (take.Count == 1) return take[0];
            return string.Join(" & ", take);
        }

        string? changeDescription = null;

        if (addedLeafs.Count > 0 && updatedLeafs.Count == 0 && removedLeafs.Count == 0)
        {
            changeDescription = $"Added {JoinLeafs(addedLeafs)}";
        }
        else if (updatedLeafs.Count > 0 && addedLeafs.Count == 0 && removedLeafs.Count == 0)
        {
            // If the leaf equals the section itself, phrase it as "Updated {Section} section"
            var leaf = JoinLeafs(updatedLeafs);
            changeDescription = leaf.Equals(sectionSummary, StringComparison.OrdinalIgnoreCase)
                ? $"Updated {sectionSummary} section"
                : $"Updated {leaf}";
        }
        else if (updatedLeafs.Count > 0 || addedLeafs.Count > 0 || removedLeafs.Count > 0)
        {
            // Mixed changes – keep generic but still anchored.
            changeDescription = $"Revised {sectionSummary} content";
        }

        return (changeDescription, sectionSummary);
    }

    private static readonly Regex HeadingNumberPrefixRegex =
        new(@"^\s*(?<number>\d+(?:\.\d+)*)(?:\.)?\s+(?<text>.+)$", RegexOptions.Compiled);

    private static Dictionary<string, string> BuildNumberToTextMap(IReadOnlyDictionary<string, string> titles)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in titles.Values)
        {
            if (string.IsNullOrWhiteSpace(t)) continue;
            var m = HeadingNumberPrefixRegex.Match(t);
            if (!m.Success) continue;
            var num = m.Groups["number"].Value.Trim();
            var txt = HumanizeHeadingText(m.Groups["text"].Value.Trim());
            if (string.IsNullOrWhiteSpace(num) || string.IsNullOrWhiteSpace(txt)) continue;
            // First win is fine; we just need a stable parent chain.
            if (!map.ContainsKey(num)) map[num] = txt;
        }
        return map;
    }

    private static string? BuildHeadingPath(string title, IReadOnlyDictionary<string, string> numberToText)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var m = HeadingNumberPrefixRegex.Match(title);
        if (!m.Success) return null;

        var num = m.Groups["number"].Value.Trim();
        if (string.IsNullOrWhiteSpace(num)) return null;

        var parts = num.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;

        var path = new List<string>();
        for (var i = 0; i < parts.Length; i++)
        {
            var prefix = string.Join('.', parts.Take(i + 1));
            if (numberToText.TryGetValue(prefix, out var txt) && !string.IsNullOrWhiteSpace(txt))
            {
                path.Add(txt.Trim());
            }
        }

        if (path.Count == 0) return null;
        // User requested format: "Heading - Sub heading1 - Sub heading2"
        return string.Join(" - ", path);
    }

    private static string HumanizeHeadingText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var trimmed = text.Trim();

        // If it's mostly ALL CAPS, convert to Title Case while preserving acronyms in parentheses like "(FSD)".
        var letters = trimmed.Count(char.IsLetter);
        var upper = trimmed.Count(c => char.IsLetter(c) && char.IsUpper(c));
        var ratio = letters == 0 ? 0 : (double)upper / letters;
        if (ratio < 0.85) return trimmed;

        var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>(words.Length);
        foreach (var w in words)
        {
            // Preserve acronyms in parentheses: "(FSD)" "(API)"
            if (w.StartsWith("(", StringComparison.Ordinal) && w.EndsWith(")", StringComparison.Ordinal))
            {
                result.Add(w);
                continue;
            }

            // Preserve all-caps short acronyms like "API", "FSD".
            var wordLetters = w.Count(char.IsLetter);
            var wordUpper = w.Count(c => char.IsLetter(c) && char.IsUpper(c));
            if (wordLetters > 0 && wordUpper == wordLetters && wordLetters <= 4)
            {
                result.Add(w);
                continue;
            }

            var lower = w.ToLowerInvariant();
            result.Add(char.ToUpperInvariant(lower[0]) + lower.Substring(1));
        }

        return string.Join(' ', result);
    }

    private sealed record CanonEntry(string Key, string Hash);

    private static Dictionary<string, CanonEntry> BuildCanonicalTitleIndex(
        IReadOnlyDictionary<string, string> fingerprints,
        IReadOnlyDictionary<string, string>? titles)
    {
        var map = new Dictionary<string, CanonEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in fingerprints)
        {
            var key = kv.Key;
            var hash = kv.Value;
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(hash)) continue;

            string? title = null;
            if (titles != null && titles.TryGetValue(key, out var t) && !string.IsNullOrWhiteSpace(t))
            {
                title = t;
            }

            var canon = CanonicalizeTitleKey(title ?? key);
            if (string.IsNullOrWhiteSpace(canon)) continue;

            // If duplicates collide, keep the first; this is best-effort and should be stable for typical docs.
            if (!map.ContainsKey(canon))
            {
                map[canon] = new CanonEntry(key, hash);
            }
        }
        return map;
    }

    private static string CanonicalizeTitleKey(string titleOrKey)
    {
        if (string.IsNullOrWhiteSpace(titleOrKey)) return string.Empty;
        // Strip leading numbering ("1.1.2 Foo" -> "Foo") so canonical matching survives numbering changes.
        var m = HeadingNumberPrefixRegex.Match(titleOrKey);
        var core = m.Success ? m.Groups["text"].Value : titleOrKey;

        var sb = new StringBuilder(core.Length);
        foreach (var ch in core)
        {
            if (char.IsLetter(ch))
            {
                sb.Append(char.ToUpperInvariant(ch));
            }
        }
        return sb.ToString();
    }

    private void ApplyMetadata(IEnumerable<TokenOccurrence> tokens, IReadOnlyDictionary<TokenOccurrence, RawSection?> matches, IDictionary<string, string> metadata)
    {
        foreach (var token in tokens)
        {
            if (matches.TryGetValue(token, out var matchedSection) && matchedSection != null)
                continue;

            var hasValue = TryGetMetadataValue(token.Token, metadata, out var value);
            if (!hasValue)
            {
                Console.WriteLine($"No metadata found for token '{token.Token}', replacing with empty value");
            }

            if (token.IsInline)
            {
                var normalizedToken = MetadataExtractor.NormalizeKey(token.Token);
                var appendLineBreak = LineBreakTokens.Contains(normalizedToken);
                ReplaceInlineToken(token.PlaceholderParagraph, token.Token, value, appendLineBreak);
            }
            else
            {
                ReplaceStandaloneToken(token.PlaceholderParagraph, token.Token, value);
            }
        }
    }

    private static void ReplaceInlineToken(Paragraph paragraph, string token, string value, bool appendLineBreak = false)
    {
        var search = $"[{token}]";
        foreach (var text in paragraph.Descendants<Text>())
        {
            if (string.IsNullOrEmpty(text.Text))
                continue;

            if (text.Text.Contains(search, StringComparison.Ordinal))
            {
                text.Text = text.Text.Replace(search, value ?? string.Empty, StringComparison.Ordinal);
                text.Space = SpaceProcessingModeValues.Preserve;

                if (appendLineBreak && text.Parent is Run run)
                {
                    run.AppendChild(new Break());
                }
            }
        }
    }

    private static Paragraph? FindTemplateHeadingParagraph(TokenOccurrence token)
    {
        var placeholder = token.PlaceholderParagraph;

        if (ParagraphContainsInlineHeading(placeholder, token.Token))
        {
            return placeholder;
        }

        var previousParagraph = placeholder.PreviousSibling<Paragraph>();
        if (previousParagraph != null && ParagraphMatchesTokenHeading(previousParagraph, token.Token))
        {
            return previousParagraph;
        }

        return null;
    }

    private static bool ParagraphContainsInlineHeading(Paragraph paragraph, string token)
    {
        var text = string.Concat(paragraph.Descendants<Text>().Select(t => t.Text));
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var pattern = $@"^\s*\d+(?:\.\d+)*\.?\s*\[{Regex.Escape(token)}\]\s*$";
        return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);
    }

    private static bool ParagraphMatchesTokenHeading(Paragraph paragraph, string token)
    {
        var paragraphText = string.Concat(paragraph.Descendants<Text>().Select(t => t.Text));
        var normalizedParagraph = NormalizeHeadingText(paragraphText);
        var normalizedToken = NormalizeHeadingText(token);

        return normalizedParagraph.Length > 0 &&
               normalizedToken.Length > 0 &&
               normalizedParagraph.Contains(normalizedToken, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeHeadingText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            // Keep digits too so "1.1 Project" and "1.2 Project" don't collide in diff keys.
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToUpperInvariant(ch));
            }
        }
        return builder.ToString();
    }

    private static RawSection CloneSection(RawSection source)
    {
        var clonedElements = source.Elements.Select(el => el.CloneNode(true)).ToList();
        return new RawSection(source.Key, source.HeadingText, clonedElements);
    }

    private static void ReplaceStandaloneToken(Paragraph paragraph, string token, string value)
    {
        var target = $"[{token}]";
        var texts = paragraph.Descendants<Text>().ToList();
        if (texts.Count == 0)
        {
            return;
        }

        var fullText = string.Concat(texts.Select(t => t.Text));
        var startIndex = fullText.IndexOf(target, StringComparison.Ordinal);
        if (startIndex < 0)
        {
            return;
        }

        var endIndex = startIndex + target.Length;
        var cursor = 0;
        var inserted = false;

        foreach (var text in texts)
        {
            var textValue = text.Text ?? string.Empty;
            var textStart = cursor;
            var textEnd = textStart + textValue.Length;
            cursor = textEnd;

            if (textValue.Length == 0)
            {
                continue;
            }

            // No overlap with placeholder
            if (textEnd <= startIndex || textStart >= endIndex)
            {
                continue;
            }

            var withinStart = Math.Max(0, startIndex - textStart);
            var withinEnd = Math.Min(textValue.Length, endIndex - textStart);

            var before = withinStart > 0 ? textValue[..withinStart] : string.Empty;
            var after = withinEnd < textValue.Length ? textValue[withinEnd..] : string.Empty;

            if (!inserted)
            {
                text.Text = before + (value ?? string.Empty) + after;
                text.Space = SpaceProcessingModeValues.Preserve;
                inserted = true;
            }
            else
            {
                text.Text = before + after;
                if (!string.IsNullOrEmpty(text.Text))
                {
                    text.Space = SpaceProcessingModeValues.Preserve;
                }
            }
        }
    }

    private static void RemoveParagraphIfExists(OpenXmlElement paragraph)
    {
        if (paragraph.Parent != null)
        {
            paragraph.Remove();
        }
    }

    private OpenXmlElement InsertSectionPreservingFormatting(WordprocessingDocument templateDoc, WordprocessingDocument rawDoc, OpenXmlElement insertAfter, RawSection section)
    {
        var anchor = insertAfter ?? (OpenXmlElement)templateDoc.MainDocumentPart!.Document.Body!;
        try
        {
            return _altChunkInserter.InsertSectionAsAltChunk(templateDoc, rawDoc, anchor, section);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AltChunk insert failed for section '{section.HeadingText}': {ex.Message}. Falling back to inline insertion.");
            return _sectionInserter.InsertSection(templateDoc, anchor, section);
        }
    }

    private static (Paragraph? Start, Paragraph? End) FindContentRegion(Body body)
    {
        const string startMarker = "[DOCUMENT_CONTENT_STARTS_HERE]";
        const string endMarker = "[DOCUMENT_CONTENT_ENDS_HERE]";

        Paragraph? start = null;
        Paragraph? end = null;

        foreach (var p in body.Descendants<Paragraph>())
        {
            var text = string.Concat(p.Descendants<Text>().Select(t => t.Text ?? string.Empty)).Trim();

            if (start == null && text.Equals(startMarker, StringComparison.OrdinalIgnoreCase))
            {
                start = p;
                continue;
            }

            if (start != null && end == null && text.Equals(endMarker, StringComparison.OrdinalIgnoreCase))
            {
                end = p;
                break;
            }
        }

        return (start, end);
    }

    private static void MoveDisclaimerToDocumentEnd(Body body)
    {
        const string disclaimerText = "This document is confidential and proprietary. Unauthorized distribution is prohibited.";

        var paragraphs = body.Elements<Paragraph>().ToList();
        if (paragraphs.Count == 0)
        {
            return;
        }

        Paragraph? disclaimerPara = null;
        foreach (var p in paragraphs)
        {
            var text = string.Concat(p.Descendants<Text>().Select(t => t.Text ?? string.Empty)).Trim();
            if (string.Equals(text, disclaimerText, StringComparison.OrdinalIgnoreCase))
            {
                disclaimerPara = p;
                break;
            }
        }

        if (disclaimerPara == null)
        {
            return;
        }

        var lastPara = paragraphs.LastOrDefault();
        if (lastPara == null || lastPara == disclaimerPara)
        {
            return;
        }

        // Move the disclaimer to the end of the body.
        disclaimerPara.Remove();
        body.AppendChild(disclaimerPara);
    }

    private static bool TryGetMetadataValue(string token, IDictionary<string, string> metadata, out string value)
    {
        var normalizedToken = MetadataExtractor.NormalizeKey(token);
        if (metadata.TryGetValue(normalizedToken, out var foundValue) && !string.IsNullOrWhiteSpace(foundValue))
        {
            value = foundValue;
            return true;
        }

        if (TokenFallbackResolvers.TryGetValue(normalizedToken, out var resolver))
        {
            var fallback = resolver(metadata);
            if (!string.IsNullOrWhiteSpace(fallback))
            {
                value = fallback!;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static string BuildOutputFileName(IDictionary<string, string> metadata)
    {
        var segments = new List<string>();

        if (metadata.TryGetValue("PROJECT_NAME", out var project) && !string.IsNullOrWhiteSpace(project))
        {
            segments.Add(project.Trim());
        }

        if (metadata.TryGetValue("VERSION_NUMBER", out var version) && !string.IsNullOrWhiteSpace(version))
        {
            segments.Add($"v{version.Trim()}");
        }
        else if (metadata.TryGetValue("DATE", out var date) && !string.IsNullOrWhiteSpace(date))
        {
            segments.Add(date.Trim());
        }

        var baseName = segments.Count > 0 ? string.Join(" - ", segments) : "merged";
        var sanitized = new string(baseName.Select(ch => InvalidFileNameChars.Contains(ch) ? '_' : ch).ToArray());
        return $"{sanitized}.docx";
    }

    private static string? GetMetadataOrNull(IDictionary<string, string> metadata, string key) =>
        metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private void EnsureMetadataTokensPreferMetadata(IDictionary<TokenOccurrence, RawSection?> matches)
    {
        foreach (var entry in matches.Keys.ToList())
        {
            var normalized = MetadataExtractor.NormalizeKey(entry.Token);
            if (ReservedMetadataTokens.Contains(normalized))
            {
                matches[entry] = null;
            }
        }
    }

    private static string? BuildDocumentId(IDictionary<string, string> metadata)
    {
        if (GetMetadataOrNull(metadata, "DOCUMENT_ID") is { } explicitId)
        {
            return explicitId;
        }

        var project = GetMetadataOrNull(metadata, "PROJECT_NAME");
        var version = GetMetadataOrNull(metadata, "VERSION_NUMBER");
        if (!string.IsNullOrWhiteSpace(project) && !string.IsNullOrWhiteSpace(version))
        {
            return $"{project.Trim()} - v{version.Trim()}";
        }

        return project;
    }
}


