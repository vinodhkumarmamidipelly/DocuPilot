// EnrichmentComponents.cs
// Consolidated: OpenXmlExtractor + DefaultNormalizer + RuleBasedClassifier + SectionMapper + OpenXmlTemplateRenderer
// Purpose: All Feedback2 enrichment components in one file with 5 classes

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Newtonsoft.Json;
using SMEPilot.FunctionApp.Interfaces;
using SMEPilot.FunctionApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SMEPilot.FunctionApp.Services
{
    #region Region 1: Content Extraction

    /// <summary>
    /// OpenXML extractor using ContentBlock model
    /// </summary>
    public class OpenXmlExtractor : IContentExtractor
    {
        public IReadOnlyList<ContentBlock> Extract(string docxPath)
        {
            var blocks = new List<ContentBlock>();
            using var doc = WordprocessingDocument.Open(docxPath, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null) return blocks;

            int pos = 0;
            foreach (var element in body.Elements())
            {
                if (element is Paragraph p)
                {
                    var text = p.InnerText ?? string.Empty;
                    var styleId = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                    if (!string.IsNullOrEmpty(styleId) && styleId.StartsWith("Heading", StringComparison.OrdinalIgnoreCase))
                    {
                        int lvl = 1;
                        var digits = new string(styleId.Where(char.IsDigit).ToArray());
                        if (!string.IsNullOrEmpty(digits)) int.TryParse(digits, out lvl);
                        blocks.Add(new ContentBlock
                        {
                            Type = BlockType.Heading,
                            Text = text,
                            HeadingLevel = lvl,
                            Position = pos++
                        });
                    }
                    else
                    {
                        blocks.Add(new ContentBlock
                        {
                            Type = BlockType.Paragraph,
                            Text = text,
                            Position = pos++
                        });
                    }
                }
                else if (element is Table)
                {
                    blocks.Add(new ContentBlock { Type = BlockType.Table, Text = "[TABLE]", Position = pos++ });
                }
                else
                {
                    var txt = element.InnerText ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(txt))
                        blocks.Add(new ContentBlock { Type = BlockType.Paragraph, Text = txt, Position = pos++ });
                }
            }

            // Extract images
            var main = doc.MainDocumentPart;
            if (main != null)
            {
                foreach (var imagePart in main.ImageParts)
                {
                    using var s = imagePart.GetStream();
                    using var ms = new MemoryStream();
                    s.CopyTo(ms);
                    blocks.Add(new ContentBlock { Type = BlockType.Image, ImageBytes = ms.ToArray(), Position = pos++ });
                }
            }

            return blocks;
        }
    }

    #endregion

    #region Region 2: Content Normalization

    /// <summary>
    /// Default normalizer that merges consecutive paragraphs
    /// </summary>
    public class DefaultNormalizer : IContentNormalizer
    {
        public NormalizedDocument Normalize(IReadOnlyList<ContentBlock> blocks)
        {
            var list = blocks?.Where(b => b != null && ((b.Text != null && !string.IsNullOrWhiteSpace(b.Text)) || b.ImageBytes != null)).ToList()
                        ?? new List<ContentBlock>();

            // Merge consecutive paragraphs with no empty lines
            var merged = new List<ContentBlock>();
            for (int i = 0; i < list.Count; i++)
            {
                var current = list[i];
                if (current.Type == BlockType.Paragraph && merged.Count > 0 && merged.Last().Type == BlockType.Paragraph)
                {
                    // Merge paragraph texts to avoid many tiny paras
                    merged.Last().Text = $"{merged.Last().Text}\n{current.Text}";
                }
                else
                {
                    merged.Add(current);
                }
            }

            var normalized = new NormalizedDocument
            {
                Blocks = merged,
                Title = merged.FirstOrDefault(b => b.Type == BlockType.Heading)?.Text ?? string.Empty
            };

            return normalized;
        }
    }

    #endregion

    #region Region 3: Document Classification

    /// <summary>
    /// Rule-based classifier using ContentBlock approach
    /// </summary>
    public class RuleBasedClassifier : IDocumentClassifier
    {
        private readonly string[] technical = new[] { "api", "architecture", "integration", "service", "endpoint", "database", "schema" };
        private readonly string[] functional = new[] { "requirement", "use case", "functional", "user story", "acceptance criteria" };
        private readonly string[] support = new[] { "troubleshoot", "steps", "resolution", "support", "issue", "guide" };
        private readonly string[] alerts = new[] { "alert", "threshold", "cpu", "notification", "alert setting" };

        public string Classify(NormalizedDocument doc, string? fileName = null)
        {
            var sample = string.Join(" ", doc.Blocks.Take(40).Select(b => b.Text ?? string.Empty)).ToLowerInvariant();

            int t = technical.Count(k => sample.Contains(k));
            int f = functional.Count(k => sample.Contains(k));
            int s = support.Count(k => sample.Contains(k));
            int a = alerts.Count(k => sample.Contains(k));

            if (!string.IsNullOrEmpty(fileName))
            {
                var fn = fileName.ToLowerInvariant();
                if (fn.Contains("alert")) return "Alerts";
                if (fn.Contains("technical") || fn.Contains("trd")) return "Technical";
                if (fn.Contains("functional") || fn.Contains("frd")) return "Functional";
            }

            var max = Math.Max(Math.Max(t, f), Math.Max(s, a));
            if (max == a) return "Alerts";
            if (max == t) return "Technical";
            if (max == s) return "Support";
            return "Functional";
        }
    }

    #endregion

    #region Region 4: Section Mapping

    /// <summary>
    /// Section mapper that reads mapping.json and maps blocks to sections
    /// </summary>
    public class SectionMapper : ISectionMapper
    {
        private readonly Dictionary<string, string[]> _rules;

        public SectionMapper(string mappingJsonPath)
        {
            if (!File.Exists(mappingJsonPath))
                throw new FileNotFoundException("mapping.json not found", mappingJsonPath);

            var raw = File.ReadAllText(mappingJsonPath);
            _rules = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(raw, new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore
            }) ?? new Dictionary<string, string[]>();
        }

        public Dictionary<string, MappedSection> Map(NormalizedDocument doc, string mappingJsonPath)
        {
            var result = new Dictionary<string, MappedSection>(StringComparer.OrdinalIgnoreCase);

            foreach (var sec in _rules.Keys)
                result[sec] = new MappedSection { SectionName = sec };

            // Fallback section
            if (!result.ContainsKey("Appendix"))
                result["Appendix"] = new MappedSection { SectionName = "Appendix" };

            foreach (var b in doc.Blocks)
            {
                string text = (b.Text ?? string.Empty).ToLowerInvariant();
                var matched = _rules.FirstOrDefault(kv => kv.Value.Any(token => text.Contains(token)));
                if (matched.Key != null)
                {
                    result[matched.Key].Blocks.Add(b);
                }
                else
                {
                    // If heading, try to map by single-word or title heuristics
                    if (b.Type == BlockType.Heading && !string.IsNullOrWhiteSpace(b.Text))
                    {
                        var heading = b.Text.Trim();
                        if (!result.ContainsKey(heading))
                            result[heading] = new MappedSection { SectionName = heading };

                        result[heading].Blocks.Add(b);
                    }
                    else
                    {
                        result["Appendix"].Blocks.Add(b);
                    }
                }
            }

            // Remove empty sections
            var emptyKeys = result.Where(k => k.Value.Blocks.Count == 0).Select(k => k.Key).ToList();
            foreach (var k in emptyKeys)
                result.Remove(k);

            return result;
        }
    }

    #endregion

    #region Region 5: Template Rendering

    /// <summary>
    /// OpenXML template renderer using ContentBlock model
    /// </summary>
    public class OpenXmlTemplateRenderer : ITemplateRenderer
    {
        public void Render(string templatePath, string outputPath, Dictionary<string, MappedSection> sections, string? title = null)
        {
            if (!File.Exists(templatePath))
                throw new FileNotFoundException("Template not found", templatePath);

            File.Copy(templatePath, outputPath, true);

            using var doc = WordprocessingDocument.Open(outputPath, true);
            var main = doc.MainDocumentPart;
            if (main == null)
                throw new InvalidOperationException("Template missing MainDocumentPart");

            var body = main.Document.Body ?? new Body();
            
            // Set title as first Heading1
            if (!string.IsNullOrWhiteSpace(title))
            {
                var titlePara = new Paragraph(new Run(new Text(title)))
                {
                    ParagraphProperties = new ParagraphProperties(new ParagraphStyleId { Val = "Title" })
                };
                body.InsertAt(titlePara, 0);
            }

            // Find insertion point: if template contains marker [CONTENT_START] use that paragraph; else append to end
            Paragraph insertion = body.Elements<Paragraph>().FirstOrDefault(p => (p.InnerText ?? "").Contains("[CONTENT_START]"));
            int insertIndex = insertion != null ? body.ChildElements.ToList().IndexOf(insertion) : -1;

            foreach (var sec in sections)
            {
                // Build heading
                var h = new Paragraph(new Run(new Text(sec.Key)))
                {
                    ParagraphProperties = new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" })
                };

                if (insertIndex >= 0)
                {
                    body.InsertAt(h, insertIndex++);
                }
                else
                {
                    body.AppendChild(h);
                }

                // Add blocks
                foreach (var block in sec.Value.Blocks)
                {
                    if (block.Type == BlockType.Image && block.ImageBytes != null && block.ImageBytes.Length > 0)
                    {
                        // Image insertion
                        var imgPart = main.AddImagePart(GetImagePartType(block.ImageBytes));
                        using var ms = new MemoryStream(block.ImageBytes);
                        ms.Position = 0;
                        imgPart.FeedData(ms);

                        var blipId = main.GetIdOfPart(imgPart);
                        var drawing = ImageHelper.CreateImageParagraph(blipId);
                        if (insertIndex >= 0)
                            body.InsertAt(drawing, insertIndex++);
                        else
                            body.AppendChild(drawing);
                    }
                    else
                    {
                        var p = new Paragraph(new Run(new Text(block.Text ?? string.Empty)));
                        if (block.Type == BlockType.Heading && block.HeadingLevel > 0)
                        {
                            p.ParagraphProperties = new ParagraphProperties(new ParagraphStyleId { Val = $"Heading{block.HeadingLevel}" });
                        }

                        if (insertIndex >= 0)
                            body.InsertAt(p, insertIndex++);
                        else
                            body.AppendChild(p);
                    }
                }
            }

            // Insert TOC placeholder if template expects it
            var tocPara = body.Elements<Paragraph>().FirstOrDefault(p => (p.InnerText ?? "").Contains("[TOC]"));
            if (tocPara != null)
            {
                var fldSimple = new SimpleField { Instruction = "TOC \\o \"1-3\" \\h \\z \\u" };
                var run = new Run(new Text("Table of Contents"));
                fldSimple.AppendChild(run);
                tocPara.Parent.InsertAfter(fldSimple, tocPara);
                tocPara.Remove();
            }

            main.Document.Save();
        }

        private static ImagePartType GetImagePartType(byte[] bytes)
        {
            if (bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50) return ImagePartType.Png;
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8) return ImagePartType.Jpeg;
            return ImagePartType.Jpeg;
        }
    }

    /// <summary>
    /// Helper class for creating image paragraphs
    /// </summary>
    internal static class ImageHelper
    {
        public static Paragraph CreateImageParagraph(string relId)
        {
            var element =
                new Paragraph(
                    new Run(
                        new Drawing(
                            new DocumentFormat.OpenXml.Drawing.Wordprocessing.Inline(
                                new DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent() { Cx = 990000L, Cy = 792000L },
                                new DocumentFormat.OpenXml.Drawing.Wordprocessing.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                                new DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties() { Id = (UInt32Value)1U, Name = "Image" },
                                new DocumentFormat.OpenXml.Drawing.Graphic(
                                    new DocumentFormat.OpenXml.Drawing.GraphicData(
                                        new DocumentFormat.OpenXml.Drawing.Pictures.Picture(
                                            new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureProperties(
                                                new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualDrawingProperties() { Id = (UInt32Value)0U, Name = "Image.jpg" },
                                                new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureDrawingProperties()
                                            ),
                                            new DocumentFormat.OpenXml.Drawing.Pictures.BlipFill(
                                                new DocumentFormat.OpenXml.Drawing.Blip() { Embed = relId },
                                                new DocumentFormat.OpenXml.Drawing.Stretch(new DocumentFormat.OpenXml.Drawing.FillRectangle())
                                            ),
                                            new DocumentFormat.OpenXml.Drawing.Pictures.ShapeProperties(
                                                new DocumentFormat.OpenXml.Drawing.Transform2D(
                                                    new DocumentFormat.OpenXml.Drawing.Offset() { X = 0L, Y = 0L },
                                                    new DocumentFormat.OpenXml.Drawing.Extents() { Cx = 990000L, Cy = 792000L }
                                                ),
                                                new DocumentFormat.OpenXml.Drawing.PresetGeometry(new DocumentFormat.OpenXml.Drawing.AdjustValueList()) { Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Rectangle }
                                            )
                                        )
                                    ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }
                                )
                            )
                        )
                    )
                );

            return element;
        }
    }

    #endregion
}

