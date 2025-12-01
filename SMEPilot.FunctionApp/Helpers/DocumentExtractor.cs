using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Spire.Pdf;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SMEPilot.FunctionApp.Helpers;
using SMEPilot.FunctionApp.Models;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Consolidated document extractor - extracts text and images from various file formats
    /// Merges: SimpleExtractor + OcrHelper
    /// </summary>
    public class DocumentExtractor
    {
        private readonly ILogger<DocumentExtractor>? _logger;
        private readonly Config? _cfg;
        private readonly HttpClient? _httpClient;

        public DocumentExtractor(ILogger<DocumentExtractor>? logger = null, Config? cfg = null)
        {
            _logger = logger;
            _cfg = cfg;
            
            // Initialize HTTP client for OCR if configured
            if (_cfg != null && !string.IsNullOrWhiteSpace(_cfg.AzureVisionEndpoint) && !string.IsNullOrWhiteSpace(_cfg.AzureVisionKey))
            {
                _httpClient = new HttpClient();
                _httpClient.BaseAddress = new Uri(_cfg.AzureVisionEndpoint);
                _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _cfg.AzureVisionKey);
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }
        }

        /// <summary>
        /// Extract structured data from DOCX file (Feedback1 - structured DTOs)
        /// Returns paragraphs with style info, tables, and images with anchor indices
        /// </summary>
        public (List<ParagraphDto> Paragraphs, List<TableDto> Tables, List<ExtractedImage> Images) ExtractDocxStructured(Stream docxStream)
        {
            var paragraphs = new List<ParagraphDto>();
            var tables = new List<TableDto>();
            var images = new List<ExtractedImage>();

            docxStream.Seek(0, SeekOrigin.Begin);
            using var ms = new MemoryStream();
            docxStream.CopyTo(ms);
            ms.Seek(0, SeekOrigin.Begin);

            WordprocessingDocument? doc = null;
            try
            {
                doc = WordprocessingDocument.Open(ms, false);
                var body = doc.MainDocumentPart?.Document?.Body;
                if (body == null) return (paragraphs, tables, images);

                int paraIndex = 0;
                foreach (var element in body.Elements())
                {
                    switch (element)
                    {
                        case Paragraph p:
                            // Preserve manual line breaks inside a Word paragraph so we
                            // can see distinct logical lines (e.g. Version/Date/Status/Project)
                            // instead of everything being flattened into a single string.
                            var sb = new StringBuilder();

                            foreach (var child in p.ChildElements)
                            {
                                if (child is DocumentFormat.OpenXml.Wordprocessing.Run run)
                                {
                                    foreach (var runChild in run.ChildElements)
                                    {
                                        if (runChild is DocumentFormat.OpenXml.Wordprocessing.Text t && !string.IsNullOrEmpty(t.Text))
                                        {
                                            sb.Append(t.Text);
                                        }
                                        else if (runChild is DocumentFormat.OpenXml.Wordprocessing.Break ||
                                                 runChild is DocumentFormat.OpenXml.Wordprocessing.CarriageReturn)
                                        {
                                            sb.AppendLine();
                                        }
                                    }
                                }
                                else if (child is DocumentFormat.OpenXml.Wordprocessing.Break ||
                                         child is DocumentFormat.OpenXml.Wordprocessing.CarriageReturn)
                                {
                                    sb.AppendLine();
                                }
                            }

                            var text = sb.ToString().Trim();

                            // Paragraph properties (style + numbering)
                            var pPr = p.ParagraphProperties ?? p.Elements<ParagraphProperties>().FirstOrDefault();
                            var styleId = pPr?.ParagraphStyleId?.Val?.Value;

                            // Detect list level (bullet / numbered lists) if present.
                            // IMPORTANT: in some docs, NumberingProperties is only exposed as a child element,
                            // not via the strongly-typed property, so we check both.
                            int? listLevel = null;
                            if (pPr != null)
                            {
                                var numPr = pPr.Elements<NumberingProperties>().FirstOrDefault() ?? pPr.NumberingProperties;
                                var ilvl = numPr?.NumberingLevelReference?.Val;
                                if (ilvl != null)
                                {
                                    listLevel = (int)ilvl.Value;
                                }
                            }

                            // If this paragraph is part of a list, prefix with a simple Markdown-style bullet.
                            // This makes bullets visible in downstream text/Markdown exports even though
                            // Word stores numbering separately from the paragraph text.
                            if (listLevel.HasValue && !string.IsNullOrWhiteSpace(text))
                            {
                                // Avoid double-prefixing if the text already looks like a bullet/numbered item.
                                var trimmed = text.TrimStart();
                                var looksLikeBullet = trimmed.StartsWith("- ") ||
                                                      trimmed.StartsWith("• ") ||
                                                      System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+\.\s+");
                                if (!looksLikeBullet)
                                {
                                    var indent = new string(' ', Math.Min(listLevel.Value, 4) * 2);
                                    text = $"{indent}- {text}";
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                paragraphs.Add(new ParagraphDto
                                {
                                    Index = paraIndex,
                                    Text = text,
                                    StyleId = styleId,
                                    ListLevel = listLevel
                                });
                            }
                            paraIndex++;
                            break;

                        case DocumentFormat.OpenXml.Wordprocessing.Table t:
                            var tableDto = TableDtoFromOpenXml(t);
                            tableDto.AnchorParagraphIndex = Math.Max(0, paraIndex - 1);
                            tables.Add(tableDto);
                            break;

                        default:
                            // Images can be in runs/drawings; check descendant drawings inside element
                            var drawings = element.Descendants<DocumentFormat.OpenXml.Wordprocessing.Drawing>();
                            foreach (var drawing in drawings)
                            {
                                var blip = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().FirstOrDefault();
                                if (blip?.Embed == null) continue;
                                var relId = blip.Embed.Value;
                                var part = doc.MainDocumentPart?.GetPartById(relId) as ImagePart;
                                if (part == null) continue;
                                using var partStream = part.GetStream();
                                using var mem = new MemoryStream();
                                partStream.CopyTo(mem);
                                images.Add(new ExtractedImage 
                                { 
                                    Id = Guid.NewGuid().ToString(), 
                                    Bytes = mem.ToArray(), 
                                    AnchorParagraphIndex = Math.Max(0, paraIndex - 1) 
                                });
                            }
                            break;
                    }
                }

                // Also extract images from ImageParts (for images not in drawings)
                if (doc.MainDocumentPart != null)
                {
                    foreach (var imagePart in doc.MainDocumentPart.ImageParts)
                    {
                        // Check if already added
                        if (images.Any(img => img.Bytes.Length > 0 && imagePart.GetStream().Length == img.Bytes.Length))
                            continue;

                        using var imgStream = imagePart.GetStream();
                        using var ims = new MemoryStream();
                        imgStream.CopyTo(ims);
                        images.Add(new ExtractedImage 
                        { 
                            Id = Guid.NewGuid().ToString(), 
                            Bytes = ims.ToArray(), 
                            AnchorParagraphIndex = Math.Max(0, paragraphs.Count - 1) 
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Warning: Failed to extract structured data from DOCX: {Error}", ex.Message);
            }
            finally
            {
                doc?.Dispose();
            }

            return (paragraphs, tables, images);
        }

        /// <summary>
        /// Converts OpenXML Table to TableDto
        /// </summary>
        private TableDto TableDtoFromOpenXml(DocumentFormat.OpenXml.Wordprocessing.Table t)
        {
            var dto = new TableDto();
            foreach (var row in t.Elements<TableRow>())
            {
                var cells = row.Elements<TableCell>().Select(c => 
                    string.Concat(c.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(x => x.Text)).Trim()).ToList();
                dto.Rows.Add(cells);
            }
            return dto;
        }

        /// <summary>
        /// Feedback2: Extract DOCX using temp file to avoid memory pressure on large files
        /// </summary>
        public async Task<(string Text, List<byte[]> Images)> ExtractDocxAsync(Stream docxStream)
        {
            var textBuilder = new StringBuilder();
            var images = new List<byte[]>();

            // Feedback2: Use TempFileLease instead of MemoryStream to avoid OOM on large files
            using var lease = new TempFileLease("extractor");
            var tempPath = lease.GetPath("input.docx");
            
            // Stream to temp file
            using (var fs = File.Create(tempPath))
            {
                await docxStream.CopyToAsync(fs);
            }

            WordprocessingDocument? doc = null;
            try
            {
                // Open from temp file instead of MemoryStream
                using var fileStream = File.OpenRead(tempPath);
                doc = WordprocessingDocument.Open(fileStream, false);
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
                _logger?.LogWarning(ex, "Warning: Stream is not a valid DOCX file: {Error}. Attempting to extract as plain text.", ex.Message);
                // Fallback: read as plain text from temp file
                using var reader = new StreamReader(tempPath);
                textBuilder.AppendLine(await reader.ReadToEndAsync());
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
        public async Task<(string Text, List<byte[]> Images)> ExtractImageAsync(Stream imageStream)
        {
            var textBuilder = new StringBuilder();
            var images = new List<byte[]>();

            // Store image as-is
            using var ms = new MemoryStream();
            await imageStream.CopyToAsync(ms);
            var imageBytes = ms.ToArray();
            images.Add(imageBytes);

            // Try OCR if configured
            if (_httpClient != null && _cfg != null)
            {
                try
                {
                    _logger?.LogInformation("🔍 [OCR] Attempting to extract text from image using Azure Computer Vision...");
                    var ocrText = await ExtractTextFromImageAsync(imageBytes);
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
                _logger?.LogInformation("ℹ️ [OCR] OCR not configured. Image stored without text extraction.");
            }
            
            return (textBuilder.ToString(), images);
        }

        /// <summary>
        /// Extract text from image using Azure Computer Vision OCR
        /// </summary>
        public async Task<string> ExtractTextFromImageAsync(byte[] imageBytes)
        {
            if (_cfg == null || _httpClient == null)
            {
                _logger?.LogWarning("⚠️ [OCR] Azure Computer Vision not configured. Skipping OCR.");
                return string.Empty;
            }

            var endpoint = _cfg.AzureVisionEndpoint;
            var key = _cfg.AzureVisionKey;

            // If OCR not configured, return empty
            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
            {
                _logger?.LogWarning("⚠️ [OCR] Azure Computer Vision not configured. Skipping OCR.");
                return string.Empty;
            }

            try
            {
                // Azure Computer Vision OCR API endpoint
                var ocrUrl = $"{endpoint.TrimEnd('/')}/vision/v3.2/read/analyze";
                
                // Send image for OCR
                using var content = new ByteArrayContent(imageBytes);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                
                var response = await _httpClient.PostAsync(ocrUrl, content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    _logger?.LogWarning("⚠️ [OCR] Failed to submit image for OCR: {StatusCode} - {ErrorText}", response.StatusCode, errorText);
                    return string.Empty;
                }

                // Get operation location
                var operationLocation = response.Headers.GetValues("Operation-Location").FirstOrDefault();
                if (string.IsNullOrWhiteSpace(operationLocation))
                {
                    _logger?.LogWarning("⚠️ [OCR] No operation location returned");
                    return string.Empty;
                }

                // Poll for results (Azure OCR is async)
                var result = await PollOcrResultAsync(operationLocation);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "⚠️ [OCR] Error during OCR: {Error}", ex.Message);
                return string.Empty;
            }
        }

        /// <summary>
        /// Poll Azure OCR API for results
        /// </summary>
        private async Task<string> PollOcrResultAsync(string operationLocation)
        {
            if (_httpClient == null) return string.Empty;

            var maxRetries = 20; // Max 20 seconds
            var delay = 1000; // 1 second

            for (int i = 0; i < maxRetries; i++)
            {
                await Task.Delay(delay);

                try
                {
                    var response = await _httpClient.GetAsync(operationLocation);
                    if (!response.IsSuccessStatusCode)
                    {
                        continue; // Keep polling
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<OcrResult>(json);

                    if (result?.Status == "succeeded")
                    {
                        // Extract text from all lines
                        var textBuilder = new StringBuilder();
                        if (result.AnalyzeResult?.ReadResults != null)
                        {
                            foreach (var page in result.AnalyzeResult.ReadResults)
                            {
                                if (page.Lines != null)
                                {
                                    foreach (var line in page.Lines)
                                    {
                                        if (!string.IsNullOrWhiteSpace(line.Text))
                                        {
                                            textBuilder.AppendLine(line.Text);
                                        }
                                    }
                                }
                            }
                        }
                        return textBuilder.ToString();
                    }
                    else if (result?.Status == "failed")
                    {
                        _logger?.LogWarning("⚠️ [OCR] OCR operation failed: {ErrorMessage}", result.Error?.Message ?? "Unknown error");
                        return string.Empty;
                    }
                    // If status is "running" or "notStarted", continue polling
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "⚠️ [OCR] Error polling OCR result: {Error}", ex.Message);
                    // Continue polling
                }
            }

            _logger?.LogWarning("⚠️ [OCR] OCR operation timed out");
            return string.Empty;
        }

        // Helper classes for JSON deserialization
        private class OcrResult
        {
            public string Status { get; set; } = string.Empty;
            public AnalyzeResult? AnalyzeResult { get; set; }
            public ErrorInfo? Error { get; set; }
        }

        private class AnalyzeResult
        {
            public ReadResult[]? ReadResults { get; set; }
        }

        private class ReadResult
        {
            public Line[]? Lines { get; set; }
        }

        private class Line
        {
            public string Text { get; set; } = string.Empty;
        }

        private class ErrorInfo
        {
            public string Message { get; set; } = string.Empty;
        }
    }
}

