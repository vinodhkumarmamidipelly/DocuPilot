using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Drawing.Wordprocessing;
using DocumentFormat.OpenXml.Drawing.Pictures;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using Microsoft.Extensions.Logging;

namespace SMEPilot.FunctionApp.Services
{
    /// <summary>
    /// TemplateFiller: Production-grade template enrichment with template inspection and simplified mapping
    /// Uses two-pass approach: 1) Inspect template, 2) Fill intelligently
    /// </summary>
    public static class TemplateFiller
    {
        /// <summary>
        /// Inspects the template to find all content control tags
        /// </summary>
        public static List<string> InspectTemplate(string templatePath, ILogger? logger = null)
        {
            var tags = new List<string>();
            
            if (!File.Exists(templatePath))
            {
                logger?.LogWarning("⚠️ [TEMPLATE] Template not found: {Path}", templatePath);
                return tags;
            }

            try
            {
                using (var wordDoc = WordprocessingDocument.Open(templatePath, false))
                {
                    var mainPart = wordDoc.MainDocumentPart;
                    if (mainPart?.Document?.Body == null)
                    {
                        logger?.LogWarning("⚠️ [TEMPLATE] Template has no body");
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
                
                logger?.LogInformation("🔍 [TEMPLATE] Found {Count} content controls: {Tags}", 
                    tags.Count, string.Join(", ", tags));
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "❌ [TEMPLATE] Error inspecting template: {Error}", ex.Message);
            }

            return tags;
        }

        /// <summary>
        /// Fills a Word template (.dotx) with content using simplified, reliable mapping
        /// </summary>
        public static string FillTemplate(
            string templatePath,
            string outputPath,
            Dictionary<string, string> contentMap,
            List<byte[]>? screenshots = null,
            List<(string version, string date, string author, string changes)>? revisions = null,
            ILogger? logger = null)
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
                logger?.LogDebug("📄 [TEMPLATE] Starting template fill: {TemplatePath} -> {OutputPath}", templatePath, outputPath);

                // Step 1: Inspect template to see what tags exist
                var availableTags = InspectTemplate(templatePath, logger);
                logger?.LogInformation("📋 [TEMPLATE] Template has {Count} content controls", availableTags.Count);
                
                // Log what we're trying to fill
                logger?.LogInformation("📝 [TEMPLATE] Content map has {Count} entries: {Keys}", 
                    contentMap.Count, string.Join(", ", contentMap.Keys));

                // Copy template to output location
                File.Copy(templatePath, outputPath, true);
                logger?.LogDebug("✅ [TEMPLATE] Template copied to output path");

                var filledTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var unfilledTags = new List<string>();

                // Open and fill the document
                using (var wordDoc = WordprocessingDocument.Open(outputPath, true))
                {
                    var mainPart = wordDoc.MainDocumentPart;
                    if (mainPart?.Document?.Body == null)
                        throw new InvalidOperationException("Template document is missing main document part or body");

                    var body = mainPart.Document.Body;

                    // Find all structured document tags (content controls)
                    var sdtList = body.Descendants<SdtElement>().ToList();
                    logger?.LogDebug("🔍 [TEMPLATE] Found {Count} SDT elements in template", sdtList.Count);

                    // Fill text content controls
                    foreach (var sdt in sdtList)
                    {
                        var tag = sdt.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;
                        if (string.IsNullOrEmpty(tag))
                        {
                            logger?.LogDebug("⏭️ [TEMPLATE] SDT element has no tag, skipping");
                            continue;
                        }

                        // Use case-insensitive matching
                        var matchingKey = contentMap.Keys.FirstOrDefault(k => 
                            string.Equals(k, tag, StringComparison.OrdinalIgnoreCase));

                        if (matchingKey != null)
                        {
                            string newText = contentMap[matchingKey] ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(newText))
                            {
                                if (ReplaceSdtContent(sdt, newText, logger))
                                {
                                    filledTags.Add(tag);
                                    logger?.LogInformation("✅ [TEMPLATE] Filled content control: {Tag} ({Length} chars)", tag, newText.Length);
                                }
                                else
                                {
                                    logger?.LogWarning("⚠️ [TEMPLATE] Failed to fill content control: {Tag}", tag);
                                    unfilledTags.Add(tag);
                                }
                            }
                            else
                            {
                                logger?.LogDebug("⏭️ [TEMPLATE] Skipping empty content for tag: {Tag}", tag);
                            }
                        }
                        else
                        {
                            logger?.LogDebug("⏭️ [TEMPLATE] No content provided for tag: {Tag}", tag);
                        }
                    }

                    // Handle screenshots
                    if (screenshots != null && screenshots.Count > 0)
                    {
                        var screenshotControl = sdtList.FirstOrDefault(s => 
                            s.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value?.Equals("Screenshots", StringComparison.OrdinalIgnoreCase) == true);
                        if (screenshotControl != null)
                        {
                            logger?.LogDebug("🖼️ [TEMPLATE] Inserting {Count} images into Screenshots section", screenshots.Count);
                            foreach (var img in screenshots)
                            {
                                InsertImage(mainPart, screenshotControl, img, logger);
                            }
                        }
                        else
                        {
                            logger?.LogWarning("⚠️ [TEMPLATE] Screenshots content control not found");
                        }
                    }

                    // Handle RevisionHistory table expansion
                    ExpandRevisionHistoryTable(body, mainPart, revisions, logger);

                    // Add page breaks before H1 sections
                    AddPageBreaksBeforeH1(body, logger);

                    // Save the document
                    mainPart.Document.Save();
                    logger?.LogDebug("💾 [TEMPLATE] Document saved successfully");
                }

