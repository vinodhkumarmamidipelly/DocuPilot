// DocumentEnricherService.cs

// --------------------------

// Purpose: deterministic, rule-based doc enrichment (OpenXML only).

// Updated: 1) Use page-break after each section (so C# output matches Python demo).

//          2) Safe custom properties helpers (no external extension helpers required).

//          3) Insert placeholder image when no images found (optional).

//

// Requirements:

// - Install-Package DocumentFormat.OpenXml

// - Install-Package Newtonsoft.Json



using System;

using System.Collections.Generic;

using System.IO;

using System.Linq;

using System.Xml.Linq;

using DocumentFormat.OpenXml;

using DocumentFormat.OpenXml.Packaging;

using DocumentFormat.OpenXml.Wordprocessing;

using Newtonsoft.Json;

using A = DocumentFormat.OpenXml.Drawing;

using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;

using PIC = DocumentFormat.OpenXml.Drawing.Pictures;



namespace SMEPilot.FunctionApp.Services

{

    public class DocumentEnricherService

    {

        private readonly MappingConfig _config;

        private readonly string _templatePath;

        private const string EnrichedMarkerProperty = "SMEPilot_Enriched";



        public DocumentEnricherService(string mappingJsonPath, string templatePath = null)

        {

            if (string.IsNullOrEmpty(mappingJsonPath) || !File.Exists(mappingJsonPath))

                throw new ArgumentException("mappingJsonPath missing or not found");



            var json = File.ReadAllText(mappingJsonPath);

            _config = JsonConvert.DeserializeObject<MappingConfig>(json) ?? new MappingConfig();

            _templatePath = templatePath;

        }



        /// <summary>

        /// Enrich the input file and write to outputPath. Returns metadata about enrichment.

        /// </summary>

        public EnrichmentResult EnrichFile(string inputPath, string outputPath, string author)

        {

            try

            {

                if (!File.Exists(inputPath)) throw new FileNotFoundException("Input file missing", inputPath);



                // Extract paragraphs and images from input

                var paragraphs = ExtractParagraphs(inputPath);

                var images = ExtractImagePartsAsBytes(inputPath);



                // Create output base by copying template if provided else blank docx

                if (!string.IsNullOrEmpty(_templatePath) && File.Exists(_templatePath))

                    File.Copy(_templatePath, outputPath, true);

                else

                    CreateBlankDocx(outputPath);



                using (var doc = WordprocessingDocument.Open(outputPath, true))

                {

                    // Idempotency: if already enriched, continue and overwrite (we chose overwrite behaviour)

                    if (GetCustomPropertyValue(doc, EnrichedMarkerProperty) == "true")

                    {

                        // Clear existing body and rebuild (overwrite)

                    }



                    var main = doc.MainDocumentPart ?? doc.AddMainDocumentPart();

                    if (main.Document == null) main.Document = new Document(new Body());



                    var body = main.Document.Body;



                    // Clear existing body content but preserve styles part if any

                    body.RemoveAllChildren();



                    // Cover page, TOC

                    AppendCoverPage(body, Path.GetFileName(inputPath), author, DetectDocumentType(paragraphs));

                    InsertTocField(body);



                    // For each configured section, append matching paragraphs

                    foreach (var section in _config.Sections)

                    {

                        // Skip Revision (we add at the end)

                        if (section.Name != null && section.Name.IndexOf("Revision", StringComparison.OrdinalIgnoreCase) >= 0)

                            continue;



                        // Find matching paragraphs

                        var matches = MatchParagraphsToKeywords(paragraphs, section.Keywords);



                        AppendSection(body, section.Name, matches, section.Mandatory);



                        // If Screenshots section, insert images below it (or placeholder)

                        if (section.Name != null && section.Name.IndexOf("Screenshots", StringComparison.OrdinalIgnoreCase) >= 0)

                        {

                            InsertImagesUnderSection(body, main, images);

                        }

                    }



                    // Add Revision table at the end

                    AppendRevisionHistory(body, author, "Rule-based enrichment applied");



                    // Trim trailing empty paragraphs and orphan breaks that can cause empty pages

                    TrimTrailingEmptyParagraphs(body);



                    // Save and set enriched marker property

                    main.Document.Save();

                    SetCustomProperty(doc, EnrichedMarkerProperty, "true");

                }



                return new EnrichmentResult

                {

                    Success = true,

                    DocumentType = "Formatted (RuleBased)",

                    EnrichedPath = outputPath,

                    Status = "Formatted"

                };

            }

            catch (Exception ex)

            {

                return new EnrichmentResult { Success = false, ErrorMessage = ex.ToString() };

            }

        }



        #region Extraction Helpers



        private List<string> ExtractParagraphs(string path)

