using System;
using System.IO;
using System.Linq;
using SMEPilot.FunctionApp.Helpers;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace SMEPilot.FunctionApp.Tools
{
    /// <summary>
    /// Quick analysis tool for template and document files
    /// </summary>
    public class AnalyzeFiles
    {
        public static void AnalyzeAll()
        {
            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..");
            
            var templatePath = Path.Combine(basePath, "Templates", "Template_C.dotx");
            var rawPath = Path.Combine(basePath, "Templates", "raw.docx");
            var enrichedPath = Path.Combine(basePath, "Enriched", "raw_enriched.docx");

            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("FILE ANALYSIS REPORT");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();

            // Analyze Template
            Console.WriteLine("📄 ANALYZING TEMPLATE: Template_C.dotx");
            Console.WriteLine("-".PadRight(80, '-'));
            AnalyzeTemplate(templatePath);
            Console.WriteLine();

            // Analyze Raw Document
            Console.WriteLine("📄 ANALYZING RAW DOCUMENT: raw.docx");
            Console.WriteLine("-".PadRight(80, '-'));
            AnalyzeDocument(rawPath);
            Console.WriteLine();

            // Analyze Enriched Document
            Console.WriteLine("📄 ANALYZING ENRICHED DOCUMENT: raw_enriched.docx");
            Console.WriteLine("-".PadRight(80, '-'));
            AnalyzeDocument(enrichedPath);
            Console.WriteLine();

            Console.WriteLine("=".PadRight(80, '='));
        }

        private static void AnalyzeTemplate(string templatePath)
        {
            if (!File.Exists(templatePath))
            {
                Console.WriteLine($"❌ Template not found: {templatePath}");
                return;
            }

            var inspector = new TemplateInspector();
            var result = inspector.InspectTemplate(templatePath);
            inspector.PrintReport(result);
        }

        private static void AnalyzeDocument(string docPath)
        {
            if (!File.Exists(docPath))
            {
                Console.WriteLine($"❌ Document not found: {docPath}");
                return;
            }

            try
            {
                using var doc = WordprocessingDocument.Open(docPath, false);
                var mainPart = doc.MainDocumentPart;
                if (mainPart?.Document?.Body == null)
                {
                    Console.WriteLine("⚠️ Document has no body");
                    return;
                }

                var body = mainPart.Document.Body;
                var paragraphs = body.Descendants<Paragraph>().ToList();
                var allText = string.Join(" ", paragraphs.SelectMany(p => p.Descendants<Text>().Select(t => t.Text ?? "")));

                Console.WriteLine($"📊 Total Paragraphs: {paragraphs.Count}");
                Console.WriteLine($"📊 Total Text Length: {allText.Length} characters");
                Console.WriteLine($"📊 First 200 characters: {allText.Substring(0, Math.Min(200, allText.Length))}...");

                // Check for content controls
                var sdtList = body.Descendants<SdtElement>().ToList();
                Console.WriteLine($"📋 Content Controls: {sdtList.Count}");
                if (sdtList.Count > 0)
                {
                    foreach (var sdt in sdtList)
                    {
                        var tag = sdt.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;
                        Console.WriteLine($"   - Tag: {tag ?? "(no tag)"}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error analyzing document: {ex.Message}");
            }
        }
    }
}

