using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentMergeApi.Models;

namespace DocumentMergeApi.Services;

public sealed class SectionInserter
{
    public OpenXmlElement InsertSection(WordprocessingDocument templateDoc, OpenXmlElement insertAfter, RawSection section)
    {
        if (section.Elements.Count == 0)
        {
            return insertAfter;
        }

        var body = templateDoc.MainDocumentPart!.Document.Body!;
        var anchor = insertAfter;

        foreach (var element in section.Elements)
        {
            if (element is SectionProperties)
            {
                // Skip section properties when inlining sections; Word expects them only at the end
                continue;
            }

            var clone = element.CloneNode(true);

            if (anchor == body)
            {
                body.Append(clone);
            }
            else
            {
                body.InsertAfter(clone, anchor);
            }

            anchor = clone;
        }

        return anchor;
    }
}

