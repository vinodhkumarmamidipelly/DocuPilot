// SMEPilot.FunctionApp/Helpers/RuleBasedFormatter.cs

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Drawing.Wordprocessing;
using DocumentFormat.OpenXml.Drawing.Pictures;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// RuleBasedFormatter: reads a raw DOCX stream and produces a standardized enriched DOCX (bytes).
    /// Conservative, rule-driven mapping; no AI, no DB.
    /// </summary>
    public class RuleBasedFormatter
    {
        private readonly ILogger<RuleBasedFormatter> _logger;
        private readonly RuleMapping _mapping;

        public RuleBasedFormatter(ILogger<RuleBasedFormatter> logger)
        {
            _logger = logger;
            _mapping = LoadMapping();
        }

        private RuleMapping LoadMapping()
        {
            try
            {
                var basePath = AppContext.BaseDirectory;
                var cfg = System.IO.Path.Combine(basePath, "Config", "RuleMapping.json");
                if (File.Exists(cfg))
                {
                    var json = File.ReadAllText(cfg);
                    return JsonConvert.DeserializeObject<RuleMapping>(json) ?? RuleMapping.Default();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed reading RuleMapping.json, using defaults.");
            }
            return RuleMapping.Default();
        }

        /// <summary>
        /// Main entry: accepts raw stream, returns enriched doc bytes.
        /// </summary>
        public async Task<byte[]> EnrichAsync(Stream rawDocxStream, string originalFileName, string? hintClassification = null)
        {
            if (rawDocxStream == null) throw new ArgumentNullException(nameof(rawDocxStream));
            rawDocxStream.Position = 0;
            string fullText;
            List<(byte[] Bytes, string ContentType)> images;
            try
            {
                // If you have SimpleExtractor with ExtractDocxAsync(Stream) - call that.
                var extractorType = Type.GetType("SMEPilot.FunctionApp.Helpers.SimpleExtractor, SMEPilot.FunctionApp");
                if (extractorType != null)
                {
                    dynamic extractor = Activator.CreateInstance(extractorType);
                    var task = extractor.ExtractDocxAsync(rawDocxStream);
                    var res = await task;
                    fullText = (string)res.Item1;
                    var imgList = (List<byte[]>)res.Item2;
                    images = imgList.Select(b => (b, "image/png")).ToList();
                }
                else
                {
                    (fullText, images) = ExtractTextAndImagesFallback(rawDocxStream);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Extraction failed; using fallback.");
                (fullText, images) = ExtractTextAndImagesFallback(rawDocxStream);
            }
            var blocks = ParseBlocks(fullText);
            var classification = DetermineClassification(blocks, hintClassification);
            var sections = MapToSections(blocks, classification);
            var enriched = BuildDocx(originalFileName, sections, images, classification);
            return enriched;
        }

        #region Extraction Fallback

        private (string, List<(byte[] Bytes,string ContentType)>) ExtractTextAndImagesFallback(Stream s)
        {
            var images = new List<(byte[] , string)>();
            string text = "";
            using (var ms = new MemoryStream())
            {
                s.Position = 0;
                s.CopyTo(ms);
                ms.Position = 0;
                using (var doc = WordprocessingDocument.Open(new MemoryStream(ms.ToArray()), false))
                {
                    var body = doc.MainDocumentPart?.Document?.Body;
                    if (body != null)
                    {
                        text = string.Join("\n", body.Elements<Paragraph>().Select(p => p.InnerText));
                    }
                    if (doc.MainDocumentPart != null)
                    {
                        foreach (var imgPart in doc.MainDocumentPart.ImageParts)
                        {
                            using (var imgStream = imgPart.GetStream())
                            using (var msImg = new MemoryStream())
                            {
                                imgStream.CopyTo(msImg);
                                var ct = imgPart.ContentType ?? "image/png";
                                images.Add((msImg.ToArray(), ct));
                            }
                        }
                    }
                }
            }
            return (text ?? string.Empty, images);
        }

        #endregion

        #region Block parsing & mapping

        private class Block
        {
            public string Text { get; set; }
            public bool IsHeader { get; set; }
            public int Order { get; set; }
        }

        private List<Block> ParseBlocks(string fullText)
        {
            // Normalize and collapse multiple spaces
            fullText = (fullText ?? "").Replace("\r\n", "\n");
            // Split by single newline but keep grouping by double newline detection in later stage
            var lines = fullText
                        .Split(new[] { '\n' }, StringSplitOptions.None)
                        .Select(l => l.Trim())
                        .ToList();
            var blocks = new List<Block>();
            int idx = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                bool isHeader = IsLikelyHeader(line);
                // If current line ends with ':' or is a short uppercase header -> mark header
                blocks.Add(new Block { Text = line, IsHeader = isHeader, Order = idx++ });
            }
            return blocks;
        }

        private bool IsLikelyHeader(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            // numbered header or colon
            if (Regex.IsMatch(line, @"^\d+[\.\)]\s+")) return true;
            if (line.EndsWith(":")) return true;
            // short uppercase-ish lines but ensure not a sentence (contains common verbs)
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= 6 && Regex.IsMatch(line, @"^[A-Z0-9 \-\/\(\)]+$"))
            {
                var lower = line.ToLowerInvariant();
                var verbs = new[] { "is", "are", "has", "have", "do", "does", "will", "can", "should", "includes", "contains" };
                if (!verbs.Any(v => lower.Contains($" {v} "))) return true;
            }
            // explicit keywords often used as headers
            if (Regex.IsMatch(line, @"^(Overview|Summary|Symptoms|Steps|Troubleshoot|Resolution|Implementation|Architecture|Screenshots|Revision|History|Notes|Details)\b", RegexOptions.IgnoreCase)) return true;
            return false;
        }

        private string DetermineClassification(List<Block> blocks, string hint)
        {
            if (!string.IsNullOrEmpty(hint)) return hint;
            var sample = string.Join(" ", blocks.Take(20).Select(b => b.Text)).ToLowerInvariant();
            if (Regex.IsMatch(sample, @"\b(error|exception|stack trace|failed|unable|ticket)\b")) return "Support";
            if (Regex.IsMatch(sample, @"\b(architecture|api|implementation|deployment|config|schema|specification)\b")) return "Technical";
            if (Regex.IsMatch(sample, @"\b(feature|workflow|user story|business|module)\b")) return "Functional";
            return "General";
        }

        private SortedDictionary<int,(string SectionKey,string Content)> MapToSections(List<Block> blocks, string classification)
        {
            var result = new SortedDictionary<int,(string,string)>();
            int order = 0;
            // Title candidate
            var titleBlock = blocks.FirstOrDefault(b => b.IsHeader) ?? blocks.FirstOrDefault();
            var title = titleBlock?.Text ?? "Document";
            result[order++] = ("Title", title);
            // Build a normalized paragraphs list by joining lines that belong together.
            var paragraphs = new List<string>();
            var sb = new System.Text.StringBuilder();
            foreach (var b in blocks)
            {
                if (b == titleBlock) continue;
                if (b.IsHeader)
                {
                    if (sb.Length > 0)
                    {
                        paragraphs.Add(sb.ToString().Trim());
                        sb.Clear();
                    }
                    // push header as its own paragraph
                    paragraphs.Add($"__HEADER__:{b.Text}");
                }
                else
                {
                    if (sb.Length > 0) sb.Append(" ");
                    sb.Append(b.Text);
                }
            }
            if (sb.Length > 0) paragraphs.Add(sb.ToString().Trim());
            // Now process paragraphs: headers create new sections; other text gets appended to current
            string currentSection = null;
            var sectionBuffer = new Dictionary<string, System.Text.StringBuilder>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in paragraphs)
            {
                if (p.StartsWith("__HEADER__:"))
                {
                    var h = p.Substring(10);
                    var key = MapHeader(h, classification);
                    currentSection = key;
                    if (!sectionBuffer.ContainsKey(key)) sectionBuffer[key] = new System.Text.StringBuilder();
                }
                else
                {
                    if (currentSection == null) currentSection = "Overview";
                    if (!sectionBuffer.ContainsKey(currentSection)) sectionBuffer[currentSection] = new System.Text.StringBuilder();
                    var sbv = sectionBuffer[currentSection];
                    if (sbv.Length > 0) sbv.AppendLine();
                    sbv.Append(p);
                }
            }
            // Promote a short summary to Overview if appropriate
            var firstNonTitle = paragraphs.FirstOrDefault(x => !x.StartsWith("__HEADER__"));
            if (!string.IsNullOrWhiteSpace(firstNonTitle) && firstNonTitle.Length <= 400 && !Regex.IsMatch(firstNonTitle, @"(stack trace|exception|http:\/\/|https:\/\/|\{|\[)"))
            {
                if (!sectionBuffer.ContainsKey("Overview")) sectionBuffer["Overview"] = new System.Text.StringBuilder();
                sectionBuffer["Overview"].Insert(0, firstNonTitle);
            }
            // Ensure mandatory sections exist
            foreach (var s in MandatoryForClass(classification))
            {
                if (!sectionBuffer.ContainsKey(s))
                    sectionBuffer[s] = new System.Text.StringBuilder("(No content provided)");
            }
            // Promote first meaningful paragraph to Overview if Overview is placeholder or missing
            var firstContentPara = paragraphs.FirstOrDefault(p => !p.StartsWith("__HEADER__") && !string.IsNullOrWhiteSpace(p));
            if (!string.IsNullOrEmpty(firstContentPara))
            {
                var cleanedFirstPara = Regex.Replace(firstContentPara, @"\s+", " ").Trim();
                if (sectionBuffer.ContainsKey("Overview"))
                {
                    var existing = sectionBuffer["Overview"].ToString().Trim();
                    if (string.IsNullOrEmpty(existing) || existing.Contains("(No content provided)"))
                    {
                        sectionBuffer["Overview"] = new System.Text.StringBuilder(cleanedFirstPara);
                        // remove this paragraph from other sections to avoid duplication
                        // (if it was appended elsewhere, remove identical text occurrences)
                        foreach (var key in sectionBuffer.Keys.ToList())
                        {
                            if (key == "Overview") continue;
                            var sectionSb = sectionBuffer[key];
                            var replaced = sectionSb.ToString().Replace(cleanedFirstPara, "").Trim();
                            sectionBuffer[key] = new System.Text.StringBuilder(replaced);
                        }
                    }
                }
                else
                {
                    sectionBuffer["Overview"] = new System.Text.StringBuilder(cleanedFirstPara);
                }
            }
            // Add to result in stable order: Overview, Details, then others in the order found
            var preferredOrder = new[] { "Overview", "Details", "Architecture", "Implementation", "User Scenarios", "Symptoms", "Steps to Reproduce", "Resolution", "Screenshots", "Revision History" };
            foreach (var pKey in preferredOrder)
            {
                if (sectionBuffer.ContainsKey(pKey))
                {
                    result[order++] = (pKey, sectionBuffer[pKey].ToString().Trim());
                    sectionBuffer.Remove(pKey);
                }
            }
            // remaining sections
            foreach (var kv in sectionBuffer)
            {
                result[order++] = (kv.Key, kv.Value.ToString().Trim());
            }
            return result;
        }

        private string MapHeader(string header, string classification)
        {
            var h = (header ?? "").ToLowerInvariant();
            if (_mapping != null && _mapping.HeaderMappings != null)
            {
                foreach (var kv in _mapping.HeaderMappings)
                {
                    if (h.Contains(kv.Key.ToLowerInvariant())) return kv.Value;
                }
            }
            if (Regex.IsMatch(h, @"\b(symptom|error|exception|stack)\b")) return "Symptoms";
            if (Regex.IsMatch(h, @"\b(step|reproduce|steps|how to)\b")) return "Steps to Reproduce";
            if (Regex.IsMatch(h, @"\b(troubleshoot|troubleshooting|workaround|resolution)\b")) return "Resolution";
            if (Regex.IsMatch(h, @"\b(archit|design|implementation|deployment|config)\b")) return "Architecture";
            if (Regex.IsMatch(h, @"\b(overview|summary)\b")) return "Overview";
            if (Regex.IsMatch(h, @"\b(user|functional|scenario|behavior)\b")) return "User Scenarios";
            if (Regex.IsMatch(h, @"\b(screenshot|image|figure|dashboard)\b")) return "Screenshots";
            return "Details";
        }

        private IEnumerable<string> MandatoryForClass(string classification)
        {
            if (string.Equals(classification, "Support", StringComparison.OrdinalIgnoreCase))
                return new[] { "Symptoms", "Steps to Reproduce", "Resolution", "Screenshots" };
            if (string.Equals(classification, "Technical", StringComparison.OrdinalIgnoreCase))
                return new[] { "Overview", "Architecture", "Implementation", "Screenshots" };
            if (string.Equals(classification, "Functional", StringComparison.OrdinalIgnoreCase))
                return new[] { "Overview", "User Scenarios", "Screenshots" };
            return new[] { "Overview", "Details", "Screenshots" };
        }

        #endregion

        #region Docx build (OpenXML)

        private byte[] BuildDocx(string originalFileName, SortedDictionary<int,(string SectionKey,string Content)> sections, List<(byte[] Bytes,string ContentType)> images, string classification)
        {
            using (var ms = new MemoryStream())
            {
                using (var word = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
                {
                    var main = word.AddMainDocumentPart();
                    main.Document = new Document();
                    var body = new Body();
                    // Title
                    var title = sections.Values.FirstOrDefault(v => v.SectionKey == "Title").Content ?? originalFileName ?? "Document";
                    AppendHeading(body, title, 1);
                    // metadata
                    AppendRun(body, $"Classification: {classification}", italic:true);
                    AppendRun(body, $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC", italic:true);
                    body.Append(new Paragraph(new Run(new Text("")))); // small blank
                    // Add TOC placeholder (no page break)
                    AddTOCField(body);
                    // Add sections
                    foreach (var kv in sections)
                    {
                        var key = kv.Value.SectionKey;
                        var content = kv.Value.Content?.Trim() ?? "";
                        if (key == "Title") continue;
                        if (key == "Summary" && string.IsNullOrWhiteSpace(content)) continue;
                        AppendHeading(body, key, 2);
                        // Smart paragraph splitting: preserve numbered/bulleted lists and explicit list markers
                        var rawSegments = Regex.Split(content, @"\r?\n\r?\n")
                            .Select(p => p.Trim())
                            .Where(p => !string.IsNullOrWhiteSpace(p))
                            .ToList();

                        var paras = new List<string>();
                        foreach(var seg in rawSegments)
                        {
                            // If segment contains numbered list without double newlines, split into lines
                            if (Regex.IsMatch(seg, @"(^|\n)\s*\d+[\.\)]\s+"))
                            {
                                var lines = Regex.Split(seg, @"\r?\n")
                                             .Select(l => l.Trim())
                                             .Where(l => !string.IsNullOrWhiteSpace(l));
                                paras.AddRange(lines);
                            }
                            else if (Regex.IsMatch(seg, @"(^|\n)\s*[-\*\u2022]\s+")) // dash/bullet
                            {
                                var lines = Regex.Split(seg, @"\r?\n")
                                             .Select(l => l.Trim())
                                             .Where(l => !string.IsNullOrWhiteSpace(l));
                                paras.AddRange(lines);
                            }
                            else
                            {
                                paras.Add(seg.Replace("\r\n", " ").Replace("\n", " ").Trim());
                            }
                        }
                        foreach (var p in paras)
                        {
                            AppendParagraph(body, p);
                        }
                        if (string.Equals(key, "Screenshots", StringComparison.OrdinalIgnoreCase) && images != null && images.Any())
                        {
                            int i = 1;
                            foreach(var img in images)
                            {
                                InsertImage(main, body, img.Bytes, $"Screenshot {i++}", img.ContentType);
                            }
                        }
                        // Revision History handled as table if present
                        if (string.Equals(key, "Revision History", StringComparison.OrdinalIgnoreCase))
                        {
                            var entries = ParseRevisionEntries(content);
                            AppendRevisionHistoryTable(body, entries);
                        }
                    }
                    // Trim trailing empty paragraphs that may cause blank page
                    TrimTrailingEmptyParas(body);
                    main.Document.Append(body);
                    main.Document.Save();
                }
                return ms.ToArray();
            }
        }

        private void AppendHeading(Body body, string text, int level)
        {
            var p = new Paragraph();
            var pPr = new ParagraphProperties();
            pPr.ParagraphStyleId = new ParagraphStyleId() { Val = $"Heading{level}" };
            p.Append(pPr);
            p.Append(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
            body.Append(p);
        }

        private void AppendParagraph(Body body, string text)
        {
            var p = new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
            body.Append(p);
        }

        private void AppendRun(Body body, string text, bool italic=false)
        {
            var p = new Paragraph();
            var r = new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            if (italic) r.RunProperties = new RunProperties(new Italic());
            p.Append(r);
            body.Append(p);
        }

        private void AddTOCField(Body body)
        {
            AppendHeading(body, "Table of Contents", 2);

            var fldBegin = new Run(new FieldChar() { FieldCharType = FieldCharValues.Begin });
            var fldCode = new Run(new FieldCode(" TOC \\o \"1-3\" \\h \\z \\u "));
            var fldSep = new Run(new FieldChar() { FieldCharType = FieldCharValues.Separate });
            var placeholderRun = new Run();
            placeholderRun.Append(new RunProperties(new Italic(), new FontSize(){ Val = "18" }));
            placeholderRun.Append(new Text("TOC will appear here when fields are updated in Word."));
            var fldEnd = new Run(new FieldChar() { FieldCharType = FieldCharValues.End });

            var p = new Paragraph();
            p.Append(fldBegin, fldCode, fldSep, placeholderRun, fldEnd);

            body.Append(p);

            // Add a single compact instruction line (no extra blank paragraph)
            var inst = new Paragraph(new Run(new RunProperties(new Italic(), new FontSize(){ Val="16" }), new Text("Select all (Ctrl+A) and press F9 in Word to update the Table of Contents.")));
            // Ensure instruction is not a page break (do not set SectionProperties or PageBreak)
            body.Append(inst);
        }

        private void InsertImage(MainDocumentPart main, Body body, byte[] bytes, string alt, string contentType)
        {
            try
            {
                ImagePartType type = ImagePartType.Png;
                if (!string.IsNullOrEmpty(contentType) && contentType.Contains("jpeg")) type = ImagePartType.Jpeg;
                else if (!string.IsNullOrEmpty(contentType) && contentType.Contains("gif")) type = ImagePartType.Gif;
                var part = main.AddImagePart(type);
                using (var s = new MemoryStream(bytes)) { s.Position = 0; part.FeedData(s); }
                var rId = main.GetIdOfPart(part);
                var element =
                    new Drawing(
                        new DW.Inline(
                            new DW.Extent() { Cx = 4000000L, Cy = 3000000L },
                            new DW.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                            new DW.DocProperties() { Id = (UInt32Value)1U, Name = alt },
                            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks() { NoChangeAspect = true }),
                            new A.Graphic(
                                new A.GraphicData(
                                    new PIC.Picture(
                                        new PIC.NonVisualPictureProperties(
                                            new PIC.NonVisualDrawingProperties() { Id = (UInt32Value)0U, Name = alt },
                                            new PIC.NonVisualPictureDrawingProperties()
                                        ),
                                        new PIC.BlipFill(
                                            new A.Blip() { Embed = rId },
                                            new A.Stretch(new A.FillRectangle())
                                        ),
                                        new PIC.ShapeProperties(
                                            new A.Transform2D(
                                                new A.Offset() { X = 0L, Y = 0L },
                                                new A.Extents() { Cx = 4000000L, Cy = 3000000L }
                                            ),
                                            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }
                                        )
                                    )
                                ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }
                            )
                        ) { DistanceFromTop = (UInt32Value)0U, DistanceFromBottom=(UInt32Value)0U, DistanceFromLeft=(UInt32Value)0U, DistanceFromRight=(UInt32Value)0U }
                    );
                var p = new Paragraph(new Run(element));
                body.Append(p);
                // caption
                var cap = new Paragraph(new Run(new RunProperties(new Italic()), new Text(alt ?? "Screenshot")));
                body.Append(cap);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "InsertImage failed.");
            }
        }

        private void TrimTrailingEmptyParas(Body body)
        {
            var paras = body.Elements<Paragraph>().ToList();
            // Remove trailing empty paragraphs
            for (int i = paras.Count - 1; i >= 0; i--)
            {
                var p = paras[i];
                if (string.IsNullOrWhiteSpace(p.InnerText))
                {
                    p.Remove();
                    continue;
                }
                break;
            }

            // Also remove consecutive empty paragraphs anywhere that might create a blank page after TOC - collapse >1 empties to 0
            var allParas = body.Elements<Paragraph>().ToList();
            int emptyStreak = 0;
            foreach (var p in allParas)
            {
                if (string.IsNullOrWhiteSpace(p.InnerText))
                {
                    emptyStreak++;
                    if (emptyStreak > 1) p.Remove();
                }
                else emptyStreak = 0;
            }
        }

        #endregion

        #region Revision table helpers

        private List<(string Version, string Date, string Author, string Notes)> ParseRevisionEntries(string content)
        {
            var lines = (content ?? "")
                .Split(new[]{'\n','\r'}, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();
            var result = new List<(string,string,string,string)>();
            foreach(var ln in lines)
            {
                string[] parts = null;
                if (ln.Contains("|")) parts = ln.Split('|').Select(p=>p.Trim()).ToArray();
                else if (ln.Contains("\t")) parts = ln.Split('\t').Select(p=>p.Trim()).ToArray();
                else if (ln.Contains(",")) parts = ln.Split(',').Select(p=>p.Trim()).ToArray();
                else
                {
                    // try to prefix with today's date if single text
                    result.Add(("","", "", ln));
                    continue;
                }

                if (parts.Length >= 4) result.Add((parts[0], parts[1], parts[2], parts[3]));
                else if (parts.Length==3) result.Add((parts[0], parts[1], parts[2], ""));
                else if (parts.Length==2) result.Add((parts[0], parts[1], "", ""));
                else result.Add(("","", "", ln));
            }

            // if no explicit version/date found format the single-line as revision with current date/author blank
            if (!result.Any() && !string.IsNullOrWhiteSpace(content))
            {
                result.Add(("", DateTime.UtcNow.ToString("yyyy-MM-dd"), "", content.Trim()));
            }

            return result;
        }

        private void AppendRevisionHistoryTable(Body body, List<(string Version, string Date, string Author, string Notes)> entries)
        {
            if (entries == null || !entries.Any()) return;
            Table table = new Table();
            TableProperties tblProps = new TableProperties(
                new TableBorders(
                    new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                    new BottomBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                    new LeftBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                    new RightBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                    new InsideHorizontalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                    new InsideVerticalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 }
                )
            );
            table.AppendChild(tblProps);
            TableGrid tg = new TableGrid(
                new GridColumn() { Width = "1500" },   // Version
                new GridColumn() { Width = "2000" },   // Date
                new GridColumn() { Width = "3000" },   // Author
                new GridColumn() { Width = "8000" }    // Notes
            );
            table.AppendChild(tg);
            var hdr = new TableRow();
            hdr.Append(
                CreateCell("Version", true),
                CreateCell("Date", true),
                CreateCell("Author", true),
                CreateCell("Notes", true)
            );
            table.Append(hdr);
            foreach(var e in entries)
            {
                var r = new TableRow();
                r.Append(
                    CreateCell(e.Version ?? "", false),
                    CreateCell(e.Date ?? "", false),
                    CreateCell(e.Author ?? "", false),
                    CreateCell(e.Notes ?? "", false)
                );
                table.Append(r);
            }
            body.Append(table);
        }

        private DocumentFormat.OpenXml.Wordprocessing.TableCell CreateCell(string text, bool isHeader)
        {
            var tc = new DocumentFormat.OpenXml.Wordprocessing.TableCell();
            tc.Append(new TableCellProperties(
                new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "2400" },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }
            ));
            var p = new Paragraph();
            var pPr = new ParagraphProperties(new Justification() { Val = JustificationValues.Left });
            p.Append(pPr);
            if (isHeader)
            {
                p.Append(new Run(new RunProperties(new Bold()), new Text(text)));
            }
            else
            {
                p.Append(new Run(new Text(text)));
            }
            tc.Append(p);
            return tc;
        }

        #endregion
    }

    public class RuleMapping
    {
        public Dictionary<string,string> HeaderMappings { get; set; } = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        public static RuleMapping Default()
        {
            return new RuleMapping
            {
                HeaderMappings = new Dictionary<string,string>
                {
                    {"symptom","Symptoms"},
                    {"error","Symptoms"},
                    {"issue","Symptoms"},
                    {"steps","Steps to Reproduce"},
                    {"reproduce","Steps to Reproduce"},
                    {"troubleshoot","Resolution"},
                    {"workaround","Resolution"},
                    {"architecture","Architecture"},
                    {"design","Architecture"},
                    {"overview","Overview"},
                    {"summary","Overview"},
                    {"user","User Scenarios"},
                    {"function","User Scenarios"},
                    {"screenshot","Screenshots"},
                    {"image","Screenshots"}
                }
            };
        }
    }
}

