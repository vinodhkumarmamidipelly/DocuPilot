using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SMEPilot.FunctionApp.Models;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Template formatter: Rule-based sectioning and template application
    /// NO AI - Just template formatting and styling
    /// </summary>
    public class HybridEnricher
    {
        public HybridEnricher()
        {
            // No dependencies - rule-based only
        }

        /// <summary>
        /// Step 1: Rule-based sectioning (no AI cost)
        /// Detects headings and splits document into sections
        /// </summary>
        public DocumentModel SectionDocument(string text, string? fileName = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new DocumentModel
                {
                    Title = fileName ?? "Document",
                    Sections = new List<Section>(),
                    Images = new List<ImageData>()
                };
            }

            // Extract title (first line or filename)
            var title = ExtractTitle(text, fileName);

            // Split text into sections based on patterns (rule-based)
            var sections = ParseSections(text);

            return new DocumentModel
            {
                Title = title,
                Sections = sections,
                Images = new List<ImageData>()
            };
        }

        // AI enrichment methods removed - template formatting only (no AI required)

        /// <summary>
        /// Classify document (Functional/Support/Technical) using keyword-based approach
        /// No AI needed - uses keyword matching
        /// </summary>
        public string ClassifyDocument(string title, string content)
        {
            var text = (title + " " + content).ToLower();

            // Technical keywords
            var technicalKeywords = new[] { "api", "endpoint", "integration", "technical", "developer", "code", "sdk", "rest", "graph", "database", "architecture", "implementation" };
            var technicalScore = technicalKeywords.Count(kw => text.Contains(kw));

            // Support keywords
            var supportKeywords = new[] { "support", "troubleshooting", "issue", "problem", "error", "help", "faq", "guide", "how to", "fix", "resolve" };
            var supportScore = supportKeywords.Count(kw => text.Contains(kw));

            // Functional keywords
            var functionalKeywords = new[] { "feature", "functionality", "user", "workflow", "process", "business", "requirement", "use case", "scenario" };
            var functionalScore = functionalKeywords.Count(kw => text.Contains(kw));

            // Return classification with highest score
            if (technicalScore >= supportScore && technicalScore >= functionalScore)
                return "Technical";
            else if (supportScore >= functionalScore)
                return "Support";
            else
                return "Functional";
        }

        private string ExtractTitle(string text, string? fileName)
        {
            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (lines.Length > 0)
            {
                var firstLine = lines[0].Trim();
                if (firstLine.Length < 100 && !firstLine.Contains('.') && char.IsUpper(firstLine[0]))
                {
                    return firstLine;
                }
            }

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return System.IO.Path.GetFileNameWithoutExtension(fileName);
            }

            return "Document";
        }

        private List<Section> ParseSections(string text)
        {
            var sections = new List<Section>();
            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            var currentSection = new StringBuilder();
            string? currentHeading = null;
            int sectionId = 1;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                if (IsLikelyHeading(trimmed, currentSection.Length == 0))
                {
                    if (currentSection.Length > 0 && !string.IsNullOrWhiteSpace(currentHeading))
                    {
                        sections.Add(CreateSection(sectionId++, currentHeading, currentSection.ToString()));
                        currentSection.Clear();
                    }
                    currentHeading = trimmed;
                }
                else
                {
                    if (currentSection.Length > 0) currentSection.AppendLine();
                    currentSection.Append(trimmed);
                }
            }

            if (currentSection.Length > 0)
            {
                sections.Add(CreateSection(sectionId++, currentHeading ?? "Content", currentSection.ToString()));
            }

            if (sections.Count == 0)
            {
                sections.Add(CreateSection(1, "Content", text));
            }

            return sections;
        }

        private bool IsLikelyHeading(string line, bool isFirstLine)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) return false;

            // First line rule
            if (isFirstLine && trimmed.Length < 100 && !trimmed.Contains('.')) return true;
            
            // Too long to be a heading
            if (trimmed.Length > 120) return false;
            
            // Ends with punctuation - likely not a heading
            if (trimmed.EndsWith(".") || trimmed.EndsWith(",") || trimmed.EndsWith(":")) 
            {
                // Exception: if it's very short and ends with colon, might be a heading
                if (trimmed.Length < 50 && trimmed.EndsWith(":")) return true;
                return false;
            }

            // Numbered headings: "1. ", "1) ", "1.1 ", etc.
            if (Regex.IsMatch(trimmed, @"^\d+([\.\)]\s+|\.[\d\.]+\s+)[A-Z]")) return true;

            // All caps or mostly caps (likely heading)
            var upperCount = trimmed.Count(c => char.IsUpper(c));
            if (upperCount > trimmed.Length * 0.5 && trimmed.Length > 5 && trimmed.Length < 80) return true;

            // Short lines that start with capital and don't contain sentence-ending punctuation
            if (trimmed.Length < 60 && 
                char.IsUpper(trimmed[0]) && 
                !trimmed.Contains('.') && 
                !trimmed.Contains('!') && 
                !trimmed.Contains('?'))
            {
                // Check if it looks like a title/heading (not a sentence)
                var wordCount = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
                if (wordCount <= 8) return true; // Short phrases are likely headings
            }

            // Module/document type patterns (e.g., "Personnel File", "Form I-983", "H-1B")
            if (Regex.IsMatch(trimmed, @"^(Form\s+[A-Z0-9-]+|([A-Z]-?\d+|[A-Z]{2,4})\s+(Visa|File|Documents?))", RegexOptions.IgnoreCase)) return true;

            // Lines that are all on one line and short, starting with capital
            if (trimmed.Length < 50 && 
                char.IsUpper(trimmed[0]) && 
                !trimmed.Contains(" and ") && 
                !trimmed.Contains(" or ") &&
                trimmed.Split(' ').Length <= 6)
            {
                return true;
            }

            return false;
        }

        private Section CreateSection(int id, string heading, string body)
        {
            return new Section
            {
                Id = $"s{id}",
                Heading = heading,
                Summary = GenerateSummary(body),
                Body = body
            };
        }

        private string GenerateSummary(string text)
        {
            var sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (sentences.Length > 0)
            {
                var firstSentence = sentences[0].Trim();
                if (firstSentence.Length <= 200)
                {
                    return firstSentence;
                }
            }

            var words = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var summaryWords = words.Take(40).ToArray();
            return string.Join(" ", summaryWords) + (words.Length > 40 ? "..." : "");
        }
    }
}

