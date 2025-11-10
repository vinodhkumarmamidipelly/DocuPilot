using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Helper for OCR (Optical Character Recognition) using Azure Computer Vision
    /// </summary>
    public class OcrHelper
    {
        private readonly Config _cfg;
        private readonly HttpClient _httpClient;
        private readonly ILogger<OcrHelper>? _logger;

        public OcrHelper(Config cfg, ILogger<OcrHelper>? logger = null)
        {
            _cfg = cfg;
            _logger = logger;
            _httpClient = new HttpClient();
            
            // Set up Azure Computer Vision API endpoint and key
            var endpoint = cfg.AzureVisionEndpoint;
            var key = cfg.AzureVisionKey;
            
            if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(key))
            {
                _httpClient.BaseAddress = new Uri(endpoint);
                _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", key);
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }
        }

        /// <summary>
        /// Extract text from image using Azure Computer Vision OCR
        /// </summary>
        public async Task<string> ExtractTextFromImageAsync(byte[] imageBytes)
        {
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

