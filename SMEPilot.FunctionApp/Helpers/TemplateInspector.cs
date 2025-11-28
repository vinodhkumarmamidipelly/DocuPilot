using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Utility to inspect Word templates and extract content controls, placeholders, and structure
    /// </summary>
    public class TemplateInspector
    {
        private readonly ILogger? _logger;

        public TemplateInspector(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Inspects a template file and returns detailed information about placeholders
        /// </summary>
        public TemplateInspectionResult InspectTemplate(string templatePath)
        {
            var result = new TemplateInspectionResult
            {
                TemplatePath = templatePath,
                Exists = File.Exists(templatePath)
            };

            if (!result.Exists)
            {
                _logger?.LogWarning("⚠️ Template file not found: {Path}", templatePath);
                return result;
            }

            try
            {
                using var doc = WordprocessingDocument.Open(templatePath, false);
                var mainPart = doc.MainDocumentPart;
                if (mainPart?.Document?.Body == null)
                {
                    _logger?.LogWarning("⚠️ Template has no body");
                    return result;
                }

                var body = mainPart.Document.Body;

                // Find Content Controls (SDT)
                var sdtList = body.Descendants<SdtElement>().ToList();
                result.ContentControlCount = sdtList.Count;

                foreach (var sdt in sdtList)
                {
                    var tag = sdt.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;
                    var alias = sdt.SdtProperties?.GetFirstChild<SdtAlias>()?.Val?.Value;
                    
                    // Try to get placeholder text from SdtContent
                    string placeholderText = "";
                    var sdtContent = sdt.GetFirstChild<SdtContentBlock>();
                    if (sdtContent != null)
                    {
                        var placeholderPara = sdtContent.Descendants<Paragraph>().FirstOrDefault();
                        if (placeholderPara != null)
                        {
                            placeholderText = string.Concat(placeholderPara.Descendants<Text>().Select(t => t.Text ?? ""));
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(tag))
                    {
                        result.ContentControls.Add(new ContentControlInfo
                        {
                            Tag = tag,
                            Title = alias ?? "",
                            PlaceholderText = placeholderText,
                            Type = sdt.GetType().Name
                        });
                    }
                }

                // Find plain text placeholders
                var paragraphs = body.Descendants<Paragraph>().ToList();
                result.ParagraphCount = paragraphs.Count;

                var placeholderPatterns = new[] { "{{", "}}", "{", "}", "[", "]" };
                var foundPlaceholders = new HashSet<string>();

                foreach (var para in paragraphs)
                {
                    var paraText = string.Concat(para.Descendants<Text>().Select(t => t.Text ?? ""));
                    
                    // Check for {{Tag}} pattern
                    var doubleBraceMatches = System.Text.RegularExpressions.Regex.Matches(paraText, @"\{\{(\w+)\}\}");
                    foreach (System.Text.RegularExpressions.Match match in doubleBraceMatches)
                    {
                        foundPlaceholders.Add(match.Groups[1].Value);
                    }

                    // Check for {Tag} pattern
                    var singleBraceMatches = System.Text.RegularExpressions.Regex.Matches(paraText, @"\{(\w+)\}");
                    foreach (System.Text.RegularExpressions.Match match in singleBraceMatches)
                    {
                        foundPlaceholders.Add(match.Groups[1].Value);
                    }

                    // Check for [Tag] pattern
                    var bracketMatches = System.Text.RegularExpressions.Regex.Matches(paraText, @"\[(\w+)\]");
                    foreach (System.Text.RegularExpressions.Match match in bracketMatches)
                    {
                        foundPlaceholders.Add(match.Groups[1].Value);
                    }
                }

                result.PlainTextPlaceholders = foundPlaceholders.ToList();

                _logger?.LogInformation("✅ Template inspection complete:");
                _logger?.LogInformation("   Content Controls: {Count}", result.ContentControlCount);
                _logger?.LogInformation("   Plain Text Placeholders: {Count}", result.PlainTextPlaceholders.Count);
                _logger?.LogInformation("   Total Paragraphs: {Count}", result.ParagraphCount);

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Error inspecting template: {Error}", ex.Message);
                result.Error = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Prints a detailed report of the template inspection
        /// </summary>
        public void PrintReport(TemplateInspectionResult result)
        {
            Console.WriteLine("=".PadRight(60, '='));
            Console.WriteLine("TEMPLATE INSPECTION REPORT");
            Console.WriteLine("=".PadRight(60, '='));
            Console.WriteLine($"Template: {result.TemplatePath}");
            Console.WriteLine($"Exists: {result.Exists}");
            Console.WriteLine();

            if (!result.Exists)
            {
                Console.WriteLine("❌ Template file not found!");
                return;
            }

            if (!string.IsNullOrEmpty(result.Error))
            {
                Console.WriteLine($"❌ Error: {result.Error}");
                return;
            }

            Console.WriteLine($"📄 Paragraphs: {result.ParagraphCount}");
            Console.WriteLine();

            // Content Controls
            Console.WriteLine($"📋 Content Controls (SDT): {result.ContentControlCount}");
            if (result.ContentControls.Count > 0)
            {
                foreach (var cc in result.ContentControls)
                {
                    Console.WriteLine($"   ✅ Tag: '{cc.Tag}'");
                    if (!string.IsNullOrEmpty(cc.Title))
                        Console.WriteLine($"      Title: {cc.Title}");
                    if (!string.IsNullOrEmpty(cc.PlaceholderText))
                        Console.WriteLine($"      Placeholder: {cc.PlaceholderText}");
                    Console.WriteLine($"      Type: {cc.Type}");
                }
            }
            else
            {
                Console.WriteLine("   ⚠️  No content controls found!");
            }
            Console.WriteLine();

            // Plain Text Placeholders
            Console.WriteLine($"📝 Plain Text Placeholders: {result.PlainTextPlaceholders.Count}");
            if (result.PlainTextPlaceholders.Count > 0)
            {
                foreach (var placeholder in result.PlainTextPlaceholders)
                {
                    Console.WriteLine($"   ✅ '{placeholder}'");
                }
            }
            else
            {
                Console.WriteLine("   ⚠️  No plain text placeholders found!");
            }
            Console.WriteLine();

            // Summary
            Console.WriteLine("=".PadRight(60, '='));
            if (result.HasPlaceholders)
            {
                Console.WriteLine("✅ Template has placeholders - ready for content filling");
            }
            else
            {
                Console.WriteLine("❌ Template has NO placeholders!");
                Console.WriteLine();
                Console.WriteLine("To fix this, add either:");
                Console.WriteLine("1. Content Controls (SDT) with tags: DocumentTitle, DocumentType, Overview");
                Console.WriteLine("2. Plain text placeholders like: {{DocumentTitle}}, {{DocumentType}}, {{Overview}}");
            }
            Console.WriteLine("=".PadRight(60, '='));
        }
    }

    public class TemplateInspectionResult
    {
        public string TemplatePath { get; set; } = "";
        public bool Exists { get; set; }
        public string? Error { get; set; }
        public int ParagraphCount { get; set; }
        public int ContentControlCount { get; set; }
        public List<ContentControlInfo> ContentControls { get; set; } = new();
        public List<string> PlainTextPlaceholders { get; set; } = new();
        public bool HasPlaceholders => ContentControlCount > 0 || PlainTextPlaceholders.Count > 0;
    }

    public class ContentControlInfo
    {
        public string Tag { get; set; } = "";
        public string Title { get; set; } = "";
        public string PlaceholderText { get; set; } = "";
        public string Type { get; set; } = "";
    }
}

