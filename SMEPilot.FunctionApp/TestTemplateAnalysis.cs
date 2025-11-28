using System;
using System.IO;
using SMEPilot.FunctionApp.Helpers;

namespace SMEPilot.FunctionApp
{
    /// <summary>
    /// Quick test to analyze template and documents
    /// Run this from Program.cs or as a standalone test
    /// </summary>
    public class TestTemplateAnalysis
    {
        public static void Run()
        {
            var baseDir = Directory.GetCurrentDirectory();
            var templatePath = Path.Combine(baseDir, "Templates", "Template_C.dotx");
            var rawPath = Path.Combine(baseDir, "Templates", "raw.docx");
            var enrichedPath = Path.Combine(baseDir, "Enriched", "raw_enriched.docx");

            Console.WriteLine("\n" + "=".PadRight(80, '='));
            Console.WriteLine("TEMPLATE & DOCUMENT ANALYSIS");
            Console.WriteLine("=".PadRight(80, '=') + "\n");

            // Analyze Template
            if (File.Exists(templatePath))
            {
                Console.WriteLine("📄 TEMPLATE: Template_C.dotx");
                Console.WriteLine("-".PadRight(80, '-'));
                var inspector = new TemplateInspector();
                var result = inspector.InspectTemplate(templatePath);
                inspector.PrintReport(result);
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine($"❌ Template not found: {templatePath}");
            }

            // Quick check of raw document
            if (File.Exists(rawPath))
            {
                Console.WriteLine("📄 RAW DOCUMENT: raw.docx");
                Console.WriteLine("-".PadRight(80, '-'));
                try
                {
                    using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(rawPath, false);
                    var body = doc.MainDocumentPart?.Document?.Body;
                    if (body != null)
                    {
                        var paras = body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>().ToList();
                        var text = string.Join(" ", paras.SelectMany(p => p.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(t => t.Text ?? "")));
                        Console.WriteLine($"✅ Document opened successfully");
                        Console.WriteLine($"   Paragraphs: {paras.Count}");
                        Console.WriteLine($"   Text length: {text.Length} characters");
                        Console.WriteLine($"   Preview: {text.Substring(0, Math.Min(150, text.Length))}...");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error: {ex.Message}");
                }
                Console.WriteLine();
            }

            // Quick check of enriched document
            if (File.Exists(enrichedPath))
            {
                Console.WriteLine("📄 ENRICHED DOCUMENT: raw_enriched.docx");
                Console.WriteLine("-".PadRight(80, '-'));
                try
                {
                    using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(enrichedPath, false);
                    var body = doc.MainDocumentPart?.Document?.Body;
                    if (body != null)
                    {
                        var paras = body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>().ToList();
                        var text = string.Join(" ", paras.SelectMany(p => p.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(t => t.Text ?? "")));
                        Console.WriteLine($"✅ Document opened successfully");
                        Console.WriteLine($"   Paragraphs: {paras.Count}");
                        Console.WriteLine($"   Text length: {text.Length} characters");
                        Console.WriteLine($"   Preview: {text.Substring(0, Math.Min(150, text.Length))}...");
                        
                        // Check if content was filled
                        var sdtList = body.Descendants<DocumentFormat.OpenXml.Wordprocessing.SdtElement>().ToList();
                        Console.WriteLine($"   Content Controls: {sdtList.Count}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error: {ex.Message}");
                }
            }
        }
    }
}

