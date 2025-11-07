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
            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            var currentSection = new StringBuilder();
            string? currentHeading = null;
            int sectionId = 1;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                // Detect potential headings (short lines, all caps, or numbered)
                if (IsLikelyHeading(trimmed, currentSection.Length == 0))
                {
                    // Save previous section if exists
                    if (currentSection.Length > 0 && !string.IsNullOrWhiteSpace(currentHeading))
                    {
                        sections.Add(CreateSection(sectionId++, currentHeading, currentSection.ToString()));
                        currentSection.Clear();
                    }
                    currentHeading = trimmed;
                }
                else
                {
                    // Add to current section body
                    if (currentSection.Length > 0) currentSection.AppendLine();
                    currentSection.Append(trimmed);
                }
            }

            // Add last section
            if (currentSection.Length > 0)
            {
                sections.Add(CreateSection(sectionId++, currentHeading ?? "Content", currentSection.ToString()));
            }

            // If no sections found, create one from entire text
            if (sections.Count == 0)
            {
                sections.Add(CreateSection(1, "Content", text));
            }

            return sections;
        }

        private bool IsLikelyHeading(string line, bool isFirstLine)
        {
            // Heuristics for detecting headings:
            // 1. Short line (< 80 chars)
            // 2. All caps (or mostly caps)
            // 3. Starts with number (1., 2., etc.)
            // 4. No ending punctuation
            // 5. First line of document

            if (isFirstLine && line.Length < 100) return true;
            if (line.Length > 80) return false;
            if (line.EndsWith(".") || line.EndsWith(",")) return false;

            // Check for numbered headings
            if (Regex.IsMatch(line, @"^\d+[\.\)]\s+[A-Z]")) return true;

            // Check for all caps (likely heading)
            var upperCount = line.Count(c => char.IsUpper(c));
            if (upperCount > line.Length * 0.5 && line.Length > 5) return true;

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

