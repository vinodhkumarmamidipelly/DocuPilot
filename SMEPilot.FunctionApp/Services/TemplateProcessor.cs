// TemplateProcessor.cs
// Consolidated: TemplateBuilder + TemplateFiller + SimplifiedContentMapper
// Purpose: All template processing logic in one file with 3 regions

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using SMEPilot.FunctionApp.Models;
using SMEPilot.FunctionApp.Helpers;
using SMEPilot.FunctionApp.Services.Extractors;
using Drawing = DocumentFormat.OpenXml.Drawing;
using DrawingWordprocessing = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace SMEPilot.FunctionApp.Services
{
    /// <summary>
    /// Template processor - combines template building, filling, and content mapping
    /// </summary>
    public class TemplateProcessor
    {
        private readonly ILogger<TemplateProcessor>? _logger;

        public TemplateProcessor(ILogger<TemplateProcessor>? logger = null)
        {
            _logger = logger;
        }

        #region Region 1: TemplateBuilder Methods (Build from scratch)

        /// <summary>
        /// Builds DOCX from DocumentModel (fallback when no template)
        /// </summary>
        public byte[] BuildDocxBytes(DocumentModel model, List<byte[]> images)
        {
            using var mem = new MemoryStream();
            using (var doc = WordprocessingDocument.Create(mem, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                
                var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
                stylesPart.Styles = GenerateStyles();
                
                mainPart.Document = new Document(new Body());
                var body = mainPart.Document.Body;

                AddCoverPage(body, model);
                AddTableOfContents(body);
                AddSectionsWithHierarchy(body, model);

                if (images != null && images.Count > 0)
                {
                    AddImagesSection(body, mainPart, images);
                }

                AddRevisionHistory(body);
                mainPart.Document.Save();
            }
            return mem.ToArray();
        }

        private void AddCoverPage(Body body, DocumentModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.Title))
            {
                body.Append(new Paragraph(new Run(new Text(model.Title)))
                {
                    ParagraphProperties = new ParagraphProperties(
                        new ParagraphStyleId() { Val = "Title" },
                        new SpacingBetweenLines() { After = "240" })
                });
            }

            body.Append(new Paragraph(new Run(new Text($"Document Type: {model.Title ?? "Documentation"}")))
            {
                ParagraphProperties = new ParagraphProperties(
                    new ParagraphStyleId() { Val = "Subtitle" },
                    new SpacingBetweenLines() { After = "120" })
            });

            body.Append(new Paragraph(new Run(new Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC")))
            {
                ParagraphProperties = new ParagraphProperties(
                    new ParagraphStyleId() { Val = "Subtitle" },
                    new SpacingBetweenLines() { After = "480" })
            });

            body.Append(new Paragraph(new Run(new Break() { Type = BreakValues.Page })));
        }

        private void AddTableOfContents(Body body)
        {
            body.Append(new Paragraph(new Run(new Text("Table of Contents")))
            {
                ParagraphProperties = new ParagraphProperties(
                    new ParagraphStyleId() { Val = "TOCHeading" },
                    new SpacingBetweenLines() { After = "240" })
            });

            var tocParagraph = new Paragraph();
            var tocRun = new Run();
            
            tocRun.Append(new FieldChar() { FieldCharType = FieldCharValues.Begin });
            tocRun.Append(new FieldCode(" TOC \\o \"1-3\" \\h \\z \\u ") { Space = SpaceProcessingModeValues.Preserve });
            tocRun.Append(new FieldChar() { FieldCharType = FieldCharValues.Separate });
            tocRun.Append(new FieldChar() { FieldCharType = FieldCharValues.End });
            
            tocParagraph.Append(tocRun);
            tocParagraph.ParagraphProperties = new ParagraphProperties(
                new SpacingBetweenLines() { After = "240" });
            
            body.Append(tocParagraph);
            body.Append(new Paragraph(new Run(new Break() { Type = BreakValues.Page })));
        }

        private void AddSectionsWithHierarchy(Body body, DocumentModel model)
        {
            int headingLevel = 1;
            
            foreach (var section in model.Sections)
            {
                var styleName = DetermineHeadingStyle(section, headingLevel, model.Sections);
                
                var headingPara = new Paragraph(new Run(new Text(section.Heading)))
                {
                    ParagraphProperties = new ParagraphProperties(
                        new ParagraphStyleId() { Val = styleName },
                        new SpacingBetweenLines() { After = "120" })
                };
                body.Append(headingPara);

                if (!string.IsNullOrWhiteSpace(section.Heading))
                {
                    var bookmarkName = $"Section_{section.Id}".Replace(" ", "_").Replace("-", "_");
                    if (bookmarkName.Length > 40) bookmarkName = bookmarkName.Substring(0, 40);
                    
                    var bookmarkId = section.Id.Replace("s", "").Replace("-", "");
                    if (string.IsNullOrWhiteSpace(bookmarkId) || !int.TryParse(bookmarkId, out _))
                    {
                        bookmarkId = headingLevel.ToString();
                    }
                    
                    var bookmarkStart = new BookmarkStart()
                    {
                        Name = bookmarkName,
                        Id = bookmarkId
                    };
                    var bookmarkEnd = new BookmarkEnd() { Id = bookmarkId };
                    
                    headingPara.InsertBefore(bookmarkStart, headingPara.GetFirstChild<Run>());
                    headingPara.InsertAfter(bookmarkEnd, headingPara.GetFirstChild<Run>());
                }

                if (!string.IsNullOrWhiteSpace(section.Summary))
                {
                    body.Append(new Paragraph(new Run(new Text("Summary: " + section.Summary)))
                    {
                        ParagraphProperties = new ParagraphProperties(
                            new ParagraphStyleId() { Val = "Normal" },
                            new SpacingBetweenLines() { After = "120" })
                    });
                }

                if (!string.IsNullOrWhiteSpace(section.Body))
                {
                    var bodyText = section.Body;
                    var paragraphs = bodyText.Split(new[] { "\n\n", "\r\n\r\n", "\n\r\n\r" }, StringSplitOptions.RemoveEmptyEntries);
                    
                    if (paragraphs.Length == 1 && bodyText.Length > 500)
                    {
                        var lines = bodyText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                        var grouped = new List<string>();
                        var currentGroup = new StringBuilder();
                        
                        foreach (var line in lines)
                        {
                            var trimmed = line.Trim();
                            if (string.IsNullOrWhiteSpace(trimmed)) continue;
                            
                            if (currentGroup.Length > 0 && 
                                (trimmed.EndsWith(".") || trimmed.EndsWith("!") || trimmed.EndsWith("?")) &&
                                currentGroup.Length > 100)
                            {
                                grouped.Add(currentGroup.ToString());
                                currentGroup.Clear();
                            }
                            
                            if (currentGroup.Length > 0) currentGroup.Append(" ");
                            currentGroup.Append(trimmed);
                            
                            if (currentGroup.Length > 400)
                            {
                                grouped.Add(currentGroup.ToString());
                                currentGroup.Clear();
                            }
                        }
                        
                        if (currentGroup.Length > 0)
                        {
                            grouped.Add(currentGroup.ToString());
                        }
                        
                        paragraphs = grouped.ToArray();
                    }
                    
                    foreach (var paraText in paragraphs)
                    {
                        var trimmed = paraText.Trim();
                        if (!string.IsNullOrWhiteSpace(trimmed))
                        {
                            body.Append(new Paragraph(new Run(new Text(trimmed)))
                            {
                                ParagraphProperties = new ParagraphProperties(
                                    new ParagraphStyleId() { Val = "Normal" },
                                    new SpacingBetweenLines() { After = "120" })
                            });
                        }
                    }
                }

                headingLevel++;
            }
        }

        private string DetermineHeadingStyle(Section section, int position, List<Section> allSections)
        {
            if (position == 1)
                return "Heading1";

            var heading = section.Heading ?? "";
            var previousHeading = position > 1 ? allSections[position - 2].Heading ?? "" : "";

            if (heading.Length < previousHeading.Length * 0.7 && 
                heading.Length < 50 &&
                !heading.Contains(".") && 
                !heading.Contains(":"))
            {
                if (position % 2 == 0)
                    return "Heading2";
            }

            if (System.Text.RegularExpressions.Regex.IsMatch(heading, @"^\d+\.\d+"))
            {
                return "Heading2";
            }

            return position % 2 == 1 ? "Heading1" : "Heading2";
        }

        private void AddImagesSection(Body body, MainDocumentPart mainPart, List<byte[]> images)
        {
            body.Append(new Paragraph(new Run(new Text("Screenshots and Images")))
            {
                ParagraphProperties = new ParagraphProperties(
                    new ParagraphStyleId() { Val = "Heading2" },
                    new SpacingBetweenLines() { Before = "480", After = "240" })
            });

            int idx = 0;
            foreach (var imgBytes in images)
            {
                idx++;
                try
                {
                    // Feedback2: Resize and compress images before embedding
                    byte[] processedImageBytes = imgBytes;
                    if (ImageResizeHelper.ShouldResize(imgBytes))
                    {
                        processedImageBytes = ImageResizeHelper.ResizeAndCompressImage(imgBytes, maxWidth: 1200, jpegQuality: 80, _logger);
                    }

                    var imagePartType = DetectImageFormat(processedImageBytes);
                    var imagePart = mainPart.AddImagePart(imagePartType);
                    using var imgStream = new MemoryStream(processedImageBytes);
                    imagePart.FeedData(imgStream);

                    var imageRelId = mainPart.GetIdOfPart(imagePart);
                    var (widthEmu, heightEmu) = CalculateImageDimensions(processedImageBytes);
                    var altText = $"Figure {idx}: Image from document";

                    var inline = CreateImageInline(imageRelId, widthEmu, heightEmu, idx, altText);
                    var drawing = CreateDrawingWrapper(inline);

                    var imagePara = new Paragraph();
                    var run = new Run();
                    run.AppendChild(drawing);
                    imagePara.Append(run);
                    imagePara.ParagraphProperties = new ParagraphProperties(
                        new Justification() { Val = JustificationValues.Center },
                        new SpacingBetweenLines() { After = "120" });
                    
                    body.Append(imagePara);

                    body.Append(new Paragraph(new Run(new Text($"Figure {idx}: Image from original document")))
                    {
                        ParagraphProperties = new ParagraphProperties(
                            new Justification() { Val = JustificationValues.Center },
                            new ParagraphStyleId() { Val = "Caption" },
                            new SpacingBetweenLines() { After = "240" })
                    });
                }
                catch (Exception ex)
                {
                    body.Append(new Paragraph(new Run(new Text($"Figure {idx}: Image could not be embedded ({ex.Message})")))
                    {
                        ParagraphProperties = new ParagraphProperties(
                            new Justification() { Val = JustificationValues.Center },
                            new SpacingBetweenLines() { After = "240" })
                    });
                }
            }
        }

        private ImagePartType DetectImageFormat(byte[] imgBytes)
        {
            if (imgBytes.Length < 8)
                return ImagePartType.Png;

            if (imgBytes[0] == 0x89 && imgBytes[1] == 0x50 && imgBytes[2] == 0x4E && imgBytes[3] == 0x47)
                return ImagePartType.Png;

            if (imgBytes[0] == 0xFF && imgBytes[1] == 0xD8)
                return ImagePartType.Jpeg;

            if (imgBytes[0] == 0x47 && imgBytes[1] == 0x49 && imgBytes[2] == 0x46)
                return ImagePartType.Gif;

            if (imgBytes[0] == 0x42 && imgBytes[1] == 0x4D)
                return ImagePartType.Bmp;

            return ImagePartType.Png;
        }

        private (long widthEmu, long heightEmu) CalculateImageDimensions(byte[] imgBytes)
        {
            try
            {
                int width = 0, height = 0;
                
                if (imgBytes.Length >= 24 && 
                    imgBytes[0] == 0x89 && imgBytes[1] == 0x50 && imgBytes[2] == 0x4E && imgBytes[3] == 0x47)
                {
                    width = (imgBytes[16] << 24) | (imgBytes[17] << 16) | (imgBytes[18] << 8) | imgBytes[19];
                    height = (imgBytes[20] << 24) | (imgBytes[21] << 16) | (imgBytes[22] << 8) | imgBytes[23];
                }
                else if (imgBytes.Length >= 20 && imgBytes[0] == 0xFF && imgBytes[1] == 0xD8)
                {
                    for (int i = 2; i < Math.Min(imgBytes.Length - 7, 1000); i++)
                    {
                        if (imgBytes[i] == 0xFF && (imgBytes[i + 1] >= 0xC0 && imgBytes[i + 1] <= 0xC3))
                        {
                            height = (imgBytes[i + 5] << 8) | imgBytes[i + 6];
                            width = (imgBytes[i + 7] << 8) | imgBytes[i + 8];
                            break;
                        }
                    }
                }
                else if (imgBytes.Length >= 10 && 
                         imgBytes[0] == 0x47 && imgBytes[1] == 0x49 && imgBytes[2] == 0x46)
                {
                    width = imgBytes[6] | (imgBytes[7] << 8);
                    height = imgBytes[8] | (imgBytes[9] << 8);
                }
                
                if (width > 0 && height > 0)
                {
                    const long maxWidthEmu = 576000L;
                    const long emuPerPixel = 9525L;
                    
                    var widthEmu = (long)width * emuPerPixel;
                    var heightEmu = (long)height * emuPerPixel;
                    
                    if (widthEmu > maxWidthEmu)
                    {
                        var scale = (double)maxWidthEmu / widthEmu;
                        widthEmu = maxWidthEmu;
                        heightEmu = (long)(heightEmu * scale);
                    }
                    
                    return (widthEmu, heightEmu);
                }
            }
            catch
            {
            }
            
            return (576000L, 432000L);
        }

        private DrawingWordprocessing.Inline CreateImageInline(string imageRelId, long widthEmu, long heightEmu, int idx, string altText)
        {
            var picture = new Drawing.Pictures.Picture(
                new Drawing.Pictures.NonVisualPictureProperties(
                    new Drawing.Pictures.NonVisualDrawingProperties() 
                    { 
                        Id = (UInt32Value)(idx + 1U), 
                        Name = $"Image {idx}",
                        Description = altText
                    },
                    new Drawing.Pictures.NonVisualPictureDrawingProperties()),
                new Drawing.Pictures.BlipFill(
                    new Drawing.Blip() { Embed = imageRelId },
                    new Drawing.Stretch(new Drawing.FillRectangle())),
                new Drawing.Pictures.ShapeProperties(
                    new Drawing.Transform2D(
                        new Drawing.Offset() { X = 0L, Y = 0L },
                        new Drawing.Extents() { Cx = widthEmu, Cy = heightEmu }),
                    new Drawing.PresetGeometry() { Preset = Drawing.ShapeTypeValues.Rectangle }));

            var graphicData = new Drawing.GraphicData(picture)
            {
                Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture"
            };

            var graphic = new Drawing.Graphic(graphicData);

            var inline = new DrawingWordprocessing.Inline(
                new DrawingWordprocessing.Extent() { Cx = widthEmu, Cy = heightEmu },
                new DrawingWordprocessing.EffectExtent() 
                { 
                    LeftEdge = 0L, 
                    TopEdge = 0L, 
                    RightEdge = 0L, 
                    BottomEdge = 0L 
                },
                new DrawingWordprocessing.DocProperties() 
                { 
                    Id = (UInt32Value)(idx + 1U), 
                    Name = $"Image {idx}",
                    Description = altText
                },
                new DrawingWordprocessing.NonVisualGraphicFrameDrawingProperties(
                    new Drawing.GraphicFrameLocks() { NoChangeAspect = true }),
                graphic)
            {
                DistanceFromTop = 0,
                DistanceFromBottom = 0,
                DistanceFromLeft = 0,
                DistanceFromRight = 0
            };
            
            return inline;
        }

        private OpenXmlUnknownElement CreateDrawingWrapper(DrawingWordprocessing.Inline inline)
        {
            var drawing = new OpenXmlUnknownElement(
                "w:drawing",
                "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
            
            drawing.AppendChild(inline);
            
            return drawing;
        }

        private void AddRevisionHistory(Body body)
        {
            body.Append(new Paragraph(new Run(new Break() { Type = BreakValues.Page })));
            
            body.Append(new Paragraph(new Run(new Text("Revision History")))
            {
                ParagraphProperties = new ParagraphProperties(
                    new ParagraphStyleId() { Val = "Heading1" },
                    new SpacingBetweenLines() { Before = "480", After = "240" })
            });

            var table = new Table(
                new TableProperties(
                    new TableBorders(
                        new TopBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new BottomBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new LeftBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new RightBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new InsideHorizontalBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new InsideVerticalBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 })),
                
                new TableRow(
                    new TableCell(new Paragraph(new Run(new Text("Date")))
                    {
                        ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "TableHeading" })
                    }),
                    new TableCell(new Paragraph(new Run(new Text("Author")))
                    {
                        ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "TableHeading" })
                    }),
                    new TableCell(new Paragraph(new Run(new Text("Change Summary")))
                    {
                        ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "TableHeading" })
                    })),
                
                new TableRow(
                    new TableCell(new Paragraph(new Run(new Text(DateTime.UtcNow.ToString("yyyy-MM-dd"))))
                    {
                        ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "TableContent" })
                    }),
                    new TableCell(new Paragraph(new Run(new Text("SMEPilot")))
                    {
                        ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "TableContent" })
                    }),
                    new TableCell(new Paragraph(new Run(new Text("Initial document formatting")))
                    {
                        ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "TableContent" })
                    }))
            );

            body.Append(table);
        }

        private Styles GenerateStyles()
        {
            var styles = new Styles();
            
            styles.Append(new Style(new StyleName() { Val = "Title" }, new BasedOn() { Val = "Normal" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "Title",
                StyleParagraphProperties = new StyleParagraphProperties(
                    new SpacingBetweenLines() { After = "240" })
            });

            styles.Append(new Style(new StyleName() { Val = "Subtitle" }, new BasedOn() { Val = "Normal" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "Subtitle"
            });

            styles.Append(new Style(new StyleName() { Val = "Heading 1" }, new BasedOn() { Val = "Normal" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "Heading1",
                StyleParagraphProperties = new StyleParagraphProperties(
                    new KeepNext(),
                    new SpacingBetweenLines() { Before = "240", After = "120" })
            });

            styles.Append(new Style(new StyleName() { Val = "Heading 2" }, new BasedOn() { Val = "Normal" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "Heading2",
                StyleParagraphProperties = new StyleParagraphProperties(
                    new KeepNext(),
                    new SpacingBetweenLines() { Before = "180", After = "120" })
            });

            styles.Append(new Style(new StyleName() { Val = "Heading 3" }, new BasedOn() { Val = "Normal" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "Heading3",
                StyleParagraphProperties = new StyleParagraphProperties(
                    new KeepNext(),
                    new SpacingBetweenLines() { Before = "120", After = "60" })
            });

            styles.Append(new Style(new StyleName() { Val = "TOC Heading" }, new BasedOn() { Val = "Heading1" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "TOCHeading"
            });

            styles.Append(new Style(new StyleName() { Val = "Caption" }, new BasedOn() { Val = "Normal" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "Caption",
                StyleParagraphProperties = new StyleParagraphProperties(
                    new SpacingBetweenLines() { After = "120" })
            });

            styles.Append(new Style(new StyleName() { Val = "Table Heading" }, new BasedOn() { Val = "Normal" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "TableHeading"
            });

            styles.Append(new Style(new StyleName() { Val = "Table Content" }, new BasedOn() { Val = "Normal" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "TableContent"
            });

            return styles;
        }

        #endregion

        #region Region 2: TemplateFiller Methods (Fill existing template)

        /// <summary>
        /// Inspects the template to find all content control tags
        /// </summary>
        public List<string> InspectTemplate(string templatePath)
        {
            var tags = new List<string>();
            
            if (!File.Exists(templatePath))
            {
                _logger?.LogWarning("⚠️ [TEMPLATE] Template not found: {Path}", templatePath);
                return tags;
            }

            try
            {
                using (var wordDoc = WordprocessingDocument.Open(templatePath, false))
                {
                    var mainPart = wordDoc.MainDocumentPart;
                    if (mainPart?.Document?.Body == null)
                    {
                        _logger?.LogWarning("⚠️ [TEMPLATE] Template has no body");
                        return tags;
                    }

                    var body = mainPart.Document.Body;
                    var sdtList = body.Descendants<SdtElement>().ToList();
                    
                    foreach (var sdt in sdtList)
                    {
                        var tag = sdt.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;
                        if (!string.IsNullOrEmpty(tag))
                        {
                            tags.Add(tag);
                        }
                    }
                }
                
                _logger?.LogInformation("🔍 [TEMPLATE] Found {Count} content controls: {Tags}", 
                    tags.Count, string.Join(", ", tags));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ [TEMPLATE] Error inspecting template: {Error}", ex.Message);
            }

            return tags;
        }

        /// <summary>
        /// Fills a Word template (.dotx) with content using simplified, reliable mapping
        /// </summary>
        public string FillTemplate(
            string templatePath,
            string outputPath,
            Dictionary<string, string> contentMap,
            List<byte[]>? screenshots = null,
            List<(string version, string date, string author, string changes)>? revisions = null,
            string? sourceDocPath = null)
        {
            if (string.IsNullOrWhiteSpace(templatePath))
                throw new ArgumentException("Template path cannot be null or empty", nameof(templatePath));
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));
            if (contentMap == null)
                throw new ArgumentNullException(nameof(contentMap));

            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"Template file not found: {templatePath}", templatePath);

            try
            {
                _logger?.LogDebug("📄 [TEMPLATE] Starting template fill: {TemplatePath} -> {OutputPath}", templatePath, outputPath);

                var availableTags = InspectTemplate(templatePath);
                _logger?.LogInformation("📋 [TEMPLATE] Template has {Count} content controls", availableTags.Count);
                
                _logger?.LogInformation("📝 [TEMPLATE] Content map has {Count} entries: {Keys}", 
                    contentMap.Count, string.Join(", ", contentMap.Keys));

                File.Copy(templatePath, outputPath, true);
                _logger?.LogDebug("✅ [TEMPLATE] Template copied to output path");

                var filledTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var unfilledTags = new List<string>();

                using (var wordDoc = WordprocessingDocument.Open(outputPath, true))
                {
                    // Feedback1: Preserve numbering definitions from template
                    PreserveNumberingDefinitions(wordDoc, templatePath);
                    
                    // Ensure a real Word TOC field exists at the TABLE_OF_CONTENTS placeholder,
                    // and also inject a static heading list so something is visible in Markdown/online viewers.
                    try
                    {
                        contentMap.TryGetValue("TableOfContents", out var staticToc);
                        InsertTocFieldAtPlaceholder(wordDoc, "[TABLE_OF_CONTENTS]", staticToc);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "⚠️ [TEMPLATE] Failed to insert TOC at [TABLE_OF_CONTENTS]: {Error}", ex.Message);
                    }

                    var mainPart = wordDoc.MainDocumentPart;
                    if (mainPart?.Document?.Body == null)
                        throw new InvalidOperationException("Template document is missing main document part or body");

                    var body = mainPart.Document.Body;

                    var sdtList = body.Descendants<SdtElement>().ToList();
                    _logger?.LogDebug("🔍 [TEMPLATE] Found {Count} SDT elements in template", sdtList.Count);

                    foreach (var sdt in sdtList)
                    {
                        var tag = sdt.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;
                        if (string.IsNullOrEmpty(tag))
                        {
                            _logger?.LogDebug("⏭️ [TEMPLATE] SDT element has no tag, skipping");
                            continue;
                        }

                        var matchingKey = contentMap.Keys.FirstOrDefault(k => 
                            string.Equals(k, tag, StringComparison.OrdinalIgnoreCase));

                        if (matchingKey != null)
                        {
                            string newText = contentMap[matchingKey] ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(newText))
                            {
                                if (ReplaceSdtContent(sdt, newText))
                                {
                                    filledTags.Add(tag);
                                    _logger?.LogInformation("✅ [TEMPLATE] Filled content control: {Tag} ({Length} chars)", tag, newText.Length);
                                }
                                else
                                {
                                    _logger?.LogWarning("⚠️ [TEMPLATE] Failed to fill content control: {Tag}", tag);
                                    unfilledTags.Add(tag);
                                }
                            }
                            else
                            {
                                _logger?.LogDebug("⏭️ [TEMPLATE] Skipping empty content for tag: {Tag}", tag);
                            }
                        }
                        else
                        {
                            _logger?.LogDebug("⏭️ [TEMPLATE] No content provided for tag: {Tag}", tag);
                        }
                    }
                    
                    // Special handling: insert RemainingContent as multi-paragraph block instead of a single huge run.
                    // This preserves logical headings/paragraphs for the unmatched part of the raw document.
                    if (contentMap.TryGetValue("RemainingContent", out var remainingContent) &&
                        !string.IsNullOrWhiteSpace(remainingContent))
                    {
                        var remainingMarkers = new[]
                        {
                            "[Document Content Starts Here]",
                            "[RemainingContent]",
                            "[Remaining Content]",
                            "[Remaining Document Content]"
                        };

                        foreach (var marker in remainingMarkers)
                        {
                            InsertComplexContent(wordDoc, marker, remainingContent);
                        }

                        filledTags.Add("RemainingContent");
                    }

                    // Feedback1: Also handle plain text placeholders (e.g., {{Tag}}) that may be split across runs
                    // This is a fallback for templates that use plain text placeholders instead of SDT
                    // CRITICAL FIX: Process both paragraphs AND table cells (which contain paragraphs)
                    var paragraphs = body.Descendants<Paragraph>().ToList();
                    var tableCells = body.Descendants<TableCell>().ToList();
                    _logger?.LogDebug("🔍 [TEMPLATE] Searching {ParaCount} paragraphs and {CellCount} table cells for plain text placeholders", 
                        paragraphs.Count, tableCells.Count);
                    
                    // Build tag mapping: system keys -> template placeholder variations
                    var tagMapping = BuildTagMapping(contentMap.Keys);
                    
                    int placeholderMatches = 0;
                    
                    // Process all paragraphs (including those in table cells)
                    var allParagraphs = paragraphs.Concat(tableCells.SelectMany(cell => cell.Descendants<Paragraph>())).Distinct().ToList();
                    
                    foreach (var para in allParagraphs)
                    {
                        var paraText = string.Concat(para.Descendants<Text>().Select(t => t.Text ?? ""));
                        
                        // Try direct matches first
                        foreach (var key in contentMap.Keys)
                        {
                            // Check for placeholder patterns: {{Tag}}, {Tag}, [Tag]
                            // CRITICAL: Also handle escaped patterns like [AUTHOR_NAME(S)] -> AUTHOR_NAME(S)
                            var normalizedKey = key.Replace("_", " ").Replace("(", "\\(").Replace(")", "\\)");
                            var keyLower = key.ToLowerInvariant();
                            var patterns = new[] { 
                                $"{{{{{key}}}}}", 
                                $"{{{key}}}", 
                                $"[{key}]",
                                // Handle variations with underscores and parentheses
                                $"[{key.Replace("_", " ")}]",
                                $"[{key.Replace("_", "_")}]"
                            };
                            foreach (var pattern in patterns)
                            {
                                if (paraText.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                                {
                                    var replacement = contentMap[key] ?? string.Empty;
                                    if (!string.IsNullOrWhiteSpace(replacement))
                                    {
                                        // CRITICAL: Limit replacement length to prevent excessive content
                                        // Use 500 chars for simple fields (author, reviewer, etc.), 2000 for content sections
                                        // BUT: Never truncate RemainingContent – it must contain the full unmatched document body.
                                        var isContentField = keyLower.Contains("overview") || keyLower.Contains("summary") || 
                                                           keyLower.Contains("description") || keyLower.Contains("content") ||
                                                           keyLower.Contains("functional") || keyLower.Contains("technical") ||
                                                           keyLower.Contains("requirements") || keyLower.Contains("specifications");
                                        var maxLength = isContentField ? 2000 : 500;
                                        
                                        var isRemainingContent = key.Equals("RemainingContent", StringComparison.OrdinalIgnoreCase);
                                        
                                        if (!isRemainingContent && replacement.Length > maxLength)
                                        {
                                            // Try to cut at sentence boundary
                                            var cutPoint = replacement.LastIndexOf('.', maxLength);
                                            if (cutPoint > maxLength * 0.5) // Only use sentence boundary if it's not too early
                                                replacement = replacement.Substring(0, cutPoint + 1);
                                            else
                                                replacement = replacement.Substring(0, maxLength).Trim() + "...";
                                            _logger?.LogDebug("⚠️ [TEMPLATE] Truncated replacement for {Pattern} to {Length} chars", pattern, maxLength);
                                        }
                                        
                                        _logger?.LogInformation("🔄 [TEMPLATE] Found and replacing plain text placeholder: {Pattern} -> {Length} chars", pattern, replacement.Length);
                                        ReplaceTokenPreservingRuns(para, pattern, replacement);
                                        filledTags.Add(key);
                                        placeholderMatches++;
                                        break; // Only replace once per paragraph
                                    }
                                    else
                                    {
                                        _logger?.LogWarning("⚠️ [TEMPLATE] Found placeholder {Pattern} but content is empty", pattern);
                                    }
                                }
                            }
                        }
                        
                        // Try mapped tags (system key -> template placeholder variations)
                        foreach (var mapping in tagMapping)
                        {
                            var systemKey = mapping.Key;
                            var templateTags = mapping.Value;
                            
                            if (!contentMap.ContainsKey(systemKey)) continue;
                            var replacement = contentMap[systemKey] ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(replacement)) continue;
                            
                            // CRITICAL: Limit replacement length - use 500 for simple fields, 2000 for content
                            var systemKeyLower = systemKey.ToLowerInvariant();
                            var isContentField = systemKeyLower.Contains("overview") || systemKeyLower.Contains("summary") || 
                                               systemKeyLower.Contains("description") || systemKeyLower.Contains("content") ||
                                               systemKeyLower.Contains("functional") || systemKeyLower.Contains("technical") ||
                                               systemKeyLower.Contains("requirements") || systemKeyLower.Contains("specifications");
                            var maxLength = isContentField ? 2000 : 500;
                            
                            var isRemainingContent = systemKey.Equals("RemainingContent", StringComparison.OrdinalIgnoreCase);
                            
                            if (!isRemainingContent && replacement.Length > maxLength)
                            {
                                // Try to cut at sentence boundary
                                var cutPoint = replacement.LastIndexOf('.', maxLength);
                                if (cutPoint > maxLength * 0.5)
                                    replacement = replacement.Substring(0, cutPoint + 1);
                                else
                                    replacement = replacement.Substring(0, maxLength).Trim() + "...";
                            }
                            
                            foreach (var templateTag in templateTags)
                            {
                                // Check for [TAG] pattern (most common in templates)
                                // Handle variations: [TAG], [TAG_NAME], [TAG(S)], etc.
                                var bracketPattern = $"[{templateTag}]";
                                var bracketPatternVariations = new[]
                                {
                                    bracketPattern,
                                    $"[{templateTag.Replace(" ", "_")}]",
                                    $"[{templateTag.Replace("_", " ")}]",
                                    $"[{templateTag.ToUpperInvariant()}]",
                                    $"[{templateTag.ToLowerInvariant()}]"
                                };
                                
                                foreach (var pattern in bracketPatternVariations)
                                {
                                    if (paraText.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                                    {
                                        _logger?.LogInformation("🔄 [TEMPLATE] Found mapped placeholder: {Pattern} (maps to {SystemKey}) -> {Length} chars", 
                                            pattern, systemKey, replacement.Length);
                                        ReplaceTokenPreservingRuns(para, pattern, replacement);
                                    filledTags.Add(systemKey);
                                    placeholderMatches++;
                                        break; // Only replace once per paragraph
                                    }
                                }
                                
                                // Also check {{TAG}} and {TAG} patterns
                                var doubleBracePattern = $"{{{{{templateTag}}}}}";
                                var singleBracePattern = $"{{{templateTag}}}";
                                
                                if (paraText.Contains(doubleBracePattern, StringComparison.OrdinalIgnoreCase))
                                {
                                    _logger?.LogInformation("🔄 [TEMPLATE] Found mapped placeholder: {{{{TemplateTag}}}} (maps to {SystemKey}) -> {Length} chars", 
                                        templateTag, systemKey, replacement.Length);
                                    ReplaceTokenPreservingRuns(para, doubleBracePattern, replacement);
                                    filledTags.Add(systemKey);
                                    placeholderMatches++;
                                }
                                
                                if (paraText.Contains(singleBracePattern, StringComparison.OrdinalIgnoreCase))
                                {
                                    _logger?.LogInformation("🔄 [TEMPLATE] Found mapped placeholder: {{TemplateTag}} (maps to {SystemKey}) -> {Length} chars", 
                                        templateTag, systemKey, replacement.Length);
                                    ReplaceTokenPreservingRuns(para, singleBracePattern, replacement);
                                    filledTags.Add(systemKey);
                                    placeholderMatches++;
                                }
                            }
                        }
                    }
                    
                    if (placeholderMatches == 0 && sdtList.Count == 0)
                    {
                        _logger?.LogWarning("⚠️ [TEMPLATE] No placeholders found in template! Template needs either:");
                        _logger?.LogWarning("   1. Content Controls (SDT) with tags: {Tags}", string.Join(", ", contentMap.Keys));
                        _logger?.LogWarning("   2. Plain text placeholders like: {{Tag}}, {Tag}, or [Tag]");
                    }

                    if (screenshots != null && screenshots.Count > 0)
                    {
                        var screenshotControl = sdtList.FirstOrDefault(s => 
                            s.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value?.Equals("Screenshots", StringComparison.OrdinalIgnoreCase) == true);
                        if (screenshotControl != null)
                        {
                            _logger?.LogDebug("🖼️ [TEMPLATE] Inserting {Count} images into Screenshots section", screenshots.Count);
                            foreach (var img in screenshots)
                            {
                                InsertImageIntoControl(mainPart, screenshotControl, img);
                            }
                        }
                        else
                        {
                            _logger?.LogWarning("⚠️ [TEMPLATE] Screenshots content control not found");
                        }
                    }

                    ExpandRevisionHistoryTable(body, mainPart, revisions);
                    AddPageBreaksBeforeH1(body);

                    // Populate structured tables if present
                    ExpandRevisionHistoryTable(body, mainPart, revisions);
                    ExpandChangeLogTable(body, mainPart, contentMap);
                    mainPart.Document.Save();
                    _logger?.LogDebug("💾 [TEMPLATE] Document saved successfully");
                }

                // Convert template to regular document to avoid Word template warning
                ConvertTemplateToDocument(outputPath);
                
                _logger?.LogInformation("✅ [TEMPLATE] Template fill completed. Filled {FilledCount}/{TotalCount} tags. Unfilled: {Unfilled}", 
                    filledTags.Count, availableTags.Count, unfilledTags.Count > 0 ? string.Join(", ", unfilledTags) : "none");

                return outputPath;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ [TEMPLATE] Error filling template: {Error}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Feedback1: Replace SDT content with formatting preservation
        /// Preserves RunProperties and ParagraphProperties when possible
        /// </summary>
        private bool ReplaceSdtContent(SdtElement sdt, string newText)
        {
            if (sdt == null || string.IsNullOrWhiteSpace(newText)) return false;

            try
            {
                if (sdt is SdtBlock sdtBlock && sdtBlock.SdtContentBlock != null)
                {
                    // Feedback1: Preserve paragraph properties (numbering, indentation, etc.)
                    ParagraphProperties? preservedParaProps = null;
                    RunProperties? preservedRunProps = null;
                    
                    // Try to preserve properties from first paragraph/run
                    var firstPara = sdtBlock.SdtContentBlock.Elements<Paragraph>().FirstOrDefault();
                    if (firstPara != null)
                    {
                        preservedParaProps = firstPara.ParagraphProperties?.CloneNode(true) as ParagraphProperties;
                        var firstRun = firstPara.Elements<Run>().FirstOrDefault();
                        if (firstRun != null)
                        {
                            preservedRunProps = firstRun.RunProperties?.CloneNode(true) as RunProperties;
                        }
                    }
                    
                    sdtBlock.SdtContentBlock.RemoveAllChildren();
                    
                    var paragraphs = newText.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                    if (paragraphs.Length == 0)
                    {
                        paragraphs = new[] { newText };
                    }

                    foreach (var paraText in paragraphs)
                    {
                        var trimmed = paraText.Trim();
                        if (!string.IsNullOrWhiteSpace(trimmed))
                        {
                            var run = new Run(new Text(trimmed));
                            // Feedback1: Preserve run formatting
                            if (preservedRunProps != null)
                            {
                                run.RunProperties = preservedRunProps.CloneNode(true) as RunProperties;
                            }
                            
                            var paragraph = new Paragraph(run);
                            // Feedback1: Preserve paragraph formatting (numbering, etc.)
                            if (preservedParaProps != null)
                            {
                                paragraph.ParagraphProperties = preservedParaProps.CloneNode(true) as ParagraphProperties;
                            }
                            
                            sdtBlock.SdtContentBlock.AppendChild(paragraph);
                        }
                    }
                    return true;
                }
                else if (sdt is SdtRun sdtRun && sdtRun.SdtContentRun != null)
                {
                    // Feedback1: Preserve run properties
                    var preservedRunProps = sdtRun.SdtContentRun.Elements<Run>().FirstOrDefault()?.RunProperties?.CloneNode(true) as RunProperties;
                    
                    sdtRun.SdtContentRun.RemoveAllChildren();
                    var run = new Run(new Text(newText));
                    if (preservedRunProps != null)
                    {
                        run.RunProperties = preservedRunProps;
                    }
                    sdtRun.SdtContentRun.AppendChild(run);
                    return true;
                }
                else if (sdt is SdtCell sdtCell && sdtCell.SdtContentCell != null)
                {
                    // Feedback1: Preserve formatting
                    ParagraphProperties? preservedParaProps = null;
                    RunProperties? preservedRunProps = null;
                    var firstPara = sdtCell.SdtContentCell.Elements<Paragraph>().FirstOrDefault();
                    if (firstPara != null)
                    {
                        preservedParaProps = firstPara.ParagraphProperties?.CloneNode(true) as ParagraphProperties;
                        var firstRun = firstPara.Elements<Run>().FirstOrDefault();
                        if (firstRun != null)
                        {
                            preservedRunProps = firstRun.RunProperties?.CloneNode(true) as RunProperties;
                        }
                    }
                    
                    sdtCell.SdtContentCell.RemoveAllChildren();
                    var run = new Run(new Text(newText));
                    if (preservedRunProps != null)
                    {
                        run.RunProperties = preservedRunProps;
                    }
                    var paragraph = new Paragraph(run);
                    if (preservedParaProps != null)
                    {
                        paragraph.ParagraphProperties = preservedParaProps;
                    }
                    sdtCell.SdtContentCell.AppendChild(paragraph);
                    return true;
                }
                else
                {
                    var contentElement = sdt.Elements().FirstOrDefault(e => 
                        e.LocalName == "sdtContent" || 
                        e.LocalName == "sdtContentBlock" || 
                        e.LocalName == "sdtContentRun" ||
                        e.LocalName == "sdtContentCell");
                    
                    if (contentElement != null)
                    {
                        // Try to preserve formatting
                        ParagraphProperties? preservedParaProps = null;
                        RunProperties? preservedRunProps = null;
                        var firstPara = contentElement.Elements<Paragraph>().FirstOrDefault();
                        if (firstPara != null)
                        {
                            preservedParaProps = firstPara.ParagraphProperties?.CloneNode(true) as ParagraphProperties;
                            var firstRun = firstPara.Elements<Run>().FirstOrDefault();
                            if (firstRun != null)
                            {
                                preservedRunProps = firstRun.RunProperties?.CloneNode(true) as RunProperties;
                            }
                        }
                        
                        contentElement.RemoveAllChildren();
                        var run = new Run(new Text(newText));
                        if (preservedRunProps != null)
                        {
                            run.RunProperties = preservedRunProps;
                        }
                        var paragraph = new Paragraph(run);
                        if (preservedParaProps != null)
                        {
                            paragraph.ParagraphProperties = preservedParaProps;
                        }
                        contentElement.AppendChild(paragraph);
                        return true;
                    }
                    
                    _logger?.LogWarning("⚠️ [TEMPLATE] Could not find content element in SDT");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ [TEMPLATE] Error replacing content in SDT: {Error}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Feedback1: Replace token across split runs - handles tokens split across multiple OpenXML runs
        /// This is critical for templates where Word splits placeholder tokens across runs
        /// </summary>
        private void ReplaceTokenPreservingRuns(Paragraph para, string token, string replacement)
        {
            if (para == null || string.IsNullOrEmpty(token)) return;

            try
            {
                var runs = para.Elements<Run>().ToList();
                if (runs.Count == 0) return;

                // Concatenate all text from runs to find token
                var texts = runs.SelectMany(r => r.Elements<Text>()).ToList();
                var fullText = string.Concat(texts.Select(t => t.Text ?? ""));
                
                var tokenIndex = fullText.IndexOf(token, StringComparison.Ordinal);
                if (tokenIndex < 0) return; // Token not found

                // Map token position to run indices
                int currentPos = 0;
                int tokenStartRunIndex = -1;
                int tokenStartRunOffset = -1;
                int tokenEndRunIndex = -1;
                int tokenEndRunOffset = -1;

                for (int runIdx = 0; runIdx < runs.Count; runIdx++)
                {
                    var run = runs[runIdx];
                    var runTexts = run.Elements<Text>().ToList();
                    int runLength = runTexts.Sum(t => t.Text?.Length ?? 0);

                    if (tokenStartRunIndex == -1 && currentPos + runLength > tokenIndex)
                    {
                        // Token starts in this run
                        tokenStartRunIndex = runIdx;
                        tokenStartRunOffset = tokenIndex - currentPos;
                    }

                    if (currentPos + runLength >= tokenIndex + token.Length)
                    {
                        // Token ends in this run
                        tokenEndRunIndex = runIdx;
                        tokenEndRunOffset = (tokenIndex + token.Length) - currentPos;
                        break;
                    }

                    currentPos += runLength;
                }

                if (tokenStartRunIndex == -1 || tokenEndRunIndex == -1) return;

                // Get preserved run properties from first run containing token
                var firstRun = runs[tokenStartRunIndex];
                var preservedRunProps = firstRun.RunProperties?.CloneNode(true) as RunProperties;

                // Remove runs that are completely within the token
                for (int i = tokenEndRunIndex; i >= tokenStartRunIndex; i--)
                {
                    if (i != tokenStartRunIndex && i != tokenEndRunIndex)
                    {
                        runs[i].Remove();
                    }
                }

                // Handle start run
                var startRun = runs[tokenStartRunIndex];
                var startRunTexts = startRun.Elements<Text>().ToList();
                var startRunText = string.Concat(startRunTexts.Select(t => t.Text ?? ""));
                
                if (tokenStartRunIndex == tokenEndRunIndex)
                {
                    // Token is entirely in one run
                    var beforeText = startRunText.Substring(0, tokenStartRunOffset);
                    var afterText = startRunText.Substring(tokenEndRunOffset);
                    
                    startRun.RemoveAllChildren<Text>();
                    if (!string.IsNullOrEmpty(beforeText))
                    {
                        startRun.AppendChild(new Text(beforeText));
                    }
                    if (!string.IsNullOrEmpty(replacement))
                    {
                        var replacementRun = new Run(new Text(replacement));
                        if (preservedRunProps != null)
                        {
                            replacementRun.RunProperties = preservedRunProps.CloneNode(true) as RunProperties;
                        }
                        para.InsertAfter(replacementRun, startRun);
                    }
                    if (!string.IsNullOrEmpty(afterText))
                    {
                        var afterRun = new Run(new Text(afterText));
                        if (preservedRunProps != null)
                        {
                            afterRun.RunProperties = preservedRunProps.CloneNode(true) as RunProperties;
                        }
                        para.InsertAfter(afterRun, startRun);
                    }
                    startRun.Remove();
                }
                else
                {
                    // Token spans multiple runs
                    var beforeText = startRunText.Substring(0, tokenStartRunOffset);
                    startRun.RemoveAllChildren<Text>();
                    if (!string.IsNullOrEmpty(beforeText))
                    {
                        startRun.AppendChild(new Text(beforeText));
                    }

                    // Insert replacement run
                    var replacementRun = new Run(new Text(replacement));
                    if (preservedRunProps != null)
                    {
                        replacementRun.RunProperties = preservedRunProps.CloneNode(true) as RunProperties;
                    }
                    para.InsertAfter(replacementRun, startRun);

                    // Handle end run
                    var endRun = runs[tokenEndRunIndex];
                    var endRunTexts = endRun.Elements<Text>().ToList();
                    var endRunText = string.Concat(endRunTexts.Select(t => t.Text ?? ""));
                    var afterText = endRunText.Substring(tokenEndRunOffset);
                    
                    endRun.RemoveAllChildren<Text>();
                    if (!string.IsNullOrEmpty(afterText))
                    {
                        endRun.AppendChild(new Text(afterText));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ [TEMPLATE] Error replacing token across runs: {Error}", ex.Message);
            }
        }

        private void InsertImageIntoControl(MainDocumentPart mainPart, SdtElement sdt, byte[] imgBytes)
        {
            if (mainPart == null || sdt == null || imgBytes == null || imgBytes.Length == 0)
                return;

            try
            {
                // Feedback2: Resize and compress images before embedding
                byte[] processedImageBytes = imgBytes;
                if (ImageResizeHelper.ShouldResize(imgBytes))
                {
                    _logger?.LogDebug("🖼️ [TEMPLATE] Resizing large image ({Size} bytes) before embedding", imgBytes.Length);
                    processedImageBytes = ImageResizeHelper.ResizeAndCompressImage(imgBytes, maxWidth: 1200, jpegQuality: 80, _logger);
                }

                var imagePartType = DetectImageFormat(processedImageBytes);
                var imagePart = mainPart.AddImagePart(imagePartType);
                using (var stream = new MemoryStream(processedImageBytes))
                {
                    imagePart.FeedData(stream);
                }

                var imageRelId = mainPart.GetIdOfPart(imagePart);
                var (widthEmu, heightEmu) = CalculateImageDimensions(processedImageBytes);
                var drawing = CreateImageDrawing(imageRelId, widthEmu, heightEmu);

                if (sdt is SdtBlock sdtBlock && sdtBlock.SdtContentBlock != null)
                {
                    var paragraph = new Paragraph(new Run(drawing));
                    paragraph.ParagraphProperties = new ParagraphProperties(
                        new Justification() { Val = JustificationValues.Center },
                        new SpacingBetweenLines() { After = "240" });
                    sdtBlock.SdtContentBlock.AppendChild(paragraph);
                }
                else if (sdt is SdtRun sdtRun && sdtRun.SdtContentRun != null)
                {
                    var run = new Run(drawing);
                    sdtRun.SdtContentRun.AppendChild(run);
                }
                else
                {
                    var contentElement = sdt.Elements().FirstOrDefault(e => 
                        e.LocalName == "sdtContent" || 
                        e.LocalName == "sdtContentBlock" || 
                        e.LocalName == "sdtContentRun");
                    
                    if (contentElement != null)
                    {
                        var paragraph = new Paragraph(new Run(drawing));
                        paragraph.ParagraphProperties = new ParagraphProperties(
                            new Justification() { Val = JustificationValues.Center },
                            new SpacingBetweenLines() { After = "240" });
                        contentElement.AppendChild(paragraph);
                    }
                }

                _logger?.LogDebug("✅ [TEMPLATE] Image inserted successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ [TEMPLATE] Error inserting image: {Error}", ex.Message);
            }
        }

        private DocumentFormat.OpenXml.Wordprocessing.Drawing CreateImageDrawing(string imageRelId, long widthEmu, long heightEmu)
        {
            var picture = new PIC.Picture(
                new PIC.NonVisualPictureProperties(
                    new PIC.NonVisualDrawingProperties() { Id = (UInt32Value)1U, Name = "Screenshot" },
                    new PIC.NonVisualPictureDrawingProperties()),
                new PIC.BlipFill(
                    new A.Blip() { Embed = imageRelId, CompressionState = A.BlipCompressionValues.Print },
                    new A.Stretch(new A.FillRectangle())),
                new PIC.ShapeProperties(
                    new A.Transform2D(
                        new A.Offset() { X = 0L, Y = 0L },
                        new A.Extents() { Cx = widthEmu, Cy = heightEmu }),
                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));

            var graphicData = new A.GraphicData(picture) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" };
            var graphic = new A.Graphic(graphicData);
            var inline = new DW.Inline(
                new DW.Extent() { Cx = widthEmu, Cy = heightEmu },
                new DW.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties() { Id = (UInt32Value)1U, Name = "Screenshot" },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks() { NoChangeAspect = true }),
                graphic)
            {
                DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U
            };

            return new DocumentFormat.OpenXml.Wordprocessing.Drawing(inline);
        }

        private void ExpandRevisionHistoryTable(
            Body body,
            MainDocumentPart mainPart,
            List<(string version, string date, string author, string changes)>? revisions)
        {
            try
            {
                var revisionControl = body.Descendants<SdtElement>()
                    .FirstOrDefault(s => s.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value?.Equals("RevisionHistory", StringComparison.OrdinalIgnoreCase) == true);

                if (revisionControl == null)
                {
                    _logger?.LogDebug("⏭️ [TEMPLATE] RevisionHistory content control not found");
                    return;
                }

                var table = revisionControl.Descendants<Table>().FirstOrDefault();
                if (table == null)
                {
                    _logger?.LogDebug("⏭️ [TEMPLATE] No table found in RevisionHistory control");
                    return;
                }

                var rows = table.Elements<TableRow>().ToList();
                var headerRow = rows.FirstOrDefault();
                if (headerRow != null)
                {
                    foreach (var row in rows.Skip(1))
                    {
                        row.Remove();
                    }
                }

                if (revisions != null && revisions.Count > 0)
                {
                    foreach (var revision in revisions)
                    {
                        var revisionRow = new TableRow(
                            new TableCell(new Paragraph(new Run(new Text(revision.date)))
                            {
                                ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "Normal" })
                            }),
                            new TableCell(new Paragraph(new Run(new Text(revision.author)))
                            {
                                ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "Normal" })
                            }),
                            new TableCell(new Paragraph(new Run(new Text(revision.changes)))
                            {
                                ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "Normal" })
                            }));
                        table.AppendChild(revisionRow);
                    }
                    _logger?.LogDebug("✅ [TEMPLATE] Added {Count} revision history rows", revisions.Count);
                }
                else
                {
                    var initialRow = new TableRow(
                        new TableCell(new Paragraph(new Run(new Text(DateTime.UtcNow.ToString("yyyy-MM-dd"))))
                        {
                            ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "Normal" })
                        }),
                        new TableCell(new Paragraph(new Run(new Text("SMEPilot")))
                        {
                            ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "Normal" })
                        }),
                        new TableCell(new Paragraph(new Run(new Text("Initial document enrichment")))
                        {
                            ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "Normal" })
                        }));
                    table.AppendChild(initialRow);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "⚠️ [TEMPLATE] Error expanding revision history table: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Populate a Change Log table inside a content control tagged 'ChangeLog'.
        /// Expected columns (left to right) when available:
        ///  - Change Number
        ///  - Date
        ///  - Sections
        ///  - Description
        ///  - Author
        /// </summary>
        private void ExpandChangeLogTable(
            Body body,
            MainDocumentPart mainPart,
            Dictionary<string, string> contentMap)
        {
            try
            {
                // Find a content control specifically tagged as ChangeLog
                var changeLogControl = body.Descendants<SdtElement>()
                    .FirstOrDefault(s => s.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value?.Equals("ChangeLog", StringComparison.OrdinalIgnoreCase) == true);

                if (changeLogControl == null)
                {
                    _logger?.LogDebug("⏭️ [TEMPLATE] ChangeLog content control not found");
                    return;
                }

                var table = changeLogControl.Descendants<Table>().FirstOrDefault();
                if (table == null)
                {
                    _logger?.LogDebug("⏭️ [TEMPLATE] No table found in ChangeLog control");
                    return;
                }

                var rows = table.Elements<TableRow>().ToList();
                var headerRow = rows.FirstOrDefault();
                if (headerRow != null)
                {
                    foreach (var row in rows.Skip(1))
                    {
                        row.Remove();
                    }
                }

                // Derive a simple summary from the content map:
                // which sections/placeholders were actually filled.
                var filledSectionNames = contentMap
                    .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value)
                                  && !string.Equals(kvp.Key, "DocumentTitle", StringComparison.OrdinalIgnoreCase)
                                  && !string.Equals(kvp.Key, "DocumentType", StringComparison.OrdinalIgnoreCase))
                    .Select(kvp => kvp.Key)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (filledSectionNames.Count == 0)
                {
                    _logger?.LogDebug("⏭️ [TEMPLATE] No filled sections detected, skipping ChangeLog row");
                    return;
                }

                var sectionsText = string.Join(", ", filledSectionNames);
                var changeNumber = "1";
                var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
                var description = $"Sections extracted and updated: {sectionsText}";

                // Try to use resolved author from contentMap if available
                var author = "SMEPilot";
                if (contentMap.TryGetValue("Author", out var mappedAuthor) && !string.IsNullOrWhiteSpace(mappedAuthor))
                {
                    author = mappedAuthor;
                }

                // Determine how many columns we have to map into
                var headerCellCount = headerRow?.Elements<TableCell>().Count() ?? 0;
                if (headerCellCount == 0)
                {
                    // Fallback: assume at least 5 columns
                    headerCellCount = 5;
                }

                // Build cells according to available columns
                var cells = new List<TableCell>();

                // 1) Change Number
                cells.Add(new TableCell(new Paragraph(new Run(new Text(changeNumber)))
                {
                    ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "Normal" })
                }));

                // 2) Date
                if (headerCellCount >= 2)
                {
                    cells.Add(new TableCell(new Paragraph(new Run(new Text(date)))
                    {
                        ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "Normal" })
                    }));
                }

                // 3) Sections
                if (headerCellCount >= 3)
                {
                    cells.Add(new TableCell(new Paragraph(new Run(new Text(sectionsText)))
                    {
                        ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "Normal" })
                    }));
                }

                // 4) Description
                if (headerCellCount >= 4)
                {
                    cells.Add(new TableCell(new Paragraph(new Run(new Text(description)))
                    {
                        ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "Normal" })
                    }));
                }

                // 5) Author
                if (headerCellCount >= 5)
                {
                    cells.Add(new TableCell(new Paragraph(new Run(new Text(author)))
                    {
                        ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "Normal" })
                    }));
                }

                var changeRow = new TableRow(cells);
                table.AppendChild(changeRow);

                _logger?.LogDebug("✅ [TEMPLATE] Added ChangeLog row for sections: {Sections}", sectionsText);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "⚠️ [TEMPLATE] Error expanding change log table: {Error}", ex.Message);
            }
        }

        private void AddPageBreaksBeforeH1(Body body)
        {
            try
            {
                var paragraphs = body.Elements<Paragraph>().ToList();
                bool isFirstParagraph = true;

                foreach (var para in paragraphs)
                {
                    if (isFirstParagraph)
                    {
                        isFirstParagraph = false;
                        continue;
                    }

                    var styleId = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                    if (styleId == "Heading1")
                    {
                        if (para.ParagraphProperties == null)
                        {
                            para.ParagraphProperties = new ParagraphProperties();
                        }
                        para.ParagraphProperties.PageBreakBefore = new PageBreakBefore();
                        _logger?.LogDebug("✅ [TEMPLATE] Added page break before Heading1");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "⚠️ [TEMPLATE] Error adding page breaks: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Feedback1: Preserve numbering definitions from template by cloning numbering.xml and remapping numId
        /// This ensures numbered lists are preserved when content is replaced
        /// </summary>
        private void PreserveNumberingDefinitions(WordprocessingDocument outputDoc, string templatePath)
        {
            try
            {
                using (var templateDoc = WordprocessingDocument.Open(templatePath, false))
                {
                    var templateNumberingPart = templateDoc.MainDocumentPart?.NumberingDefinitionsPart;
                    if (templateNumberingPart == null)
                    {
                        _logger?.LogDebug("📋 [NUMBERING] Template has no numbering definitions, skipping preservation");
                        return;
                    }

                    var outputMainPart = outputDoc.MainDocumentPart;
                    if (outputMainPart == null) return;

                    // Clone numbering part from template
                    var outputNumberingPart = outputMainPart.NumberingDefinitionsPart;
                    if (outputNumberingPart == null)
                    {
                        // Create numbering part if it doesn't exist
                        outputNumberingPart = outputMainPart.AddNewPart<NumberingDefinitionsPart>();
                    }

                    // Copy numbering definitions
                    using (var templateStream = templateNumberingPart.GetStream())
                    using (var outputStream = outputNumberingPart.GetStream(FileMode.Create))
                    {
                        templateStream.CopyTo(outputStream);
                    }

                    _logger?.LogDebug("✅ [NUMBERING] Preserved numbering definitions from template");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "⚠️ [NUMBERING] Failed to preserve numbering definitions: {Error}", ex.Message);
                // Continue without numbering preservation - document will still work
            }
        }

        #endregion

        #region Region 3: SimplifiedContentMapper Methods (Content mapping)

        /// <summary>
        /// Builds the content map for a DOCX template using the legacy, rule-based mapper.
        /// This is the "old engine" behavior: extract placeholders from the template,
        /// then map document sections to a small set of semantic keys and let FillTemplate
        /// handle placeholder variations (e.g. [PROJECT_NAME], {{Overview}}, etc.).
        /// </summary>
        public Dictionary<string, string> BuildContentMapFromTemplate(
            string templatePath,
            DocumentModel docModel,
            string? documentType,
            string? fullText = null,
            Dictionary<string, string>? metadataOverrides = null)
        {
            _logger?.LogInformation("📝 [TEMPLATE] Building content map using legacy mapper (no structured NEW-ARCH engine)");

            // Extract all placeholders from the template to know which tags actually exist
            var templatePlaceholders = ExtractAllPlaceholdersFromTemplate(templatePath);
            var availableTemplateTags = templatePlaceholders
                .Select(p => p.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            _logger?.LogInformation("📋 [TEMPLATE] Found {Count} unique placeholder tags in template for legacy mapping", availableTemplateTags.Count);

            // Use legacy content mapping based on DocumentModel sections and documentType
            var contentMap = BuildContentMap(docModel, documentType, availableTemplateTags, fullText);

            // TEMPLATE-DRIVEN MATCHING: For each explicit placeholder in the template, try to
            // compute a dedicated content value using FindContentForPlaceholder and add it
            // to the content map keyed by placeholder name. This improves fill rate when
            // placeholder names don't exactly match our semantic keys.
            foreach (var placeholder in templatePlaceholders)
            {
                if (string.IsNullOrWhiteSpace(placeholder.Name))
                    continue;

                var nameLower = placeholder.Name.ToLowerInvariant();

                // Skip metadata-style placeholders here; they are handled via semantic
                // mapping + metadataOverrides (to avoid accidentally filling them with
                // long body text like project descriptions).
                if (nameLower.Contains("author") ||
                    nameLower.Contains("reviewer") ||
                    nameLower.Contains("approver") ||
                    nameLower.Contains("version") ||
                    nameLower.Contains("date") ||
                    nameLower.Contains("status") ||
                    nameLower.Contains("classification") ||
                    nameLower.Contains("document_id") ||
                    nameLower.Contains("document id") ||
                    nameLower.Contains("docid"))
                {
                    continue;
                }

                // Skip if we already have a value for this key (from semantic mapping)
                if (contentMap.ContainsKey(placeholder.Name))
                    continue;

                try
                {
                    var value = FindContentForPlaceholder(placeholder, docModel, documentType, fullText);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        contentMap[placeholder.Name] = value;
                        _logger?.LogDebug("✅ [TEMPLATE-DRIVEN] Mapped placeholder '{Name}' directly from document content ({Length} chars)",
                            placeholder.Name, value.Length);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "⚠️ [TEMPLATE-DRIVEN] Failed to map placeholder '{Name}': {Error}", placeholder.Name, ex.Message);
                }
            }

            // Apply any metadata overrides coming from caller (e.g., SharePoint list item fields)
            if (metadataOverrides != null && metadataOverrides.Count > 0)
            {
                foreach (var kvp in metadataOverrides)
                {
                    if (!string.IsNullOrWhiteSpace(kvp.Value))
                    {
                        contentMap[kvp.Key] = kvp.Value;
                        _logger?.LogInformation("🔧 [MAPPER] Applied metadata override: {Key} = '{Value}'", kvp.Key, kvp.Value);
                    }
                }
            }

            var filledCount = contentMap.Count(kvp => !string.IsNullOrWhiteSpace(kvp.Value));
            var emptyCount = contentMap.Count(kvp => string.IsNullOrWhiteSpace(kvp.Value));
            _logger?.LogInformation("✅ [TEMPLATE] Legacy content map built: {Filled}/{Total} filled ({Empty} empty)", 
                filledCount, contentMap.Count, emptyCount);

            return contentMap;
        }

        /// <summary>
        /// Builds content map using simple, direct mapping approach (LEGACY - kept for backward compatibility)
        /// Maps sections by explicit markers first, then by position/heading
        /// </summary>
        public Dictionary<string, string> BuildContentMap(
            DocumentModel docModel, 
            string? documentType,
            List<string>? availableTemplateTags = null,
            string? fullText = null)
        {
            var contentMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            // Tracks which sections have been explicitly mapped to a semantic tag
            var usedSections = new HashSet<int>();

            if (!string.IsNullOrWhiteSpace(docModel.Title))
                contentMap["DocumentTitle"] = docModel.Title;

            if (!string.IsNullOrWhiteSpace(documentType))
                contentMap["DocumentType"] = documentType;

            // Basic metadata keys – only compute if the template actually exposes matching tags.
            // Project/title-like placeholders are handled via DocumentTitle + tag mapping.

            // Author
            if (availableTemplateTags == null || availableTemplateTags.Any(t =>
                    t.Contains("Author", StringComparison.OrdinalIgnoreCase)))
            {
                var author = ExtractAuthor(docModel, fullText);
                if (!string.IsNullOrWhiteSpace(author))
                {
                    contentMap["Author"] = author;
                }
            }

            // Version
            if (availableTemplateTags == null || availableTemplateTags.Any(t =>
                    t.Contains("Version", StringComparison.OrdinalIgnoreCase)))
            {
                var version = ExtractVersion(docModel, fullText) ?? "1.0";
                contentMap["Version"] = version;
            }

            // Date
            if (availableTemplateTags == null || availableTemplateTags.Any(t =>
                    t.Contains("Date", StringComparison.OrdinalIgnoreCase)))
            {
                var date = ExtractDate(docModel, fullText) ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
                contentMap["Date"] = date;
            }

            // Status
            if (availableTemplateTags == null || availableTemplateTags.Any(t =>
                    t.Contains("Status", StringComparison.OrdinalIgnoreCase)))
            {
                var status = ExtractStatus(docModel, fullText);
                if (!string.IsNullOrWhiteSpace(status))
                {
                    contentMap["Status"] = status;
                }
            }

            // Classification
            if (availableTemplateTags == null || availableTemplateTags.Any(t =>
                    t.Contains("Classification", StringComparison.OrdinalIgnoreCase)))
            {
                var classificationValue = documentType ?? "Documentation";
                contentMap["Classification"] = classificationValue;
            }

            // DocumentId (short stable-looking ID for header tables)
            if (availableTemplateTags == null || availableTemplateTags.Any(t =>
                    t.Contains("DOCUMENT_ID", StringComparison.OrdinalIgnoreCase) ||
                    t.Contains("DocumentId", StringComparison.OrdinalIgnoreCase) ||
                    t.Contains("DocId", StringComparison.OrdinalIgnoreCase)))
            {
                if (!contentMap.ContainsKey("DocumentId"))
                {
                    var docId = GenerateDocumentId();
                    contentMap["DocumentId"] = docId;
                }
            }

            // Static Table of Contents text (list of headings) for templates exposing TOC placeholder.
            if (availableTemplateTags == null || availableTemplateTags.Any(t =>
                    t.Contains("TABLE_OF_CONTENTS", StringComparison.OrdinalIgnoreCase) ||
                    t.Contains("Table of Contents", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t, "TOC", StringComparison.OrdinalIgnoreCase)))
            {
                if (docModel.Sections != null && docModel.Sections.Count > 0)
                {
                    var tocBuilder = new StringBuilder();
                    int index = 1;
                    foreach (var section in docModel.Sections)
                    {
                        var heading = section.Heading ?? "";
                        if (string.IsNullOrWhiteSpace(heading))
                            continue;

                        tocBuilder.AppendLine($"{index}. {heading.Trim()}");
                        index++;
                    }

                    var tocText = tocBuilder.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(tocText))
                    {
                        contentMap["TableOfContents"] = tocText;
                    }
                }
            }

            if (docModel.Sections == null || docModel.Sections.Count == 0)
            {
                _logger?.LogWarning("⚠️ [MAPPER] No sections in DocumentModel");
                return contentMap;
            }

            _logger?.LogInformation("📋 [MAPPER] Mapping {SectionCount} sections to template tags", docModel.Sections.Count);

            for (int i = 0; i < docModel.Sections.Count; i++)
            {
                var section = docModel.Sections[i];
                var heading = section.Heading ?? "";
                var body = section.Body ?? "";
                if (string.IsNullOrWhiteSpace(body)) continue;

                var headingLower = heading.ToLowerInvariant();
                var bodyLower = body.ToLowerInvariant();

                string? targetTag = null;
                string contentToMap = body;

                if (bodyLower.Contains("functional overview:") || bodyLower.Contains("functional details:"))
                {
                    targetTag = "Functional";
                    contentToMap = ExtractAfterMarker(body, new[] { "Functional Overview:", "Functional Details:" });
                }
                else if (bodyLower.Contains("technical implementation:") || bodyLower.Contains("technical details:"))
                {
                    targetTag = "Technical";
                    contentToMap = ExtractAfterMarker(body, new[] { "Technical Implementation:", "Technical Details:" });
                }
                else if (bodyLower.Contains("troubleshooting:") || bodyLower.Contains("known issues:"))
                {
                    targetTag = "Findings";
                }
                else if (bodyLower.Contains("references:") || bodyLower.Contains("reference:") || 
                         bodyLower.Contains("http://") || bodyLower.Contains("https://"))
                {
                    targetTag = "References";
                }
                else if (headingLower.Contains("overview") || headingLower.Contains("summary") || headingLower.Contains("introduction"))
                {
                    targetTag = "Overview";
                }
                else if (headingLower.Contains("functional") && !headingLower.Contains("technical"))
                {
                    targetTag = "Functional";
                }
                else if (headingLower.Contains("technical") || headingLower.Contains("implementation"))
                {
                    targetTag = "Technical";
                }
                else if (headingLower.Contains("reference") || headingLower.Contains("link"))
                {
                    targetTag = "References";
                }

                if (targetTag != null)
                {
                    if (availableTemplateTags == null || availableTemplateTags.Contains(targetTag, StringComparer.OrdinalIgnoreCase))
                    {
                        if (contentMap.ContainsKey(targetTag))
                        {
                            contentMap[targetTag] = contentMap[targetTag] + "\n\n" + contentToMap;
                        }
                        else
                        {
                            contentMap[targetTag] = contentToMap;
                        }
                        usedSections.Add(i);
                        _logger?.LogDebug("✅ [MAPPER] Mapped section {Index} ({Heading}) → {Tag}", i, heading, targetTag);
                    }
                }
            }

            if (!contentMap.ContainsKey("Overview"))
            {
                for (int i = 0; i < docModel.Sections.Count; i++)
                {
                    if (usedSections.Contains(i)) continue;
                    var body = docModel.Sections[i].Body ?? "";
                    if (body.Length > 50)
                    {
                        contentMap["Overview"] = body;
                        usedSections.Add(i);
                        _logger?.LogDebug("✅ [MAPPER] Mapped first unmapped section → Overview");
                        break;
                    }
                }
            }

            for (int i = 0; i < docModel.Sections.Count; i++)
            {
                if (usedSections.Contains(i)) continue;
                var section = docModel.Sections[i];
                var body = section.Body ?? "";
                if (string.IsNullOrWhiteSpace(body)) continue;
                
                var bodyLower = body.ToLowerInvariant();
                string? targetTag = null;

                if (bodyLower.Contains("api") || bodyLower.Contains("endpoint") || bodyLower.Contains("cron") || 
                    bodyLower.Contains("webhook") || bodyLower.Contains("microservice") || bodyLower.Contains("service"))
                {
                    targetTag = "Technical";
                }
                else if (bodyLower.Contains("functional") || bodyLower.Contains("feature") || bodyLower.Contains("workflow"))
                {
                    targetTag = "Functional";
                }

                if (targetTag != null)
                {
                    if ((availableTemplateTags == null || availableTemplateTags.Contains(targetTag, StringComparer.OrdinalIgnoreCase)) &&
                        !contentMap.ContainsKey(targetTag))
                    {
                        contentMap[targetTag] = body;
                        usedSections.Add(i);
                        _logger?.LogDebug("✅ [MAPPER] Mapped section {Index} → {Tag} (keyword-based)", i, targetTag);
                    }
                }
            }

            // Map section-level semantics for common high-level areas when the template
            // exposes corresponding placeholders. This does NOT force any ordering –
            // the actual order is still controlled entirely by the template. We only
            // compute stable semantic keys like ProjectOverview / UserStories so that
            // [PROJECT_OVERVIEW], [USER_STORIES], etc. can be filled wherever they
            // appear in the template.
            if (availableTemplateTags == null ||
                availableTemplateTags.Any(t => t.Contains("PROJECT_OVERVIEW", StringComparison.OrdinalIgnoreCase) ||
                                               t.Contains("Project Overview", StringComparison.OrdinalIgnoreCase)))
            {
                for (int i = 0; i < docModel.Sections.Count; i++)
                {
                    if (usedSections.Contains(i)) continue;
                    var headingLower = (docModel.Sections[i].Heading ?? "").ToLowerInvariant();
                    if (headingLower.Contains("project overview"))
                    {
                        contentMap["ProjectOverview"] = docModel.Sections[i].Body ?? string.Empty;
                        usedSections.Add(i);
                        _logger?.LogDebug("✅ [MAPPER] Mapped section {Index} ({Heading}) → ProjectOverview", i, docModel.Sections[i].Heading);
                        break;
                    }
                }
            }

            if (availableTemplateTags == null ||
                availableTemplateTags.Any(t => t.Contains("USER_STORIES", StringComparison.OrdinalIgnoreCase) ||
                                               t.Contains("User Stories", StringComparison.OrdinalIgnoreCase)))
            {
                for (int i = 0; i < docModel.Sections.Count; i++)
                {
                    if (usedSections.Contains(i)) continue;
                    var headingLower = (docModel.Sections[i].Heading ?? "").ToLowerInvariant();
                    if (headingLower.Contains("user stories"))
                    {
                        contentMap["UserStories"] = docModel.Sections[i].Body ?? string.Empty;
                        usedSections.Add(i);
                        _logger?.LogDebug("✅ [MAPPER] Mapped section {Index} ({Heading}) → UserStories", i, docModel.Sections[i].Heading);
                        break;
                    }
                }
            }

            // NEW: Map all remaining, unmapped sections into a single catch-all bucket.
            // This allows templates to expose a [RemainingContent] (or a marker like
            // [Document Content Starts Here]) which will receive everything from the raw
            // document that was not explicitly mapped to a semantic tag like
            // Overview/Functional/Technical/References.
            //
            // NOTE: This still works at the text level (not OpenXML block moves), but it ensures
            // that no textual content is silently dropped by the mapper.
            bool templateWantsRemaining =
                availableTemplateTags == null ||
                availableTemplateTags.Any(t =>
                    t.Contains("RemainingContent", StringComparison.OrdinalIgnoreCase) ||
                    t.Contains("Remaining Document Content", StringComparison.OrdinalIgnoreCase) ||
                    t.Contains("Document Content Starts Here", StringComparison.OrdinalIgnoreCase));

            if (templateWantsRemaining)
            {
                var remainingBuilder = new StringBuilder();

                for (int i = 0; i < docModel.Sections.Count; i++)
                {
                    if (usedSections.Contains(i)) continue;

                    var section = docModel.Sections[i];
                    var heading = section.Heading ?? "";
                    var body = section.Body ?? "";

                    if (string.IsNullOrWhiteSpace(body)) continue;

                    if (!string.IsNullOrWhiteSpace(heading))
                    {
                        // Preserve a simple heading marker so the remaining content
                        // stays readable inside the catch-all region
                        remainingBuilder.AppendLine(heading);
                        remainingBuilder.AppendLine();
                    }

                    remainingBuilder.AppendLine(body);
                    remainingBuilder.AppendLine();
                }

                var remainingText = remainingBuilder.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(remainingText))
                {
                    contentMap["RemainingContent"] = remainingText;
                    _logger?.LogInformation("📦 [MAPPER] Mapped remaining {Count} unmapped sections to RemainingContent", 
                        docModel.Sections.Count - usedSections.Count);
                }
            }

            _logger?.LogInformation("✅ [MAPPER] Content map built: {Count} entries ({Keys})", 
                contentMap.Count, string.Join(", ", contentMap.Keys));
            
            // Log content lengths for debugging
            foreach (var kvp in contentMap)
            {
                var contentLength = kvp.Value?.Length ?? 0;
                _logger?.LogDebug("📝 [MAPPER] Content for '{Key}': {Length} characters", kvp.Key, contentLength);
            }

            return contentMap;
        }

        #region Template-Driven Content Matching

        #region Helper Classes for Template-Driven Matching

        public class TemplatePlaceholder
        {
            public string Name { get; set; } = "";
            public PlaceholderType Type { get; set; }
            public string Pattern { get; set; } = "";
        }

        public enum PlaceholderType
        {
            ContentControl,
            PlainTextBrackets,
            PlainTextDoubleBraces,
            PlainTextSingleBraces
        }

        #endregion

        /// <summary>
        /// Extracts ALL placeholders from template (both SDT content controls and plain text placeholders)
        /// </summary>
        private List<TemplatePlaceholder> ExtractAllPlaceholdersFromTemplate(string templatePath)
        {
            var placeholders = new List<TemplatePlaceholder>();

            try
            {
                using var doc = WordprocessingDocument.Open(templatePath, false);
                var mainPart = doc.MainDocumentPart;
                if (mainPart?.Document?.Body == null)
                    return placeholders;

                var body = mainPart.Document.Body;

                // Extract SDT Content Controls
                var sdtList = body.Descendants<SdtElement>().ToList();
                foreach (var sdt in sdtList)
                {
                    var tag = sdt.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;
                    if (!string.IsNullOrEmpty(tag))
                    {
                        placeholders.Add(new TemplatePlaceholder
                        {
                            Name = tag,
                            Type = PlaceholderType.ContentControl,
                            Pattern = $"[{tag}]"
                        });
                    }
                }

                // Extract plain text placeholders: [TAG], {{TAG}}, {TAG}
                var paragraphs = body.Descendants<Paragraph>().ToList();
                var foundPlaceholders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var para in paragraphs)
                {
                    var paraText = string.Concat(para.Descendants<Text>().Select(t => t.Text ?? ""));

                    // Match [TAG] pattern - CRITICAL: Now captures ALL placeholders including instructional ones
                    // Pattern matches: [PROJECT_NAME], [Provide a high-level overview...], [Description], etc.
                    // Updated to match any text inside brackets (not just uppercase tags)
                    var bracketMatches = System.Text.RegularExpressions.Regex.Matches(paraText, @"\[([^\]]+)\]");
                    foreach (System.Text.RegularExpressions.Match match in bracketMatches)
                    {
                        var tag = match.Groups[1].Value.Trim();
                        // Skip empty placeholders and very long ones (likely not placeholders)
                        if (string.IsNullOrWhiteSpace(tag) || tag.Length > 200)
                            continue;
                        
                        // Skip if it's clearly not a placeholder (e.g., markdown links [text](url))
                        if (tag.Contains("(") && tag.Contains("http"))
                            continue;
                            
                        if (!foundPlaceholders.Contains(tag))
                        {
                            foundPlaceholders.Add(tag);
                            placeholders.Add(new TemplatePlaceholder
                            {
                                Name = tag,
                                Type = PlaceholderType.PlainTextBrackets,
                                Pattern = $"[{tag}]"
                            });
                        }
                    }

                    // Match {{TAG}} pattern
                    var doubleBraceMatches = System.Text.RegularExpressions.Regex.Matches(paraText, @"\{\{([A-Z_][A-Z0-9_()| ]*)\}\}");
                    foreach (System.Text.RegularExpressions.Match match in doubleBraceMatches)
                    {
                        var tag = match.Groups[1].Value.Trim();
                        if (!foundPlaceholders.Contains(tag))
                        {
                            foundPlaceholders.Add(tag);
                            placeholders.Add(new TemplatePlaceholder
                            {
                                Name = tag,
                                Type = PlaceholderType.PlainTextDoubleBraces,
                                Pattern = $"{{{{tag}}}}"
                            });
                        }
                    }

                    // Match {TAG} pattern
                    var singleBraceMatches = System.Text.RegularExpressions.Regex.Matches(paraText, @"\{([A-Z_][A-Z0-9_()| ]*)\}");
                    foreach (System.Text.RegularExpressions.Match match in singleBraceMatches)
                    {
                        var tag = match.Groups[1].Value.Trim();
                        if (!foundPlaceholders.Contains(tag))
                        {
                            foundPlaceholders.Add(tag);
                            placeholders.Add(new TemplatePlaceholder
                            {
                                Name = tag,
                                Type = PlaceholderType.PlainTextSingleBraces,
                                Pattern = $"{{{tag}}}"
                            });
                        }
                    }
                }

                _logger?.LogDebug("📋 [TEMPLATE-DRIVEN] Extracted {Count} placeholders: {Names}", 
                    placeholders.Count, string.Join(", ", placeholders.Select(p => p.Name)));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ [TEMPLATE-DRIVEN] Error extracting placeholders: {Error}", ex.Message);
            }

            return placeholders;
        }

        /// <summary>
        /// Intelligently finds content from document that matches a template placeholder
        /// Uses multiple matching strategies: exact match, fuzzy match, semantic match, content analysis
        /// </summary>
        private string? FindContentForPlaceholder(TemplatePlaceholder placeholder, DocumentModel docModel, string? documentType, string? fullText = null)
        {
            var placeholderName = placeholder.Name;
            var placeholderLower = placeholderName.ToLowerInvariant();

            // CRITICAL FIX: Handle multi-option placeholders (e.g., "DRAFT | REVIEW | APPROVED")
            // These should extract actual value from document, not return entire document body
            if (placeholderName.Contains("|"))
            {
                var multiOptionResult = HandleMultiOptionPlaceholder(placeholderName, docModel, fullText);
                if (multiOptionResult != null)
                {
                    _logger?.LogDebug("✅ [MATCHING] Multi-option match for '{Placeholder}': {Value}", placeholderName, multiOptionResult);
                    return multiOptionResult;
                }
            }

            // Strategy 1: Exact name matching (highest priority)
            if (TryExactMatch(placeholderName, docModel, documentType, fullText, out var exactMatch))
            {
                // CRITICAL: Reject if content is document title
                if (IsDocumentTitleContentStub(exactMatch, docModel.Title, placeholderName))
                    return null;

                _logger?.LogDebug("✅ [MATCHING] Exact match for '{Placeholder}': {Source}", placeholderName, "ExactMatch");
                return exactMatch;
            }

            // Strategy 2: Fuzzy/semantic name matching
            if (TryFuzzyMatch(placeholderName, docModel, documentType, fullText, out var fuzzyMatch))
            {
                // CRITICAL: Reject if content is document title
                if (IsDocumentTitleContentStub(fuzzyMatch, docModel.Title, placeholderName))
                    return null;

                _logger?.LogDebug("✅ [MATCHING] Fuzzy match for '{Placeholder}': {Source}", placeholderName, "FuzzyMatch");
                return fuzzyMatch;
            }

            // Strategy 3: Content-based matching (analyze document sections)
            if (!placeholderName.Contains("|"))
            {
                if (TryContentBasedMatch(placeholderName, docModel, out var contentMatch))
                {
                    // CRITICAL: Reject if content is document title
                    if (IsDocumentTitleContentStub(contentMatch, docModel.Title, placeholderName))
                        return null;

                    _logger?.LogDebug("✅ [MATCHING] Content-based match for '{Placeholder}': {Source}", placeholderName, "ContentAnalysis");
                    return contentMatch;
                }

                // Strategy 4: Keyword-based matching
                if (TryKeywordMatch(placeholderName, docModel, out var keywordMatch))
                {
                    // CRITICAL: Reject if content is document title
                    if (IsDocumentTitleContentStub(keywordMatch, docModel.Title, placeholderName))
                        return null;

                    _logger?.LogDebug("✅ [MATCHING] Keyword match for '{Placeholder}': {Source}", placeholderName, "KeywordMatch");
                    return keywordMatch;
                }
            }

            // Strategy 6: Position-based matching (first section, etc.)
            if (TryPositionBasedMatch(placeholderName, docModel, out var positionMatch))
            {
                // CRITICAL: Reject if content is document title
                if (IsDocumentTitleContentStub(positionMatch, docModel.Title, placeholderName))
                    return null;
                
                _logger?.LogDebug("✅ [MATCHING] Position-based match for '{Placeholder}': {Source}", placeholderName, "PositionMatch");
                return positionMatch;
            }

            // Strategy 6: Instructional placeholder matching (for placeholders like [Provide a high-level overview...])
            // Extract keywords from instructional text and match to document sections
            if (TryInstructionalPlaceholderMatch(placeholderName, docModel, fullText, out var instructionalMatch))
            {
                // CRITICAL: Reject if content is document title
                if (IsDocumentTitleContentStub(instructionalMatch, docModel.Title, placeholderName))
                    return null;
                
                _logger?.LogDebug("✅ [MATCHING] Instructional placeholder match for '{Placeholder}': {Source}", placeholderName, "InstructionalMatch");
                return instructionalMatch;
            }

            // Strategy 7: FALLBACK - For generic placeholders, use section content even with weak matches
            // This ensures we fill placeholders even if exact matching fails
            if (TryFallbackMatch(placeholderName, docModel, fullText, out var fallbackMatch))
            {
                // CRITICAL: Reject if content is document title
                if (IsDocumentTitleContentStub(fallbackMatch, docModel.Title, placeholderName))
                    return null;
                
                _logger?.LogDebug("✅ [MATCHING] Fallback match for '{Placeholder}': {Source}", placeholderName, "FallbackMatch");
                return fallbackMatch;
            }

            // Strategy 8: LAST RESORT - Full-text search when all else fails
            // Search the entire document text for placeholder keywords
            if (TryFullTextSearch(placeholderName, docModel, fullText, out var fullTextMatch))
            {
                // CRITICAL: Reject if content is document title
                if (IsDocumentTitleContentStub(fullTextMatch, docModel.Title, placeholderName))
                    return null;
                
                _logger?.LogDebug("✅ [MATCHING] Full-text search match for '{Placeholder}': {Source}", placeholderName, "FullTextSearch");
                return fullTextMatch;
            }

            return null;
        }

        /// <summary>
        /// Strategy 6: Match instructional placeholders (e.g., [Provide a high-level overview...]) to document sections
        /// Extracts keywords from instructional text and finds matching sections
        /// </summary>
        private bool TryInstructionalPlaceholderMatch(string placeholderName, DocumentModel docModel, string? fullText, out string? content)
        {
            content = null;
            
            // Only process if it looks like an instructional placeholder (contains lowercase, descriptive text)
            var placeholderLower = placeholderName.ToLowerInvariant();
            if (placeholderName == placeholderName.ToUpperInvariant() || placeholderName.Length < 10)
            {
                return false; // Not an instructional placeholder
            }

            // Extract key concepts from instructional text
            var keywords = ExtractKeywordsFromInstructionalText(placeholderLower);
            if (keywords.Count == 0)
                return false;

            _logger?.LogDebug("🔍 [INSTRUCTIONAL] Extracted keywords from '{Placeholder}': {Keywords}", 
                placeholderName, string.Join(", ", keywords));

            // Find section that best matches these keywords
            if (docModel.Sections != null && docModel.Sections.Count > 0)
            {
                // CRITICAL: Filter out document title sections and sections with empty body
                var bestMatch = docModel.Sections
                    .Where(section => 
                        !string.IsNullOrWhiteSpace(section.Body) && // Must have body content
                        !(section.Heading?.Equals(docModel.Title ?? "", StringComparison.OrdinalIgnoreCase) == true)) // Skip document title section
                    .Select(section => new
                    {
                        Section = section,
                        Heading = section.Heading ?? "",
                        Body = section.Body ?? "",
                        HeadingLower = (section.Heading ?? "").ToLowerInvariant(),
                        BodyLower = (section.Body ?? "").ToLowerInvariant(),
                        // Score based on keyword matches in heading and body
                        // CRITICAL: Weight heading matches more heavily (2x) since headings are more descriptive
                        Score = keywords.Sum(kw => 
                            ((section.Heading ?? "").ToLowerInvariant().Contains(kw) ? 2 : 0) +
                            ((section.Body ?? "").ToLowerInvariant().Contains(kw) ? 1 : 0))
                    })
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.Body.Length) // Prefer sections with more content
                    .FirstOrDefault();

                if (bestMatch != null && !string.IsNullOrWhiteSpace(bestMatch.Body))
                {
                    // CRITICAL: Additional validation - reject if body contains document header pattern
                    if (bestMatch.BodyLower.Contains("functional specification document", StringComparison.OrdinalIgnoreCase) &&
                        bestMatch.BodyLower.Contains("version:", StringComparison.OrdinalIgnoreCase) &&
                        bestMatch.BodyLower.Contains("date:", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.LogWarning("⚠️ [INSTRUCTIONAL] Rejected section '{Heading}' - contains document header pattern", bestMatch.Heading);
                        return false;
                    }
                    
                    // CRITICAL: Limit content length to prevent returning entire document (max 1000 chars - more conservative)
                    var bodyText = bestMatch.Body;
                    if (bodyText.Length > 1000)
                    {
                        var firstParagraphEnd = bodyText.IndexOf("\n\n", StringComparison.Ordinal);
                        if (firstParagraphEnd > 0 && firstParagraphEnd < 1000)
                        {
                            bodyText = bodyText.Substring(0, firstParagraphEnd);
                        }
                        else
                        {
                            var cutPoint = bodyText.LastIndexOf('.', 1000);
                            if (cutPoint > 500)
                                bodyText = bodyText.Substring(0, cutPoint + 1);
                            else
                                bodyText = bodyText.Substring(0, 1000) + "...";
                        }
                    }
                    content = bodyText;
                    
                    _logger?.LogDebug("✅ [INSTRUCTIONAL] Matched '{Placeholder}' to section '{Heading}' (score: {Score}, length: {Length})", 
                        placeholderName, bestMatch.Heading, bestMatch.Score, content.Length);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Extract meaningful keywords from instructional placeholder text
        /// </summary>
        private List<string> ExtractKeywordsFromInstructionalText(string text)
        {
            var keywords = new List<string>();
            
            // Common stop words to ignore
            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "a", "an", "the", "is", "are", "was", "were", "be", "been", "being",
                "have", "has", "had", "do", "does", "did", "will", "would", "should",
                "can", "could", "may", "might", "must", "shall",
                "of", "in", "on", "at", "to", "for", "with", "by", "from", "as",
                "and", "or", "but", "if", "then", "else", "when", "where", "why", "how",
                "this", "that", "these", "those", "it", "its", "they", "them", "their",
                "provide", "describe", "list", "include", "specify", "detail", "outline",
                "high", "level", "brief", "short", "long", "complete", "full"
            };

            // Extract meaningful words (3+ characters, not stop words)
            var words = System.Text.RegularExpressions.Regex.Matches(text, @"\b[a-z]{3,}\b")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Value.ToLowerInvariant())
                .Where(w => !stopWords.Contains(w))
                .Distinct()
                .ToList();

            // Prioritize important keywords (longer words, common document terms)
            var importantTerms = new[] { "overview", "summary", "objectives", "requirements", "features", 
                "workflow", "description", "context", "scope", "goals", "metrics", "specifications",
                "architecture", "design", "implementation", "testing", "deployment", "project",
                "system", "application", "platform", "solution", "component", "module", "service" };

            // Add important terms first, then other words
            keywords.AddRange(words.Where(w => importantTerms.Contains(w)));
            keywords.AddRange(words.Where(w => !importantTerms.Contains(w)));

            // CRITICAL: If no keywords found, try to extract key phrases (e.g., "high-level overview" -> "overview")
            if (keywords.Count == 0)
            {
                var phrases = new[] { "high-level", "executive", "general", "detailed", "technical", "functional" };
                foreach (var phrase in phrases)
                {
                    if (text.Contains(phrase))
                    {
                        // Extract the main noun after the phrase
                        var phraseIndex = text.IndexOf(phrase);
                        if (phraseIndex >= 0)
                        {
                            var afterPhrase = text.Substring(phraseIndex + phrase.Length).Trim();
                            var nextWord = System.Text.RegularExpressions.Regex.Match(afterPhrase, @"\b([a-z]{4,})\b");
                            if (nextWord.Success && !stopWords.Contains(nextWord.Groups[1].Value))
                            {
                                keywords.Add(nextWord.Groups[1].Value);
                            }
                        }
                    }
                }
            }

            return keywords.Take(10).ToList(); // Limit to top 10 keywords
        }

        /// <summary>
        /// Strategy 1: Exact name matching
        /// </summary>
        private bool TryExactMatch(string placeholderName, DocumentModel docModel, string? documentType, string? fullText, out string? content)
        {
            content = null;
            var placeholderLower = placeholderName.ToLowerInvariant();

            // CRITICAL FIX: Exclude author/reviewer/approver placeholders from project name matching
            // These should be handled by their specific extraction methods in TryFuzzyMatch
            var isAuthorReviewerApprover = placeholderLower.Contains("author") || 
                                           placeholderLower.Contains("reviewer") || 
                                           placeholderLower.Contains("approver");
            
            // Direct matches for project name/title (but NOT author/reviewer/approver names)
            if (!isAuthorReviewerApprover && 
                (placeholderLower.Contains("title") || 
                 placeholderLower.Contains("project_name") || 
                 (placeholderLower.Contains("name") && placeholderLower.Contains("project"))))
            {
                // Try to extract project name from document, not just use title
                var projectName = ExtractProjectName(docModel, fullText);
                if (!string.IsNullOrWhiteSpace(projectName))
                {
                    content = projectName;
                    return true;
                }
                // Fallback to title if project name extraction fails
                if (!string.IsNullOrWhiteSpace(docModel.Title) && docModel.Title.Length > 3)
                {
                    content = docModel.Title;
                    return true;
                }
            }

            if (placeholderLower.Contains("type") || placeholderLower.Contains("documenttype") || placeholderLower.Contains("doctype"))
            {
                if (!string.IsNullOrWhiteSpace(documentType))
                {
                    content = documentType;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Strategy 2: Fuzzy/semantic name matching using similarity
        /// </summary>
        private bool TryFuzzyMatch(string placeholderName, DocumentModel docModel, string? documentType, string? fullText, out string? content)
        {
            content = null;
            // CRITICAL FIX: Normalize placeholder name - handle AUTHOR_NAME(S) -> author name
            // Remove parentheses and their contents, underscores, normalize spaces
            var placeholderLower = placeholderName.ToLowerInvariant()
                .Replace("_", " ")
                .Replace("(s)", " ")  // Handle (S) or (s) specifically
                .Replace("(S)", " ")
                .Replace("(", " ")
                .Replace(")", " ")
                .Replace("|", " ")
                .Trim();
            
            // Remove extra spaces
            placeholderLower = System.Text.RegularExpressions.Regex.Replace(placeholderLower, @"\s+", " ");

            // Semantic mappings with improved extraction
            var semanticMappings = new Dictionary<string, Func<DocumentModel, string?>>(StringComparer.OrdinalIgnoreCase)
            {
                { "project name", (dm) => ExtractProjectName(dm, fullText) ?? dm.Title },
                { "project title", (dm) => ExtractProjectName(dm, fullText) ?? dm.Title },
                { "document title", (dm) => dm.Title },
                { "title", (dm) => ExtractProjectName(dm, fullText) ?? dm.Title },
                { "document type", (dm) => documentType },
                { "type", (dm) => documentType },
                { "overview", (dm) => GetFirstSectionBody(dm) },
                { "executive summary", (dm) => GetFirstSectionBody(dm) },
                { "summary", (dm) => GetFirstSectionBody(dm) },
                { "purpose", (dm) => GetFirstSectionBody(dm) },
                { "introduction", (dm) => GetFirstSectionBody(dm) },
                { "version", (dm) => ExtractVersion(dm, fullText) },
                { "version number", (dm) => ExtractVersion(dm, fullText) },
                { "date", (dm) => ExtractDate(dm, fullText) },
                { "author", (dm) => ExtractAuthor(dm, fullText) },
                { "author name", (dm) => ExtractAuthor(dm, fullText) },
                { "reviewer", (dm) => ExtractReviewer(dm, fullText) },
                { "reviewer name", (dm) => ExtractReviewer(dm, fullText) },
                { "approver", (dm) => ExtractApprover(dm, fullText) },
                { "approver name", (dm) => ExtractApprover(dm, fullText) },
                { "status", (dm) => ExtractStatus(dm, fullText) },
                { "document id", (dm) => Guid.NewGuid().ToString("N")[..8].ToUpper() },
                { "classification", (dm) => documentType ?? "INTERNAL" }
            };

            // CRITICAL: Try exact match first (after normalization)
            // This handles cases like "author name" matching "author name" exactly
            if (semanticMappings.ContainsKey(placeholderLower))
            {
                content = semanticMappings[placeholderLower](docModel);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    _logger?.LogDebug("✅ [FUZZY] Exact normalized match for '{Placeholder}' -> '{Key}'", placeholderName, placeholderLower);
                    return true;
                }
            }

            // Try direct semantic match (contains check)
            foreach (var mapping in semanticMappings)
            {
                // Check if normalized placeholder contains the mapping key
                if (placeholderLower.Contains(mapping.Key))
                {
                    content = mapping.Value(docModel);
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        _logger?.LogDebug("✅ [FUZZY] Contains match for '{Placeholder}' (normalized: '{Normalized}') -> '{Key}'", 
                            placeholderName, placeholderLower, mapping.Key);
                        return true;
                    }
                }
            }

            // Try fuzzy similarity (Levenshtein distance)
            foreach (var mapping in semanticMappings)
            {
                var similarity = CalculateSimilarity(placeholderLower, mapping.Key);
                if (similarity > 0.7) // 70% similarity threshold
                {
                    content = mapping.Value(docModel);
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        _logger?.LogDebug("🔍 [FUZZY] Matched '{Placeholder}' to '{Mapping}' (similarity: {Similarity:P0})", 
                            placeholderName, mapping.Key, similarity);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Strategy 3: Content-based matching - analyze document sections to find best fit
        /// </summary>
        private bool TryContentBasedMatch(string placeholderName, DocumentModel docModel, out string? content)
        {
            content = null;
            if (docModel.Sections == null || docModel.Sections.Count == 0)
                return false;

            var placeholderLower = placeholderName.ToLowerInvariant().Replace("_", " ").Replace("(", "").Replace(")", "").Replace("|", " ");

            // CRITICAL: If only 1 section, be very selective - only match if there's a strong semantic match
            if (docModel.Sections.Count == 1)
            {
                var section = docModel.Sections[0];
                var score = CalculateSectionMatchScore(placeholderLower, section);
                
                // Require reasonable similarity (0.5+) when there's only 1 section - LOWERED from 0.7 to 0.5
                if (score < 0.5)
                {
                    _logger?.LogDebug("⏭️ [CONTENT-MATCH] Skipping single-section match for '{Placeholder}' - similarity too low ({Score:P0})", 
                        placeholderName, score);
                    return false;
                }
                
                // For single section, extract only relevant portion (first 2000 chars max)
                var body = section.Body ?? "";
                if (body.Length > 2000)
                {
                    // Try to find a sentence boundary near 2000 chars
                    var cutPoint = body.LastIndexOf('.', 2000);
                    if (cutPoint > 1000)
                        body = body.Substring(0, cutPoint + 1);
                    else
                        body = body.Substring(0, 2000) + "...";
                }
                content = body;
                _logger?.LogDebug("📊 [CONTENT-MATCH] Matched '{Placeholder}' to single section (score: {Score:P0}, length: {Length})", 
                    placeholderName, score, content.Length);
                return true;
            }

            // Find section that best matches placeholder by analyzing heading and content
            // CRITICAL: Filter out document title sections and sections with empty body
            var bestMatch = docModel.Sections
                .Where(section => 
                    !string.IsNullOrWhiteSpace(section.Body) && // Must have body content
                    !(section.Heading?.Equals(docModel.Title ?? "", StringComparison.OrdinalIgnoreCase) == true)) // Skip document title section
                .Select((section, index) => new
                {
                    Section = section,
                    Index = index,
                    Heading = section.Heading ?? "",
                    Body = section.Body ?? "",
                    Score = CalculateSectionMatchScore(placeholderLower, section)
                })
                .Where(x => x.Score > 0.1) // LOWERED from 0.3 to 0.1 to be MUCH more permissive - any match is better than nothing
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            if (bestMatch != null && !string.IsNullOrWhiteSpace(bestMatch.Body))
            {
                // CRITICAL: Limit content length to prevent returning entire document (max 1000 chars for better selectivity)
                // LOWERED from 2000 to 1000 to be more conservative
                var bodyText = bestMatch.Body;
                
                // If body is very long, try to extract just the first paragraph or first few sentences
                if (bodyText.Length > 1000)
                {
                    // Try to find first paragraph break
                    var firstParagraphEnd = bodyText.IndexOf("\n\n", StringComparison.Ordinal);
                    if (firstParagraphEnd > 0 && firstParagraphEnd < 1000)
                    {
                        bodyText = bodyText.Substring(0, firstParagraphEnd);
                    }
                    else
                    {
                        // Try to find first sentence boundary near 1000 chars
                        var cutPoint = bodyText.LastIndexOf('.', 1000);
                        if (cutPoint > 500)
                            bodyText = bodyText.Substring(0, cutPoint + 1);
                        else
                            bodyText = bodyText.Substring(0, 1000) + "...";
                    }
                }
                
                content = bodyText;
                _logger?.LogDebug("📊 [CONTENT-MATCH] Matched '{Placeholder}' to section '{Heading}' (score: {Score:P0}, length: {Length})", 
                    placeholderName, bestMatch.Heading, bestMatch.Score, content.Length);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Strategy 4: Keyword-based matching
        /// </summary>
        private bool TryKeywordMatch(string placeholderName, DocumentModel docModel, out string? content)
        {
            content = null;
            if (docModel.Sections == null || docModel.Sections.Count == 0)
                return false;

            var placeholderLower = placeholderName.ToLowerInvariant();
            var keywords = ExtractKeywords(placeholderLower);

            // CRITICAL: If only 1 section, require multiple keyword matches to avoid false positives
            if (docModel.Sections.Count == 1)
            {
                var section = docModel.Sections[0];
                var sectionText = (section.Heading ?? "") + " " + (section.Body ?? "");
                var keywordMatches = keywords.Count(kw => sectionText.ToLowerInvariant().Contains(kw));
                
                // Require at least 1 keyword match when there's only 1 section - LOWERED from 2 to 1
                if (keywordMatches < 1)
                {
                    _logger?.LogDebug("⏭️ [KEYWORD-MATCH] Skipping single-section match for '{Placeholder}' - only {Matches} keyword matches", 
                        placeholderName, keywordMatches);
                    return false;
                }
                
                // For single section, extract only relevant portion (first 1000 chars max - more conservative)
                var body = section.Body ?? "";
                if (body.Length > 1000)
                {
                    // Try to find first paragraph break
                    var firstParagraphEnd = body.IndexOf("\n\n", StringComparison.Ordinal);
                    if (firstParagraphEnd > 0 && firstParagraphEnd < 1000)
                    {
                        body = body.Substring(0, firstParagraphEnd);
                    }
                    else
                    {
                        var cutPoint = body.LastIndexOf('.', 1000);
                        if (cutPoint > 500)
                        body = body.Substring(0, cutPoint + 1);
                    else
                            body = body.Substring(0, 1000) + "...";
                    }
                }
                content = body;
                _logger?.LogDebug("🔑 [KEYWORD-MATCH] Matched '{Placeholder}' to single section ({Matches} keyword matches, length: {Length})", 
                    placeholderName, keywordMatches, content.Length);
                return true;
            }

            // Find section with most keyword matches
            // CRITICAL: Filter out document title sections and sections with empty body
            var bestMatch = docModel.Sections
                .Where(section => 
                    !string.IsNullOrWhiteSpace(section.Body) && // Must have body content
                    !(section.Heading?.Equals(docModel.Title ?? "", StringComparison.OrdinalIgnoreCase) == true)) // Skip document title section
                .Select(section => new
                {
                    Section = section,
                    Heading = section.Heading ?? "",
                    Body = section.Body ?? "",
                    KeywordMatches = keywords.Count(kw => 
                        (section.Heading ?? "").ToLowerInvariant().Contains(kw) ||
                        (section.Body ?? "").ToLowerInvariant().Contains(kw))
                })
                .Where(x => x.KeywordMatches > 0)
                .OrderByDescending(x => x.KeywordMatches)
                .FirstOrDefault();

            if (bestMatch != null && !string.IsNullOrWhiteSpace(bestMatch.Body))
            {
                // CRITICAL: Limit content length to prevent returning entire document (max 1000 chars - more conservative)
                var bodyText = bestMatch.Body;
                if (bodyText.Length > 1000)
                {
                    // Try to find first paragraph break
                    var firstParagraphEnd = bodyText.IndexOf("\n\n", StringComparison.Ordinal);
                    if (firstParagraphEnd > 0 && firstParagraphEnd < 1000)
                    {
                        bodyText = bodyText.Substring(0, firstParagraphEnd);
                    }
                    else
                    {
                        var cutPoint = bodyText.LastIndexOf('.', 1000);
                        if (cutPoint > 500)
                            bodyText = bodyText.Substring(0, cutPoint + 1);
                        else
                            bodyText = bodyText.Substring(0, 1000) + "...";
                    }
                }
                content = bodyText;
                _logger?.LogDebug("🔑 [KEYWORD-MATCH] Matched '{Placeholder}' to section '{Heading}' ({Matches} keyword matches, length: {Length})", 
                    placeholderName, bestMatch.Heading, bestMatch.KeywordMatches, content.Length);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Strategy 7: Fallback matching - For generic placeholders, use section content even with weak matches
        /// This ensures we fill placeholders when other strategies fail
        /// </summary>
        private bool TryFallbackMatch(string placeholderName, DocumentModel docModel, string? fullText, out string? content)
        {
            content = null;
            if (docModel.Sections == null || docModel.Sections.Count == 0)
                return false;

            var placeholderLower = placeholderName.ToLowerInvariant();
            
            // CRITICAL FIX: Allow fallback for ALL placeholders, but prioritize extraction methods for specific fields
            // Only skip if we already tried extraction methods (they're handled in TryFuzzyMatch)
            // For now, allow fallback for everything - it's better to have content than empty placeholders

            // For generic placeholders like [Description], [Details], [Content], etc.
            // Use the first section that has content, or best matching section
            var placeholderKeywords = ExtractKeywords(placeholderLower);
            
            // Find best matching section by keyword matches
            // CRITICAL: Filter out document title sections and sections with empty body
            var bestSection = docModel.Sections
                .Where(section => 
                    !string.IsNullOrWhiteSpace(section.Body) && // Must have body content
                    section.Body.Length > 50 && // Must have meaningful content
                    !(section.Heading?.Equals(docModel.Title ?? "", StringComparison.OrdinalIgnoreCase) == true)) // Skip document title section
                .Select(section => new
                {
                    Section = section,
                    Heading = section.Heading ?? "",
                    Body = section.Body ?? "",
                    KeywordMatches = placeholderKeywords.Count(kw => 
                        (section.Heading ?? "").ToLowerInvariant().Contains(kw) ||
                        (section.Body ?? "").ToLowerInvariant().Contains(kw))
                })
                .Where(x => x.KeywordMatches > 0) // Must have at least one keyword match
                .OrderByDescending(x => x.KeywordMatches)
                .ThenByDescending(x => x.Body.Length) // Prefer longer content
                .FirstOrDefault();

            if (bestSection != null)
            {
                // CRITICAL: Limit to 1000 chars to prevent excessive content (more conservative)
                var bodyText = bestSection.Body;
                if (bodyText.Length > 1000)
                {
                    // Try to find first paragraph break
                    var firstParagraphEnd = bodyText.IndexOf("\n\n", StringComparison.Ordinal);
                    if (firstParagraphEnd > 0 && firstParagraphEnd < 1000)
                    {
                        bodyText = bodyText.Substring(0, firstParagraphEnd);
                    }
                    else
                    {
                        var cutPoint = bodyText.LastIndexOf('.', 1000);
                        if (cutPoint > 500)
                            bodyText = bodyText.Substring(0, cutPoint + 1);
                        else
                            bodyText = bodyText.Substring(0, 1000) + "...";
                    }
                }
                content = bodyText;
                
                _logger?.LogDebug("🔄 [FALLBACK] Using section '{Heading}' for placeholder '{Placeholder}' ({Matches} keyword matches, {Length} chars)", 
                    bestSection.Heading, placeholderName, bestSection.KeywordMatches, content.Length);
                return true;
            }

            // Last resort: Use first section with content (even if no keyword matches)
            // CRITICAL: Filter out document title sections
            var firstSectionWithContent = docModel.Sections
                .FirstOrDefault(s => 
                    !string.IsNullOrWhiteSpace(s.Body) && 
                    s.Body.Length > 50 &&
                    !(s.Heading?.Equals(docModel.Title ?? "", StringComparison.OrdinalIgnoreCase) == true));
            
            if (firstSectionWithContent != null)
            {
                var bodyText = firstSectionWithContent.Body!;
                if (bodyText.Length > 1000)
                {
                    // Try to find first paragraph break
                    var firstParagraphEnd = bodyText.IndexOf("\n\n", StringComparison.Ordinal);
                    if (firstParagraphEnd > 0 && firstParagraphEnd < 1000)
                    {
                        bodyText = bodyText.Substring(0, firstParagraphEnd);
                    }
                    else
                    {
                        var cutPoint = bodyText.LastIndexOf('.', 1000);
                        if (cutPoint > 500)
                            bodyText = bodyText.Substring(0, cutPoint + 1);
                        else
                            bodyText = bodyText.Substring(0, 1000) + "...";
                    }
                }
                content = bodyText;
                
                _logger?.LogDebug("🔄 [FALLBACK] Using first section '{Heading}' for placeholder '{Placeholder}' ({Length} chars)", 
                    firstSectionWithContent.Heading ?? "Unknown", placeholderName, content.Length);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Strategy 8: Full-text search - Search entire document for placeholder keywords
        /// Last resort when section-based matching fails
        /// </summary>
        private bool TryFullTextSearch(string placeholderName, DocumentModel docModel, string? fullText, out string? content)
        {
            content = null;
            
            // Only use for generic/descriptive placeholders
            var placeholderLower = placeholderName.ToLowerInvariant();
            var isSpecificField = placeholderLower.Contains("author") || 
                                 placeholderLower.Contains("reviewer") || 
                                 placeholderLower.Contains("approver") ||
                                 (placeholderLower.Contains("date") && !placeholderLower.Contains("updated")) ||
                                 placeholderLower.Contains("version") ||
                                 placeholderLower.Contains("project name") ||
                                 placeholderLower.Contains("status") ||
                                 placeholderLower.Contains("document id") ||
                                 placeholderLower.Contains("classification");
            
            if (isSpecificField)
            {
                return false; // Don't use full-text search for specific fields (they have extraction methods)
            }

            // Get full text if available
            if (string.IsNullOrWhiteSpace(fullText))
            {
                if (docModel.Sections != null && docModel.Sections.Count > 0)
                {
                    fullText = string.Join("\n\n", docModel.Sections.Select(s => $"{s.Heading}\n{s.Body}"));
                }
                else
                {
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(fullText))
                return false;

            // Extract keywords from placeholder
            var keywords = ExtractKeywords(placeholderLower);
            if (keywords.Count == 0)
                return false;

            // Find the section that contains the most keyword matches
            var fullTextLower = fullText.ToLowerInvariant();
            var keywordMatches = keywords.Count(kw => fullTextLower.Contains(kw));
            
            if (keywordMatches == 0)
                return false;

            // Find the best matching section by keyword density
            if (docModel.Sections != null && docModel.Sections.Count > 0)
            {
                // CRITICAL: Filter out document title sections and sections with empty body
                var bestSection = docModel.Sections
                    .Where(section => 
                        !string.IsNullOrWhiteSpace(section.Body) && // Must have body content
                        section.Body.Length > 50 && // Must have meaningful content
                        !(section.Heading?.Equals(docModel.Title ?? "", StringComparison.OrdinalIgnoreCase) == true)) // Skip document title section
                    .Select(section => new
                    {
                        Section = section,
                        Heading = section.Heading ?? "",
                        Body = section.Body ?? "",
                        KeywordMatches = keywords.Count(kw => 
                            (section.Heading ?? "").ToLowerInvariant().Contains(kw) ||
                            (section.Body ?? "").ToLowerInvariant().Contains(kw)),
                        KeywordDensity = keywords.Count(kw => (section.Body ?? "").ToLowerInvariant().Contains(kw)) / 
                                       (double)Math.Max(1, (section.Body ?? "").Length / 100) // Matches per 100 chars
                    })
                    .Where(x => x.KeywordMatches > 0)
                    .OrderByDescending(x => x.KeywordMatches)
                    .ThenByDescending(x => x.KeywordDensity)
                    .FirstOrDefault();

                if (bestSection != null)
                {
                    // CRITICAL: Limit to 1000 chars (more conservative)
                    var bodyText = bestSection.Body;
                    if (bodyText.Length > 1000)
                    {
                        var firstParagraphEnd = bodyText.IndexOf("\n\n", StringComparison.Ordinal);
                        if (firstParagraphEnd > 0 && firstParagraphEnd < 1000)
                        {
                            bodyText = bodyText.Substring(0, firstParagraphEnd);
                        }
                        else
                        {
                            var cutPoint = bodyText.LastIndexOf('.', 1000);
                            if (cutPoint > 500)
                                bodyText = bodyText.Substring(0, cutPoint + 1);
                            else
                                bodyText = bodyText.Substring(0, 1000) + "...";
                        }
                    }
                    content = bodyText;
                    
                    _logger?.LogDebug("🔍 [FULLTEXT] Found content via full-text search for '{Placeholder}' in section '{Heading}' ({Matches} keyword matches, {Length} chars)", 
                        placeholderName, bestSection.Heading, bestSection.KeywordMatches, content.Length);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Strategy 5: Position-based matching (first section, etc.)
        /// </summary>
        private bool TryPositionBasedMatch(string placeholderName, DocumentModel docModel, out string? content)
        {
            content = null;
            if (docModel.Sections == null || docModel.Sections.Count == 0)
                return false;

            var placeholderLower = placeholderName.ToLowerInvariant();

            // Overview/Summary/Introduction typically in first section
            if (placeholderLower.Contains("overview") || placeholderLower.Contains("summary") || 
                placeholderLower.Contains("introduction") || placeholderLower.Contains("executive"))
            {
                var firstSection = docModel.Sections.FirstOrDefault();
                if (firstSection != null && !string.IsNullOrWhiteSpace(firstSection.Body))
                {
                    content = firstSection.Body;
                    return true;
                }
            }

            return false;
        }

        // Helper methods for content matching
        private string? GetFirstSectionBody(DocumentModel docModel)
        {
            return docModel.Sections?.FirstOrDefault()?.Body;
        }

        /// <summary>
        /// Handles multi-option placeholders (e.g., "DRAFT | REVIEW | APPROVED", "YES | NO", "HIGH | MEDIUM | LOW")
        /// Extracts actual value from document and maps to one of the options
        /// Works generically for ANY multi-option placeholder format
        /// </summary>
        private string? HandleMultiOptionPlaceholder(string placeholderName, DocumentModel docModel, string? fullText)
        {
            var options = placeholderName.Split('|')
                .Select(o => o.Trim())
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .ToList();

            if (options.Count < 2)
                return null; // Not a multi-option placeholder

            var searchText = fullText ?? string.Join("\n\n", 
                docModel.Sections?.Select(s => $"{s.Heading}\n{s.Body}") ?? Enumerable.Empty<string>());
            
            if (string.IsNullOrWhiteSpace(searchText))
                searchText = docModel.Title ?? "";

            var searchTextLower = searchText.ToLowerInvariant();

            // Strategy 1: Look for exact option matches (case-insensitive, whole word)
            // This handles most cases: "DRAFT", "REVIEW", "APPROVED", "YES", "NO", "HIGH", "MEDIUM", "LOW", etc.
            var exactMatches = new List<(string option, int position)>();
            foreach (var option in options)
            {
                var optionLower = option.ToLowerInvariant();
                // Try whole word match first (more precise)
                var wholeWordPattern = $@"\b{System.Text.RegularExpressions.Regex.Escape(optionLower)}\b";
                var wholeWordMatch = System.Text.RegularExpressions.Regex.Match(searchTextLower, wholeWordPattern, 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                if (wholeWordMatch.Success)
                {
                    exactMatches.Add((option, wholeWordMatch.Index));
                }
            }

            // If we found exact matches, return the first one (closest to start of document)
            if (exactMatches.Any())
            {
                var bestMatch = exactMatches.OrderBy(m => m.position).First();
                _logger?.LogDebug("📝 [MULTI-OPTION] Found exact match for '{Placeholder}': {Option} at position {Position}", 
                    placeholderName, bestMatch.option, bestMatch.position);
                return bestMatch.option;
            }

            // Strategy 2: Look for semantic/contextual matches (for status-like placeholders)
            // This handles variations like "Draft for Review" -> "DRAFT" or "REVIEW"
            var statusMappings = new Dictionary<string, List<string>>
            {
                { "draft", new List<string> { "draft", "drafting", "in progress", "working", "pending" } },
                { "review", new List<string> { "review", "reviewing", "under review", "for review", "pending review" } },
                { "approved", new List<string> { "approved", "approval", "accepted", "signed off", "finalized" } },
                { "rejected", new List<string> { "rejected", "rejection", "declined", "denied" } },
                { "yes", new List<string> { "yes", "true", "enabled", "active", "on" } },
                { "no", new List<string> { "no", "false", "disabled", "inactive", "off" } },
                { "high", new List<string> { "high", "critical", "urgent", "important", "priority" } },
                { "medium", new List<string> { "medium", "moderate", "normal", "standard" } },
                { "low", new List<string> { "low", "minor", "optional", "nice to have" } }
            };

            foreach (var option in options)
            {
                var optionLower = option.ToLowerInvariant();
                if (statusMappings.ContainsKey(optionLower))
                {
                    var synonyms = statusMappings[optionLower];
                    foreach (var synonym in synonyms)
                    {
                        if (searchTextLower.Contains(synonym))
                        {
                            _logger?.LogDebug("📝 [MULTI-OPTION] Found semantic match for '{Placeholder}': {Option} (via '{Synonym}')", 
                                placeholderName, option, synonym);
                            return option;
                        }
                    }
                }
            }

            // Strategy 3: Look for partial matches (for longer option names)
            // This handles cases where option might appear as part of a phrase
            foreach (var option in options)
            {
                var optionLower = option.ToLowerInvariant();
                // Only do partial match if option is at least 4 characters (to avoid false positives)
                if (optionLower.Length >= 4 && searchTextLower.Contains(optionLower))
                {
                    _logger?.LogDebug("📝 [MULTI-OPTION] Found partial match for '{Placeholder}': {Option}", 
                        placeholderName, option);
                    return option;
                }
            }

            // Strategy 4: For HTTP methods and technical terms, look for context
            // Check if document mentions the option in a technical context
            var technicalTerms = new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" };
            foreach (var option in options)
            {
                if (technicalTerms.Contains(option.ToUpperInvariant()))
                {
                    // Look for HTTP method in context (e.g., "GET request", "POST method", "using PUT")
                    var patterns = new[]
                    {
                        $@"\b{option}\s+(?:request|method|endpoint|api|call)",
                        $@"(?:using|via|with)\s+{option}\b",
                        $@"{option}\s+verb"
                    };
                    
                    foreach (var pattern in patterns)
                    {
                        if (System.Text.RegularExpressions.Regex.IsMatch(searchTextLower, pattern, 
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        {
                            _logger?.LogDebug("📝 [MULTI-OPTION] Found contextual match for '{Placeholder}': {Option}", 
                                placeholderName, option);
                            return option;
                        }
                    }
                }
            }

            // Strategy 5: Default to first option if no match found
            // This ensures we always return a value, even if document doesn't contain any option
            _logger?.LogDebug("📝 [MULTI-OPTION] No match found for '{Placeholder}', defaulting to first option: {Option}", 
                placeholderName, options[0]);
            return options[0];
        }

        /// <summary>
        /// Extracts project name from document - looks for actual project name, not just filename
        /// </summary>
        private string? ExtractProjectName(DocumentModel docModel, string? fullText)
        {
            _logger?.LogInformation("🔍 [EXTRACT-PROJECT] Starting extraction. Title: {Title}, Sections: {Count}, HasFullText: {HasText}",
                docModel.Title, docModel.Sections?.Count ?? 0, !string.IsNullOrWhiteSpace(fullText));

            // Use full text if available - this is most reliable
            if (!string.IsNullOrWhiteSpace(fullText))
            {
                // Look for title-like patterns at the beginning (first 20 lines)
                var lines = fullText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                _logger?.LogDebug("🔍 [EXTRACT-PROJECT] Analyzing {LineCount} lines from full text", lines.Length);
                
                foreach (var line in lines.Take(20))
                {
                    var trimmed = line.Trim();
                    _logger?.LogDebug("🔍 [EXTRACT-PROJECT] Checking line: {Line}", trimmed.Substring(0, Math.Min(50, trimmed.Length)));
                    
                    // Handle markdown headers (## Project Name) - this is the most common case
                    if (trimmed.StartsWith("##") && !trimmed.StartsWith("###"))
                    {
                        trimmed = trimmed.TrimStart('#', ' ').Trim();
                        // This is likely a project name if it's not a generic document type
                        if (trimmed.Length > 5 && trimmed.Length < 100 &&
                            !trimmed.Contains("FUNCTIONAL SPECIFICATION", StringComparison.OrdinalIgnoreCase) &&
                            !trimmed.Contains("TECHNICAL SPECIFICATION", StringComparison.OrdinalIgnoreCase) &&
                            !trimmed.Contains("DOCUMENT", StringComparison.OrdinalIgnoreCase) &&
                            !trimmed.Contains("OVERVIEW", StringComparison.OrdinalIgnoreCase) &&
                            !trimmed.Contains("INTRODUCTION", StringComparison.OrdinalIgnoreCase) &&
                            !trimmed.Contains("TABLE OF CONTENTS", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger?.LogInformation("✅ [EXTRACT-PROJECT] Found project name in markdown header: {Name}", trimmed);
                            return trimmed;
                        }
                    }
                    // Also check for patterns like "**Project Name**: ..." or "Project Name: ..." or "Project: ..."
                    else if (trimmed.Contains("Project", StringComparison.OrdinalIgnoreCase))
                    {
                        // Pattern: "**Project Name**: FitNex Fitness App" or "Project Name: FitNex Fitness App" or "Project: FitNex Fitness App"
                        // CRITICAL: Use greedy matching to capture full project name, then stop at end of line or specific delimiters
                        var patterns = new[]
                        {
                            // Pattern 1: "**Project:** Full Name" or "Project: Full Name" - capture until end of line, backslash, or section marker
                            // Use GREEDY matching to capture full name, then lookahead to stop at delimiter
                            @"(?:project\s*name|project)\s*:?\s*\*?\*?\s*([A-Z][A-Za-z0-9\s\-&]+)(?=\s*$|\s*\\|\s*\n|\s+\d+\.|\s+##|\s+\*\*)",
                            // Pattern 2: "**Project Name:** Full Name" with explicit bold markers
                            @"\*\*project\s*name\*\*\s*:?\s*([A-Z][A-Za-z0-9\s\-&]+)(?=\s*$|\s*\\|\s*\n|\s+\d+\.|\s+##|\s+\*\*)",
                            // Pattern 3: More permissive - capture until we hit a number followed by period (section marker), double hash, or next bold field
                            @"(?:project\s*name|project)\s*:?\s*\*?\*?\s*([A-Z][A-Za-z0-9\s\-&]+)(?=\s*\d+\.|\s*##|\s*$|\s*\\|\s*\n|\s*\*\*)"
                        };
                        
                        foreach (var pattern in patterns)
                        {
                            var projectMatch = System.Text.RegularExpressions.Regex.Match(
                                trimmed, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);
                            if (projectMatch.Success)
                            {
                                var name = projectMatch.Groups[1].Value.Trim().Trim('*', '\\', ')', ']', '[', '(', '|');
                                // Additional validation: should not end with a single digit (likely a mistake)
                                // Also ensure it's not just "FitNex" - should be longer if it's a full project name
                                if (name.Length >= 10 && name.Length < 100 && 
                                    !System.Text.RegularExpressions.Regex.IsMatch(name, @"\d$") &&
                                    name.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries).Length >= 2)
                                {
                                    _logger?.LogInformation("✅ [EXTRACT-PROJECT] Found project name in metadata line: {Name}", name);
                                    return name;
                                }
                                else if (name.Length > 5)
                                {
                                    // If it ends with a digit, try removing it
                                    var cleanedName = System.Text.RegularExpressions.Regex.Replace(name, @"\d+$", "").Trim();
                                    if (cleanedName.Length >= 10 && cleanedName.Length < 100 &&
                                        cleanedName.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries).Length >= 2)
                                    {
                                        _logger?.LogInformation("✅ [EXTRACT-PROJECT] Found project name in metadata line (cleaned): {Name}", cleanedName);
                                        return cleanedName;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Second, try to find project name in sections (look for headings that might be project name)
            if (docModel.Sections != null)
            {
                _logger?.LogDebug("🔍 [EXTRACT-PROJECT] Checking {Count} sections for project name", docModel.Sections.Count);
                
                // Look for section headings that look like project names (not generic headings)
                var projectNamePatterns = new[] { "project", "application", "app", "system", "product" };
                foreach (var section in docModel.Sections)
                {
                    var heading = section.Heading ?? "";
                    if (string.IsNullOrWhiteSpace(heading) || heading.Length > 100)
                        continue;

                    // Clean markdown from heading
                    heading = heading.TrimStart('#', ' ').Trim();

                    // Skip generic document type headings
                    if (heading.Contains("FUNCTIONAL SPECIFICATION", StringComparison.OrdinalIgnoreCase) || 
                        heading.Contains("TECHNICAL SPECIFICATION", StringComparison.OrdinalIgnoreCase) ||
                        heading.Contains("DOCUMENT", StringComparison.OrdinalIgnoreCase) ||
                        heading.Contains("TABLE OF CONTENTS", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // If heading doesn't match generic patterns but is substantial, it might be project name
                    if (heading.Length > 5 && heading.Length < 80 && 
                        !heading.All(c => char.IsUpper(c) || char.IsWhiteSpace(c) || c == '-' || c == '&'))
                    {
                        // Check if it's not just a section heading
                        var headingLower = heading.ToLowerInvariant();
                        var isGenericHeading = projectNamePatterns.Any(p => headingLower.StartsWith(p + " ")) ||
                                             headingLower.Contains(" overview") ||
                                             headingLower.Contains(" introduction") ||
                                             headingLower.Contains(" summary");
                        
                        if (!isGenericHeading)
                        {
                            _logger?.LogInformation("✅ [EXTRACT-PROJECT] Found project name in section heading: {Heading}", heading);
                            return heading.Trim();
                        }
                    }
                }

                // Look in first section body for project name patterns
                var firstSection = docModel.Sections.FirstOrDefault();
                if (firstSection != null && !string.IsNullOrWhiteSpace(firstSection.Body))
                {
                    var body = firstSection.Body;
                    // Look for patterns like "Project Name: ..." or "Application: ..."
                    // Use word boundary or end of line to stop capturing
                    var projectMatch = System.Text.RegularExpressions.Regex.Match(
                        body, 
                        @"(?:project\s*name|project)\s*:?\s*\*?\*?\s*([A-Z][A-Za-z0-9\s\-&]{5,80}?)(?:\s|$|\\|\)|\[)",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (projectMatch.Success)
                    {
                        var name = projectMatch.Groups[1].Value.Trim().Trim('*', '\\', ')', ']', '[', '(', '|');
                        // Additional validation: should not end with a single digit
                        if (name.Length > 5 && name.Length < 80 && 
                            !System.Text.RegularExpressions.Regex.IsMatch(name, @"\d$"))
                        {
                            _logger?.LogInformation("✅ [EXTRACT-PROJECT] Found project name in section body: {Name}", name);
                            return name;
                        }
                        else if (name.Length > 5)
                        {
                            // If it ends with a digit, try removing it
                            var cleanedName = System.Text.RegularExpressions.Regex.Replace(name, @"\d+$", "").Trim();
                            if (cleanedName.Length > 5 && cleanedName.Length < 80)
                            {
                                _logger?.LogInformation("✅ [EXTRACT-PROJECT] Found project name in section body (cleaned): {Name}", cleanedName);
                                return cleanedName;
                            }
                        }
                    }
                }
            }

            // Fallback to title if it's not just a filename
            if (!string.IsNullOrWhiteSpace(docModel.Title) && 
                docModel.Title.Length > 5 && 
                !docModel.Title.Equals("Document", StringComparison.OrdinalIgnoreCase) &&
                !docModel.Title.Equals("raw", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogDebug("📝 [EXTRACT-PROJECT] Using title as fallback: {Title}", docModel.Title);
                return docModel.Title;
            }

            _logger?.LogWarning("⚠️ [EXTRACT-PROJECT] Could not extract project name from document");
            return null;
        }

        private string? ExtractVersion(DocumentModel docModel, string? fullText)
        {
            var searchText = fullText ?? (docModel.Title + " " + (GetFirstSectionBody(docModel) ?? ""));
            if (string.IsNullOrWhiteSpace(searchText))
            {
                _logger?.LogDebug("⚠️ [EXTRACT-VERSION] No search text, defaulting to 1.0");
            return "1.0";
        }

            _logger?.LogDebug("🔍 [EXTRACT-VERSION] Searching in {Length} chars (first 200: {Preview})", 
                searchText.Length, searchText.Substring(0, Math.Min(200, searchText.Length)));

            // Try various version patterns - handles multiple formats (markdown and Word)
            var patterns = new[]
            {
                // Markdown bold: "**Version:** 1.0" or "**Version:**1.0"
                @"\*\*version\*\*\s*:?\s*([0-9]+(?:\.[0-9]+)*(?:\.[0-9]+)?)",
                // Standard formats: "Version: 1.0", "Version 1.0", "v1.0", "Ver 1.0"
                @"version\s*:?\s*v?([0-9]+(?:\.[0-9]+)*(?:\.[0-9]+)?)",
                @"\bv([0-9]+(?:\.[0-9]+)*(?:\.[0-9]+)?)\b",
                @"ver\s*:?\s*([0-9]+(?:\.[0-9]+)*(?:\.[0-9]+)?)",
                // With labels: "Version Number: 1.0", "Ver No: 1.0"
                @"version\s+(?:number|no|#)\s*:?\s*([0-9]+(?:\.[0-9]+)*(?:\.[0-9]+)?)",
                // In brackets/parentheses: "(v1.0)", "[Version 1.0]"
                @"[\[\(]v?([0-9]+(?:\.[0-9]+)*(?:\.[0-9]+)?)[\]\)]",
                // Standalone version numbers at start of line or after colon
                @"^[^\w]*([0-9]+\.[0-9]+(?:\.[0-9]+)?)",
                // Semantic versioning: "1.0.0", "2.1.3"
                @"\b([0-9]+\.[0-9]+\.[0-9]+)\b"
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    searchText, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);
                if (match.Success)
                {
                    var version = match.Groups[1].Value.Trim();
                    // Validate it looks like a version number
                    if (System.Text.RegularExpressions.Regex.IsMatch(version, @"^[0-9]+(?:\.[0-9]+)*$"))
                    {
                        _logger?.LogInformation("✅ [EXTRACT-VERSION] Found version: {Version}", version);
                        return version;
                    }
                }
            }

            _logger?.LogWarning("⚠️ [EXTRACT-VERSION] Could not extract version, defaulting to 1.0");
            return "1.0";
        }

        private string? ExtractDate(DocumentModel docModel, string? fullText)
        {
            _logger?.LogInformation("🔍 [EXTRACT-DATE] Starting date extraction");
            
            // CRITICAL FIX: Search in document header/metadata section first (first 2000 chars)
            // This is where dates are typically found in document headers
            var headerText = "";
            if (fullText != null && fullText.Length > 0)
            {
                headerText = fullText.Substring(0, Math.Min(2000, fullText.Length));
            }
            else if (docModel.Title != null)
            {
                headerText = docModel.Title + " " + (GetFirstSectionBody(docModel) ?? "");
                headerText = headerText.Substring(0, Math.Min(2000, headerText.Length));
            }
            
            // Also search in full text as fallback
            var searchText = fullText ?? (docModel.Title + " " + (GetFirstSectionBody(docModel) ?? ""));
            
            if (string.IsNullOrWhiteSpace(headerText) && string.IsNullOrWhiteSpace(searchText))
            {
                _logger?.LogWarning("⚠️ [EXTRACT-DATE] No search text available, using default date");
                return DateTime.UtcNow.ToString("yyyy-MM-dd");
            }

            _logger?.LogDebug("🔍 [EXTRACT-DATE] Searching in header ({HeaderLength} chars) and full text ({FullLength} chars)", 
                headerText.Length, searchText?.Length ?? 0);

            // Look for date patterns - handles multiple formats including markdown
            // CRITICAL: Order matters! Prioritize "Month Year" format (e.g., "December 2024") over ISO dates
            // CRITICAL: Search header first, then full text
            var datePatterns = new[]
            {
                // PRIORITY 1: Short format with "Date:" label: "**Date:** December 2024" or "Date: December 2024\" - most common in document headers
                // Handle backslash at end: "December 2024\"
                @"(?:\*{0,2}(?:date|created|updated)\*{0,2}\s*:?\s*\*{0,2})([A-Z][a-z]+\s+\d{4})(?:\s*\\|\s*$|\s*\n|\s*\d+\.|\s*\|)",
                // PRIORITY 2: Month Year format (standalone) - stop at backslash, newline, pipe, or section marker
                // CRITICAL: Must be at word boundary to avoid matching "December 2024" inside longer text
                @"\b([A-Z][a-z]+\s+\d{4})(?:\s*\\|\s*$|\s*\n|\s*\d+\.|\s*\|)",
                // PRIORITY 3: With month names and day: "December 17, 2024", "17 December 2024"
                @"(?:\*{0,2}(?:date|created|updated)\*{0,2}\s*:?\s*\*{0,2})([A-Z][a-z]+\s+\d{1,2},?\s+\d{4})",
                @"([A-Z][a-z]+\s+\d{1,2},?\s+\d{4})", // Month name format
                // PRIORITY 4: ISO format with "Date:" label: "**Date:** 2024-12-17" - less common in headers
                @"(?:\*{0,2}(?:date|created|updated|modified|documented)\*{0,2}\s*:?\s*\*{0,2})(\d{4}-\d{2}-\d{2})",
                // PRIORITY 5: US format with "Date:" label: "Date: 12/17/2024"
                @"(?:\*{0,2}(?:date|created|updated)\*{0,2}\s*:?\s*\*{0,2})(\d{1,2}/\d{1,2}/\d{4})",
                // PRIORITY 6: European format with "Date:" label: "Date: 17/12/2024"
                @"(?:\*{0,2}(?:date|created|updated)\*{0,2}\s*:?\s*\*{0,2})(\d{1,2}[-/]\d{1,2}[-/]\d{4})",
                // PRIORITY 7: ISO format standalone (may be in project timelines, less reliable)
                @"(\d{4}-\d{2}-\d{2})", // YYYY-MM-DD standalone
                // PRIORITY 8: US format standalone
                @"(\d{1,2}/\d{1,2}/\d{4})", // MM/DD/YYYY standalone
                // PRIORITY 9: European format standalone
                @"(\d{1,2}[-/]\d{1,2}[-/]\d{4})" // DD/MM/YYYY or DD-MM-YYYY
            };
            
            // Search header first (where metadata usually is)
            var searchTargets = new[] { (Text: headerText, Name: "header"), (Text: searchText, Name: "full text") };

            foreach (var searchTarget in searchTargets)
            {
                if (string.IsNullOrWhiteSpace(searchTarget.Text)) continue;

            foreach (var pattern in datePatterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                        searchTarget.Text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);
                if (match.Success)
                {
                    try
                    {
                            var dateStr = match.Groups[1].Value.Trim().Trim('*', '\\', '|', '\n', '\r');
                        // For "Month Year" format (e.g., "December 2024"), parse as first day of that month
                        if (System.Text.RegularExpressions.Regex.IsMatch(dateStr, @"^[A-Z][a-z]+\s+\d{4}$"))
                        {
                                // Try parsing with "1 " prefix to get first day of month
                                if (DateTime.TryParse($"1 {dateStr}", System.Globalization.CultureInfo.InvariantCulture, 
                                    System.Globalization.DateTimeStyles.None, out var date))
                            {
                                    _logger?.LogInformation("✅ [EXTRACT-DATE] Found date in {Source} (Month Year format): {Date} -> {Parsed}", 
                                        searchTarget.Name, dateStr, date.ToString("yyyy-MM-dd"));
                                return date.ToString("yyyy-MM-dd");
                            }
                                // Fallback: try parsing directly
                                else if (DateTime.TryParse(dateStr, System.Globalization.CultureInfo.InvariantCulture,
                                    System.Globalization.DateTimeStyles.None, out date))
                                {
                                    _logger?.LogInformation("✅ [EXTRACT-DATE] Found date in {Source} (direct parse): {Date} -> {Parsed}", 
                                        searchTarget.Name, dateStr, date.ToString("yyyy-MM-dd"));
                                    return date.ToString("yyyy-MM-dd");
                                }
                                else
                                {
                                    _logger?.LogDebug("⚠️ [EXTRACT-DATE] Could not parse Month Year format: {DateStr}", dateStr);
                                }
                            }
                            else if (DateTime.TryParse(dateStr, System.Globalization.CultureInfo.InvariantCulture,
                                System.Globalization.DateTimeStyles.None, out var date))
                            {
                                _logger?.LogInformation("✅ [EXTRACT-DATE] Found date in {Source}: {Date} -> {Parsed}", 
                                    searchTarget.Name, dateStr, date.ToString("yyyy-MM-dd"));
                            return date.ToString("yyyy-MM-dd");
                        }
                        else
                        {
                            _logger?.LogDebug("⚠️ [EXTRACT-DATE] Could not parse date string: {DateStr}", dateStr);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug("⚠️ [EXTRACT-DATE] Error parsing date: {Error}", ex.Message);
                        }
                    }
                }
            }

            _logger?.LogWarning("⚠️ [EXTRACT-DATE] Could not extract date from document after trying {PatternCount} patterns, using default: {DefaultDate}", 
                datePatterns.Length, DateTime.UtcNow.ToString("yyyy-MM-dd"));
            return DateTime.UtcNow.ToString("yyyy-MM-dd");
        }

        private string? ExtractAuthor(DocumentModel docModel, string? fullText)
        {
            _logger?.LogInformation("🔍 [EXTRACT-AUTHOR] Starting author extraction");
            
            var searchText = fullText ?? string.Join("\n\n", 
                docModel.Sections?.Select(s => $"{s.Heading}\n{s.Body}") ?? Enumerable.Empty<string>());
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                _logger?.LogWarning("⚠️ [EXTRACT-AUTHOR] No search text available");
                return null;
            }

            _logger?.LogDebug("🔍 [EXTRACT-AUTHOR] Searching in {Length} chars of text", searchText.Length);

            // Look for author patterns - handles multiple formats
            // CRITICAL: Use word boundaries to ensure we match complete words, not partial words
            var patterns = new[]
            {
                // Standard formats: "Author: John Doe", "Author Name: John Doe", "Author(s): John Doe"
                // Use word boundary to ensure we start at beginning of a word, not middle
                @"\b(?:author|created by|written by|prepared by|documented by)\s*(?:name)?\s*\(?s\)?\s*:?\s*\*?\*?\s*\b([A-Z][A-Za-z\s\.\-]{2,50}?)(?:\s*$|\s*\\|\s*\n|\s*\d+\.)",
                // With asterisks/bold: "**Author:** John Doe"
                @"\*\*(?:author|created by|written by)\*\*\s*:?\s*\b([A-Z][A-Za-z\s\.\-]{2,50}?)(?:\s*$|\s*\\|\s*\n|\s*\d+\.)",
                // In brackets: "[Author: John Doe]"
                @"\[(?:author|created by|written by)\s*:?\s*\b([A-Z][A-Za-z\s\.\-]{2,50}?)(?:\s*\]|\s*$|\s*\\|\s*\n)",
                // Pattern that stops at end of line or next field marker
                @"(?:author|created by|written by)\s*\(?s\)?\s*:?\s*\*?\*?\s*\b([A-Z][A-Za-z\s\.\-]{2,50}?)(?=\s*(?:reviewer|approver|status|version|date|project|\d+\.|##|$|\\|\n))"
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    searchText, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);
                if (match.Success)
                {
                    var author = match.Groups[1].Value.Trim().Trim('*', '[', ']', '(', ')', '\\');
                    // Validate it looks like a name (not too short, not too long, contains letters, NOT "Content", starts with capital)
                    // CRITICAL: Must start with capital letter and be a complete word/phrase (not partial word like "ization")
                    if (author.Length >= 3 && author.Length <= 80 && 
                        !author.Equals("Content", StringComparison.OrdinalIgnoreCase) &&
                        char.IsUpper(author[0]) && // Must start with capital
                        System.Text.RegularExpressions.Regex.IsMatch(author, @"^[A-Z][A-Za-z\s\.\-]+$") &&
                        !author.ToLowerInvariant().StartsWith("ization") && // Reject partial words
                        !author.ToLowerInvariant().StartsWith("decisions") && // Reject if it's just "Decisions"
                        author.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries).All(w => w.Length >= 2)) // Each word at least 2 chars
                    {
                        _logger?.LogInformation("✅ [EXTRACT-AUTHOR] Found author: {Author}", author);
                        return author;
                    }
                    else
                    {
                        _logger?.LogDebug("⚠️ [EXTRACT-AUTHOR] Rejected match (invalid format, partial word, or 'Content'): {Author}", author);
                    }
                }
            }

            _logger?.LogWarning("⚠️ [EXTRACT-AUTHOR] Could not extract author from document after trying {PatternCount} patterns", patterns.Length);
            return null;
        }

        private string? ExtractReviewer(DocumentModel docModel, string? fullText)
        {
            _logger?.LogInformation("🔍 [EXTRACT-REVIEWER] Starting reviewer extraction");
            
            var searchText = fullText ?? string.Join("\n\n", 
                docModel.Sections?.Select(s => $"{s.Heading}\n{s.Body}") ?? Enumerable.Empty<string>());
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                _logger?.LogWarning("⚠️ [EXTRACT-REVIEWER] No search text available");
                return null;
            }

            // Look for reviewer patterns - handles multiple formats
            // CRITICAL: Use word boundaries to ensure we match complete words, not partial words
            var patterns = new[]
            {
                // Standard formats: "Reviewer: John Doe", "Reviewed By: John Doe", "Reviewer(s): John Doe"
                @"\b(?:reviewer|reviewed by|reviewed|review)\s*(?:name)?\s*\(?s\)?\s*:?\s*\*?\*?\s*\b([A-Z][A-Za-z\s\.\-]{2,50}?)(?:\s*$|\s*\\|\s*\n|\s*\d+\.)",
                // With asterisks/bold: "**Reviewer:** John Doe"
                @"\*\*(?:reviewer|reviewed by)\*\*\s*:?\s*\b([A-Z][A-Za-z\s\.\-]{2,50}?)(?:\s*$|\s*\\|\s*\n|\s*\d+\.)",
                // In brackets: "[Reviewer: John Doe]"
                @"\[(?:reviewer|reviewed by)\s*:?\s*\b([A-Z][A-Za-z\s\.\-]{2,50}?)(?:\s*\]|\s*$|\s*\\|\s*\n)",
                // Pattern that stops at end of line or next field marker
                @"(?:reviewer|reviewed by)\s*\(?s\)?\s*:?\s*\*?\*?\s*\b([A-Z][A-Za-z\s\.\-]{2,50}?)(?=\s*(?:approver|author|status|version|date|project|\d+\.|##|$|\\|\n))"
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    searchText, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);
                if (match.Success)
                {
                    var reviewer = match.Groups[1].Value.Trim().Trim('*', '[', ']', '(', ')', '\\');
                    // Validate it looks like a name (NOT "Content", must start with capital, complete words)
                    if (reviewer.Length >= 3 && reviewer.Length <= 80 && 
                        !reviewer.Equals("Content", StringComparison.OrdinalIgnoreCase) &&
                        char.IsUpper(reviewer[0]) && // Must start with capital
                        System.Text.RegularExpressions.Regex.IsMatch(reviewer, @"^[A-Z][A-Za-z\s\.\-]+$") &&
                        reviewer.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries).All(w => w.Length >= 2)) // Each word at least 2 chars
                    {
                        _logger?.LogInformation("✅ [EXTRACT-REVIEWER] Found reviewer: {Reviewer}", reviewer);
                        return reviewer;
                    }
                    else
                    {
                        _logger?.LogDebug("⚠️ [EXTRACT-REVIEWER] Rejected match (invalid format or partial word): {Reviewer}", reviewer);
                    }
                }
            }

            _logger?.LogWarning("⚠️ [EXTRACT-REVIEWER] Could not extract reviewer from document after trying {PatternCount} patterns", patterns.Length);
            return null;
        }

        private string? ExtractApprover(DocumentModel docModel, string? fullText)
        {
            _logger?.LogInformation("🔍 [EXTRACT-APPROVER] Starting approver extraction");
            
            var searchText = fullText ?? string.Join("\n\n", 
                docModel.Sections?.Select(s => $"{s.Heading}\n{s.Body}") ?? Enumerable.Empty<string>());
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                _logger?.LogWarning("⚠️ [EXTRACT-APPROVER] No search text available");
                return null;
            }

            // Look for approver patterns - handles multiple formats
            // CRITICAL: Use word boundaries to ensure we match complete words, not partial words
            var patterns = new[]
            {
                // Standard formats: "Approver: John Doe", "Approved By: John Doe", "Approver(s): John Doe"
                @"\b(?:approver|approved by|approval|approved)\s*(?:name)?\s*\(?s\)?\s*:?\s*\*?\*?\s*\b([A-Z][A-Za-z\s\.\-]{2,50}?)(?:\s*$|\s*\\|\s*\n|\s*\d+\.)",
                // With asterisks/bold: "**Approver:** John Doe"
                @"\*\*(?:approver|approved by)\*\*\s*:?\s*\b([A-Z][A-Za-z\s\.\-]{2,50}?)(?:\s*$|\s*\\|\s*\n|\s*\d+\.)",
                // In brackets: "[Approver: John Doe]"
                @"\[(?:approver|approved by)\s*:?\s*\b([A-Z][A-Za-z\s\.\-]{2,50}?)(?:\s*\]|\s*$|\s*\\|\s*\n)",
                // Pattern that stops at end of line or next field marker
                @"(?:approver|approved by)\s*\(?s\)?\s*:?\s*\*?\*?\s*\b([A-Z][A-Za-z\s\.\-]{2,50}?)(?=\s*(?:author|reviewer|status|version|date|project|\d+\.|##|$|\\|\n))"
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    searchText, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);
                if (match.Success)
                {
                    var approver = match.Groups[1].Value.Trim().Trim('*', '[', ']', '(', ')', '\\');
                    // Validate it looks like a name (NOT "Content", must start with capital, complete words)
                    if (approver.Length >= 3 && approver.Length <= 80 && 
                        !approver.Equals("Content", StringComparison.OrdinalIgnoreCase) &&
                        char.IsUpper(approver[0]) && // Must start with capital
                        System.Text.RegularExpressions.Regex.IsMatch(approver, @"^[A-Z][A-Za-z\s\.\-]+$") &&
                        approver.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries).All(w => w.Length >= 2)) // Each word at least 2 chars
                    {
                        _logger?.LogInformation("✅ [EXTRACT-APPROVER] Found approver: {Approver}", approver);
                        return approver;
                    }
                    else
                    {
                        _logger?.LogDebug("⚠️ [EXTRACT-APPROVER] Rejected match (invalid format or partial word): {Approver}", approver);
                    }
                }
            }

            _logger?.LogWarning("⚠️ [EXTRACT-APPROVER] Could not extract approver from document after trying {PatternCount} patterns", patterns.Length);
            return null;
        }

        private string? ExtractStatus(DocumentModel docModel, string? fullText)
        {
            var searchText = fullText ?? string.Join("\n\n", 
                docModel.Sections?.Select(s => $"{s.Heading}\n{s.Body}") ?? Enumerable.Empty<string>());
            
            if (string.IsNullOrWhiteSpace(searchText))
                return "DRAFT";

            var searchTextLower = searchText.ToLowerInvariant();

            // Look for status indicators
            if (searchTextLower.Contains("approved") || searchTextLower.Contains("approval"))
                return "APPROVED";
            
            if (searchTextLower.Contains("review") || searchTextLower.Contains("under review"))
                return "REVIEW";

            return "DRAFT";
        }

        private double CalculateSectionMatchScore(string placeholderLower, Section section)
        {
            double score = 0;
            var heading = (section.Heading ?? "").ToLowerInvariant();
            var body = (section.Body ?? "").ToLowerInvariant();
            var combinedText = heading + " " + body;

            // Exact heading match (highest priority)
            if (heading.Contains(placeholderLower) || placeholderLower.Contains(heading))
                score += 0.6; // INCREASED from 0.5

            // Keyword matches in heading (more weight)
            var placeholderWords = placeholderLower.Split(new[] { ' ', '_', '-', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2) // Filter out very short words
                .ToList();
            var headingWords = heading.Split(new[] { ' ', '_', '-', '.', ':', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2)
                .ToList();
            
            if (placeholderWords.Count > 0)
            {
            var commonWords = placeholderWords.Intersect(headingWords, StringComparer.OrdinalIgnoreCase).Count();
                var keywordMatchRatio = commonWords / (double)placeholderWords.Count;
                score += 0.4 * keywordMatchRatio; // INCREASED from 0.3, and now based on ratio
                
                // Bonus if ALL keywords match
                if (commonWords == placeholderWords.Count && commonWords > 0)
                    score += 0.2;
            }

            // Keyword matches in body (lower weight but still counts)
            if (placeholderWords.Count > 0)
            {
                var bodyWords = body.Split(new[] { ' ', '_', '-', '.', ':', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 2)
                    .ToList();
                var bodyCommonWords = placeholderWords.Intersect(bodyWords, StringComparer.OrdinalIgnoreCase).Count();
                if (bodyCommonWords > 0)
                    score += 0.1 * (bodyCommonWords / (double)placeholderWords.Count);
            }

            // Semantic similarity (reduced weight)
            score += CalculateSimilarity(placeholderLower, heading) * 0.1; // REDUCED from 0.2

            return Math.Min(1.0, score);
        }

        private List<string> ExtractKeywords(string text)
        {
            // Extract meaningful keywords (remove common words, keep important terms)
            var commonWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by", "from",
                "is", "are", "was", "were", "be", "been", "being", "have", "has", "had", "do", "does", "did"
            };

            // CRITICAL: Handle underscore-separated placeholders like "PROJECT_NAME" -> ["project", "name"]
            // Also handle parentheses like "AUTHOR_NAME(S)" -> ["author", "name"]
            var keywords = text.Split(new[] { ' ', '_', '-', '(', ')', '|', '[', ']', '{', '}' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim().ToLowerInvariant())
                .Where(w => w.Length > 2 && !commonWords.Contains(w))
                .Distinct()
                .ToList();

            // CRITICAL: If we have compound words like "projectname", try to split them
            // This handles cases where placeholders might be concatenated
            var expandedKeywords = new List<string>(keywords);
            foreach (var keyword in keywords)
            {
                // Try to split camelCase or ALLCAPS words
                if (keyword.Length > 6 && (char.IsUpper(keyword[0]) || keyword.All(c => char.IsUpper(c) || char.IsDigit(c))))
                {
                    // Split on capital letters: "ProjectName" -> ["project", "name"]
                    var parts = System.Text.RegularExpressions.Regex.Split(keyword, @"(?<!^)(?=[A-Z])")
                        .Where(p => p.Length > 2 && !commonWords.Contains(p.ToLowerInvariant()))
                        .Select(p => p.ToLowerInvariant());
                    expandedKeywords.AddRange(parts);
                }
            }

            return expandedKeywords.Distinct().ToList();
        }

        /// <summary>
        /// Calculate similarity between two strings using Levenshtein distance
        /// Returns a value between 0.0 (no similarity) and 1.0 (identical)
        /// </summary>
        private double CalculateSimilarity(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2))
                return 1.0;
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
                return 0.0;

            var maxLen = Math.Max(s1.Length, s2.Length);
            if (maxLen == 0)
                return 1.0;

            var distance = LevenshteinDistance(s1, s2);
            return 1.0 - (distance / (double)maxLen);
        }

        private int LevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 0 : t.Length;
            if (string.IsNullOrEmpty(t)) return s.Length;

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }

        /// <summary>
        /// Builds a mapping from system content keys to template placeholder variations
        /// This allows the system to map its internal keys (DocumentTitle, Overview) to template placeholders ([PROJECT_NAME], etc.)
        /// </summary>
        private Dictionary<string, List<string>> BuildTagMapping(IEnumerable<string> systemKeys)
        {
            var mapping = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var key in systemKeys)
            {
                var variations = new List<string> { key }; // Always include the original key
                
                // CRITICAL: Handle variations with underscores, spaces, and parentheses
                var keyNormalized = key.Replace("_", " ").Replace("(", "").Replace(")", "");
                variations.Add(keyNormalized);
                variations.Add(key.Replace(" ", "_"));
                variations.Add(key.Replace("_", " "));
                
                // Map common system keys to template placeholder variations
                var keyLower = key.ToLowerInvariant();
                switch (keyLower)
                {
                    case "documenttitle":
                    case "projectname":
                    case "project name":
                        variations.AddRange(new[] { "PROJECT_NAME", "ProjectName", "Title", "DocumentTitle", "Project Title", "Project_Name", "Document_Name" });
                        break;
                    case "documenttype":
                    case "document type":
                        variations.AddRange(new[] { "DocumentType", "DocType", "Type", "Document Type", "Document_Type" });
                        break;
                    case "author":
                    case "author name":
                    case "author_name":
                    case "author(s)":
                    case "author_name(s)":
                        variations.AddRange(new[] { "AUTHOR_NAME(S)", "AuthorName", "Author", "Author(s)", "AUTHOR_NAME", "Author_Name" });
                        break;
                    case "reviewer":
                    case "reviewer name":
                    case "reviewer_name":
                    case "reviewer(s)":
                    case "reviewer_name(s)":
                        variations.AddRange(new[] { "REVIEWER_NAME(S)", "ReviewerName", "Reviewer", "Reviewer(s)", "REVIEWER_NAME", "Reviewer_Name" });
                        break;
                    case "approver":
                    case "approver name":
                    case "approver_name":
                    case "approver(s)":
                    case "approver_name(s)":
                        variations.AddRange(new[] { "APPROVER_NAME(S)", "ApproverName", "Approver", "Approver(s)", "APPROVER_NAME", "Approver_Name" });
                        break;
                    case "date":
                        variations.AddRange(new[] { "DATE", "Date", "DocumentDate", "Document_Date" });
                        break;
                    case "version":
                    case "version number":
                    case "version_number":
                        variations.AddRange(new[] { "VERSION_NUMBER", "Version", "VersionNumber", "Version_Number", "VERSION" });
                        break;
                    case "status":
                        variations.AddRange(new[] { "STATUS", "Status", "DocumentStatus", "Document_Status" });
                        break;
                    case "overview":
                        variations.AddRange(new[] { "Overview", "ExecutiveSummary", "Summary", "Purpose", "EXECUTIVE_SUMMARY", "2.1 Overview" });
                        break;
                    case "functional":
                        variations.AddRange(new[] { "Functional", "FunctionalRequirements", "Features", "FUNCTIONAL_REQUIREMENTS", "4. Functional Requirements" });
                        break;
                    case "technical":
                        variations.AddRange(new[] { "Technical", "TechnicalSpecifications", "Implementation", "TECHNICAL_SPECIFICATIONS", "6. Technical Specifications" });
                        break;
                    case "references":
                        variations.AddRange(new[] { "References", "RelatedDocuments", "Links", "RELATED_DOCUMENTS", "1.4 References" });
                        break;
                    case "classification":
                        variations.AddRange(new[] { "CLASSIFICATION", "Classification", "DocumentClassification", "Document_Classification" });
                        break;
                    case "projectoverview":
                    case "project_overview":
                        variations.AddRange(new[] { "PROJECT_OVERVIEW", "ProjectOverview", "Project Overview", "Project_Overview" });
                        break;
                    case "userstories":
                    case "user_stories":
                        variations.AddRange(new[] { "USER_STORIES", "UserStories", "User Stories", "User_Stories" });
                        break;
                    case "documentid":
                        variations.AddRange(new[] { "DOCUMENT_ID", "DocumentId", "DocumentID", "DocId" });
                        break;
                    case "tableofcontents":
                    case "table_of_contents":
                    case "toc":
                        variations.AddRange(new[] { "TABLE_OF_CONTENTS", "TableOfContents", "Table of Contents", "TOC" });
                        break;
                    case "remainingcontent":
                        variations.AddRange(new[]
                        {
                            "RemainingContent",
                            "Remaining Content",
                            "Remaining_Document_Content",
                            "Remaining Document Content",
                            "Document Content Starts Here",
                            "Document_Content_Starts_Here"
                        });
                        break;
                }
                
                mapping[key] = variations.Distinct().ToList();
            }
            
            _logger?.LogDebug("📋 [TEMPLATE] Built tag mapping for {Count} keys", mapping.Count);
            return mapping;
        }

        /// <summary>
        /// Generate a short document identifier suitable for header tables
        /// </summary>
        private string GenerateDocumentId()
        {
            // 16-character uppercase hex, stable-looking but generated per run
            return Guid.NewGuid().ToString("N").ToUpperInvariant().Substring(0, 16);
        }

        private string ExtractAfterMarker(string text, string[] markers)
        {
            var textLower = text.ToLowerInvariant();
            foreach (var marker in markers)
            {
                var markerLower = marker.ToLowerInvariant();
                var index = textLower.IndexOf(markerLower);
                if (index >= 0)
                {
                    var startIndex = index + marker.Length;
                    return text.Substring(startIndex).Trim();
                }
            }
            return text;
        }

        #endregion

        #endregion

        #region Region 4: Feedback1 - Enhanced Template Processing

        /// <summary>
        /// Insert complex content (multi-paragraph, tables, images) at token marker (Feedback1)
        /// </summary>
        private void InsertComplexContent(WordprocessingDocument doc, string tokenMarker, object content)
        {
            var body = doc.MainDocumentPart.Document.Body;
            var paragraphs = body.Elements<Paragraph>().ToList();
            
            for (int i = 0; i < paragraphs.Count; i++)
            {
                var para = paragraphs[i];
                var full = string.Concat(para.Descendants<Text>().Select(t => t.Text));
                if (!full.Contains(tokenMarker)) continue;

                // Remove the placeholder paragraph and insert replacement content at its position
                var insertionIndex = body.ChildElements.ToList().IndexOf(para);
                para.Remove();

                // Depending on content type, insert:
                if (content is string s)
                {
                    // Split by double newline into paragraphs
                    var parts = s.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.None);
                    foreach (var ptext in parts)
                    {
                        var trimmed = ptext ?? string.Empty;
                        var run = new Run(new Text(trimmed) { Space = SpaceProcessingModeValues.Preserve });
                        var newPara = new Paragraph(run);

                        // Try to apply heading styles for heading-like lines so TOC picks them up
                        // and the structure of the remaining content is preserved.
                        if (!string.IsNullOrWhiteSpace(trimmed))
                        {
                            var level = GetHeadingLevel(string.Empty, trimmed);
                            if (level > 0)
                            {
                                // Clamp level to 1-3 to match common TOC ranges
                                var styleName = level <= 1 ? "Heading1" :
                                                level == 2 ? "Heading2" : "Heading3";

                                newPara.ParagraphProperties ??= new ParagraphProperties();
                                newPara.ParagraphProperties.ParagraphStyleId = new ParagraphStyleId { Val = styleName };
                            }
                        }

                        body.InsertAt(newPara, insertionIndex++);
                    }
                }
                else if (content is Models.TableDto table)
                {
                    // Build OpenXML table and insert
                    var openXmlTable = BuildOpenXmlTable(table);
                    body.InsertAt(openXmlTable, insertionIndex++);
                }
                else if (content is IEnumerable<Models.TableDto> tableList)
                {
                    foreach (var tbl in tableList)
                    {
                        var openXmlTable = BuildOpenXmlTable(tbl);
                        body.InsertAt(openXmlTable, insertionIndex++);
                    }
                }
                else if (content is Models.ExtractedImage img)
                {
                    // Create image part and drawing - insert at this body index
                    InsertImageAtBodyIndex(doc, img.Bytes, insertionIndex++);
                }
                else if (content is IEnumerable<Models.ExtractedImage> imgs)
                {
                    foreach (var im in imgs)
                        InsertImageAtBodyIndex(doc, im.Bytes, insertionIndex++);
                }
                // else unknown types - serialize as text
                else
                {
                    var newPara = new Paragraph(new Run(new Text(content?.ToString() ?? "") { Space = SpaceProcessingModeValues.Preserve }));
                    body.InsertAt(newPara, insertionIndex++);
                }
            }
        }

        /// <summary>
        /// Insert the raw body of a source DOCX (with original styles, lists, images)
        /// at the paragraph containing the given marker token.
        /// </summary>
        private void InsertRawBodyAtMarker(WordprocessingDocument outputDoc, string sourceDocPath, string markerToken)
        {
            if (string.IsNullOrWhiteSpace(sourceDocPath) || string.IsNullOrWhiteSpace(markerToken))
                return;

            if (outputDoc.MainDocumentPart?.Document?.Body == null)
                return;

            var body = outputDoc.MainDocumentPart.Document.Body;
            var paragraphs = body.Elements<Paragraph>().ToList();

            for (int i = 0; i < paragraphs.Count; i++)
            {
                var para = paragraphs[i];
                var full = string.Concat(para.Descendants<Text>().Select(t => t.Text ?? string.Empty));
                if (string.IsNullOrEmpty(full) || !full.Contains(markerToken, StringComparison.OrdinalIgnoreCase))
                    continue;

                var insertionIndex = body.ChildElements.ToList().IndexOf(para);
                para.Remove();

                try
                {
                    using var srcDoc = WordprocessingDocument.Open(sourceDocPath, false);
                    var srcBody = srcDoc.MainDocumentPart?.Document?.Body;
                    if (srcBody == null)
                        return;

                    // Clone each top-level element from the source body into the output body
                    foreach (var elem in srcBody.ChildElements)
                    {
                        var cloned = elem.CloneNode(true);
                        body.InsertAt(cloned, insertionIndex++);
                    }

                    _logger?.LogInformation("✅ [RAW-CONTENT] Inserted raw DOCX body from source at marker '{Marker}'", markerToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "⚠️ [RAW-CONTENT] Failed to insert raw body from source DOCX: {Error}", ex.Message);
                }

                break;
            }
        }

        /// <summary>
        /// Insert a real Word TOC field at the given placeholder token.
        /// This ensures the document has a dynamic TOC that Word / Word Online can render with page numbers.
        /// </summary>
        private void InsertTocFieldAtPlaceholder(WordprocessingDocument doc, string placeholderToken, string? staticToc = null)
        {
            if (doc?.MainDocumentPart?.Document?.Body == null || string.IsNullOrWhiteSpace(placeholderToken))
                return;

            var body = doc.MainDocumentPart.Document.Body;
            var paragraphs = body.Elements<Paragraph>().ToList();

            for (int i = 0; i < paragraphs.Count; i++)
            {
                var para = paragraphs[i];
                var full = string.Concat(para.Descendants<Text>().Select(t => t.Text));
                if (string.IsNullOrEmpty(full) || !full.Contains(placeholderToken, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Remove the placeholder paragraph and insert TOC content at its position
                var insertionIndex = body.ChildElements.ToList().IndexOf(para);
                para.Remove();

                // Optional heading above TOC if template does not already provide one
                var headingPara = new Paragraph(new Run(new Text("Table of Contents")))
                {
                    ParagraphProperties = new ParagraphProperties(
                        new ParagraphStyleId() { Val = "TOCHeading" },
                        new SpacingBetweenLines() { After = "240" })
                };
                body.InsertAt(headingPara, insertionIndex++);

                // Optional: insert a static TOC list (headings only) so that Markdown and
                // simple viewers show something even if the Word TOC field isn't updated.
                if (!string.IsNullOrWhiteSpace(staticToc))
                {
                    var lines = staticToc.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var run = new Run(new Text(line) { Space = SpaceProcessingModeValues.Preserve });
                        var tocListPara = new Paragraph(run)
                        {
                            ParagraphProperties = new ParagraphProperties(
                                new SpacingBetweenLines() { After = "40" })
                        };
                        body.InsertAt(tocListPara, insertionIndex++);
                    }
                }

                // TOC field paragraph (same switches as BuildDocx: \o "1-3" \h \z \u)
                var tocParagraph = new Paragraph();
                var tocRun = new Run();

                tocRun.Append(new FieldChar() { FieldCharType = FieldCharValues.Begin });
                tocRun.Append(new FieldCode(" TOC \\o \"1-3\" \\h \\z \\u ") { Space = SpaceProcessingModeValues.Preserve });
                tocRun.Append(new FieldChar() { FieldCharType = FieldCharValues.Separate });
                tocRun.Append(new FieldChar() { FieldCharType = FieldCharValues.End });

                tocParagraph.Append(tocRun);
                tocParagraph.ParagraphProperties = new ParagraphProperties(
                    new SpacingBetweenLines() { After = "240" });

                body.InsertAt(tocParagraph, insertionIndex);

                break;
            }
        }

        /// <summary>
        /// Feedback1: Build OpenXML Table from TableDto with support for merged cells and complex structures
        /// </summary>
        private Table BuildOpenXmlTable(Models.TableDto tbl)
        {
            var table = new Table();
            var tableProps = new TableProperties(
                new TableBorders(
                    new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                    new BottomBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                    new LeftBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                    new RightBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                    new InsideHorizontalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                    new InsideVerticalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 }
                ),
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct }
            );
            table.AppendChild(tableProps);
            
            // Feedback1: Enhanced table rendering - handle merged cells if TableDto supports it
            // For now, render basic table structure
            foreach (var row in tbl.Rows)
            {
                var tr = new TableRow();
                foreach (var cellText in row)
                {
                    var tc = new TableCell(new Paragraph(new Run(new Text(cellText ?? ""))));
                    var tcProps = new TableCellProperties(
                        new TableCellWidth { Type = TableWidthUnitValues.Auto },
                        new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Top }
                    );
                    tc.Append(tcProps);
                    tr.Append(tc);
                }
                table.Append(tr);
            }
            
            // Feedback1: If TableDto has merged cell information, apply it
            // This would require extending TableDto to include merge information
            // For now, we render the basic structure
            
            return table;
        }

        /// <summary>
        /// Insert image at specific body index (Feedback1)
        /// </summary>
        private void InsertImageAtBodyIndex(WordprocessingDocument doc, byte[] bytes, int bodyIndex)
        {
            var main = doc.MainDocumentPart;
            
            // Feedback2: Resize and compress images before embedding
            byte[] processedBytes = bytes;
            if (ImageResizeHelper.ShouldResize(bytes))
            {
                processedBytes = ImageResizeHelper.ResizeAndCompressImage(bytes, maxWidth: 1200, jpegQuality: 80, _logger);
            }
            
            var imagePartType = DetectImageFormat(processedBytes);
            var imagePart = main.AddImagePart(imagePartType);
            using var stream = new MemoryStream(processedBytes);
            imagePart.FeedData(stream);

            var element = new Paragraph(new Run(
                new DocumentFormat.OpenXml.Wordprocessing.Drawing(
                    new DocumentFormat.OpenXml.Drawing.Wordprocessing.Inline(
                        new DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent { Cx = 990000L, Cy = 792000L },
                        new DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties { Id = (UInt32Value)1U, Name = "Image" },
                        new DocumentFormat.OpenXml.Drawing.Wordprocessing.NonVisualGraphicFrameDrawingProperties(
                            new DocumentFormat.OpenXml.Drawing.GraphicFrameLocks { NoChangeAspect = true }),
                        new DocumentFormat.OpenXml.Drawing.Graphic(
                            new DocumentFormat.OpenXml.Drawing.GraphicData(
                                new DocumentFormat.OpenXml.Drawing.Pictures.Picture(
                                    new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureProperties(
                                        new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualDrawingProperties { Id = 0U, Name = "Pic" },
                                        new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureDrawingProperties()),
                                    new DocumentFormat.OpenXml.Drawing.Pictures.BlipFill(
                                        new DocumentFormat.OpenXml.Drawing.Blip { Embed = main.GetIdOfPart(imagePart) },
                                        new DocumentFormat.OpenXml.Drawing.Stretch(new DocumentFormat.OpenXml.Drawing.FillRectangle())),
                                    new DocumentFormat.OpenXml.Drawing.Pictures.ShapeProperties(
                                        new DocumentFormat.OpenXml.Drawing.Transform2D(new DocumentFormat.OpenXml.Drawing.Offset(), new DocumentFormat.OpenXml.Drawing.Extents()),
                                        new DocumentFormat.OpenXml.Drawing.PresetGeometry(new DocumentFormat.OpenXml.Drawing.AdjustValueList()) { Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Rectangle }))
                            ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })))));
            
            doc.MainDocumentPart.Document.Body.InsertAt(element, bodyIndex);
        }

        /// <summary>
        /// Converts a template file (.dotx) to a regular document (.docx) to avoid Word template warnings
        /// </summary>
        private void ConvertTemplateToDocument(string filePath)
        {
            try
            {
                _logger?.LogDebug("🔄 [TEMPLATE] Converting template to document: {FilePath}", filePath);
                
                // Check if file is actually a template by opening it
                bool isTemplate = false;
                using (var templateDoc = WordprocessingDocument.Open(filePath, false))
                {
                    isTemplate = templateDoc.DocumentType == WordprocessingDocumentType.Template;
                }
                
                if (!isTemplate)
                {
                    _logger?.LogDebug("✅ [TEMPLATE] File is already a document, no conversion needed");
                    return;
                }
                
                // Create a temporary file for the new document
                var tempPath = Path.Combine(Path.GetDirectoryName(filePath) ?? Path.GetTempPath(), 
                    Path.GetFileNameWithoutExtension(filePath) + "_temp.docx");
                
                // Open template and create new document
                using (var templateDoc = WordprocessingDocument.Open(filePath, false))
                using (var newDoc = WordprocessingDocument.Create(tempPath, WordprocessingDocumentType.Document))
                {
                    // Copy MainDocumentPart
                    var templateMainPart = templateDoc.MainDocumentPart;
                    if (templateMainPart != null)
                    {
                        var newMainPart = newDoc.AddMainDocumentPart();
                        using (var stream = templateMainPart.GetStream())
                        {
                            newMainPart.FeedData(stream);
                        }
                        
                        // Copy all related parts (styles, numbering, settings, etc.)
                        foreach (var part in templateMainPart.Parts)
                        {
                            try
                            {
                                var newPart = newMainPart.AddPart(part.OpenXmlPart, part.RelationshipId);
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogWarning(ex, "⚠️ [TEMPLATE] Could not copy part {PartType}: {Error}", 
                                    part.OpenXmlPart.GetType().Name, ex.Message);
                            }
                        }
                    }
                    
                    // Copy other parts (header parts, footer parts, etc.)
                    foreach (var part in templateDoc.Parts)
                    {
                        if (part.OpenXmlPart is MainDocumentPart) continue; // Already copied
                        
                        try
                        {
                            newDoc.AddPart(part.OpenXmlPart, part.RelationshipId);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "⚠️ [TEMPLATE] Could not copy part {PartType}: {Error}", 
                                part.OpenXmlPart.GetType().Name, ex.Message);
                        }
                    }
                    
                    newDoc.MainDocumentPart?.Document?.Save();
                }
                
                // Replace original file with converted document
                File.Delete(filePath);
                File.Move(tempPath, filePath);
                
                _logger?.LogInformation("✅ [TEMPLATE] Successfully converted template to document");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "⚠️ [TEMPLATE] Failed to convert template to document: {Error}. File will remain as template.", ex.Message);
                // Don't throw - allow processing to continue even if conversion fails
            }
        }

        #endregion

        #region Region 4: Section-Level Structural Mapping

        /// <summary>
        /// Represents a template section with its heading and placeholders
        /// </summary>
        private class TemplateSectionInfo
        {
            public string? Heading { get; set; }
            public List<TemplatePlaceholder> Placeholders { get; set; } = new();
            public int Level { get; set; } // Heading level (1, 2, 3, etc.)
        }

        /// <summary>
        /// Represents the template structure
        /// </summary>
        private class TemplateStructure
        {
            public List<TemplateSectionInfo> Sections { get; set; } = new();
        }

        /// <summary>
        /// Represents a mapping between template section and raw document section
        /// </summary>
        private class SectionMapping
        {
            public string? TemplateHeading { get; set; }
            public string? RawHeading { get; set; }
            public Section? RawSection { get; set; }
            public double Confidence { get; set; }
        }

        /// <summary>
        /// Parse template structure: extract sections, headings, and their placeholders
        /// </summary>
        private TemplateStructure ParseTemplateStructure(string templatePath)
        {
            var structure = new TemplateStructure();
            
            try
            {
                using var doc = WordprocessingDocument.Open(templatePath, false);
                var mainPart = doc.MainDocumentPart;
                if (mainPart?.Document?.Body == null)
                {
                    _logger?.LogWarning("⚠️ [TEMPLATE-STRUCTURE] Template body is null");
                    return structure;
                }

                var paragraphs = mainPart.Document.Body.Descendants<Paragraph>().ToList();
                TemplateSectionInfo? currentSection = null;
                var allPlaceholders = ExtractAllPlaceholdersFromTemplate(templatePath);
                var placeholderMap = allPlaceholders.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

                foreach (var para in paragraphs)
                {
                    var paraText = string.Concat(para.Descendants<Text>().Select(t => t.Text ?? ""));
                    if (string.IsNullOrWhiteSpace(paraText)) continue;

                    // Check if this paragraph is a heading
                    var style = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "";
                    var isHeading = IsHeadingStyle(style) || IsLikelyHeading(paraText);
                    var headingLevel = GetHeadingLevel(style, paraText);

                    if (isHeading && headingLevel > 0)
                    {
                        // Save current section if it exists
                        if (currentSection != null)
                        {
                            structure.Sections.Add(currentSection);
                        }

                        // Start new section
                        currentSection = new TemplateSectionInfo
                        {
                            Heading = CleanHeading(paraText),
                            Level = headingLevel,
                            Placeholders = new List<TemplatePlaceholder>()
                        };
                        _logger?.LogDebug("📑 [TEMPLATE-STRUCTURE] Found section: '{Heading}' (Level {Level})", 
                            currentSection.Heading, headingLevel);
                    }
                    else if (currentSection != null)
                    {
                        // Check if this paragraph contains placeholders
                        var bracketMatches = System.Text.RegularExpressions.Regex.Matches(paraText, @"\[([^\]]+)\]");
                        foreach (System.Text.RegularExpressions.Match match in bracketMatches)
                        {
                            var placeholderName = match.Groups[1].Value.Trim();
                            if (placeholderMap.TryGetValue(placeholderName, out var placeholder))
                            {
                                if (!currentSection.Placeholders.Any(p => p.Name.Equals(placeholderName, StringComparison.OrdinalIgnoreCase)))
                                {
                                    currentSection.Placeholders.Add(placeholder);
                                    _logger?.LogDebug("   📌 Placeholder in section: '{Placeholder}'", placeholderName);
                                }
                            }
                        }
                    }
                }

                // Add final section
                if (currentSection != null)
                {
                    structure.Sections.Add(currentSection);
                }

                _logger?.LogInformation("✅ [TEMPLATE-STRUCTURE] Parsed {Count} sections from template", structure.Sections.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ [TEMPLATE-STRUCTURE] Error parsing template structure: {Error}", ex.Message);
            }

            return structure;
        }

        /// <summary>
        /// Build section mapping: match template sections to raw document sections semantically
        /// </summary>
        private List<SectionMapping> BuildSectionMapping(TemplateStructure templateStructure, DocumentModel docModel)
        {
            var mappings = new List<SectionMapping>();
            
            if (docModel.Sections == null || docModel.Sections.Count == 0)
            {
                _logger?.LogWarning("⚠️ [SECTION-MAPPING] No sections in raw document");
                return mappings;
            }

            _logger?.LogInformation("🔍 [SECTION-MAPPING] Matching {TemplateCount} template sections to {RawCount} raw sections", 
                templateStructure.Sections.Count, docModel.Sections.Count);

            foreach (var templateSection in templateStructure.Sections)
            {
                if (string.IsNullOrWhiteSpace(templateSection.Heading))
                    continue;

                var bestMatch = FindBestMatchingSection(templateSection.Heading, docModel.Sections);
                
                if (bestMatch != null)
                {
                    var mapping = new SectionMapping
                    {
                        TemplateHeading = templateSection.Heading,
                        RawHeading = bestMatch.Heading,
                        RawSection = bestMatch,
                        Confidence = CalculateSectionSimilarity(templateSection.Heading, bestMatch.Heading ?? "")
                    };
                    mappings.Add(mapping);
                    
                    _logger?.LogDebug("✅ [SECTION-MAPPING] Matched '{Template}' -> '{Raw}' (confidence: {Confidence:P0})", 
                        templateSection.Heading, bestMatch.Heading, mapping.Confidence);
                }
                else
                {
                    _logger?.LogDebug("⏭️ [SECTION-MAPPING] No match found for template section: '{Heading}'", templateSection.Heading);
                }
            }

            return mappings;
        }

        /// <summary>
        /// Find best matching section in raw document for a template section heading
        /// </summary>
        private Section? FindBestMatchingSection(string templateHeading, List<Section> rawSections)
        {
            if (rawSections == null || rawSections.Count == 0)
                return null;

            var templateHeadingClean = NormalizeHeading(templateHeading);
            var bestMatch = rawSections
                .Select(section => new
                {
                    Section = section,
                    Heading = section.Heading ?? "",
                    CleanHeading = NormalizeHeading(section.Heading ?? ""),
                    Similarity = CalculateSectionSimilarity(templateHeading, section.Heading ?? "")
                })
                .Where(x => x.Similarity > 0.3) // Minimum similarity threshold
                .OrderByDescending(x => x.Similarity)
                .FirstOrDefault();

            return bestMatch?.Section;
        }

        /// <summary>
        /// Calculate similarity between two section headings
        /// </summary>
        private double CalculateSectionSimilarity(string heading1, string heading2)
        {
            if (string.IsNullOrWhiteSpace(heading1) || string.IsNullOrWhiteSpace(heading2))
                return 0;

            var h1 = NormalizeHeading(heading1);
            var h2 = NormalizeHeading(heading2);

            // Exact match
            if (h1.Equals(h2, StringComparison.OrdinalIgnoreCase))
                return 1.0;

            // Contains match
            if (h1.Contains(h2, StringComparison.OrdinalIgnoreCase) || 
                h2.Contains(h1, StringComparison.OrdinalIgnoreCase))
                return 0.8;

            // Word overlap
            var words1 = h1.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.ToLowerInvariant()).ToHashSet();
            var words2 = h2.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.ToLowerInvariant()).ToHashSet();

            var intersection = words1.Intersect(words2).Count();
            var union = words1.Union(words2).Count();

            if (union == 0) return 0;
            return (double)intersection / union;
        }

        /// <summary>
        /// Normalize heading text for comparison (remove numbers, special chars, etc.)
        /// </summary>
        private string NormalizeHeading(string heading)
        {
            if (string.IsNullOrWhiteSpace(heading))
                return "";

            // Remove section numbers (e.g., "1.1", "2.3.1", etc.)
            var normalized = System.Text.RegularExpressions.Regex.Replace(heading, @"^\d+\.?\d*\.?\d*\s*", "");
            
            // Remove special characters, keep only alphanumeric and spaces
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^\w\s-]", "");
            
            return normalized.Trim();
        }

        /// <summary>
        /// Clean heading text (remove markdown, extra spaces, etc.)
        /// </summary>
        private string CleanHeading(string heading)
        {
            if (string.IsNullOrWhiteSpace(heading))
                return "";

            // Remove markdown headers
            var cleaned = System.Text.RegularExpressions.Regex.Replace(heading, @"^#+\s*", "");
            
            // Remove extra whitespace
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ");
            
            return cleaned.Trim();
        }

        /// <summary>
        /// Check if style indicates a heading
        /// </summary>
        private bool IsHeadingStyle(string styleId)
        {
            if (string.IsNullOrWhiteSpace(styleId))
                return false;

            var sid = styleId.Replace(" ", "").ToLowerInvariant();
            return sid.StartsWith("heading") || sid.StartsWith("title");
        }

        /// <summary>
        /// Check if text looks like a heading
        /// </summary>
        private bool IsLikelyHeading(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 200)
                return false;

            // Markdown headers
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^#+\s+"))
                return true;

            // Numbered sections (e.g., "1. ", "2.1 ", "3.2.1 ")
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^\d+\.?\d*\.?\d*\s+[A-Z]"))
                return true;

            // Short, title-case text
            if (text.Length < 100 && char.IsUpper(text[0]) && text.Split(' ').Length <= 8)
                return true;

            return false;
        }

        /// <summary>
        /// Get heading level from style or text
        /// </summary>
        private int GetHeadingLevel(string styleId, string text)
        {
            // Check style
            if (!string.IsNullOrWhiteSpace(styleId))
            {
                var sid = styleId.Replace(" ", "").ToLowerInvariant();
                if (sid.StartsWith("heading"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(sid, @"heading(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var level))
                        return level;
                }
            }

            // Check markdown headers
            var mdMatch = System.Text.RegularExpressions.Regex.Match(text, @"^(#+)\s+");
            if (mdMatch.Success)
                return mdMatch.Groups[1].Value.Length;

            // Check numbered sections (count dots)
            var numMatch = System.Text.RegularExpressions.Regex.Match(text, @"^(\d+\.?\d*\.?\d*)");
            if (numMatch.Success)
            {
                var numPart = numMatch.Groups[1].Value;
                return numPart.Split('.').Length;
            }

            return 0;
        }

        /// <summary>
        /// Find content for a placeholder within a specific section (section-aware matching)
        /// </summary>
        private string? FindContentForPlaceholderInSection(TemplatePlaceholder placeholder, Section section, string? fullText)
        {
            if (section == null)
                return null;

            _logger?.LogDebug("🔍 [SECTION-AWARE] Searching for '{Placeholder}' in section '{Heading}'", 
                placeholder.Name, section.Heading);

            var placeholderName = placeholder.Name;
            var placeholderLower = placeholderName.ToLowerInvariant();

            // For instructional placeholders, extract keywords and search in section
            if (placeholderName != placeholderName.ToUpperInvariant() && placeholderName.Length >= 10)
            {
                var keywords = ExtractKeywordsFromInstructionalText(placeholderLower);
                if (keywords.Count > 0)
                {
                    var sectionText = (section.Heading ?? "") + " " + (section.Body ?? "");
                    var keywordMatches = keywords.Count(kw => sectionText.ToLowerInvariant().Contains(kw));
                    
                    if (keywordMatches > 0)
                    {
                        // Return section body (limited length)
                        var content = section.Body ?? "";
                        if (content.Length > 2000)
                            content = content.Substring(0, 2000) + "...";
                        
                        _logger?.LogDebug("✅ [SECTION-AWARE] Found content via keyword matching ({Matches} keywords matched)", keywordMatches);
                        return content;
                    }
                }
            }

            // For explicit placeholders, try extraction methods within this section
            var sectionTextForSearch = (section.Heading ?? "") + "\n" + (section.Body ?? "");
            
            // Try exact field matching within section
            if (placeholderLower.Contains("project") && !placeholderLower.Contains("author") && !placeholderLower.Contains("reviewer"))
            {
                var projectName = ExtractProjectNameFromText(sectionTextForSearch);
                if (!string.IsNullOrWhiteSpace(projectName))
                    return projectName;
            }

            if (placeholderLower.Contains("version"))
            {
                var version = ExtractVersionFromText(sectionTextForSearch);
                if (!string.IsNullOrWhiteSpace(version))
                    return version;
            }

            if (placeholderLower.Contains("date") && !placeholderLower.Contains("updated"))
            {
                var date = ExtractDateFromText(sectionTextForSearch);
                if (!string.IsNullOrWhiteSpace(date))
                    return date;
            }

            // Fallback: return section body if it's not too long
            var body = section.Body ?? "";
            if (!string.IsNullOrWhiteSpace(body) && body.Length <= 2000)
            {
                _logger?.LogDebug("✅ [SECTION-AWARE] Using section body as content");
                return body;
            }

            return null;
        }

        /// <summary>
        /// Extract project name from text (helper for section-aware matching)
        /// </summary>
        private string? ExtractProjectNameFromText(string text)
        {
            var patterns = new[]
            {
                @"(?:project\s*name|project)\s*:?\s*\*?\*?\s*([A-Z][A-Za-z0-9\s\-&]+)(?=\s*$|\s*\\|\s*\n|\s+\d+\.|\s+##|\s+\*\*)",
                @"\*\*project\s*name\*\*\s*:?\s*([A-Z][A-Za-z0-9\s\-&]+)(?=\s*$|\s*\\|\s*\n|\s+\d+\.|\s+##|\s+\*\*)"
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(text, pattern, 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);
                if (match.Success)
                {
                    var name = match.Groups[1].Value.Trim().Trim('*', '\\', ')', ']', '[', '(', '|');
                    if (name.Length >= 10 && name.Length < 100 && 
                        name.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries).Length >= 2)
                    {
                        return name;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Extract version from text (helper for section-aware matching)
        /// </summary>
        private string? ExtractVersionFromText(string text)
        {
            var patterns = new[]
            {
                @"(?:version|ver)\s*:?\s*\*?\*?\s*([0-9]+(?:\.[0-9]+)*)",
                @"\*\*version\*\*\s*:?\s*([0-9]+(?:\.[0-9]+)*)"
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(text, pattern, 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
            return null;
        }

        /// <summary>
        /// Extract date from text (helper for section-aware matching)
        /// </summary>
        private string? ExtractDateFromText(string text)
        {
            var patterns = new[]
            {
                @"(?:\*{0,2}(?:date|created)\*{0,2}\s*:?\s*\*{0,2})(\d{4}-\d{2}-\d{2})",
                @"(?:\*{0,2}(?:date|created)\*{0,2}\s*:?\s*\*{0,2})([A-Z][a-z]+\s+\d{4})(?:\s*\\|\s*$|\s*\n|\s*\d+\.)"
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(text, pattern, 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);
                if (match.Success)
                {
                    var dateStr = match.Groups[1].Value.Trim().Trim('*', '\\');
                    if (System.Text.RegularExpressions.Regex.IsMatch(dateStr, @"^[A-Z][a-z]+\s+\d{4}$"))
                    {
                        if (DateTime.TryParse($"1 {dateStr}", out var date))
                            return date.ToString("yyyy-MM-dd");
                    }
                    else if (DateTime.TryParse(dateStr, out var date))
                    {
                        return date.ToString("yyyy-MM-dd");
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Stub method for old matching strategies (not used in new architecture)
        /// </summary>
        private bool IsDocumentTitleContentStub(string? content, string? documentTitle, string placeholderName)
        {
            if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(documentTitle))
                return false;

            var placeholderLower = placeholderName.ToLowerInvariant();
            var isDocumentTitleField = placeholderLower.Contains("document title") ||
                                      (placeholderLower.Contains("title") && !placeholderLower.Contains("project"));

            if (isDocumentTitleField)
                return false; // If the placeholder is for the document title, don't reject it

            var titleTrimmed = documentTitle.Trim();
            var contentTrimmed = content.Trim();

            // Exact match
            if (contentTrimmed.Equals(titleTrimmed, StringComparison.OrdinalIgnoreCase))
                return true;

            // Contains match (for cases where document title is embedded in short content)
            if (contentTrimmed.Length <= 100 && 
                contentTrimmed.Contains(titleTrimmed, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        #endregion
    }
}