        {

            var paragraphs = new List<string>();

            using (var doc = WordprocessingDocument.Open(path, false))

            {

                var body = doc.MainDocumentPart?.Document?.Body;

                if (body == null) return paragraphs;



                // Keep paragraph boundaries. Filter out tiny paragraphs (less than 2 chars).

                foreach (var p in body.Elements<Paragraph>())

                {

                    var txt = p.InnerText?.Trim();

                    if (!string.IsNullOrWhiteSpace(txt))

                        paragraphs.Add(txt);

                }

            }

            return paragraphs;

        }



        private List<byte[]> ExtractImagePartsAsBytes(string path)

        {

            var images = new List<byte[]>();

            using (var doc = WordprocessingDocument.Open(path, false))

            {

                var part = doc.MainDocumentPart;

                if (part == null) return images;

                foreach (var imgPart in part.ImageParts)

                {

                    using var s = imgPart.GetStream();

                    using var ms = new MemoryStream();

                    s.CopyTo(ms);

                    images.Add(ms.ToArray());

                }

            }

            return images;

        }



        #endregion



        #region Matching & Classification



        private List<string> MatchParagraphsToKeywords(List<string> paragraphs, List<string> keywords)

        {

            if (keywords == null || keywords.Count == 0) return new List<string>();

            var lowerKeys = keywords.Select(k => k.ToLowerInvariant()).ToList();

            return paragraphs.Where(p => lowerKeys.Any(k => p.ToLowerInvariant().Contains(k))).ToList();

        }



        private string DetectDocumentType(List<string> paragraphs)

        {

            var all = string.Join(" ", paragraphs).ToLowerInvariant();

            int t = _config.TechnicalKeywords.Count(k => all.Contains(k.ToLowerInvariant()));

            int f = _config.FunctionalKeywords.Count(k => all.Contains(k.ToLowerInvariant()));

            int s = _config.SupportKeywords.Count(k => all.Contains(k.ToLowerInvariant()));



            if (t >= f && t >= s && t > 0) return "Technical";

            if (s >= f && s > 0) return "Support";

            if (f > 0) return "Functional";

            return "Generic";

        }



        #endregion



        #region Document Builders (OpenXML)



        private void AppendCoverPage(Body body, string title, string author, string docType)

        {

            // Title

            body.AppendChild(CreateParagraphWithStyle(title ?? "Document", "Title"));

            body.AppendChild(CreateParagraphWithStyle($"Document Type: {docType}", "Subtitle"));

            body.AppendChild(CreateParagraphWithStyle($"Author: {author}", "Subtitle"));

            body.AppendChild(CreateParagraphWithStyle($"Date: {DateTime.UtcNow:yyyy-MM-dd} (UTC)", "Subtitle"));

            // Page break

            body.AppendChild(CreateParagraphWithPageBreak());

        }



        private void InsertTocField(Body body)

        {

            // Insert the TOC field. Word displays it after update; for UI users we put a placeholder visible line too.

            var tocHeading = CreateParagraphWithStyle("Table of Contents", "Heading1");

            body.AppendChild(tocHeading);



            var p = new Paragraph();

            // Begin field

            var begin = new Run(new FieldChar { FieldCharType = FieldCharValues.Begin });

            // Instruction

            var instr = new Run(new FieldCode(" TOC \\o \"1-3\" \\h \\z \\u ") { Space = SpaceProcessingModeValues.Preserve });

            // Separate & placeholder text

            var sep = new Run(new FieldChar { FieldCharType = FieldCharValues.Separate });

            var placeholder = new Run(new Text("TOC will appear here when fields are updated in Word."));

            var end = new Run(new FieldChar { FieldCharType = FieldCharValues.End });



            p.Append(begin);

            p.Append(instr);

            p.Append(sep);

            p.Append(placeholder);

            p.Append(end);



            body.AppendChild(p);



            // Page break after TOC

            body.AppendChild(CreateParagraphWithPageBreak());

        }



        private void AppendSection(Body body, string heading, List<string> paragraphs, bool mandatory)

        {

            if (string.IsNullOrWhiteSpace(heading)) heading = "Section";



            // Heading - ensure it is styled as Heading1 for TOC indexing

            body.AppendChild(CreateParagraphWithStyle(heading, "Heading1"));



            // Append paragraphs (each as its own Paragraph element)

            if (paragraphs != null && paragraphs.Any())

            {

                foreach (var t in paragraphs)

                {

                    body.AppendChild(CreateParagraphPreserveSpace(t));

                }

            }

            else if (mandatory)

            {

                body.AppendChild(CreateParagraphPreserveSpace("_This section is not applicable for this document._"));

            }

            // Insert a page break after every section so the C# output visually matches expected enriched doc

            body.AppendChild(CreateParagraphWithPageBreak());

        }



