using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SMEPilot.FunctionApp.Models;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Simple document enricher WITHOUT AI
    /// Uses rule-based parsing instead of AI for sectioning
    /// </summary>
    public class SimpleEnricher
    {
        /// <summary>
        /// Generate document sections from text WITHOUT using AI
        /// Uses rule-based parsing: detects headings, paragraphs, etc.
        /// </summary>
        public DocumentModel GenerateSectionsFromText(string text, string? fileName = null)
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

            // Split text into sections based on patterns
            var sections = ParseSections(text);

            // Log parsing results for debugging
            System.Diagnostics.Debug.WriteLine($"[SimpleEnricher] Parsed document '{title}': {sections.Count} sections");
            foreach (var section in sections.Take(10)) // Log first 10 sections
            {
                System.Diagnostics.Debug.WriteLine($"  - Section: '{section.Heading}' ({section.Body?.Length ?? 0} chars)");
            }
            if (sections.Count > 10)
            {
                System.Diagnostics.Debug.WriteLine($"  ... and {sections.Count - 10} more sections");
            }

            return new DocumentModel
            {
                Title = title,
                Sections = sections,
                Images = new List<ImageData>()
            };
        }

        private string ExtractTitle(string text, string? fileName)
        {
            // Try to find title in first few lines
            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (lines.Length > 0)
            {
                var firstLine = lines[0].Trim();
                // If first line looks like a title (short, no period, capitalized)
                if (firstLine.Length < 100 && !firstLine.Contains('.') && char.IsUpper(firstLine[0]))
                {
                    return firstLine;
                }
            }

            // Fallback to filename
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return System.IO.Path.GetFileNameWithoutExtension(fileName);
            }

            return "Document";
        }

        private List<Section> ParseSections(string text)
        {
            var sections = new List<Section>();
            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.None);

            var currentSection = new StringBuilder();
            string? currentHeading = null;
            int sectionId = 1;
            bool isFirstLine = true;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                
                // Skip empty lines but preserve them in section body
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    if (currentSection.Length > 0)
                    {
                        currentSection.AppendLine();
                    }
                    continue;
                }

                // Detect headings (markdown, numbered, or heuristics)
                if (IsLikelyHeading(trimmed, isFirstLine))
                {
                    // Save previous section if exists
                    if (currentSection.Length > 0 && !string.IsNullOrWhiteSpace(currentHeading))
                    {
                        var body = currentSection.ToString().Trim();
                        if (!string.IsNullOrWhiteSpace(body))
                        {
                            sections.Add(CreateSection(sectionId++, currentHeading, body));
                        }
                        currentSection.Clear();
                    }
                    
                    // Clean up markdown heading markers
                    currentHeading = Regex.Replace(trimmed, @"^#{1,6}\s+", "").Trim();
                    isFirstLine = false;
                }
                else
                {
                    // Add to current section body
                    if (currentSection.Length > 0) currentSection.AppendLine();
                    currentSection.Append(trimmed);
                    isFirstLine = false;
                }
            }

            // Add last section
            if (currentSection.Length > 0 || !string.IsNullOrWhiteSpace(currentHeading))
            {
                var body = currentSection.ToString().Trim();
                sections.Add(CreateSection(sectionId++, currentHeading ?? "Content", 
                    !string.IsNullOrWhiteSpace(body) ? body : ""));
            }

            // If no sections found, create one from entire text
            if (sections.Count == 0)
            {
                sections.Add(CreateSection(1, "Content", text.Trim()));
            }

            return sections;
        }

        private bool IsLikelyHeading(string line, bool isFirstLine)
        {
            var trimmed = line.Trim();
            
            // PRIORITY 1: Markdown headings (#, ##, ###, etc.)
            if (Regex.IsMatch(trimmed, @"^#{1,6}\s+")) return true;
            
            // PRIORITY 2: Numbered headings (1., 1.1, 1.1.1, etc.)
            if (Regex.IsMatch(trimmed, @"^\d+(\.\d+)*[\.\)]\s+[A-Z]")) return true;
            
            // PRIORITY 3: First line of document (if short and looks like title)
            if (isFirstLine && trimmed.Length < 100 && !trimmed.Contains('.') && char.IsUpper(trimmed[0]))
                return true;
            
            // PRIORITY 4: Short lines that look like headings
            if (trimmed.Length > 80) return false;
            if (trimmed.EndsWith(".") || trimmed.EndsWith(",")) return false;

            // Check for all caps (likely heading) - but not if it's too short (might be acronym)
            var upperCount = trimmed.Count(c => char.IsUpper(c));
            if (upperCount > trimmed.Length * 0.5 && trimmed.Length > 5) return true;

            return false;
        }

        private Section CreateSection(int id, string heading, string body)
        {
            // Generate simple summary (first sentence or first 40 words)
            var summary = GenerateSimpleSummary(body);

            return new Section
            {
                Id = $"s{id}",
                Heading = heading,
                Summary = summary,
                Body = body
            };
        }

        private string GenerateSimpleSummary(string text)
        {
            // Simple summary: first sentence or first 40 words
            var sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (sentences.Length > 0)
            {
                var firstSentence = sentences[0].Trim();
                if (firstSentence.Length <= 200)
                {
                    return firstSentence;
                }
            }

            // Fallback: first 40 words
            var words = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var summaryWords = words.Take(40).ToArray();
            return string.Join(" ", summaryWords) + (words.Length > 40 ? "..." : "");
        }
    }
}

