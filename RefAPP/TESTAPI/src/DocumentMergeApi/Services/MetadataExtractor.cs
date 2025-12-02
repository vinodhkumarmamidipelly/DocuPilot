using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocumentMergeApi.Services;

public sealed class MetadataExtractor
{
    // Match multiple "Key: Value" pairs in a single fragment, e.g.:
    // "Version: 1.0Date: December 2024Status: Draft for Review"
    // by stopping each value just before the next "Key:" or end of string.
    private static readonly Regex KeyValueRegex = new(@"(?:^|\s)(?:[-•\u2022–]\s*)?(?<key>[^:：]{1,50}?)\s*[:：]\s*(?<value>.*?)(?=(?:\s(?:[-•\u2022–]\s*)?[^:：]{1,50}?\s*[:：])|$)", RegexOptions.Compiled);
    private static readonly Dictionary<string, string> KeyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["VERSION"] = "VERSION_NUMBER",
        ["VERSION_NO"] = "VERSION_NUMBER",
        ["VERSION_NUMBER"] = "VERSION_NUMBER",
        ["AUTHORS"] = "AUTHOR_NAME",
        ["AUTHOR_S"] = "AUTHOR_NAME",
        ["AUTHOR"] = "AUTHOR_NAME",
        ["REVIEWERS"] = "REVIEWER_NAME",
        ["REVIEWER_S"] = "REVIEWER_NAME",
        ["REVIEWER"] = "REVIEWER_NAME",
        ["APPROVERS"] = "APPROVER_NAME",
        ["APPROVER_S"] = "APPROVER_NAME",
        ["APPROVER"] = "APPROVER_NAME",
        ["PROJECT"] = "PROJECT_NAME",
        ["PROJECT_NAME"] = "PROJECT_NAME",
        ["DOC_NAME"] = "DOCUMENT_NAME",
        ["DOC_TITLE"] = "DOCUMENT_NAME",
        ["DOCUMENT_TITLE"] = "DOCUMENT_NAME",
        ["DOCUMENTNAME"] = "DOCUMENT_NAME",
        ["AUTHER"] = "AUTHOR_NAME"
    };
    private static readonly string[] DocumentTypeKeywords = { "DOCUMENT", "SPECIFICATION", "GUIDE", "MANUAL", "POLICY" };
    private static readonly HashSet<string> KnownMetadataKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "PROJECT_NAME",
        "VERSION_NUMBER",
        "DATE",
        "STATUS",
        "AUTHOR_NAME",
        "REVIEWER_NAME",
        "APPROVER_NAME",
        "DOCUMENT_NAME",
        "DOCUMENT_ID",
        "DOCUMENT_TYPE",
        "CLASSIFICATION",
        "STATUS",
        "PROJECT",
        "PROJECT_DESCRIPTION"
    };

    public IDictionary<string, string> Extract(WordprocessingDocument rawDoc)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var body = rawDoc.MainDocumentPart!.Document.Body!;
        var docTypeCaptured = metadata.ContainsKey("DOCUMENT_TYPE");
        var docNameCaptured = metadata.ContainsKey("DOCUMENT_NAME");
        var encounteredKeyValue = false;

        foreach (var paragraph in body.Elements<Paragraph>())
        {
            // Preserve manual line breaks (<w:br/>) inside a paragraph so that
            // "Version: 1.0" and "Date: December 2024" on separate visual lines
            // are treated as separate fragments instead of concatenated.
            var rawText = GetParagraphTextWithLineBreaks(paragraph);
            if (string.IsNullOrWhiteSpace(rawText))
                continue;

            var fragments = SplitFragments(rawText);
            
            foreach (var fragment in fragments)
            {
                if (string.IsNullOrWhiteSpace(fragment))
                    continue;

                if (RawSectionExtractor.IsHeading(fragment))
                    continue;

                var matches = KeyValueRegex.Matches(fragment);
                if (matches.Count == 0)
                {
                    if (!encounteredKeyValue)
                    {
                        CaptureDocumentMetadata(fragment, metadata, ref docTypeCaptured, ref docNameCaptured);
                    }
                    continue;
                }

                encounteredKeyValue = true;
                foreach (Match match in matches.Cast<Match>())
                {
                    var key = NormalizeKey(match.Groups["key"].Value);
                    if (string.IsNullOrWhiteSpace(key))
                        continue;

                    if (KeyAliases.TryGetValue(key, out var alias))
                    {
                        key = alias;
                    }

                    if (!KnownMetadataKeys.Contains(key))
                        continue;

                    metadata[key] = match.Groups["value"].Value.Trim();
                }
            }
        }

        // 1) Project Name fallback:
        // If PROJECT_NAME was not supplied explicitly (via "Project Name:" or aliases),
        // fall back to the main heading / document name. This satisfies:
        //   - Prefer main heading text when available
        //   - Otherwise use DOCUMENT_NAME (typically first non-empty fragment)
        if (!metadata.TryGetValue("PROJECT_NAME", out var projectName) || string.IsNullOrWhiteSpace(projectName))
        {
            if (metadata.TryGetValue("DOCUMENT_NAME", out var documentName) && !string.IsNullOrWhiteSpace(documentName))
            {
                metadata["PROJECT_NAME"] = documentName;
            }
        }

        // 2) Default version number:
        // If no version was detected from the raw or metadata, default to "1.0"
        // so that version tokens / header cells are never empty.
        if (!metadata.TryGetValue("VERSION_NUMBER", out var version) || string.IsNullOrWhiteSpace(version))
        {
            metadata["VERSION_NUMBER"] = "1.0";
        }

        return metadata;
    }

    private static string GetParagraphTextWithLineBreaks(Paragraph paragraph)
    {
        var builder = new StringBuilder();
        AppendTextWithLineBreaks(paragraph, builder);
        return builder.ToString();
    }

    private static void AppendTextWithLineBreaks(OpenXmlElement element, StringBuilder builder)
    {
        foreach (var child in element.ChildElements)
        {
            switch (child)
            {
                case Break:
                case CarriageReturn:
                    builder.Append('\n');
                    break;
                case Text text:
                    builder.Append(text.Text);
                    break;
                default:
                    AppendTextWithLineBreaks(child, builder);
                    break;
            }
        }
    }

    private static IEnumerable<string> SplitFragments(string text)
    {
        return text
            .Split(new[] { '\r', '\n', '•', '●', '▪', '·' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static void CaptureDocumentMetadata(string fragment, IDictionary<string, string> metadata, ref bool docTypeCaptured, ref bool docNameCaptured)
    {
        var trimmed = fragment.Trim();
        if (string.IsNullOrEmpty(trimmed) || LooksLikeSeparator(trimmed))
        {
            return;
        }

        // Be conservative: only infer DOCUMENT_TYPE when the fragment actually
        // looks like a document type (contains keywords such as DOCUMENT, GUIDE, etc.).
        if (!docTypeCaptured && LooksLikeDocumentType(trimmed))
        {
            metadata["DOCUMENT_TYPE"] = trimmed;
            docTypeCaptured = true;
            return;
        }

        // Use the first non-empty fragment as DOCUMENT_NAME, but do not fall back
        // to also treating arbitrary intro text as DOCUMENT_TYPE. If callers need
        // DOCUMENT_TYPE, they should provide it explicitly (e.g., as a key/value row
        // or from SharePoint list metadata).
        if (!docNameCaptured)
        {
            metadata["DOCUMENT_NAME"] = trimmed;
            docNameCaptured = true;
        }
    }

    private static bool LooksLikeSeparator(string text) =>
        text.All(ch => ch == '_' || ch == '-' || char.IsWhiteSpace(ch));

    private static bool LooksLikeDocumentType(string text)
    {
        var upper = text.ToUpperInvariant();
        return DocumentTypeKeywords.Any(keyword => upper.Contains(keyword, StringComparison.Ordinal));
    }

    public static string NormalizeKey(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var builder = new StringBuilder(input.Length);
        foreach (var ch in input.Trim())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToUpperInvariant(ch));
            }
            else if (ch is ' ' or '-' or '_' or '/')
            {
                builder.Append('_');
            }
        }

        var normalized = builder.ToString().Trim('_');
        while (normalized.Contains("__", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("__", "_", StringComparison.Ordinal);
        }

        return normalized;
    }
}