        private void InsertImagesUnderSection(Body body, MainDocumentPart mainPart, List<byte[]> images)

        {

            // Insert each image as inline with caption and alt text

            if (images == null || images.Count == 0)

            {

                // Insert placeholder image if available in Templates folder

                var placeholderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "Templates", "placeholder.png");

                if (File.Exists(placeholderPath))

                {

                    var bytes = File.ReadAllBytes(placeholderPath);

                    var relId = AddImageToMainPart(mainPart, bytes, out string imagePartId);

                    var drawing = CreateImageInline(relId, "Placeholder Screenshot", 600, 400);

                    body.AppendChild(new Paragraph(new Run(drawing)));

                    body.AppendChild(CreateParagraphWithStyle("Figure 1: Placeholder screenshot", "Caption"));

                }

                else

                {

                    // If no placeholder, insert a note

                    body.AppendChild(CreateParagraphPreserveSpace("_No screenshots were embedded in the raw document._"));

                }

                return;

            }



            int idx = 0;

            foreach (var b in images)

            {

                idx++;

                var relId = AddImageToMainPart(mainPart, b, out string imagePartId);

                var drawing = CreateImageInline(relId, $"Screenshot {idx}", 600, 400); // dimensions in pixels

                var paraImage = new Paragraph(new Run(drawing));

                body.AppendChild(paraImage);



                // Caption

                var cap = CreateParagraphWithStyle($"Figure {idx}: Screenshot", "Caption");

                body.AppendChild(cap);

            }

        }



        private void AppendRevisionHistory(Body body, string author, string summary)

        {

            body.AppendChild(CreateParagraphWithStyle("Revision History", "Heading1"));



            var table = new Table();



            // Table properties (simple grid)

            TableProperties tblProps = new TableProperties(

                new TableBorders(

                    new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 8 },

                    new BottomBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 8 },

                    new LeftBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 8 },

