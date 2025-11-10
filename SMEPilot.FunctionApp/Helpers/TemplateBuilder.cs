using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using SMEPilot.FunctionApp.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using System.Text;
using Drawing = DocumentFormat.OpenXml.Drawing;
using DrawingWordprocessing = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace SMEPilot.FunctionApp.Helpers
{
    public class TemplateBuilder
    {
        public static byte[] BuildDocxBytes(DocumentModel model, List<byte[]> images)
        {
            using var mem = new MemoryStream();
            using (var doc = WordprocessingDocument.Create(mem, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                
                // Create styles part for proper formatting
                var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
                stylesPart.Styles = GenerateStyles();
                
                mainPart.Document = new Document(new Body());
                var body = mainPart.Document.Body;

                // 1. Cover Page Section
                AddCoverPage(body, model);

                // 2. Table of Contents (Word Field)
                AddTableOfContents(body);

                // 3. Document Sections with Heading Hierarchy
                AddSectionsWithHierarchy(body, model);

                // 4. Images Section (properly embedded)
                if (images != null && images.Count > 0)
                {
                    AddImagesSection(body, mainPart, images);
                }

                // 5. Revision History
                AddRevisionHistory(body);

                mainPart.Document.Save();
            }
            return mem.ToArray();
        }

        private static void AddCoverPage(Body body, DocumentModel model)
        {
            // Cover page title
            if (!string.IsNullOrWhiteSpace(model.Title))
            {
                body.Append(new Paragraph(new Run(new Text(model.Title)))
                {
                    ParagraphProperties = new ParagraphProperties(
                        new ParagraphStyleId() { Val = "Title" },
                        new SpacingBetweenLines() { After = "240" })
                });
            }

            // Cover page metadata (if available)
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

            // Page break after cover
            body.Append(new Paragraph(new Run(new Break() { Type = BreakValues.Page })));
        }

        private static void AddTableOfContents(Body body)
        {
            // TOC Heading
            body.Append(new Paragraph(new Run(new Text("Table of Contents")))
            {
                ParagraphProperties = new ParagraphProperties(
                    new ParagraphStyleId() { Val = "TOCHeading" },
                    new SpacingBetweenLines() { After = "240" })
            });

            // Create TOC field using FieldChar and FieldCode
            var tocParagraph = new Paragraph();
            var tocRun = new Run();
            
            // Field start
            tocRun.Append(new FieldChar() { FieldCharType = FieldCharValues.Begin });
            
            // Field code: TOC \o "1-3" \h \z \u
            // \o "1-3" = Include heading levels 1-3
            // \h = Create hyperlinks
            // \z = Hide tab leader and page numbers in Web view
            // \u = Use heading styles (Heading1, Heading2, Heading3)
            // Note: Word will need to update this field to show content
            tocRun.Append(new FieldCode(" TOC \\o \"1-3\" \\h \\z \\u ") { Space = SpaceProcessingModeValues.Preserve });
            
            // Field separator
            tocRun.Append(new FieldChar() { FieldCharType = FieldCharValues.Separate });
            
            // Field end
            tocRun.Append(new FieldChar() { FieldCharType = FieldCharValues.End });
            
            tocParagraph.Append(tocRun);
            tocParagraph.ParagraphProperties = new ParagraphProperties(
                new SpacingBetweenLines() { After = "240" });
            
            body.Append(tocParagraph);

            // Page break after TOC
            body.Append(new Paragraph(new Run(new Break() { Type = BreakValues.Page })));
        }

        private static void AddSectionsWithHierarchy(Body body, DocumentModel model)
        {
            int headingLevel = 1; // Track heading hierarchy
            
            foreach (var section in model.Sections)
            {
                // Determine heading level based on section position and content
                // First section is always H1, subsequent sections can be H2 if they seem like subsections
                var styleName = DetermineHeadingStyle(section, headingLevel, model.Sections);
                
                // Add heading with proper style
                var headingPara = new Paragraph(new Run(new Text(section.Heading)))
                {
                    ParagraphProperties = new ParagraphProperties(
                        new ParagraphStyleId() { Val = styleName },
                        new SpacingBetweenLines() { After = "120" })
                };
                body.Append(headingPara);

                // Add bookmark for navigation (helps Copilot jump to sections)
                if (!string.IsNullOrWhiteSpace(section.Heading))
                {
                    // Sanitize bookmark name and ID (Word has restrictions)
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
                    
                    // Insert bookmark around heading
                    headingPara.InsertBefore(bookmarkStart, headingPara.GetFirstChild<Run>());
                    headingPara.InsertAfter(bookmarkEnd, headingPara.GetFirstChild<Run>());
                }

                // Add summary if available
                if (!string.IsNullOrWhiteSpace(section.Summary))
                {
                    body.Append(new Paragraph(new Run(new Text("Summary: " + section.Summary)))
                    {
                        ParagraphProperties = new ParagraphProperties(
                            new ParagraphStyleId() { Val = "Normal" },
                            new SpacingBetweenLines() { After = "120" })
                    });
                }

                // Add body content
                if (!string.IsNullOrWhiteSpace(section.Body))
                {
                    // Split body into paragraphs for better formatting
                    // Try multiple splitting strategies
                    var bodyText = section.Body;
                    
                    // First, try splitting by double newlines (paragraph breaks)
                    var paragraphs = bodyText.Split(new[] { "\n\n", "\r\n\r\n", "\n\r\n\r" }, StringSplitOptions.RemoveEmptyEntries);
                    
                    // If that doesn't work well, also split by single newlines for long content
                    if (paragraphs.Length == 1 && bodyText.Length > 500)
                    {
                        // Split by single newlines and group related lines
                        var lines = bodyText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                        var grouped = new List<string>();
                        var currentGroup = new StringBuilder();
                        
                        foreach (var line in lines)
                        {
                            var trimmed = line.Trim();
                            if (string.IsNullOrWhiteSpace(trimmed)) continue;
                            
                            // If line ends with period/exclamation/question, it's end of sentence
                            // If next line starts with capital and is short, might be new paragraph
                            if (currentGroup.Length > 0 && 
                                (trimmed.EndsWith(".") || trimmed.EndsWith("!") || trimmed.EndsWith("?")) &&
                                currentGroup.Length > 100)
                            {
                                grouped.Add(currentGroup.ToString());
                                currentGroup.Clear();
                            }
                            
                            if (currentGroup.Length > 0) currentGroup.Append(" ");
                            currentGroup.Append(trimmed);
                            
                            // If group is getting long, start new paragraph
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
                    
                    // Add paragraphs
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

        private static string DetermineHeadingStyle(Section section, int position, List<Section> allSections)
        {
            // First section is always Heading1
            if (position == 1)
                return "Heading1";

            // Check if this looks like a subsection (shorter heading, similar to previous)
            var heading = section.Heading ?? "";
            var previousHeading = position > 1 ? allSections[position - 2].Heading ?? "" : "";

            // If heading is much shorter and seems related to previous, it might be H2
            if (heading.Length < previousHeading.Length * 0.7 && 
                heading.Length < 50 &&
                !heading.Contains(".") && 
                !heading.Contains(":"))
            {
                // Check if previous section was H1 (odd position)
                if (position % 2 == 0)
                    return "Heading2";
            }

            // Check for numbered subsections (1.1, 1.2, etc.)
            if (System.Text.RegularExpressions.Regex.IsMatch(heading, @"^\d+\.\d+"))
            {
                return "Heading2";
            }

            // Default: alternate between H1 and H2 for better structure
            return position % 2 == 1 ? "Heading1" : "Heading2";
        }

        private static void AddImagesSection(Body body, MainDocumentPart mainPart, List<byte[]> images)
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
                    // Detect image format
                    var imagePartType = DetectImageFormat(imgBytes);
                    
                    // Add image part
                    var imagePart = mainPart.AddImagePart(imagePartType);
                    using var imgStream = new MemoryStream(imgBytes);
                    imagePart.FeedData(imgStream);

                    // Get image relationship ID
                    var imageRelId = mainPart.GetIdOfPart(imagePart);

                    // Calculate dimensions preserving aspect ratio
                    var (widthEmu, heightEmu) = CalculateImageDimensions(imgBytes);
                    var altText = $"Figure {idx}: Image from document";

                    // Create drawing element using proper OpenXML classes
                    var inline = CreateImageInline(imageRelId, widthEmu, heightEmu, idx, altText);

                    // Wrap inline in Drawing element (required by Word structure)
                    var drawing = CreateDrawingWrapper(inline);

                    // Add image paragraph
                    var imagePara = new Paragraph();
                    var run = new Run();
                    run.AppendChild(drawing);
                    imagePara.Append(run);
                    imagePara.ParagraphProperties = new ParagraphProperties(
                        new Justification() { Val = JustificationValues.Center },
                        new SpacingBetweenLines() { After = "120" });
                    
                    body.Append(imagePara);

                    // Add caption with alt text
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
                    // Log error but continue (TemplateBuilder doesn't have ILogger)
                    // Add error note instead of placeholder
                    body.Append(new Paragraph(new Run(new Text($"Figure {idx}: Image could not be embedded ({ex.Message})")))
                    {
                        ParagraphProperties = new ParagraphProperties(
                            new Justification() { Val = JustificationValues.Center },
                            new SpacingBetweenLines() { After = "240" })
                    });
                }
            }
        }

        private static ImagePartType DetectImageFormat(byte[] imgBytes)
        {
            if (imgBytes.Length < 8)
                return ImagePartType.Png;

            // PNG signature: 89 50 4E 47
            if (imgBytes[0] == 0x89 && imgBytes[1] == 0x50 && imgBytes[2] == 0x4E && imgBytes[3] == 0x47)
                return ImagePartType.Png;

            // JPEG signature: FF D8
            if (imgBytes[0] == 0xFF && imgBytes[1] == 0xD8)
                return ImagePartType.Jpeg;

            // GIF signature: 47 49 46
            if (imgBytes[0] == 0x47 && imgBytes[1] == 0x49 && imgBytes[2] == 0x46)
                return ImagePartType.Gif;

            // BMP signature: 42 4D
            if (imgBytes[0] == 0x42 && imgBytes[1] == 0x4D)
                return ImagePartType.Bmp;

            return ImagePartType.Png; // Default
        }

        private static (long widthEmu, long heightEmu) CalculateImageDimensions(byte[] imgBytes)
        {
            try
            {
                // Read image dimensions from binary headers (cross-platform approach)
                int width = 0, height = 0;
                
                // PNG: bytes 16-23 contain width and height (4 bytes each, big-endian)
                if (imgBytes.Length >= 24 && 
                    imgBytes[0] == 0x89 && imgBytes[1] == 0x50 && imgBytes[2] == 0x4E && imgBytes[3] == 0x47)
                {
                    width = (imgBytes[16] << 24) | (imgBytes[17] << 16) | (imgBytes[18] << 8) | imgBytes[19];
                    height = (imgBytes[20] << 24) | (imgBytes[21] << 16) | (imgBytes[22] << 8) | imgBytes[23];
                }
                // JPEG: requires parsing SOF markers (simplified - check common positions)
                else if (imgBytes.Length >= 20 && imgBytes[0] == 0xFF && imgBytes[1] == 0xD8)
                {
                    // JPEG dimensions are in SOF markers - simplified approach
                    // Look for common SOF markers (0xFFC0, 0xFFC1, 0xFFC2)
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
                // GIF: bytes 6-9 contain width and height (2 bytes each, little-endian)
                else if (imgBytes.Length >= 10 && 
                         imgBytes[0] == 0x47 && imgBytes[1] == 0x49 && imgBytes[2] == 0x46)
                {
                    width = imgBytes[6] | (imgBytes[7] << 8);
                    height = imgBytes[8] | (imgBytes[9] << 8);
                }
                
                if (width > 0 && height > 0)
                {
                    // Max width: 6 inches (15.24 cm) = 576000 EMU
                    // Preserve aspect ratio
                    const long maxWidthEmu = 576000L; // 6 inches
                    const long emuPerPixel = 9525L; // 1 pixel = 9525 EMU (96 DPI)
                    
                    var widthEmu = (long)width * emuPerPixel;
                    var heightEmu = (long)height * emuPerPixel;
                    
                    // Scale down if too large
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
                // Fall through to default
            }
            
            // Fallback to default size if image can't be read
            return (576000L, 432000L); // 6" x 4.5" (4:3 ratio)
        }

        private static DrawingWordprocessing.Inline CreateImageInline(string imageRelId, long widthEmu, long heightEmu, int idx, string altText)
        {
            // Create picture element
            var picture = new Drawing.Pictures.Picture(
                new Drawing.Pictures.NonVisualPictureProperties(
                    new Drawing.Pictures.NonVisualDrawingProperties() 
                    { 
                        Id = (UInt32Value)(idx + 1U), 
                        Name = $"Image {idx}",
                        Description = altText // Alt text for accessibility
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

        private static OpenXmlUnknownElement CreateDrawingWrapper(DrawingWordprocessing.Inline inline)
        {
            // Create Drawing element in wordprocessingml namespace (w:drawing)
            // The Drawing element in a Run must be in wordprocessingml namespace, NOT wordprocessingDrawing
            // This is critical for SharePoint/Word Online compatibility
            var drawing = new OpenXmlUnknownElement(
                "w:drawing",
                "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
            
            // Add the inline as a child
            drawing.AppendChild(inline);
            
            return drawing;
        }

        private static void AddRevisionHistory(Body body)
        {
            body.Append(new Paragraph(new Run(new Break() { Type = BreakValues.Page })));
            
            body.Append(new Paragraph(new Run(new Text("Revision History")))
            {
                ParagraphProperties = new ParagraphProperties(
                    new ParagraphStyleId() { Val = "Heading1" },
                    new SpacingBetweenLines() { Before = "480", After = "240" })
            });

            // Create revision history table
            var table = new Table(
                new TableProperties(
                    new TableBorders(
                        new TopBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new BottomBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new LeftBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new RightBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new InsideHorizontalBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new InsideVerticalBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 })),
                
                // Header row
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
                
                // Initial revision
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

        private static Styles GenerateStyles()
        {
            var styles = new Styles();
            
            // Title style
            styles.Append(new Style(new StyleName() { Val = "Title" }, new BasedOn() { Val = "Normal" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "Title",
                StyleParagraphProperties = new StyleParagraphProperties(
                    new SpacingBetweenLines() { After = "240" })
            });

            // Subtitle style
            styles.Append(new Style(new StyleName() { Val = "Subtitle" }, new BasedOn() { Val = "Normal" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "Subtitle"
            });

            // Heading1 style
            styles.Append(new Style(new StyleName() { Val = "Heading 1" }, new BasedOn() { Val = "Normal" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "Heading1",
                StyleParagraphProperties = new StyleParagraphProperties(
                    new KeepNext(),
                    new SpacingBetweenLines() { Before = "240", After = "120" })
            });

            // Heading2 style
            styles.Append(new Style(new StyleName() { Val = "Heading 2" }, new BasedOn() { Val = "Normal" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "Heading2",
                StyleParagraphProperties = new StyleParagraphProperties(
                    new KeepNext(),
                    new SpacingBetweenLines() { Before = "180", After = "120" })
            });

            // Heading3 style
            styles.Append(new Style(new StyleName() { Val = "Heading 3" }, new BasedOn() { Val = "Normal" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "Heading3",
                StyleParagraphProperties = new StyleParagraphProperties(
                    new KeepNext(),
                    new SpacingBetweenLines() { Before = "120", After = "60" })
            });

            // TOCHeading style
            styles.Append(new Style(new StyleName() { Val = "TOC Heading" }, new BasedOn() { Val = "Heading1" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "TOCHeading"
            });

            // Caption style
            styles.Append(new Style(new StyleName() { Val = "Caption" }, new BasedOn() { Val = "Normal" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "Caption",
                StyleParagraphProperties = new StyleParagraphProperties(
                    new SpacingBetweenLines() { After = "120" })
            });

            // Table styles
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
    }
}