                // Log summary
                logger?.LogInformation("✅ [TEMPLATE] Template fill completed. Filled {FilledCount}/{TotalCount} tags. Unfilled: {Unfilled}", 
                    filledTags.Count, availableTags.Count, unfilledTags.Count > 0 ? string.Join(", ", unfilledTags) : "none");

                return outputPath;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "❌ [TEMPLATE] Error filling template: {Error}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Replaces content in a structured document tag - returns true if successful
        /// </summary>
        private static bool ReplaceSdtContent(SdtElement sdt, string newText, ILogger? logger)
        {
            if (sdt == null || string.IsNullOrWhiteSpace(newText)) return false;

            try
            {
                // Handle block-level content controls (SdtBlock -> SdtContentBlock)
                if (sdt is SdtBlock sdtBlock && sdtBlock.SdtContentBlock != null)
                {
                    sdtBlock.SdtContentBlock.RemoveAllChildren();
                    
                    // Split text into paragraphs
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
                            var paragraph = new Paragraph(run);
                            sdtBlock.SdtContentBlock.AppendChild(paragraph);
                        }
                    }
                    return true;
                }
                // Handle run-level content controls (SdtRun -> SdtContentRun)
                else if (sdt is SdtRun sdtRun && sdtRun.SdtContentRun != null)
                {
                    sdtRun.SdtContentRun.RemoveAllChildren();
                    var run = new Run(new Text(newText));
                    sdtRun.SdtContentRun.AppendChild(run);
                    return true;
                }
                // Handle cell-level content controls (SdtCell -> SdtContentCell)
                else if (sdt is SdtCell sdtCell && sdtCell.SdtContentCell != null)
                {
                    sdtCell.SdtContentCell.RemoveAllChildren();
                    var run = new Run(new Text(newText));
                    var paragraph = new Paragraph(run);
                    sdtCell.SdtContentCell.AppendChild(paragraph);
                    return true;
                }
                else
                {
                    // Fallback: try to find any content element
                    var contentElement = sdt.Elements().FirstOrDefault(e => 
                        e.LocalName == "sdtContent" || 
                        e.LocalName == "sdtContentBlock" || 
                        e.LocalName == "sdtContentRun" ||
                        e.LocalName == "sdtContentCell");
                    
                    if (contentElement != null)
                    {
                        contentElement.RemoveAllChildren();
                        var run = new Run(new Text(newText));
                        var paragraph = new Paragraph(run);
                        contentElement.AppendChild(paragraph);
                        return true;
                    }
                    
                    logger?.LogWarning("⚠️ [TEMPLATE] Could not find content element in SDT");
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "❌ [TEMPLATE] Error replacing content in SDT: {Error}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Inserts an image into a content control as an inline image
        /// </summary>
        private static void InsertImage(MainDocumentPart mainPart, SdtElement sdt, byte[] imgBytes, ILogger? logger)
        {
            if (mainPart == null || sdt == null || imgBytes == null || imgBytes.Length == 0)
                return;

            try
            {
                var imagePartType = DetectImageFormat(imgBytes);
                var imagePart = mainPart.AddImagePart(imagePartType);
                using (var stream = new MemoryStream(imgBytes))
                {
                    imagePart.FeedData(stream);
                }

                var imageRelId = mainPart.GetIdOfPart(imagePart);
                var (widthEmu, heightEmu) = CalculateImageDimensions(imgBytes);
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

                logger?.LogDebug("✅ [TEMPLATE] Image inserted successfully");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "❌ [TEMPLATE] Error inserting image: {Error}", ex.Message);
            }
        }

