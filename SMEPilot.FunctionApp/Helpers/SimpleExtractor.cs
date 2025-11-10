using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Spreadsheet;
using Spire.Pdf;
using Spire.Pdf.Graphics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SMEPilot.FunctionApp.Helpers
{
    public class SimpleExtractor
    {
        private readonly ILogger<SimpleExtractor>? _logger;

        public SimpleExtractor(ILogger<SimpleExtractor>? logger = null)
        {
            _logger = logger;
        }
        /// <summary>
        /// Extract text and images from DOCX file
        /// </summary>
        public async Task<(string Text, List<byte[]> Images)> ExtractDocxAsync(Stream docxStream)
        {
            var textBuilder = new StringBuilder();
            var images = new List<byte[]>();

            using var ms = new MemoryStream();
            await docxStream.CopyToAsync(ms);
            ms.Position = 0;

            WordprocessingDocument? doc = null;
            try
            {
                doc = WordprocessingDocument.Open(ms, false);
                var body = doc.MainDocumentPart?.Document?.Body;
                if (body != null)
                {
                    textBuilder.AppendLine(body.InnerText);
                }

                // Extract images
                var imageParts = doc.MainDocumentPart?.ImageParts;
                if (imageParts != null)
                {
                    foreach (var imgPart in imageParts)
                    {
                        using var imgStream = imgPart.GetStream();
                        using var ims = new MemoryStream();
                        await imgStream.CopyToAsync(ims);
                        images.Add(ims.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                // If stream is not a valid DOCX (e.g., mock content), extract raw text
                _logger?.LogWarning(ex, "Warning: Stream is not a valid DOCX file: {Error}. Attempting to extract as plain text.", ex.Message);
                ms.Position = 0;
                using var reader = new StreamReader(ms);
                textBuilder.AppendLine(await reader.ReadToEndAsync());
                // No images for mock content
            }
            finally
            {
                doc?.Dispose();
            }

            return (textBuilder.ToString(), images);
        }

        /// <summary>
        /// Extract text and images from PPTX file
        /// </summary>
        public async Task<(string Text, List<byte[]> Images)> ExtractPptxAsync(Stream pptxStream)
        {
            var textBuilder = new StringBuilder();
            var images = new List<byte[]>();

            using var ms = new MemoryStream();
            await pptxStream.CopyToAsync(ms);
            ms.Position = 0;

            PresentationDocument? doc = null;
            try
            {
                doc = PresentationDocument.Open(ms, false);
                var presentationPart = doc.PresentationPart;
                
                if (presentationPart?.Presentation?.SlideIdList != null)
                {
                    var slideIds = presentationPart.Presentation.SlideIdList.Elements<SlideId>();
                    
                    foreach (var slideId in slideIds)
                    {
                        var slidePart = presentationPart.GetPartById(slideId.RelationshipId) as SlidePart;
                        if (slidePart?.Slide?.CommonSlideData?.ShapeTree != null)
                        {
                            // Extract text from shapes
                            var shapes = slidePart.Slide.CommonSlideData.ShapeTree.Elements<DocumentFormat.OpenXml.Presentation.Shape>();
                            foreach (var shape in shapes)
                            {
                                if (shape.TextBody != null)
                                {
                                    textBuilder.AppendLine(shape.TextBody.InnerText);
                                }
                            }
                            
                            // Extract images from slide
                            var imageParts = slidePart.ImageParts;
                            foreach (var imgPart in imageParts)
                            {
                                using var imgStream = imgPart.GetStream();
                                using var ims = new MemoryStream();
                                await imgStream.CopyToAsync(ims);
                                images.Add(ims.ToArray());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Warning: Failed to extract from PPTX: {Error}", ex.Message);
                throw new InvalidOperationException($"Failed to extract content from PPTX file: {ex.Message}", ex);
            }
            finally
            {
                doc?.Dispose();
            }

            return (textBuilder.ToString(), images);
        }

        /// <summary>
        /// Extract text and images from XLSX file
        /// </summary>
        public async Task<(string Text, List<byte[]> Images)> ExtractXlsxAsync(Stream xlsxStream)
        {
            var textBuilder = new StringBuilder();
            var images = new List<byte[]>();

            using var ms = new MemoryStream();
            await xlsxStream.CopyToAsync(ms);
            ms.Position = 0;

            SpreadsheetDocument? doc = null;
            try
            {
                doc = SpreadsheetDocument.Open(ms, false);
                var workbookPart = doc.WorkbookPart;
                
                if (workbookPart?.Workbook?.Sheets != null)
                {
                    var sheets = workbookPart.Workbook.Sheets.Elements<Sheet>();
                    
                    foreach (var sheet in sheets)
                    {
                        textBuilder.AppendLine($"Sheet: {sheet.Name}");
                        
                        var worksheetPart = workbookPart.GetPartById(sheet.Id) as WorksheetPart;
                        if (worksheetPart?.Worksheet != null)
                        {
                            var sheetData = worksheetPart.Worksheet.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.SheetData>();
                            if (sheetData != null)
                            {
                                var rows = sheetData.Elements<Row>();
                            foreach (var row in rows)
                            {
                                var cellValues = new List<string>();
                                var cells = row.Elements<Cell>();
                                
                                foreach (var cell in cells)
                                {
                                    var cellValue = GetCellValue(cell, workbookPart);
                                    if (!string.IsNullOrWhiteSpace(cellValue))
                                    {
                                        cellValues.Add(cellValue);
                                    }
                                }
                                
                                if (cellValues.Any())
                                {
                                    textBuilder.AppendLine(string.Join(" | ", cellValues));
                                }
                            }
                            }
                        }
                        
                        // Extract images from worksheet
                        if (worksheetPart != null)
                        {
                            var imageParts = worksheetPart.ImageParts;
                            foreach (var imgPart in imageParts)
                            {
                                using var imgStream = imgPart.GetStream();
                                using var ims = new MemoryStream();
                                await imgStream.CopyToAsync(ims);
                                images.Add(ims.ToArray());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Warning: Failed to extract from XLSX: {Error}", ex.Message);
                throw new InvalidOperationException($"Failed to extract content from XLSX file: {ex.Message}", ex);
            }
            finally
            {
                doc?.Dispose();
            }

            return (textBuilder.ToString(), images);
        }

        /// <summary>
        /// Get cell value from Excel cell (handles shared strings)
        /// </summary>
        private string GetCellValue(Cell cell, WorkbookPart workbookPart)
        {
            if (cell.CellValue == null)
                return string.Empty;

            var value = cell.CellValue.Text;
            
            // Handle shared strings
            if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
            {
                var sharedStringTablePart = workbookPart.SharedStringTablePart;
                if (sharedStringTablePart != null && int.TryParse(value, out int index))
                {
                    var sharedStringTable = sharedStringTablePart.SharedStringTable;
                    if (sharedStringTable != null && index < sharedStringTable.Elements<DocumentFormat.OpenXml.Spreadsheet.SharedStringItem>().Count())
                    {
                        var sharedStringItem = sharedStringTable.Elements<DocumentFormat.OpenXml.Spreadsheet.SharedStringItem>().ElementAt(index);
                        return sharedStringItem.Text?.Text ?? value;
                    }
                }
            }
            
            return value ?? string.Empty;
        }

        /// <summary>
        /// Extract text and images from PDF file
        /// Uses Spire.PDF library (commercial license)
        /// </summary>
        public async Task<(string Text, List<byte[]> Images)> ExtractPdfAsync(Stream pdfStream)
        {
            var textBuilder = new StringBuilder();
            var images = new List<byte[]>();

            using var ms = new MemoryStream();
            await pdfStream.CopyToAsync(ms);
            ms.Position = 0;

            PdfDocument? document = null;
            try
            {
                document = new PdfDocument();
                document.LoadFromStream(ms);
                
                // Extract text from all pages
                for (int i = 0; i < document.Pages.Count; i++)
                {
                    var page = document.Pages[i];
                    
                    // Extract text from page
                    var pageText = page.ExtractText();
                    if (!string.IsNullOrWhiteSpace(pageText))
                    {
                        textBuilder.AppendLine(pageText);
                    }
                    
                    // Extract images from page
                    var pageImages = page.ExtractImages();
                    if (pageImages != null)
                    {
                        foreach (System.Drawing.Image image in pageImages)
                        {
                            try
                            {
                                // Convert Spire image to byte array
                                using var imageStream = new MemoryStream();
                                image.Save(imageStream, System.Drawing.Imaging.ImageFormat.Png);
                                images.Add(imageStream.ToArray());
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogWarning(ex, "Warning: Failed to extract image from PDF page {PageNumber}: {Error}", i + 1, ex.Message);
                                // Continue with other images
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Warning: Failed to extract from PDF: {Error}", ex.Message);
                throw new InvalidOperationException($"Failed to extract content from PDF file: {ex.Message}", ex);
            }
            finally
            {
                document?.Close();
            }

            return (textBuilder.ToString(), images);
        }

        /// <summary>
        /// Extract text from image file (with optional OCR)
        /// </summary>
        public async Task<(string Text, List<byte[]> Images)> ExtractImageAsync(Stream imageStream, OcrHelper? ocrHelper = null)
        {
            var textBuilder = new StringBuilder();
            var images = new List<byte[]>();

            // Store image as-is
            using var ms = new MemoryStream();
            await imageStream.CopyToAsync(ms);
            var imageBytes = ms.ToArray();
            images.Add(imageBytes);

            // Try OCR if available
            if (ocrHelper != null)
            {
                try
                {
                    _logger?.LogInformation("🔍 [OCR] Attempting to extract text from image using Azure Computer Vision...");
                    var ocrText = await ocrHelper.ExtractTextFromImageAsync(imageBytes);
                    if (!string.IsNullOrWhiteSpace(ocrText))
                    {
                        textBuilder.AppendLine(ocrText);
                        _logger?.LogInformation("✅ [OCR] Extracted {TextLength} characters from image", ocrText.Length);
                    }
                    else
                    {
                        textBuilder.AppendLine("[Image file - No text detected or OCR not configured]");
                        _logger?.LogWarning("⚠️ [OCR] No text extracted from image");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "⚠️ [OCR] OCR failed: {Error}. Storing image without text extraction.", ex.Message);
                    textBuilder.AppendLine("[Image file - OCR failed]");
                }
            }
            else
            {
                textBuilder.AppendLine("[Image file - OCR not configured. Add AzureVision_Endpoint and AzureVision_Key to enable OCR]");
                _logger?.LogInformation("ℹ️ [OCR] OCR helper not provided. Image stored without text extraction.");
            }
            
            return (textBuilder.ToString(), images);
        }
    }
}
