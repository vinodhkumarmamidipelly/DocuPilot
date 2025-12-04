using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentMergeApi.Models;
using System.Text.RegularExpressions;
using System.Text;

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

    public MergeResult Merge(Stream templateStream, Stream rawStream, string? author, string? previousVersion = null)
    {
        // Use temporary file to ensure proper document saving
        var tempFile = Path.Combine(Path.GetTempPath(), $"merge_{Guid.NewGuid():N}.docx");
        var rawMemoryStream = new MemoryStream();
        IDictionary<string, string> metadataValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        string effectiveVersion = "1.0";
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
                // version from the tracking list (previousVersion).
                effectiveVersion = ComputeEffectiveVersion(metadataValues, previousVersion);

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
                var sectionSummary = sectionNames.Count == 0
                    ? "Content"
                    : string.Join(", ", sectionNames);

                _tables.AppendVersionHistory(templateDoc, author, effectiveVersion);
                _tables.AppendChangeLog(templateDoc, author, sectionSummary);

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
            
            return new MergeResult(mergedBytes, outputFileName, effectiveVersion);
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
            if (char.IsLetter(ch))
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


