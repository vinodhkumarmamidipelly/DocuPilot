using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using SMEPilot.FunctionApp.Models;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Hybrid enricher: Rule-based sectioning + AI content enrichment
    /// Cost-saving approach: Uses AI only for enriching content, not for sectioning
    /// </summary>
    public class HybridEnricher
    {
        private readonly OpenAiHelper _openai;

        public HybridEnricher(OpenAiHelper openai)
        {
            _openai = openai;
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

        /// <summary>
        /// Step 2: AI enrichment of section content only (minimal AI usage)
        /// Only enriches the body text, not sectioning
        /// </summary>
        public async Task<DocumentModel> EnrichSectionsAsync(DocumentModel docModel, List<string> imageOcrs)
        {
            if (docModel.Sections == null || docModel.Sections.Count == 0)
            {
                return docModel;
            }

            Console.WriteLine($"🔧 [HYBRID] Enriching {docModel.Sections.Count} sections with AI (content only)...");

            // Enrich each section's body text using AI
            foreach (var section in docModel.Sections)
            {
                if (string.IsNullOrWhiteSpace(section.Body))
                {
                    // Generate simple summary if body is empty
                    section.Summary = "Content section";
                    continue;
                }

                try
                {
                    // Use AI ONLY to enrich/expand the body text
                    var enrichedBody = await EnrichSectionContentAsync(section.Body);
                    
                    // Update body with enriched content
                    section.Body = enrichedBody;
                    
                    // Generate summary from enriched content (first sentence)
                    section.Summary = GenerateSummary(enrichedBody);
                    
                    Console.WriteLine($"✅ [HYBRID] Enriched section: {section.Heading}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ [HYBRID] Failed to enrich section '{section.Heading}': {ex.Message}");
                    // Keep original content if enrichment fails
                    section.Summary = GenerateSummary(section.Body);
                }
            }

            return docModel;
        }

        /// <summary>
        /// Use AI to enrich/expand section content only
        /// This is the ONLY AI call per section (minimal cost)
        /// </summary>
        private async Task<string> EnrichSectionContentAsync(string originalText)
        {
            if (_openai == null)
            {
                // Mock mode - return original
                return originalText;
            }

            try
            {
                var client = _openai.GetClient();
                var deployment = _openai.GetDeployment();
                
                if (client == null || string.IsNullOrWhiteSpace(deployment))
                {
                    return originalText; // Fallback to original
                }

                // Minimal AI prompt - only enrich content, don't restructure
                string systemPrompt = @"You are a content enricher. Your job is to EXPAND and ENRICH the given text.
PRESERVE all original information.
ADD detail, explanations, and context.
Make the text MORE comprehensive and detailed.
Do NOT change the structure or remove any information.
Return ONLY the enriched text, no JSON, no formatting.";

                string userPrompt = $"Enrich and expand this content while preserving all information:\n\n{originalText}";

                var options = new ChatCompletionsOptions(
                    deploymentName: deployment,
                    messages: new ChatRequestMessage[]
                    {
                        new ChatRequestSystemMessage(systemPrompt),
                        new ChatRequestUserMessage(userPrompt)
                    })
                {
                    Temperature = 0.3f,
                    MaxTokens = Math.Min(2000, (originalText.Length / 4) * 2) // Dynamic based on input
                };

                var response = await client.GetChatCompletionsAsync(options);
                var enrichedText = response.Value.Choices[0].Message.Content;

                return enrichedText ?? originalText;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ [HYBRID] AI enrichment failed: {ex.Message}");
                return originalText; // Fallback to original
            }
        }

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
            if (isFirstLine && line.Length < 100) return true;
            if (line.Length > 80) return false;
            if (line.EndsWith(".") || line.EndsWith(",")) return false;

            if (Regex.IsMatch(line, @"^\d+[\.\)]\s+[A-Z]")) return true;

            var upperCount = line.Count(c => char.IsUpper(c));
            if (upperCount > line.Length * 0.5 && line.Length > 5) return true;

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

