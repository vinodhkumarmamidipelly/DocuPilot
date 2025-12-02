using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocumentMergeApi.Services;

public sealed class TocManager
{
    private const string TocFieldCode = @" TOC \o ""1-3"" \h \z \u ";

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
        if (placeholder.Parent != null)
        {
            placeholder.InsertBeforeSelf(tocParagraph);
            placeholder.Remove();
        }
        else
        {
            // Placeholder already detached; append TOC to document body as a fallback
            templateDoc.MainDocumentPart!.Document.Body!.AppendChild(tocParagraph);
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
            new Run(new Text("Table of Contents")),
            new Run(end));

        return paragraph;
    }
}

