using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Packaging;
using DocumentMergeApi.Models;

namespace DocumentMergeApi.Services;

public sealed class RawSectionExtractor
{
    private static readonly Regex HeadingRegex =
        // Supports headings like:
        // - "1. PROJECT OVERVIEW"
        // - "1.1 Project Information"
        // - "2.3.4 Detailed Design"
        new(@"^\s*(?<number>\d+(?:\.\d+)*)(?:\.)?\s+(?<text>.+)$", RegexOptions.Compiled);

    public IList<RawSection> Extract(WordprocessingDocument rawDoc)
    {
        var body = rawDoc.MainDocumentPart!.Document.Body!;
        var sections = new List<RawSection>();
        RawSection? current = null;
        bool createdSyntheticIntro = false;

        foreach (var element in body.Elements())
        {
            if (element is Paragraph paragraph)
            {
                var text = string.Concat(paragraph.Descendants<Text>().Select(t => t.Text));
                var match = HeadingRegex.Match(text);
                if (match.Success)
                {
                    if (current != null)
                    {
                        sections.Add(current);
                    }

                    var numberRaw = match.Groups["number"].Value;
                    var headingText = $"{numberRaw} {match.Groups["text"].Value}".Trim();
                    // Key should remain numeric for legacy matching, but unique for sub-headings:
                    // "1" -> 1, "1.1" -> 11, "1.2.3" -> 123
                    var numericKeyRaw = numberRaw.Replace(".", string.Empty);
                    var numericKey = int.TryParse(numericKeyRaw, out var parsedKey) ? parsedKey : 0;

                    current = new RawSection(
                        Key: numericKey,
                        HeadingText: headingText,
                        Elements: new List<OpenXmlElement>());

                    current.Elements.Add(paragraph.CloneNode(true));
                    continue;
                }

                // If we don't have any section yet and this paragraph is non-empty but
                // not a numbered heading, treat the leading content as an "intro" section
                // so it isn't lost. This covers docs like Alerts where the top content
                // is descriptive text rather than "1. Heading".
                if (current == null && !string.IsNullOrWhiteSpace(text))
                {
                    createdSyntheticIntro = true;
                    current = new RawSection(
                        Key: 0,
                        HeadingText: text.Trim(),
                        Elements: new List<OpenXmlElement>());

                    current.Elements.Add(paragraph.CloneNode(true));
                    continue;
                }
            }

            if (current != null)
            {
                current.Elements.Add(element.CloneNode(true));
            }
        }

        if (current != null)
        {
            sections.Add(current);
        }

        return sections;
    }

    public static bool IsHeading(string text) =>
        !string.IsNullOrWhiteSpace(text) && HeadingRegex.IsMatch(text);
}


