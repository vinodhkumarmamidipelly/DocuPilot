// DocumentEnricher.cs
// Consolidated: DocumentEnricherService + RuleBasedFormatter + HybridEnricher
// Purpose: All document enrichment logic in one file with 3 regions

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SMEPilot.FunctionApp.Models;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace SMEPilot.FunctionApp.Services
{
    /// <summary>
    /// Main document enrichment service - combines all enrichment strategies
    /// </summary>
    public class DocumentEnricher
    {
        private readonly MappingConfig _config;
        private readonly RuleMapping _ruleMapping;
        private readonly string? _templatePath;
        private readonly ILogger<DocumentEnricher>? _logger;
        private const string EnrichedMarkerProperty = "SMEPilot_Enriched";

        public DocumentEnricher(string mappingJsonPath, string? templatePath = null, ILogger<DocumentEnricher>? logger = null)
        {
            if (string.IsNullOrEmpty(mappingJsonPath) || !File.Exists(mappingJsonPath))
                throw new ArgumentException("mappingJsonPath missing or not found");

            var json = File.ReadAllText(mappingJsonPath);
            _config = JsonConvert.DeserializeObject<MappingConfig>(json) ?? new MappingConfig();
            _templatePath = templatePath;
            _logger = logger;
            _ruleMapping = LoadRuleMapping();
        }

        private RuleMapping LoadRuleMapping()
        {
            try
            {
                var basePath = AppContext.BaseDirectory;
                var cfg = Path.Combine(basePath, "Config", "RuleMapping.json");
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

        #region Region 1: DocumentEnricherService Methods (Keyword-based enrichment)

        /// <summary>
        /// Enrich the input file using keyword-based mapping (original DocumentEnricherService logic)
        /// </summary>
        public EnrichmentResult EnrichFile(string inputPath, string outputPath, string author)
        {
            try
            {
                if (!File.Exists(inputPath)) throw new FileNotFoundException("Input file missing", inputPath);

                var paragraphs = ExtractParagraphs(inputPath);
                var images = ExtractImagePartsAsBytes(inputPath);

                if (!string.IsNullOrEmpty(_templatePath) && File.Exists(_templatePath))
                    File.Copy(_templatePath, outputPath, true);
                else
                    CreateBlankDocx(outputPath);

                using (var doc = WordprocessingDocument.Open(outputPath, true))
                {
                    if (GetCustomPropertyValue(doc, EnrichedMarkerProperty) == "true")
                    {
                        // Clear existing body and rebuild (overwrite)
                    }

                    var main = doc.MainDocumentPart ?? doc.AddMainDocumentPart();
                    Document? document = null;
                    try
                    {
                        document = main.Document;
                    }
                    catch (System.Xml.XmlException xmlEx)
                    {
                        _logger?.LogWarning("⚠️ [ENRICHMENT] Output document has corrupted XML, replacing with new document. Error: {Error}", xmlEx.Message);
                        document = null;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning("⚠️ [ENRICHMENT] Error accessing document, replacing with new document. Error: {Error}", ex.Message);
                        document = null;
                    }

                    if (document == null)
                    {
                        document = new Document(new Body());
                        main.Document = document;
                    }

                    var body = document.Body;
                    body.RemoveAllChildren();

                    AppendCoverPage(body, Path.GetFileName(inputPath), author, DetectDocumentType(paragraphs));
                    InsertTocField(body);

                    foreach (var section in _config.Sections)
                    {
                        if (section.Name != null && section.Name.IndexOf("Revision", StringComparison.OrdinalIgnoreCase) >= 0)
                            continue;

                        var matches = MatchParagraphsToKeywords(paragraphs, section.Keywords);
                        AppendSection(body, section.Name, matches, section.Mandatory);

                        if (section.Name != null && section.Name.IndexOf("Screenshots", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            InsertImagesUnderSection(body, main, images);
                        }
                    }

                    AppendRevisionHistory(body, author, "Rule-based enrichment applied");
                    TrimTrailingEmptyParagraphs(body);
                    main.Document.Save();
                    SetCustomProperty(doc, EnrichedMarkerProperty, "true");
                }

                var result = new EnrichmentResult
                {
                    Success = true,
                    DocumentType = "Formatted (RuleBased)",
                    EnrichedPath = outputPath,
                    Status = "Succeeded",
                    OriginalFileName = Path.GetFileName(inputPath),
                    EnrichedFileName = Path.GetFileName(outputPath),
                    Confidence = 0.8, // Default confidence for keyword-based enrichment
                    ProcessedAtUtc = DateTime.UtcNow
                };
                
                // Add diagnostics
                result.Diagnostics["ParagraphCount"] = paragraphs.Count;
                result.Diagnostics["ImageCount"] = images.Count;
                result.Diagnostics["DocumentType"] = result.DocumentType;
                result.Diagnostics["TemplateUsed"] = !string.IsNullOrEmpty(_templatePath) && File.Exists(_templatePath);
                
                // Try to populate section mappings if we can use structured extraction
                try
                {
                    using var inputStream = File.OpenRead(inputPath);
                    var extractor = new Helpers.DocumentExtractor(null);
                    var (paras, tables, extractedImages) = extractor.ExtractDocxStructured(inputStream);
                    
                    if (paras.Any())
                    {
                        var sections = ParseSections(paras, tables, extractedImages);
                        var templateTags = _config.Sections?.Select(s => s.Name).Where(n => !string.IsNullOrEmpty(n)).ToList() ?? new List<string>();
                        
                        double totalConfidence = 0;
                        int mappedCount = 0;
                        
                        // Feedback1: Get autoAcceptThreshold from config (default 0.6)
                        var autoAcceptThreshold = _config.AutoAcceptThreshold > 0 ? _config.AutoAcceptThreshold : 0.6;
                        var lowConfidenceSections = new List<SectionMappingResult>();
                        
                        foreach (var section in sections)
                        {
                            if (templateTags.Any())
                            {
                                var (tag, score, reasons) = MapSectionToTemplate(section, templateTags);
                                if (!string.IsNullOrEmpty(tag) && score > 0)
                                {
                                    var mappingResult = new SectionMappingResult
                                    {
                                        Heading = section.Heading,
                                        TemplateTag = tag,
                                        Score = score,
                                        Reasons = reasons
                                    };
                                    
                                    result.Sections.Add(mappingResult);
                                    totalConfidence += score;
                                    mappedCount++;
                                    
                                    // Feedback1: Track low-confidence mappings for ManualReview
                                    if (score < autoAcceptThreshold)
                                    {
                                        lowConfidenceSections.Add(mappingResult);
                                    }
                                }
                            }
                        }
                        
                        // Feedback1: Add diagnostics with per-section mapping details
                        result.Diagnostics["sectionMapping"] = new Dictionary<string, object>
                        {
                            ["totalSections"] = sections.Count,
                            ["mappedSections"] = mappedCount,
                            ["autoAcceptThreshold"] = autoAcceptThreshold,
                            ["lowConfidenceCount"] = lowConfidenceSections.Count,
                            ["lowConfidenceSections"] = lowConfidenceSections.Select(s => new Dictionary<string, object>
                            {
                                ["heading"] = s.Heading,
                                ["tag"] = s.TemplateTag,
                                ["score"] = s.Score,
                                ["reasons"] = s.Reasons
                            }).ToList()
                        };
                        
                        // Feedback1: Mark as ManualReview if too many low-confidence mappings
                        if (lowConfidenceSections.Count > 0 && result.Status != "Error")
                        {
                            var lowConfidenceRatio = (double)lowConfidenceSections.Count / Math.Max(1, mappedCount);
                            if (lowConfidenceRatio > 0.3) // More than 30% low confidence
                            {
                                result.Status = "ManualReview";
                                _logger?.LogWarning("⚠️ [MAPPING] {LowCount}/{TotalCount} sections have low confidence scores. Marking for ManualReview.", 
                                    lowConfidenceSections.Count, mappedCount);
                            }
                        }
                        
                        // Calculate overall confidence
                        if (mappedCount > 0)
                        {
                            result.Confidence = totalConfidence / mappedCount;
                            result.Diagnostics["MappedSections"] = mappedCount;
                            result.Diagnostics["TotalSections"] = sections.Count;
                            
                            // Set status to ManualReview if confidence is low
                            if (result.Confidence < 0.5)
                            {
                                result.Status = "ManualReview";
                                result.Diagnostics["LowConfidenceReason"] = $"Average confidence {result.Confidence:F2} below threshold 0.5";
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to populate section mappings with structured extraction: {Error}", ex.Message);
                    // Continue with basic result
                }
                
                return result;
            }
            catch (Exception ex)
            {
                return new EnrichmentResult 
                { 
                    Success = false, 
                    ErrorMessage = ex.ToString(),
                    Status = "Error",
                    ProcessedAtUtc = DateTime.UtcNow,
                    Diagnostics = new Dictionary<string, object> { { "Exception", ex.GetType().Name }, { "Message", ex.Message } }
                };
            }
        }

        private List<string> ExtractParagraphs(string path)
        {
            var paragraphs = new List<string>();
            try
            {
                WordprocessingDocument? doc = null;
                Document? document = null;
                bool needsRepair = false;
                
                try
                {
                    doc = WordprocessingDocument.Open(path, false);
                    try
                    {
                        document = doc.MainDocumentPart?.Document;
                    }
                    catch (System.Xml.XmlException xmlEx)
                    {
                        _logger?.LogWarning("⚠️ [EXTRACTION] Document has XML parsing error at line {Line}, position {Position}. Attempting repair...", xmlEx.LineNumber, xmlEx.LinePosition);
                        needsRepair = true;
                        doc.Dispose();
                        doc = null;
                    }
                }
                catch (System.Xml.XmlException)
                {
                    try
                    {
                        var openSettings = new DocumentFormat.OpenXml.Packaging.OpenSettings
                        {
                            AutoSave = false,
                            MarkupCompatibilityProcessSettings = new DocumentFormat.OpenXml.Packaging.MarkupCompatibilityProcessSettings(
                                DocumentFormat.OpenXml.Packaging.MarkupCompatibilityProcessMode.ProcessAllParts,
                                DocumentFormat.OpenXml.FileFormatVersions.Office2016)
                        };
                        doc = WordprocessingDocument.Open(path, false, openSettings);
                        document = doc.MainDocumentPart?.Document;
                    }
                    catch (System.Xml.XmlException xmlEx2)
                    {
                        _logger?.LogWarning("⚠️ [EXTRACTION] Document has XML parsing error even with OpenSettings. Attempting repair...", xmlEx2.LineNumber, xmlEx2.LinePosition);
                        needsRepair = true;
                    }
                }
                
                if (needsRepair && doc == null)
                {
                    try
                    {
                        RepairDocumentXml(path);
                        _logger?.LogInformation("✅ [EXTRACTION] Successfully repaired corrupted document XML");
                        doc = WordprocessingDocument.Open(path, false);
                        document = doc.MainDocumentPart?.Document;
                    }
                    catch (Exception repairEx)
                    {
                        // Feedback1: Return ManualReview status instead of throwing for broken XML
                        _logger?.LogError(repairEx, "❌ [EXTRACTION] Document XML repair failed. Marking for ManualReview.");
                        var errorMessage = $"Document contains corrupted XML and could not be repaired. " +
                            $"The document file may contain unescaped entities (like '&' without 'amp;'). " +
                            $"Repair attempt failed: {repairEx.Message}. " +
                            $"Please try opening the document in Microsoft Word and saving it again - Word will automatically repair the XML.";
                        
                        // Feedback1: Try fallback - extract raw text and mark for ManualReview
                        try
                        {
                            // Attempt to read as plain text from ZIP
                            using (var zipArchive = ZipFile.Open(path, ZipArchiveMode.Read))
                            {
                                var documentEntry = zipArchive.GetEntry("word/document.xml");
                                if (documentEntry != null)
                                {
                                    using var stream = documentEntry.Open();
                                    using var reader = new StreamReader(stream, Encoding.UTF8);
                                    var rawXml = reader.ReadToEnd();
                                    // Extract text between tags (very basic)
                                    var textMatches = System.Text.RegularExpressions.Regex.Matches(rawXml, @">([^<]+)<");
                                    foreach (System.Text.RegularExpressions.Match match in textMatches)
                                    {
                                        var text = match.Groups[1].Value.Trim();
                                        if (text.Length > 2)
                                            paragraphs.Add(text);
                                    }
                                    
                                    _logger?.LogWarning("⚠️ [EXTRACTION] Extracted raw text from corrupted XML. Document marked for ManualReview.");
                                    // Return paragraphs but caller should mark as ManualReview
                                    return paragraphs;
                                }
                            }
                        }
                        catch
                        {
                            // If even fallback fails, throw
                        }
                        
                        throw new InvalidOperationException(errorMessage, repairEx);
                    }
                }
                
                if (doc == null || document == null)
                {
                    throw new InvalidOperationException("Failed to open document - could not access document content.");
                }
                
                using (doc)
                {
                    var body = document?.Body;
                    if (body == null) return paragraphs;

                    try
                    {
                        foreach (var p in body.Elements<Paragraph>())
                        {
                            var txt = p.InnerText?.Trim();
                            if (!string.IsNullOrWhiteSpace(txt))
                                paragraphs.Add(txt);
                        }
                    }
                    catch (System.Xml.XmlException)
                    {
                        var allText = body.InnerText;
                        if (!string.IsNullOrWhiteSpace(allText))
                        {
                            var lines = allText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in lines)
                            {
                                var trimmed = line.Trim();
                                if (trimmed.Length >= 2)
                                    paragraphs.Add(trimmed);
                            }
                        }
                    }
                }
            }
            catch (System.Xml.XmlException xmlEx)
            {
                throw new InvalidOperationException($"Document contains corrupted XML and cannot be processed. The document file may be damaged. Error: {xmlEx.Message}", xmlEx);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to open document for processing. Error: {ex.Message}", ex);
            }

            return paragraphs;
        }

        private List<byte[]> ExtractImagePartsAsBytes(string path)
        {
            var images = new List<byte[]>();
            try
            {
                var openSettings = new DocumentFormat.OpenXml.Packaging.OpenSettings
                {
                    AutoSave = false,
                    MarkupCompatibilityProcessSettings = new DocumentFormat.OpenXml.Packaging.MarkupCompatibilityProcessSettings(
                        DocumentFormat.OpenXml.Packaging.MarkupCompatibilityProcessMode.ProcessAllParts,
                        DocumentFormat.OpenXml.FileFormatVersions.Office2016)
                };
                
                using (var doc = WordprocessingDocument.Open(path, false, openSettings))
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
            }
            catch
            {
                return images;
            }

            return images;
        }

        private void RepairDocumentXml(string path)
        {
            using (var zipArchive = ZipFile.Open(path, ZipArchiveMode.Update))
            {
                var documentEntry = zipArchive.GetEntry("word/document.xml");
                if (documentEntry == null) throw new InvalidOperationException("Could not find word/document.xml in the document archive");

                string xmlContent;
                using (var stream = documentEntry.Open())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    xmlContent = reader.ReadToEnd();
                }
                
                var repairedXml = System.Text.RegularExpressions.Regex.Replace(
                    xmlContent,
                    @"&(?!(?:amp|lt|gt|quot|apos|#\d+|#x[0-9a-fA-F]+);)",
                    "&amp;"
                );

                documentEntry.Delete();
                var newEntry = zipArchive.CreateEntry("word/document.xml");
                using (var writer = new StreamWriter(newEntry.Open(), Encoding.UTF8))
                {
                    writer.Write(repairedXml);
                }
            }
        }

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

        private void AppendCoverPage(Body body, string title, string author, string docType)
        {
            body.AppendChild(CreateParagraphWithStyle(title ?? "Document", "Title"));
            body.AppendChild(CreateParagraphWithStyle($"Document Type: {docType}", "Subtitle"));
            body.AppendChild(CreateParagraphWithStyle($"Author: {author}", "Subtitle"));
            body.AppendChild(CreateParagraphWithStyle($"Date: {DateTime.UtcNow:yyyy-MM-dd} (UTC)", "Subtitle"));
            body.AppendChild(CreateParagraphWithPageBreak());
        }

        private void InsertTocField(Body body)
        {
            var tocHeading = CreateParagraphWithStyle("Table of Contents", "Heading1");
            body.AppendChild(tocHeading);

            var p = new Paragraph();
            var begin = new Run(new FieldChar { FieldCharType = FieldCharValues.Begin });
            var instr = new Run(new FieldCode(" TOC \\o \"1-3\" \\h \\z \\u ") { Space = SpaceProcessingModeValues.Preserve });
            var sep = new Run(new FieldChar { FieldCharType = FieldCharValues.Separate });
            var placeholder = new Run(new Text("TOC will appear here when fields are updated in Word."));
            var end = new Run(new FieldChar { FieldCharType = FieldCharValues.End });

            p.Append(begin);
            p.Append(instr);
            p.Append(sep);
            p.Append(placeholder);
            p.Append(end);

            body.AppendChild(p);
            body.AppendChild(CreateParagraphWithPageBreak());
        }

        private void AppendSection(Body body, string heading, List<string> paragraphs, bool mandatory)
        {
            if (string.IsNullOrWhiteSpace(heading)) heading = "Section";
            body.AppendChild(CreateParagraphWithStyle(heading, "Heading1"));

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
            body.AppendChild(CreateParagraphWithPageBreak());
        }

        private void InsertImagesUnderSection(Body body, MainDocumentPart mainPart, List<byte[]> images)
        {
            if (images == null || images.Count == 0)
            {
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
                    body.AppendChild(CreateParagraphPreserveSpace("_No screenshots were embedded in the raw document._"));
                }
                return;
            }

            int idx = 0;
            foreach (var b in images)
            {
                idx++;
                var relId = AddImageToMainPart(mainPart, b, out string imagePartId);
                var drawing = CreateImageInline(relId, $"Screenshot {idx}", 600, 400);
                var paraImage = new Paragraph(new Run(drawing));
                body.AppendChild(paraImage);
                var cap = CreateParagraphWithStyle($"Figure {idx}: Screenshot", "Caption");
                body.AppendChild(cap);
            }
        }

        private void AppendRevisionHistory(Body body, string author, string summary)
        {
            body.AppendChild(CreateParagraphWithStyle("Revision History", "Heading1"));

            var table = new Table();
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

            var headerRow = new TableRow();
            headerRow.Append(
                CreateTableCell("Date", true),
                CreateTableCell("Author", true),
                CreateTableCell("Change Summary", true)
            );
            table.Append(headerRow);

            var dataRow = new TableRow();
            dataRow.Append(
                CreateTableCell(DateTime.UtcNow.ToString("yyyy-MM-dd")),
                CreateTableCell(author),
                CreateTableCell(summary)
            );
            table.Append(dataRow);

            body.AppendChild(table);
        }

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
            for (int i = body.ChildElements.Count - 1; i >= 0; i--)
            {
                var child = body.ChildElements[i] as Paragraph;
                if (child == null) break;
                if (string.IsNullOrWhiteSpace(child.InnerText))
                    body.RemoveChild(child);
                else break;
            }
        }

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

        private Drawing CreateImageInline(string relationshipId, string description, int pxWidth, int pxHeight)
        {
            const long emuPerPixel = 9525;
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

        #region Region 2: RuleBasedFormatter Methods (Header-based enrichment)

        /// <summary>
        /// Enrich document using header-based detection (original RuleBasedFormatter logic)
        /// </summary>
        public async Task<byte[]> EnrichAsync(Stream rawDocxStream, string originalFileName, string? hintClassification = null)
        {
            if (rawDocxStream == null) throw new ArgumentNullException(nameof(rawDocxStream));
            rawDocxStream.Position = 0;
            string fullText;
            List<(byte[] Bytes, string ContentType)> images;
            try
            {
                // Use fallback extraction method (SimpleExtractor was consolidated into DocumentExtractor,
                // but this method provides direct extraction without external dependencies)
                (fullText, images) = ExtractTextAndImagesFallback(rawDocxStream);
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

        private (string, List<(byte[] Bytes, string ContentType)>) ExtractTextAndImagesFallback(Stream s)
        {
            var images = new List<(byte[], string)>();
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

        private class Block
        {
            public string Text { get; set; }
            public bool IsHeader { get; set; }
            public int Order { get; set; }
        }

        private List<Block> ParseBlocks(string fullText)
        {
            fullText = (fullText ?? "").Replace("\r\n", "\n");
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
                blocks.Add(new Block { Text = line, IsHeader = isHeader, Order = idx++ });
            }
            return blocks;
        }

        private bool IsLikelyHeader(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            if (Regex.IsMatch(line, @"^\d+[\.\)]\s+")) return true;
            if (line.EndsWith(":")) return true;
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= 6 && Regex.IsMatch(line, @"^[A-Z0-9 \-\/\(\)]+$"))
            {
                var lower = line.ToLowerInvariant();
                var verbs = new[] { "is", "are", "has", "have", "do", "does", "will", "can", "should", "includes", "contains" };
                if (!verbs.Any(v => lower.Contains($" {v} "))) return true;
            }
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

        private SortedDictionary<int, (string SectionKey, string Content)> MapToSections(List<Block> blocks, string classification)
        {
            var result = new SortedDictionary<int, (string, string)>();
            int order = 0;
            var titleBlock = blocks.FirstOrDefault(b => b.IsHeader) ?? blocks.FirstOrDefault();
            var title = titleBlock?.Text ?? "Document";
            result[order++] = ("Title", title);
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
                    paragraphs.Add($"__HEADER__:{b.Text}");
                }
                else
                {
                    if (sb.Length > 0) sb.Append(" ");
                    sb.Append(b.Text);
                }
            }
            if (sb.Length > 0) paragraphs.Add(sb.ToString().Trim());
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
            var firstNonTitle = paragraphs.FirstOrDefault(x => !x.StartsWith("__HEADER__"));
            if (!string.IsNullOrWhiteSpace(firstNonTitle) && firstNonTitle.Length <= 400 && !Regex.IsMatch(firstNonTitle, @"(stack trace|exception|http:\/\/|https:\/\/|\{|\[)"))
            {
                if (!sectionBuffer.ContainsKey("Overview")) sectionBuffer["Overview"] = new System.Text.StringBuilder();
                sectionBuffer["Overview"].Insert(0, firstNonTitle);
            }
            foreach (var s in MandatoryForClass(classification))
            {
                if (!sectionBuffer.ContainsKey(s))
                    sectionBuffer[s] = new System.Text.StringBuilder("(No content provided)");
            }
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
            var preferredOrder = new[] { "Overview", "Details", "Architecture", "Implementation", "User Scenarios", "Symptoms", "Steps to Reproduce", "Resolution", "Screenshots", "Revision History" };
            foreach (var pKey in preferredOrder)
            {
                if (sectionBuffer.ContainsKey(pKey))
                {
                    result[order++] = (pKey, sectionBuffer[pKey].ToString().Trim());
                    sectionBuffer.Remove(pKey);
                }
            }
            foreach (var kv in sectionBuffer)
            {
                result[order++] = (kv.Key, kv.Value.ToString().Trim());
            }
            return result;
        }

        private string MapHeader(string header, string classification)
        {
            var h = (header ?? "").ToLowerInvariant();
            if (_ruleMapping != null && _ruleMapping.HeaderMappings != null)
            {
                foreach (var kv in _ruleMapping.HeaderMappings)
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

        private byte[] BuildDocx(string originalFileName, SortedDictionary<int, (string SectionKey, string Content)> sections, List<(byte[] Bytes, string ContentType)> images, string classification)
        {
            using (var ms = new MemoryStream())
            {
                using (var word = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
                {
                    var main = word.AddMainDocumentPart();
                    main.Document = new Document();
                    var body = new Body();
                    var title = sections.Values.FirstOrDefault(v => v.SectionKey == "Title").Content ?? originalFileName ?? "Document";
                    AppendHeading(body, title, 1);
                    AppendRun(body, $"Classification: {classification}", italic: true);
                    AppendRun(body, $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC", italic: true);
                    body.Append(new Paragraph(new Run(new Text(""))));
                    AddTOCField(body);
                    foreach (var kv in sections)
                    {
                        var key = kv.Value.SectionKey;
                        var content = kv.Value.Content?.Trim() ?? "";
                        if (key == "Title") continue;
                        if (key == "Summary" && string.IsNullOrWhiteSpace(content)) continue;
                        AppendHeading(body, key, 2);
                        var rawSegments = Regex.Split(content, @"\r?\n\r?\n")
                            .Select(p => p.Trim())
                            .Where(p => !string.IsNullOrWhiteSpace(p))
                            .ToList();

                        var paras = new List<string>();
                        foreach (var seg in rawSegments)
                        {
                            if (Regex.IsMatch(seg, @"(^|\n)\s*\d+[\.\)]\s+"))
                            {
                                var lines = Regex.Split(seg, @"\r?\n")
                                             .Select(l => l.Trim())
                                             .Where(l => !string.IsNullOrWhiteSpace(l));
                                paras.AddRange(lines);
                            }
                            else if (Regex.IsMatch(seg, @"(^|\n)\s*[-\*\u2022]\s+"))
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
                            foreach (var img in images)
                            {
                                InsertImage(main, body, img.Bytes, $"Screenshot {i++}", img.ContentType);
                            }
                        }
                        if (string.Equals(key, "Revision History", StringComparison.OrdinalIgnoreCase))
                        {
                            var entries = ParseRevisionEntries(content);
                            AppendRevisionHistoryTable(body, entries);
                        }
                    }
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

        private void AppendRun(Body body, string text, bool italic = false)
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
            placeholderRun.Append(new RunProperties(new Italic(), new FontSize() { Val = "18" }));
            placeholderRun.Append(new Text("TOC will appear here when fields are updated in Word."));
            var fldEnd = new Run(new FieldChar() { FieldCharType = FieldCharValues.End });

            var p = new Paragraph();
            p.Append(fldBegin, fldCode, fldSep, placeholderRun, fldEnd);

            body.Append(p);

            var inst = new Paragraph(new Run(new RunProperties(new Italic(), new FontSize() { Val = "16" }), new Text("Select all (Ctrl+A) and press F9 in Word to update the Table of Contents.")));
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
                        ) { DistanceFromTop = (UInt32Value)0U, DistanceFromBottom = (UInt32Value)0U, DistanceFromLeft = (UInt32Value)0U, DistanceFromRight = (UInt32Value)0U }
                    );
                var p = new Paragraph(new Run(element));
                body.Append(p);
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

        private List<(string Version, string Date, string Author, string Notes)> ParseRevisionEntries(string content)
        {
            var lines = (content ?? "")
                .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();
            var result = new List<(string, string, string, string)>();
            foreach (var ln in lines)
            {
                string[] parts = null;
                if (ln.Contains("|")) parts = ln.Split('|').Select(p => p.Trim()).ToArray();
                else if (ln.Contains("\t")) parts = ln.Split('\t').Select(p => p.Trim()).ToArray();
                else if (ln.Contains(",")) parts = ln.Split(',').Select(p => p.Trim()).ToArray();
                else
                {
                    result.Add(("", "", "", ln));
                    continue;
                }

                if (parts.Length >= 4) result.Add((parts[0], parts[1], parts[2], parts[3]));
                else if (parts.Length == 3) result.Add((parts[0], parts[1], parts[2], ""));
                else if (parts.Length == 2) result.Add((parts[0], parts[1], "", ""));
                else result.Add(("", "", "", ln));
            }

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
                new GridColumn() { Width = "1500" },
                new GridColumn() { Width = "2000" },
                new GridColumn() { Width = "3000" },
                new GridColumn() { Width = "8000" }
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
            foreach (var e in entries)
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

        #region Region 3: HybridEnricher Methods (Sectioning and classification)

        /// <summary>
        /// Rule-based sectioning (no AI cost) - detects headings and splits document into sections
        /// </summary>
        public DocumentModel SectionDocument(string text, string? fileName = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new DocumentModel
                {
                    Title = fileName ?? "Document",
                    Sections = new List<Section>(),
                    Images = new List<ImageData>()
                };
            }

            var title = ExtractTitle(text, fileName);
            var sections = ParseSections(text);

            return new DocumentModel
            {
                Title = title,
                Sections = sections,
                Images = new List<ImageData>()
            };
        }

        /// <summary>
        /// Classify document (Functional/Support/Technical) using keyword-based approach
        /// </summary>
        public string ClassifyDocument(string title, string content)
        {
            var text = (title + " " + content).ToLower();

            var technicalKeywords = new[] { "api", "endpoint", "integration", "technical", "developer", "code", "sdk", "rest", "graph", "database", "architecture", "implementation" };
            var technicalScore = technicalKeywords.Count(kw => text.Contains(kw));

            var supportKeywords = new[] { "support", "troubleshooting", "issue", "problem", "error", "help", "faq", "guide", "how to", "fix", "resolve" };
            var supportScore = supportKeywords.Count(kw => text.Contains(kw));

            var functionalKeywords = new[] { "feature", "functionality", "user", "workflow", "process", "business", "requirement", "use case", "scenario" };
            var functionalScore = functionalKeywords.Count(kw => text.Contains(kw));

            if (technicalScore >= supportScore && technicalScore >= functionalScore)
                return "Technical";
            else if (supportScore >= functionalScore)
                return "Support";
            else
                return "Functional";
        }

        private string ExtractTitle(string text, string? fileName)
        {
            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (lines.Length > 0)
            {
                var firstLine = lines[0].Trim();
                if (firstLine.Length < 100 && !firstLine.Contains('.') && char.IsUpper(firstLine[0]))
                {
                    return firstLine;
                }
            }

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return Path.GetFileNameWithoutExtension(fileName);
            }

            return "Document";
        }

        private List<Section> ParseSections(string text)
        {
            var sections = new List<Section>();
            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.None);

            var currentSection = new StringBuilder();
            string? currentHeading = null;
            int sectionId = 1;
            bool isFirstLine = true;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                
                // Skip empty lines but preserve them in section body
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    if (currentSection.Length > 0)
                    {
                        currentSection.AppendLine();
                    }
                    continue;
                }

                if (IsLikelyHeadingHybrid(trimmed, isFirstLine))
                {
                    // Save previous section if exists
                    if (currentSection.Length > 0 && !string.IsNullOrWhiteSpace(currentHeading))
                    {
                        var body = currentSection.ToString().Trim();
                        if (!string.IsNullOrWhiteSpace(body))
                        {
                            sections.Add(CreateSection(sectionId++, currentHeading, body));
                        }
                        currentSection.Clear();
                    }
                    
                    // Clean up markdown heading markers
                    currentHeading = Regex.Replace(trimmed, @"^#{1,6}\s+", "").Trim();
                    isFirstLine = false;
                }
                else
                {
                    // Add to current section body
                    if (currentSection.Length > 0) currentSection.AppendLine();
                    currentSection.Append(trimmed);
                    isFirstLine = false;
                }
            }

            // Add last section
            if (currentSection.Length > 0 || !string.IsNullOrWhiteSpace(currentHeading))
            {
                var body = currentSection.ToString().Trim();
                sections.Add(CreateSection(sectionId++, currentHeading ?? "Content", 
                    !string.IsNullOrWhiteSpace(body) ? body : ""));
            }

            // If no sections found, create one from entire text
            if (sections.Count == 0)
            {
                sections.Add(CreateSection(1, "Content", text.Trim()));
            }

            return sections;
        }

        /// <summary>
        /// Enhanced heading detection with robust heuristics
        /// Includes: markdown headings, numbered headings, short text, no ending punctuation
        /// </summary>
        private bool IsLikelyHeadingHybrid(string line, bool isFirstLine)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) return false;

            // PRIORITY 1: Markdown headings (#, ##, ###, etc.)
            if (Regex.IsMatch(trimmed, @"^#{1,6}\s+")) return true;
            
            // PRIORITY 2: Numbered headings (1., 1.1, 1.1.1, etc.) - improved regex to be more flexible
            // Matches: "1. PROJECT OVERVIEW", "1.1 Project Information", "1.1.1 Details", etc.
            // CRITICAL: Also match lowercase after number (some documents use lowercase)
            if (Regex.IsMatch(trimmed, @"^\d+(\.\d+)*[\.\)]\s+[A-Za-z]")) return true;
            
            // Also match patterns like "1 PROJECT OVERVIEW" (without dot) or "1) PROJECT OVERVIEW"
            if (Regex.IsMatch(trimmed, @"^\d+[\.\)]\s+[A-Za-z]{3,}")) return true;
            
            // PRIORITY 3: First line heuristic - likely title
            if (isFirstLine && trimmed.Length < 100 && !trimmed.Contains('.')) return true;
            
            // Too long is unlikely to be heading
            if (trimmed.Length > 120) return false;
            
            // Punctuation check - headings rarely end with period/comma (colon is OK)
            if (trimmed.EndsWith(".") || trimmed.EndsWith(","))
            {
                return false; // Very unlikely to be heading
            }
            if (trimmed.EndsWith(":"))
            {
                // Colon is OK for headings if short
                if (trimmed.Length < 50) return true;
            }

            // ALL CAPS with reasonable length
            var upperCount = trimmed.Count(c => char.IsUpper(c));
            if (upperCount > trimmed.Length * 0.5 && trimmed.Length > 5 && trimmed.Length < 80) return true;

            // Short text with title case, no punctuation
            if (trimmed.Length < 60 && 
                char.IsUpper(trimmed[0]) && 
                !trimmed.Contains('.') && 
                !trimmed.Contains('!') && 
                !trimmed.Contains('?'))
            {
                var wordCount = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
                if (wordCount <= 8) return true;
            }

            // Common document patterns
            if (Regex.IsMatch(trimmed, @"^(Form\s+[A-Z0-9-]+|([A-Z]-?\d+|[A-Z]{2,4})\s+(Visa|File|Documents?))", RegexOptions.IgnoreCase)) return true;

            // Short title-case text without conjunctions
            if (trimmed.Length < 50 && 
                char.IsUpper(trimmed[0]) && 
                !trimmed.Contains(" and ") && 
                !trimmed.Contains(" or ") &&
                trimmed.Split(' ').Length <= 6)
            {
                return true;
            }

            return false;
        }

        private Section CreateSection(int id, string heading, string body)
        {
            return new Section
            {
                Id = $"s{id}",
                Heading = heading,
                Summary = GenerateSummary(body),
                Body = body
            };
        }

        private string GenerateSummary(string text)
        {
            var sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (sentences.Length > 0)
            {
                var firstSentence = sentences[0].Trim();
                if (firstSentence.Length <= 200)
                {
                    return firstSentence;
                }
            }

            var words = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var summaryWords = words.Take(40).ToArray();
            return string.Join(" ", summaryWords) + (words.Length > 40 ? "..." : "");
        }

        #endregion

        #region Region 4: Feedback1 - Weighted Section Mapping & Structured DTOs

        /// <summary>
        /// Parse sections from structured DTOs using style-first logic (Feedback1)
        /// </summary>
        public List<SectionModel> ParseSections(List<ParagraphDto> paras, List<TableDto> tables, List<ExtractedImage> images)
        {
            var sections = new List<SectionModel>();
            var current = new SectionModel { Heading = "Introduction" };

            int headingCount = 0;
            int checkedCount = 0;
            foreach (var p in paras)
            {
                bool isHeading = false;
                checkedCount++;
                
                // Check style-based heading first
                if (!string.IsNullOrEmpty(p.StyleId) && IsHeadingStyle(p.StyleId))
                {
                    isHeading = true;
                    headingCount++;
                    _logger?.LogDebug("✅ [HEADING] Style-based heading detected: '{Text}' (Style: {Style})", p.Text.Substring(0, Math.Min(50, p.Text.Length)), p.StyleId);
                }
                // Check content-based heading detection
                else if (IsProbableHeading(p.Text))
                {
                    isHeading = true;
                    headingCount++;
                }
                
                // DIAGNOSTIC: Log first 20 paragraphs to see what we're getting
                if (checkedCount <= 20)
                {
                    _logger?.LogDebug("📝 [PARA-{Index}] Text: '{Text}' | Style: {Style} | IsHeading: {IsHeading}", 
                        checkedCount, p.Text.Substring(0, Math.Min(80, p.Text.Length)), p.StyleId ?? "none", isHeading);
                }

                if (isHeading)
                {
                    // Push current section
                    if (current.Paragraphs.Any() || current.Heading != "Introduction")
                        sections.Add(current);
                    current = new SectionModel { Heading = p.Text };
                    continue;
                }

                current.Paragraphs.Add(p.Text);
            }

            // Final push
            if (current.Paragraphs.Any() || current.Heading != "Introduction")
                sections.Add(current);

            // CRITICAL DIAGNOSTIC: Log section parsing results
            _logger?.LogInformation("📊 [SECTION-PARSING] Parsed {SectionCount} sections from {ParaCount} paragraphs. Detected {HeadingCount} headings.", 
                sections.Count, paras.Count, headingCount);
            
            if (sections.Count == 1)
            {
                _logger?.LogWarning("⚠️ [SECTION-PARSING] CRITICAL: Only 1 section detected! This will cause low fill rate. First 10 paragraphs: {Paragraphs}", 
                    string.Join(" | ", paras.Take(10).Select(p => $"'{p.Text.Substring(0, Math.Min(50, p.Text.Length))}'")));
            }

            // Attach tables/images by anchor paragraph index -> map to nearest section
            AttachTablesToSections(sections, tables, paras);
            AttachImagesToSections(sections, images, paras);

            return sections;
        }

        /// <summary>
        /// Check if style ID indicates a heading
        /// </summary>
        private bool IsHeadingStyle(string styleId)
        {
            var sid = styleId?.Replace(" ", "").ToLowerInvariant() ?? "";
            return sid.StartsWith("heading");
        }

        /// <summary>
        /// CRITICAL FIX: Enhanced heading detection - MUST detect numbered headings like "1. PROJECT OVERVIEW", "1.1 Project Information"
        /// This is the root cause of single-section parsing failure
        /// </summary>
        private bool IsProbableHeading(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            
            var trimmed = text.Trim();
            
            // CRITICAL: Numbered headings MUST be detected first (highest priority)
            // Pattern: "1. PROJECT OVERVIEW", "1.1 Project Information", "1.1.1 Details"
            // Handle both with and without space after number/dot
            // IMPROVED: More flexible regex to catch edge cases
            if (Regex.IsMatch(trimmed, @"^\d+(\.\d+)*[\.\)]\s*[A-Za-z]", RegexOptions.IgnoreCase)) 
            {
                _logger?.LogDebug("✅ [HEADING] Detected numbered heading (pattern 1): '{Text}'", trimmed);
                return true;
            }
            
            // Also match patterns like "1 PROJECT OVERVIEW" (without dot) or "1) PROJECT OVERVIEW"
            // IMPROVED: More flexible - allow optional space, handle parentheses
            if (Regex.IsMatch(trimmed, @"^\d+[\.\)]?\s*[A-Za-z]{2,}", RegexOptions.IgnoreCase)) 
            {
                _logger?.LogDebug("✅ [HEADING] Detected numbered heading (pattern 2): '{Text}'", trimmed);
                return true;
            }
            
            // NEW: Match "1 PROJECT OVERVIEW" (number followed by space and text, no punctuation)
            if (Regex.IsMatch(trimmed, @"^\d+\s+[A-Z]{2,}", RegexOptions.IgnoreCase)) 
            {
                _logger?.LogDebug("✅ [HEADING] Detected numbered heading (pattern 3): '{Text}'", trimmed);
                return true;
            }
            
            // Short text (< 6 words) - strong indicator
            var wordCount = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            if (wordCount < 6 && trimmed.Length < 100)
            {
                // Not ending with punctuation (except colon)
                if (!trimmed.EndsWith(".") && !trimmed.EndsWith(",") && !trimmed.EndsWith("!"))
                {
                    // ALL CAPS (but not too long) - e.g., "PROJECT OVERVIEW"
                    if (trimmed.Length < 60 && trimmed.Count(char.IsWhiteSpace) < 5 && trimmed.All(c => !char.IsLower(c))) return true;
                    
                    // Title case with few words - e.g., "Project Information"
                    if (wordCount <= 6 && char.IsUpper(trimmed[0]) && !trimmed.Contains(" and ") && !trimmed.Contains(" or ")) return true;
                }
            }
            
            // Not ending with punctuation (except colon which is OK for headings)
            if (trimmed.Length < 50 && !trimmed.EndsWith(".") && !trimmed.EndsWith(",") && !trimmed.EndsWith("!") && text.Count(c => char.IsWhiteSpace(c)) < 6) return true;
            
            // Common heading patterns
            if (Regex.IsMatch(trimmed, @"^(Overview|Summary|Introduction|Background|Requirements|Architecture|Implementation|Testing|Conclusion|Appendix|References|Table of Contents|Project Overview|Project Information|Target Users|Business Goals|Scope|Functional Requirements|Technical Specifications)\b", RegexOptions.IgnoreCase)) return true;
            
            return false;
        }

        /// <summary>
        /// Attach tables to sections based on anchor paragraph index
        /// </summary>
        private void AttachTablesToSections(List<SectionModel> sections, List<TableDto> tables, List<ParagraphDto> paras)
        {
            foreach (var t in tables)
            {
                var sec = FindSectionByParagraphIndex(sections, paras, t.AnchorParagraphIndex);
                if (sec != null) sec.Tables.Add(t);
            }
        }

        /// <summary>
        /// Attach images to sections based on anchor paragraph index
        /// </summary>
        private void AttachImagesToSections(List<SectionModel> sections, List<ExtractedImage> images, List<ParagraphDto> paras)
        {
            foreach (var img in images)
            {
                var sec = FindSectionByParagraphIndex(sections, paras, img.AnchorParagraphIndex);
                if (sec != null) sec.Images.Add(img);
            }
        }

        /// <summary>
        /// Find section by paragraph index
        /// </summary>
        private SectionModel? FindSectionByParagraphIndex(List<SectionModel> sections, List<ParagraphDto> paras, int paraIndex)
        {
            // Map paraIndex to section by searching section paragraph ranges
            int cursor = 0;
            foreach (var sec in sections)
            {
                int count = sec.Paragraphs.Count;
                if (cursor <= paraIndex && paraIndex < cursor + count) return sec;
                cursor += count;
            }
            // Fallback to last section
            return sections.LastOrDefault();
        }

        /// <summary>
        /// Map section to template tag using weighted scorer (Feedback1)
        /// Returns best match tag with confidence score and reasons
        /// </summary>
        /// <summary>
        /// Feedback1: Map section to template tag with weighted scoring and diagnostics
        /// Returns tag, score, and reasons for mapping decision
        /// </summary>
        public (string tag, double score, List<string> reasons) MapSectionToTemplate(SectionModel sec, List<string> templateTags)
        {
            var bestTag = "";
            double bestScore = 0;
            var bestReasons = new List<string>();
            var allCandidates = new List<(string tag, double score, List<string> reasons)>();

            foreach (var tag in templateTags)
            {
                double score = 0;
                var reasons = new List<string>();

                // Exact heading match (0.5 points)
                if (string.Equals(tag, sec.Heading, StringComparison.OrdinalIgnoreCase))
                {
                    score += 0.5;
                    reasons.Add("ExactHeading");
                }

                // Heading style presence (0.25 points)
                if (IsProbableHeading(sec.Heading))
                {
                    score += 0.25;
                    reasons.Add("HeadingHeuristic");
                }

                // Keyword hits from mapping config (0.05 per hit, max 0.2)
                var keywords = GetKeywordsForTag(tag);
                var hits = keywords.Count(k => 
                    sec.Heading.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0 || 
                    sec.Paragraphs.Any(p => p.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0));
                if (hits > 0)
                {
                    score += Math.Min(0.2, hits * 0.05);
                    reasons.Add($"KeywordHits:{hits}");
                }

                // Table/image presence (0.05 each)
                if (sec.Tables.Any()) { score += 0.05; reasons.Add("HasTable"); }
                if (sec.Images.Any()) { score += 0.05; reasons.Add("HasImage"); }

                // Fuzzy match using Levenshtein distance (0.1 if distance <= 3)
                var lev = LevenshteinDistance(tag.ToLowerInvariant(), sec.Heading.ToLowerInvariant());
                if (lev <= 3 && sec.Heading.Length > 4)
                {
                    score += 0.1;
                    reasons.Add($"FuzzyMatch:{lev}");
                }

                allCandidates.Add((tag, score, reasons));

                if (score > bestScore) 
                { 
                    bestScore = score; 
                    bestTag = tag; 
                    bestReasons = reasons; 
                }
            }

            // Feedback1: Log top 3 candidates for diagnostics
            var topCandidates = allCandidates.OrderByDescending(c => c.score).Take(3).ToList();
            _logger?.LogDebug("🔍 [MAPPING] Section '{Heading}' -> Top candidates: {Candidates}", 
                sec.Heading, 
                string.Join(", ", topCandidates.Select(c => $"{c.tag}({c.score:F2})")));

            return (bestTag, Math.Round(bestScore, 3), bestReasons);
        }

        /// <summary>
        /// Get keywords for a template tag from mapping config
        /// </summary>
        private List<string> GetKeywordsForTag(string tag)
        {
            var keywords = new List<string>();
            
            // Check RuleMapping first
            if (_ruleMapping?.HeaderMappings != null)
            {
                foreach (var kvp in _ruleMapping.HeaderMappings)
                {
                    if (string.Equals(kvp.Value, tag, StringComparison.OrdinalIgnoreCase))
                    {
                        keywords.Add(kvp.Key);
                    }
                }
            }

            // Check MappingConfig sections
            if (_config?.Sections != null)
            {
                var section = _config.Sections.FirstOrDefault(s => 
                    string.Equals(s.Name, tag, StringComparison.OrdinalIgnoreCase));
                if (section != null && section.Keywords != null)
                {
                    keywords.AddRange(section.Keywords);
                }
            }

            return keywords.Distinct().ToList();
        }

        /// <summary>
        /// Calculate Levenshtein distance between two strings (Feedback1)
        /// </summary>
        private int LevenshteinDistance(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return string.IsNullOrEmpty(b) ? 0 : b.Length;
            if (string.IsNullOrEmpty(b)) return a.Length;

            var dp = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) dp[0, j] = j;
            
            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = (a[i - 1] == b[j - 1]) ? 0 : 1;
                    dp[i, j] = Math.Min(
                        Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                        dp[i - 1, j - 1] + cost);
                }
            }
            
            return dp[a.Length, b.Length];
        }

        #endregion
    }
}