                    new RightBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 8 },

                    new InsideHorizontalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 8 },

                    new InsideVerticalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 8 }

                )

            );

            table.AppendChild(tblProps);



            // Header row

            var headerRow = new TableRow();

            headerRow.Append(

                CreateTableCell("Date", true),

                CreateTableCell("Author", true),

                CreateTableCell("Change Summary", true)

            );

            table.Append(headerRow);



            // Data row

            var dataRow = new TableRow();

            dataRow.Append(

                CreateTableCell(DateTime.UtcNow.ToString("yyyy-MM-dd")),

                CreateTableCell(author),

                CreateTableCell(summary)

            );

            table.Append(dataRow);



            body.AppendChild(table);

        }



        #endregion



        #region OpenXML Low-level Helpers



        private Paragraph CreateParagraphWithStyle(string text, string styleId)

        {

            var p = new Paragraph();

            var pPr = new ParagraphProperties();

            if (!string.IsNullOrWhiteSpace(styleId)) pPr.Append(new ParagraphStyleId() { Val = styleId });

            p.Append(pPr);

            var run = new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

            p.Append(run);

            return p;

        }



        private Paragraph CreateParagraphPreserveSpace(string text)

        {

            var run = new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

            return new Paragraph(run);

        }



        private Paragraph CreateParagraphWithPageBreak()

        {

            var run = new Run();

            run.Append(new Break { Type = BreakValues.Page });

            return new Paragraph(run);

        }



        private TableCell CreateTableCell(string text, bool bold = false)

        {

            var p = new Paragraph();

            var r = new Run();

            if (bold) r.Append(new RunProperties(new Bold()));

            r.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

            p.Append(r);

            return new TableCell(p);

        }



        private void TrimTrailingEmptyParagraphs(Body body)

        {

            // Remove consecutive trailing empty paragraphs or runs to avoid blank final pages

            for (int i = body.ChildElements.Count - 1; i >= 0; i--)

            {

                var child = body.ChildElements[i] as Paragraph;

                if (child == null) break;

                if (string.IsNullOrWhiteSpace(child.InnerText))

                    body.RemoveChild(child);

                else break;

            }

        }



        // Adds image bytes to document and returns relationship ID

        private string AddImageToMainPart(MainDocumentPart mainPart, byte[] bytes, out string partId)

        {

            var imagePart = mainPart.AddImagePart(ImagePartType.Png);

            using (var stream = new MemoryStream(bytes))

            {

                imagePart.FeedData(stream);

            }

            partId = mainPart.GetIdOfPart(imagePart);

            return partId;

        }



        // Create image inline element (uses Drawing and DrawingML). Sizes are in pixels and translated to emu.

        private Drawing CreateImageInline(string relationshipId, string description, int pxWidth, int pxHeight)

        {

            const long emuPerPixel = 9525; // OpenXML conversion

            long cx = pxWidth * emuPerPixel;

            long cy = pxHeight * emuPerPixel;



            var element =

                new Drawing(

                    new DW.Inline(

                        new DW.Extent() { Cx = cx, Cy = cy },

                        new DW.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },

                        new DW.DocProperties() { Id = (UInt32Value)1U, Name = "Picture", Description = description },

                        new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks() { NoChangeAspect = true }),

                        new A.Graphic(

                            new A.GraphicData(

                                new PIC.Picture(

                                    new PIC.NonVisualPictureProperties(

                                        new PIC.NonVisualDrawingProperties() { Id = (UInt32Value)0U, Name = "embedded.png", Description = description },

                                        new PIC.NonVisualPictureDrawingProperties(new A.PictureLocks() { NoChangeAspect = true })),

                                    new PIC.BlipFill(

                                        new A.Blip() { Embed = relationshipId },

                                        new A.Stretch(new A.FillRectangle())),

                                    new PIC.ShapeProperties(new A.Transform2D(new A.Offset() { X = 0, Y = 0 }, new A.Extents() { Cx = cx, Cy = cy }))

                                )

                            ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }

                        )

                    ) { DistanceFromTop = (UInt32Value)0U, DistanceFromBottom = (UInt32Value)0U, DistanceFromLeft = (UInt32Value)0U, DistanceFromRight = (UInt32Value)0U }

                );



            return element;

        }



        private void CreateBlankDocx(string path)

        {

            using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);

            var main = doc.AddMainDocumentPart();

            main.Document = new Document(new Body());

            main.Document.Save();

        }



        #endregion



        #region Custom File Property Helpers (safe implementations)



        private void SetCustomProperty(WordprocessingDocument doc, string propName, string propValue)

        {

            var customPropsPart = doc.CustomFilePropertiesPart ?? doc.AddCustomFilePropertiesPart();

            XNamespace ns = "http://schemas.openxmlformats.org/officeDocument/2006/custom-properties";

            XNamespace vt = "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes";

            var xml = GetXDocumentSafely(customPropsPart);



            var props = xml.Root;

            if (props == null)

            {

                props = new XElement(ns + "Properties");

                xml.Add(props);

            }



            // Remove existing with same name

            var existing = props.Elements().FirstOrDefault(e => e.Attribute("name")?.Value == propName);

            existing?.Remove();



            int pid = 2;

            var pids = props.Elements().Select(x => (int?)x.Attribute("pid")).Where(i => i.HasValue).Select(i => i.Value);

            if (pids.Any()) pid = pids.Max() + 1;



            var newProp = new XElement(ns + "property",

                new XAttribute("fmtid", "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}"),

                new XAttribute("pid", pid),

                new XAttribute("name", propName),

                new XElement(vt + "lpwstr", propValue)

            );

            props.Add(newProp);



            // write back

            using (var writer = new StreamWriter(customPropsPart.GetStream(FileMode.Create, FileAccess.Write)))

            {

                writer.Write(xml.ToString(SaveOptions.DisableFormatting));

            }

        }



        private string GetCustomPropertyValue(WordprocessingDocument doc, string propName)

        {

            var customPropsPart = doc.CustomFilePropertiesPart;

            if (customPropsPart == null) return null;

            var xml = GetXDocumentSafely(customPropsPart);

            var prop = xml.Root?.Elements().FirstOrDefault(e => e.Attribute("name")?.Value == propName);

            if (prop == null) return null;

            XNamespace vt = "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes";

            var valueNode = prop.Element(vt + "lpwstr");

            return valueNode?.Value;

        }



        private XDocument GetXDocumentSafely(OpenXmlPart part)

        {

            try

            {

                using (var stream = part.GetStream())

                using (var sr = new StreamReader(stream))

                {

                    var s = sr.ReadToEnd();

                    if (string.IsNullOrWhiteSpace(s)) return new XDocument();

                    return XDocument.Parse(s);

                }

            }

            catch

            {

                return new XDocument();

            }

        }



        #endregion

    }



    // Models

    public class MappingConfig

    {

        public List<SectionRule> Sections { get; set; } = new List<SectionRule>();

        public List<string> FunctionalKeywords { get; set; } = new List<string>();

        public List<string> TechnicalKeywords { get; set; } = new List<string>();

        public List<string> SupportKeywords { get; set; } = new List<string>();

    }



    public class SectionRule

    {

        public string Name { get; set; } = "";

        public List<string> Keywords { get; set; } = new List<string>();

        public bool Mandatory { get; set; } = false;

    }



    public class EnrichmentResult

    {

        public bool Success { get; set; } = false;

        public string DocumentType { get; set; } = "";

        public string EnrichedPath { get; set; } = "";

        public string Status { get; set; } = "";

        public string ErrorMessage { get; set; } = "";

    }

}
