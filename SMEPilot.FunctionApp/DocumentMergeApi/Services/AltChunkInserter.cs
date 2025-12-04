using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Vml;
using DocumentMergeApi.Models;

namespace DocumentMergeApi.Services;

public sealed class AltChunkInserter
{
    public AltChunk InsertSectionAsAltChunk(WordprocessingDocument templateDoc, WordprocessingDocument rawDoc, OpenXmlElement insertAfter, RawSection section)
    {
        using var sectionDocStream = BuildSectionDocx(rawDoc, section);

        var mainPart = templateDoc.MainDocumentPart!;
        var part = mainPart.AddAlternativeFormatImportPart(AlternativeFormatImportPartType.WordprocessingML);
        sectionDocStream.Position = 0;
        part.FeedData(sectionDocStream);

        var altChunkId = mainPart.GetIdOfPart(part);
        var altChunk = new AltChunk { Id = altChunkId };

        var body = mainPart.Document.Body!;
        
        // If insertAfter is the body itself, append
        if (insertAfter == body)
        {
            body.Append(altChunk);
        }
        else
        {
            // Find the parent of insertAfter
            var parent = insertAfter.Parent;
            
            // If insertAfter is not a direct child of body, find the body child that contains it
            if (parent != body)
            {
                // Find the body child that contains insertAfter
                var bodyChild = body.Elements().FirstOrDefault(e => e == insertAfter || e.Descendants().Contains(insertAfter));
                if (bodyChild != null)
                {
                    // Insert after the containing element
                    body.InsertAfter(altChunk, bodyChild);
                }
                else
                {
                    // Fallback: append to body
                    body.Append(altChunk);
                }
            }
            else
            {
                // Direct child of body - safe to insert after
                body.InsertAfter(altChunk, insertAfter);
            }
        }
        
        return altChunk;
    }

    private MemoryStream BuildSectionDocx(WordprocessingDocument rawDoc, RawSection section)
    {
        var ms = new MemoryStream();
        WordprocessingDocument? pkg = null;
        try
        {
            pkg = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true);
            var main = pkg.AddMainDocumentPart();
            main.Document = new Document(new Body());
            var body = main.Document.Body!;
            CopySupportingParts(rawDoc, main);
            EnsureDefaultStyle(main);

            var clonedElements = new List<OpenXmlElement>();
            foreach (var el in section.Elements)
            {
                var clone = el.CloneNode(true);
                body.Append(clone);
                clonedElements.Add(clone);
            }

            CopyMediaParts(clonedElements, rawDoc, main);
            
            // Save the document part
            main.Document.Save();
        }
        finally
        {
            pkg?.Dispose();
        }
        