        private static ImagePartType DetectImageFormat(byte[] imgBytes)
        {
            if (imgBytes.Length < 8) return ImagePartType.Png;
            if (imgBytes[0] == 0x89 && imgBytes[1] == 0x50 && imgBytes[2] == 0x4E && imgBytes[3] == 0x47) return ImagePartType.Png;
            if (imgBytes[0] == 0xFF && imgBytes[1] == 0xD8) return ImagePartType.Jpeg;
            if (imgBytes[0] == 0x47 && imgBytes[1] == 0x49 && imgBytes[2] == 0x46) return ImagePartType.Gif;
            if (imgBytes[0] == 0x42 && imgBytes[1] == 0x4D) return ImagePartType.Bmp;
            return ImagePartType.Png;
        }

        private static (long widthEmu, long heightEmu) CalculateImageDimensions(byte[] imgBytes)
        {
            try
            {
                int width = 0, height = 0;
                if (imgBytes.Length >= 24 && imgBytes[0] == 0x89 && imgBytes[1] == 0x50 && imgBytes[2] == 0x4E && imgBytes[3] == 0x47)
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
                else if (imgBytes.Length >= 10 && imgBytes[0] == 0x47 && imgBytes[1] == 0x49 && imgBytes[2] == 0x46)
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
            catch { }
            return (576000L, 432000L);
        }

        private static Drawing CreateImageDrawing(string imageRelId, long widthEmu, long heightEmu)
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

            return new Drawing(inline);
        }

        private static void ExpandRevisionHistoryTable(
            Body body,
            MainDocumentPart mainPart,
            List<(string version, string date, string author, string changes)>? revisions,
            ILogger? logger)
        {
            try
            {
                var revisionControl = body.Descendants<SdtElement>()
                    .FirstOrDefault(s => s.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value?.Equals("RevisionHistory", StringComparison.OrdinalIgnoreCase) == true);

                if (revisionControl == null)
                {
                    logger?.LogDebug("⏭️ [TEMPLATE] RevisionHistory content control not found");
                    return;
                }

                var table = revisionControl.Descendants<Table>().FirstOrDefault();
                if (table == null)
                {
                    logger?.LogDebug("⏭️ [TEMPLATE] No table found in RevisionHistory control");
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
                    logger?.LogDebug("✅ [TEMPLATE] Added {Count} revision history rows", revisions.Count);
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
                logger?.LogWarning(ex, "⚠️ [TEMPLATE] Error expanding revision history table: {Error}", ex.Message);
            }
        }

        private static void AddPageBreaksBeforeH1(Body body, ILogger? logger)
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
                        logger?.LogDebug("✅ [TEMPLATE] Added page break before Heading1");
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "⚠️ [TEMPLATE] Error adding page breaks: {Error}", ex.Message);
            }
        }
    }
}

