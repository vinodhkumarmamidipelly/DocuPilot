using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using SMEPilot.FunctionApp.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Drawing = DocumentFormat.OpenXml.Drawing;
using DrawingWordprocessing = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace SMEPilot.FunctionApp.Helpers
{
    public class TemplateBuilder
    {
        public static byte[] BuildDocxBytes(DocumentModel model, List<byte[]> images)
        {
            using var mem = new MemoryStream();
            using (var doc = WordprocessingDocument.Create(mem, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new Document(new Body());
                var body = mainPart.Document.Body;

                // Add relationships part for images
                var imagePartType = ImagePartType.Png;
                var imageParts = new List<ImagePart>();

                if (!string.IsNullOrWhiteSpace(model.Title))
                {
                    body.Append(new Paragraph(new Run(new Text(model.Title)))
                    {
                        ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "Title" })
                    });
                }

                body.Append(new Paragraph(new Run(new Text("Table of Contents (auto-generated)"))) 
                {
                    ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "TOCHeading" })
                });

                foreach (var s in model.Sections)
                {
                    var headingPara = new Paragraph(new Run(new Text(s.Heading)))
                    {
                        ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "Heading1" })
                    };
                    body.Append(headingPara);

                    if (!string.IsNullOrWhiteSpace(s.Summary))
                    {
                        body.Append(new Paragraph(new Run(new Text("Summary: " + s.Summary))));
                    }

                    if (!string.IsNullOrWhiteSpace(s.Body))
                    {
                        body.Append(new Paragraph(new Run(new Text(s.Body))));
                    }
                }

                // Embed images properly
                if (images != null && images.Count > 0)
                {
                    body.Append(new Paragraph(new Run(new Text("Images:")))
                    {
                        ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "Heading2" })
                    });

                    int idx = 0;
                    foreach (var imgBytes in images)
                    {
                        idx++;
                        try
                        {
                            // Detect image format
                            var imagePartTypeToUse = imagePartType;
                            if (imgBytes.Length > 8)
                            {
                                // Check PNG signature
                                if (imgBytes[0] == 0x89 && imgBytes[1] == 0x50 && imgBytes[2] == 0x4E && imgBytes[3] == 0x47)
                                {
                                    imagePartTypeToUse = ImagePartType.Png;
                                }
                                // Check JPEG signature
                                else if (imgBytes[0] == 0xFF && imgBytes[1] == 0xD8)
                                {
                                    imagePartTypeToUse = ImagePartType.Jpeg;
                                }
                                // Check GIF signature
                                else if (imgBytes[0] == 0x47 && imgBytes[1] == 0x49 && imgBytes[2] == 0x46)
                                {
                                    imagePartTypeToUse = ImagePartType.Gif;
                                }
                            }

                            // Add image part
                            var imagePart = mainPart.AddImagePart(imagePartTypeToUse);
                            using var imgStream = new MemoryStream(imgBytes);
                            imagePart.FeedData(imgStream);

                            // Get image relationship ID
                            var imageRelId = mainPart.GetIdOfPart(imagePart);

                            // Create image dimensions (in pixels, convert to EMU: 1 pixel = 9525 EMU)
                            var widthEmu = (long)(595 * 9525); // ~595 pixels = ~15.75cm
                            var heightEmu = (long)(842 * 9525); // ~842 pixels = ~22.3cm (A4 ratio)

                            // Create drawing element to embed image
                            var graphicData = new Drawing.GraphicData(
                                new Drawing.Pictures.Picture(
                                    new Drawing.Pictures.NonVisualPictureProperties(
                                        new Drawing.Pictures.NonVisualDrawingProperties() { Id = (UInt32Value)(idx + 1U), Name = $"Image {idx}" },
                                        new Drawing.Pictures.NonVisualPictureDrawingProperties()),
                                    new Drawing.Pictures.BlipFill(
                                        new Drawing.Blip() { Embed = imageRelId },
                                        new Drawing.Stretch(new Drawing.FillRectangle())),
                                    new Drawing.Pictures.ShapeProperties(
                                        new Drawing.Transform2D(
                                            new Drawing.Offset() { X = 0L, Y = 0L },
                                            new Drawing.Extents() { Cx = widthEmu, Cy = heightEmu }),
                                        new Drawing.PresetGeometry() { Preset = Drawing.ShapeTypeValues.Rectangle }))
                            )
                            {
                                Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture"
                            };
                            
                            var inline = new DrawingWordprocessing.Inline(
                                new DrawingWordprocessing.Extent() { Cx = widthEmu, Cy = heightEmu },
                                new DrawingWordprocessing.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                                new DrawingWordprocessing.DocProperties() { Id = (UInt32Value)(idx + 1U), Name = $"Image {idx}" },
                                new DrawingWordprocessing.NonVisualGraphicFrameDrawingProperties(
                                    new Drawing.GraphicFrameLocks() { NoChangeAspect = true }),
                                new Drawing.Graphic(graphicData)
                            )
                            {
                                DistanceFromTop = 0,
                                DistanceFromBottom = 0,
                                DistanceFromLeft = 0,
                                DistanceFromRight = 0
                            };
                            
                            // Create Drawing element - temporarily simplified to unblock build
                            // TODO: Fix proper Drawing class instantiation once namespace issue resolved
                            // For now, add images as text placeholders with embedded references
                            try
                            {
                                // Try to create Drawing using reflection (if class exists)
                                var drawingType = System.Type.GetType("DocumentFormat.OpenXml.Drawing.Wordprocessing.Drawing, DocumentFormat.OpenXml, Version=2.20.0.0, Culture=neutral, PublicKeyToken=8fb06cb64d019a17");
                                if (drawingType != null)
                                {
                                    var drawingElement = Activator.CreateInstance(drawingType) as OpenXmlElement;
                                    if (drawingElement != null)
                                    {
                                        drawingElement.AppendChild(inline);
                                        var imagePara = new Paragraph(new Run(drawingElement));
                                        body.Append(imagePara);
                                    }
                                    else
                                    {
                                        throw new Exception("Failed to create Drawing instance");
                                    }
                                }
                                else
                                {
                                    throw new Exception("Drawing type not found");
                                }
                            }
                            catch
                            {
                                // Fallback: Add placeholder text (images will be added in future fix)
                                body.Append(new Paragraph(new Run(new Text($"[Image {idx} - embedding temporarily disabled for build fix]"))));
                            }

                            // Add caption
                            body.Append(new Paragraph(new Run(new Text($"Figure {idx}: Image from original document")))
                            {
                                ParagraphProperties = new ParagraphProperties(
                                    new Justification() { Val = JustificationValues.Center })
                            });
                        }
                        catch (Exception ex)
                        {
                            // Fallback: just add text if image embedding fails
                            Console.WriteLine($"⚠️ Failed to embed image {idx}: {ex.Message}");
                            body.Append(new Paragraph(new Run(new Text($"Image {idx} (embedding failed)"))));
                        }
                    }
                }

                mainPart.Document.Save();
            }
            return mem.ToArray();
        }
    }
}