        // Reset stream position after closing the document
        ms.Position = 0;
        return ms;
    }

    private static void CopySupportingParts(WordprocessingDocument sourceDoc, MainDocumentPart targetMainPart)
    {
        var sourceMain = sourceDoc.MainDocumentPart;
        if (sourceMain == null)
        {
            return;
        }

        CopyStyles(sourceMain.StyleDefinitionsPart, targetMainPart);
        CopyFonts(sourceMain.FontTablePart, targetMainPart);
        CopyNumbering(sourceMain.NumberingDefinitionsPart, targetMainPart);
        CopyTheme(sourceMain.ThemePart, targetMainPart);
    }

    private static void CopyStyles(StyleDefinitionsPart? source, MainDocumentPart target)
    {
        if (source?.Styles == null)
        {
            return;
        }

        var dest = target.StyleDefinitionsPart ?? target.AddNewPart<StyleDefinitionsPart>();
        dest.Styles = (Styles)source.Styles.CloneNode(true);
    }

    private static void CopyFonts(FontTablePart? source, MainDocumentPart target)
    {
        if (source?.Fonts == null)
        {
            return;
        }

        var dest = target.FontTablePart ?? target.AddNewPart<FontTablePart>();
        dest.Fonts = (DocumentFormat.OpenXml.Wordprocessing.Fonts)source.Fonts.CloneNode(true);
    }

    private static void CopyNumbering(NumberingDefinitionsPart? source, MainDocumentPart target)
    {
        if (source?.Numbering == null)
        {
            return;
        }

        var dest = target.NumberingDefinitionsPart ?? target.AddNewPart<NumberingDefinitionsPart>();
        dest.Numbering = (Numbering)source.Numbering.CloneNode(true);
    }

    private static void CopyTheme(ThemePart? source, MainDocumentPart target)
    {
        if (source?.Theme == null)
        {
            return;
        }

        var dest = target.ThemePart ?? target.AddNewPart<ThemePart>();
        dest.Theme = (DocumentFormat.OpenXml.Drawing.Theme)source.Theme.CloneNode(true);
    }

    private static void CopyMediaParts(IEnumerable<OpenXmlElement> clonedElements, WordprocessingDocument sourceDoc, MainDocumentPart targetMainPart)
    {
        var sourceMain = sourceDoc.MainDocumentPart;
        if (sourceMain == null)
        {
            return;
        }

        var relIds = CollectMediaRelationshipIds(clonedElements);
        if (relIds.Count == 0)
        {
            return;
        }

        var relMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var relId in relIds)
        {
            OpenXmlPart sourcePart;
            try
            {
                sourcePart = sourceMain.GetPartById(relId);
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }

            var newPart = targetMainPart.AddPart(sourcePart);
            var newRelId = targetMainPart.GetIdOfPart(newPart);
            relMap[relId] = newRelId;
        }

        UpdateMediaRelationshipIds(clonedElements, relMap);
    }

    private static HashSet<string> CollectMediaRelationshipIds(IEnumerable<OpenXmlElement> elements)
    {
        var relIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var blip in elements.SelectMany(e => e.Descendants<DocumentFormat.OpenXml.Drawing.Blip>()))
        {
            var id = blip.Embed?.Value;
            if (!string.IsNullOrWhiteSpace(id))
            {
                relIds.Add(id);
            }
        }

        foreach (var imageData in elements.SelectMany(e => e.Descendants<DocumentFormat.OpenXml.Vml.ImageData>()))
        {
            var id = imageData.RelationshipId?.Value;
            if (!string.IsNullOrWhiteSpace(id))
            {
                relIds.Add(id);
            }
        }

        return relIds;
    }

    private static void UpdateMediaRelationshipIds(IEnumerable<OpenXmlElement> elements, IReadOnlyDictionary<string, string> relMap)
    {
        if (relMap.Count == 0)
        {
            return;
        }

        foreach (var blip in elements.SelectMany(e => e.Descendants<DocumentFormat.OpenXml.Drawing.Blip>()))
        {
            var id = blip.Embed?.Value;
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (relMap.TryGetValue(id, out var newId))
            {
                blip.Embed = newId;
            }
        }

        foreach (var imageData in elements.SelectMany(e => e.Descendants<DocumentFormat.OpenXml.Vml.ImageData>()))
        {
            var id = imageData.RelationshipId?.Value;
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (relMap.TryGetValue(id, out var newId))
            {
                imageData.RelationshipId = newId;
            }
        }
    }

    private static void EnsureDefaultStyle(MainDocumentPart main)
    {
        if (main.StyleDefinitionsPart?.Styles != null)
        {
            return;
        }

        var stylesPart = main.StyleDefinitionsPart ?? main.AddNewPart<StyleDefinitionsPart>();
        stylesPart.Styles = new Styles(
            new Style
            {
                Type = StyleValues.Paragraph,
                Default = true,
                StyleId = "Normal",
                StyleName = new StyleName { Val = "Normal" },
                StyleParagraphProperties = new StyleParagraphProperties
                {
                    SpacingBetweenLines = new SpacingBetweenLines
                    {
                        After = "0",
                        Line = "240",
                        LineRule = LineSpacingRuleValues.Auto
                    }
                },
                StyleRunProperties = new StyleRunProperties
                {
                    FontSize = new FontSize { Val = "22" },
                    FontSizeComplexScript = new FontSizeComplexScript { Val = "22" }
                }
            });
    }
}


