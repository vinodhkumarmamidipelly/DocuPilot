using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocumentMergeApi.Services;

public sealed class TocManager
{
    private const string TocFieldCode = @" TOC \o ""1-3"" \h \z \u ";
    private const string TocHeadingText = "Table of Contents";
    private const string TocPlaceholderText = "TOC will appear here";

    public bool RawHasToc(WordprocessingDocument rawDoc)
    {
        var body = rawDoc.MainDocumentPart!.Document.Body!;
        // Only treat the document as having a TOC if a real TOC field is present.
        // A plain "Table of Contents" heading without a field should NOT count.
        return body.Descendants<SimpleField>().Any(sf => sf.Instruction?.Value?.Contains("TOC") == true)
            || body.Descendants<FieldChar>().Any(fc => fc.FieldCharType?.Value == FieldCharValues.Begin);
    }

    public Paragraph InsertAutoTocAtPlaceholder(WordprocessingDocument templateDoc, Paragraph placeholder)
    {
        if (placeholder == null)
        {
            throw new ArgumentNullException(nameof(placeholder));
        }

        var tocParagraph = BuildTocParagraph();
        var placeholderText = placeholder.InnerText?.Trim() ?? string.Empty;
        var isTocHeading = placeholderText.Equals(TocHeadingText, StringComparison.OrdinalIgnoreCase);

        if (placeholder.Parent != null)
        {
            if (isTocHeading)
            {
                // Template already contains the "Table of Contents" heading; keep it and insert TOC under it.
                placeholder.InsertAfterSelf(tocParagraph);
            }
            else
            {
                // Template has only a [TOC] token (or a blank placeholder). Insert a visible heading + TOC, then remove placeholder.
                var headingPara = BuildTocHeadingParagraph();
                placeholder.InsertBeforeSelf(headingPara);
                headingPara.InsertAfterSelf(tocParagraph);
                placeholder.Remove();
            }
        }
        else
        {
            // Placeholder already detached; append heading + TOC to document body as a fallback.
            var body = templateDoc.MainDocumentPart!.Document.Body!;
            body.AppendChild(BuildTocHeadingParagraph());
            body.AppendChild(tocParagraph);
        }

        return tocParagraph;
    }

    public void EnsureFieldsUpdateOnOpen(WordprocessingDocument templateDoc)
    {
        var mainPart = templateDoc.MainDocumentPart!;
        var settingsPart = mainPart.DocumentSettingsPart ?? mainPart.AddNewPart<DocumentSettingsPart>();
        settingsPart.Settings ??= new Settings();

        if (!settingsPart.Settings.Elements<UpdateFieldsOnOpen>().Any())
        {
            settingsPart.Settings.AppendChild(new UpdateFieldsOnOpen { Val = true });
        }

        settingsPart.Settings.Save();
    }

    private static Paragraph BuildTocParagraph()
    {
        var begin = new FieldChar { FieldCharType = FieldCharValues.Begin };
        var fieldCode = new FieldCode(TocFieldCode) { Space = SpaceProcessingModeValues.Preserve };
        var separate = new FieldChar { FieldCharType = FieldCharValues.Separate };
        var end = new FieldChar { FieldCharType = FieldCharValues.End };

        var paragraph = new Paragraph(
            new Run(begin),
            new Run(fieldCode),
            new Run(separate),
            // Placeholder visible until fields are updated by Word / Word Online or external service.
            new Run(new Text(TocPlaceholderText)),
            new Run(end));

        return paragraph;
    }

    private static Paragraph BuildTocHeadingParagraph()
    {
        // Keep it simple; if the template provides a style named TOCHeading, Word will apply it.
        return new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "TOCHeading" }),
            new Run(new Text(TocHeadingText) { Space = SpaceProcessingModeValues.Preserve }));
    }
}


