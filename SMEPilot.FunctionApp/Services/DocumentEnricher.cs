// DocumentEnricher.cs
// Consolidated: DocumentEnricherService + RuleBasedFormatter + HybridEnricher
// Purpose: All document enrichment logic in one file with 3 regions

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SMEPilot.FunctionApp.Models;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace SMEPilot.FunctionApp.Services
{
    /// <summary>
    /// Main document enrichment service - combines all enrichment strategies
    /// </summary>
    public class DocumentEnricher
    {
        private readonly MappingConfig _config;
        private readonly RuleMapping _ruleMapping;
        private readonly string? _templatePath;
        private readonly ILogger<DocumentEnricher>? _logger;
        private const string EnrichedMarkerProperty = "SMEPilot_Enriched";

        public DocumentEnricher(string mappingJsonPath, string? templatePath = null, ILogger<DocumentEnricher>? logger = null)
        {
            if (string.IsNullOrEmpty(mappingJsonPath) || !File.Exists(mappingJsonPath))
                throw new ArgumentException("mappingJsonPath missing or not found");

            var json = File.ReadAllText(mappingJsonPath);
            _config = JsonConvert.DeserializeObject<MappingConfig>(json) ?? new MappingConfig();
            _templatePath = templatePath;
            _logger = logger;
            _ruleMapping = LoadRuleMapping();
        }

        private RuleMapping LoadRuleMapping()
        {
            try
            {
                var basePath = AppContext.BaseDirectory;
                var cfg = Path.Combine(basePath, "Config", "RuleMapping.json");
                if (File.Exists(cfg))
                {
                    var json = File.ReadAllText(cfg);
                    return JsonConvert.DeserializeObject<RuleMapping>(json) ?? RuleMapping.Default();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed reading RuleMapping.json, using defaults.");
            }
            return RuleMapping.Default();
        }
        #region Region 3: HybridEnricher Methods (Sectioning and classification)

        /// <summary>
        /// Rule-based sectioning (no AI cost) - detects headings and splits document into sections
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

            var title = ExtractTitle(text, fileName);
            var sections = ParseSections(text);

            return new DocumentModel
            {
                Title = title,
                Sections = sections,
                Images = new List<ImageData>()
            };
        }

        /// <summary>
        /// Classify document (Functional/Support/Technical) using keyword-based approach
        /// </summary>
        public string ClassifyDocument(string title, string content)
        {
            var text = (title + " " + content).ToLower();

            var technicalKeywords = new[] { "api", "endpoint", "integration", "technical", "developer", "code", "sdk", "rest", "graph", "database", "architecture", "implementation" };
            var technicalScore = technicalKeywords.Count(kw => text.Contains(kw));

            var supportKeywords = new[] { "support", "troubleshooting", "issue", "problem", "error", "help", "faq", "guide", "how to", "fix", "resolve" };
            var supportScore = supportKeywords.Count(kw => text.Contains(kw));

            var functionalKeywords = new[] { "feature", "functionality", "user", "workflow", "process", "business", "requirement", "use case", "scenario" };
            var functionalScore = functionalKeywords.Count(kw => text.Contains(kw));

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
                return Path.GetFileNameWithoutExtension(fileName);
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

                if (IsLikelyHeadingHybrid(trimmed, isFirstLine))
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

        /// <summary>
        /// Enhanced heading detection with robust heuristics
        /// Includes: markdown headings, numbered headings, short text, no ending punctuation
        /// </summary>
        private bool IsLikelyHeadingHybrid(string line, bool isFirstLine)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) return false;

            // PRIORITY 1: Markdown headings (#, ##, ###, etc.)
            if (Regex.IsMatch(trimmed, @"^#{1,6}\s+")) return true;
            
            // PRIORITY 2: Numbered headings (1., 1.1, 1.1.1, etc.) - improved regex to be more flexible
            // Matches: "1. PROJECT OVERVIEW", "1.1 Project Information", "1.1.1 Details", etc.
            // CRITICAL: Also match lowercase after number (some documents use lowercase)
            if (Regex.IsMatch(trimmed, @"^\d+(\.\d+)*[\.\)]\s+[A-Za-z]")) return true;
            
            // Also match patterns like "1 PROJECT OVERVIEW" (without dot) or "1) PROJECT OVERVIEW"
            if (Regex.IsMatch(trimmed, @"^\d+[\.\)]\s+[A-Za-z]{3,}")) return true;
            
            // PRIORITY 3: First line heuristic - likely title
            if (isFirstLine && trimmed.Length < 100 && !trimmed.Contains('.')) return true;
            
            // Too long is unlikely to be heading
            if (trimmed.Length > 120) return false;
            
            // Punctuation check - headings rarely end with period/comma (colon is OK)
            if (trimmed.EndsWith(".") || trimmed.EndsWith(","))
            {
                return false; // Very unlikely to be heading
            }
            if (trimmed.EndsWith(":"))
            {
                // Colon is OK for headings if short
                if (trimmed.Length < 50) return true;
            }

            // ALL CAPS with reasonable length
            var upperCount = trimmed.Count(c => char.IsUpper(c));
            if (upperCount > trimmed.Length * 0.5 && trimmed.Length > 5 && trimmed.Length < 80) return true;

            // Short text with title case, no punctuation
            if (trimmed.Length < 60 && 
                char.IsUpper(trimmed[0]) && 
                !trimmed.Contains('.') && 
                !trimmed.Contains('!') && 
                !trimmed.Contains('?'))
            {
                var wordCount = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
                if (wordCount <= 8) return true;
            }

            // Common document patterns
            if (Regex.IsMatch(trimmed, @"^(Form\s+[A-Z0-9-]+|([A-Z]-?\d+|[A-Z]{2,4})\s+(Visa|File|Documents?))", RegexOptions.IgnoreCase)) return true;

            // Short title-case text without conjunctions
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

        #endregion

        #region Region 4: Feedback1 - Weighted Section Mapping & Structured DTOs

        /// <summary>
        /// Parse sections from structured DTOs using style-first logic (Feedback1)
        /// </summary>
        public List<SectionModel> ParseSections(List<ParagraphDto> paras, List<TableDto> tables, List<ExtractedImage> images)
        {
            var sections = new List<SectionModel>();
            var current = new SectionModel { Heading = "Introduction" };

            int headingCount = 0;
            int checkedCount = 0;
            foreach (var p in paras)
            {
                bool isHeading = false;
                checkedCount++;
                
                // Check style-based heading first
                if (!string.IsNullOrEmpty(p.StyleId) && IsHeadingStyle(p.StyleId))
                {
                    isHeading = true;
                    headingCount++;
                    _logger?.LogDebug("✅ [HEADING] Style-based heading detected: '{Text}' (Style: {Style})", p.Text.Substring(0, Math.Min(50, p.Text.Length)), p.StyleId);
                }
                // Check content-based heading detection
                else if (IsProbableHeading(p.Text))
                {
                    isHeading = true;
                    headingCount++;
                }
                
                // DIAGNOSTIC: Log first 20 paragraphs to see what we're getting
                if (checkedCount <= 20)
                {
                    _logger?.LogDebug("📝 [PARA-{Index}] Text: '{Text}' | Style: {Style} | IsHeading: {IsHeading}", 
                        checkedCount, p.Text.Substring(0, Math.Min(80, p.Text.Length)), p.StyleId ?? "none", isHeading);
                }

                if (isHeading)
                {
                    // Push current section
                    if (current.Paragraphs.Any() || current.Heading != "Introduction")
                        sections.Add(current);
                    current = new SectionModel { Heading = p.Text };
                    continue;
                }

                current.Paragraphs.Add(p.Text);
            }

            // Final push
            if (current.Paragraphs.Any() || current.Heading != "Introduction")
                sections.Add(current);

            // CRITICAL DIAGNOSTIC: Log section parsing results
            _logger?.LogInformation("📊 [SECTION-PARSING] Parsed {SectionCount} sections from {ParaCount} paragraphs. Detected {HeadingCount} headings.", 
                sections.Count, paras.Count, headingCount);
            
            if (sections.Count == 1)
            {
                _logger?.LogWarning("⚠️ [SECTION-PARSING] CRITICAL: Only 1 section detected! This will cause low fill rate. First 10 paragraphs: {Paragraphs}", 
                    string.Join(" | ", paras.Take(10).Select(p => $"'{p.Text.Substring(0, Math.Min(50, p.Text.Length))}'")));
            }

            // Attach tables/images by anchor paragraph index -> map to nearest section
            AttachTablesToSections(sections, tables, paras);
            AttachImagesToSections(sections, images, paras);

            return sections;
        }

        /// <summary>
        /// Check if style ID indicates a heading
        /// </summary>
        private bool IsHeadingStyle(string styleId)
        {
            var sid = styleId?.Replace(" ", "").ToLowerInvariant() ?? "";
            return sid.StartsWith("heading");
        }

        /// <summary>
        /// CRITICAL FIX: Enhanced heading detection - MUST detect numbered headings like "1. PROJECT OVERVIEW", "1.1 Project Information"
        /// This is the root cause of single-section parsing failure
        /// </summary>
        private bool IsProbableHeading(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            
            var trimmed = text.Trim();
            
            // CRITICAL: Numbered headings MUST be detected first (highest priority)
            // Pattern: "1. PROJECT OVERVIEW", "1.1 Project Information", "1.1.1 Details"
            // Handle both with and without space after number/dot
            // IMPROVED: More flexible regex to catch edge cases
            if (Regex.IsMatch(trimmed, @"^\d+(\.\d+)*[\.\)]\s*[A-Za-z]", RegexOptions.IgnoreCase)) 
            {
                _logger?.LogDebug("✅ [HEADING] Detected numbered heading (pattern 1): '{Text}'", trimmed);
                return true;
            }
            
            // Also match patterns like "1 PROJECT OVERVIEW" (without dot) or "1) PROJECT OVERVIEW"
            // IMPROVED: More flexible - allow optional space, handle parentheses
            if (Regex.IsMatch(trimmed, @"^\d+[\.\)]?\s*[A-Za-z]{2,}", RegexOptions.IgnoreCase)) 
            {
                _logger?.LogDebug("✅ [HEADING] Detected numbered heading (pattern 2): '{Text}'", trimmed);
                return true;
            }
            
            // NEW: Match "1 PROJECT OVERVIEW" (number followed by space and text, no punctuation)
            if (Regex.IsMatch(trimmed, @"^\d+\s+[A-Z]{2,}", RegexOptions.IgnoreCase)) 
            {
                _logger?.LogDebug("✅ [HEADING] Detected numbered heading (pattern 3): '{Text}'", trimmed);
                return true;
            }
            
            // Short text (< 6 words) - strong indicator
            var wordCount = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            if (wordCount < 6 && trimmed.Length < 100)
            {
                // Not ending with punctuation (except colon)
                if (!trimmed.EndsWith(".") && !trimmed.EndsWith(",") && !trimmed.EndsWith("!"))
                {
                    // ALL CAPS (but not too long) - e.g., "PROJECT OVERVIEW"
                    if (trimmed.Length < 60 && trimmed.Count(char.IsWhiteSpace) < 5 && trimmed.All(c => !char.IsLower(c))) return true;
                    
                    // Title case with few words - e.g., "Project Information"
                    if (wordCount <= 6 && char.IsUpper(trimmed[0]) && !trimmed.Contains(" and ") && !trimmed.Contains(" or ")) return true;
                }
            }
            
            // Not ending with punctuation (except colon which is OK for headings)
            if (trimmed.Length < 50 && !trimmed.EndsWith(".") && !trimmed.EndsWith(",") && !trimmed.EndsWith("!") && text.Count(c => char.IsWhiteSpace(c)) < 6) return true;
            
            // Common heading patterns
            if (Regex.IsMatch(trimmed, @"^(Overview|Summary|Introduction|Background|Requirements|Architecture|Implementation|Testing|Conclusion|Appendix|References|Table of Contents|Project Overview|Project Information|Target Users|Business Goals|Scope|Functional Requirements|Technical Specifications)\b", RegexOptions.IgnoreCase)) return true;
            
            return false;
        }

        /// <summary>
        /// Attach tables to sections based on anchor paragraph index
        /// </summary>
        private void AttachTablesToSections(List<SectionModel> sections, List<TableDto> tables, List<ParagraphDto> paras)
        {
            foreach (var t in tables)
            {
                var sec = FindSectionByParagraphIndex(sections, paras, t.AnchorParagraphIndex);
                if (sec != null) sec.Tables.Add(t);
            }
        }

        /// <summary>
        /// Attach images to sections based on anchor paragraph index
        /// </summary>
        private void AttachImagesToSections(List<SectionModel> sections, List<ExtractedImage> images, List<ParagraphDto> paras)
        {
            foreach (var img in images)
            {
                var sec = FindSectionByParagraphIndex(sections, paras, img.AnchorParagraphIndex);
                if (sec != null) sec.Images.Add(img);
            }
        }

        /// <summary>
        /// Find section by paragraph index
        /// </summary>
        private SectionModel? FindSectionByParagraphIndex(List<SectionModel> sections, List<ParagraphDto> paras, int paraIndex)
        {
            // Map paraIndex to section by searching section paragraph ranges
            int cursor = 0;
            foreach (var sec in sections)
            {
                int count = sec.Paragraphs.Count;
                if (cursor <= paraIndex && paraIndex < cursor + count) return sec;
                cursor += count;
            }
            // Fallback to last section
            return sections.LastOrDefault();
        }

        /// <summary>
        /// Map section to template tag using weighted scorer (Feedback1)
        /// Returns best match tag with confidence score and reasons
        /// </summary>
        /// <summary>
        /// Feedback1: Map section to template tag with weighted scoring and diagnostics
        /// Returns tag, score, and reasons for mapping decision
        /// </summary>
        public (string tag, double score, List<string> reasons) MapSectionToTemplate(SectionModel sec, List<string> templateTags)
        {
            var bestTag = "";
            double bestScore = 0;
            var bestReasons = new List<string>();
            var allCandidates = new List<(string tag, double score, List<string> reasons)>();

            foreach (var tag in templateTags)
            {
                double score = 0;
                var reasons = new List<string>();

                // Exact heading match (0.5 points)
                if (string.Equals(tag, sec.Heading, StringComparison.OrdinalIgnoreCase))
                {
                    score += 0.5;
                    reasons.Add("ExactHeading");
                }

                // Heading style presence (0.25 points)
                if (IsProbableHeading(sec.Heading))
                {
                    score += 0.25;
                    reasons.Add("HeadingHeuristic");
                }

                // Keyword hits from mapping config (0.05 per hit, max 0.2)
                var keywords = GetKeywordsForTag(tag);
                var hits = keywords.Count(k => 
                    sec.Heading.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0 || 
                    sec.Paragraphs.Any(p => p.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0));
                if (hits > 0)
                {
                    score += Math.Min(0.2, hits * 0.05);
                    reasons.Add($"KeywordHits:{hits}");
                }

                // Table/image presence (0.05 each)
                if (sec.Tables.Any()) { score += 0.05; reasons.Add("HasTable"); }
                if (sec.Images.Any()) { score += 0.05; reasons.Add("HasImage"); }

                // Fuzzy match using Levenshtein distance (0.1 if distance <= 3)
                var lev = LevenshteinDistance(tag.ToLowerInvariant(), sec.Heading.ToLowerInvariant());
                if (lev <= 3 && sec.Heading.Length > 4)
                {
                    score += 0.1;
                    reasons.Add($"FuzzyMatch:{lev}");
                }

                allCandidates.Add((tag, score, reasons));

                if (score > bestScore) 
                { 
                    bestScore = score; 
                    bestTag = tag; 
                    bestReasons = reasons; 
                }
            }

            // Feedback1: Log top 3 candidates for diagnostics
            var topCandidates = allCandidates.OrderByDescending(c => c.score).Take(3).ToList();
            _logger?.LogDebug("🔍 [MAPPING] Section '{Heading}' -> Top candidates: {Candidates}", 
                sec.Heading, 
                string.Join(", ", topCandidates.Select(c => $"{c.tag}({c.score:F2})")));

            return (bestTag, Math.Round(bestScore, 3), bestReasons);
        }

        /// <summary>
        /// Get keywords for a template tag from mapping config
        /// </summary>
        private List<string> GetKeywordsForTag(string tag)
        {
            var keywords = new List<string>();
            
            // Check RuleMapping first
            if (_ruleMapping?.HeaderMappings != null)
            {
                foreach (var kvp in _ruleMapping.HeaderMappings)
                {
                    if (string.Equals(kvp.Value, tag, StringComparison.OrdinalIgnoreCase))
                    {
                        keywords.Add(kvp.Key);
                    }
                }
            }

            // Check MappingConfig sections
            if (_config?.Sections != null)
            {
                var section = _config.Sections.FirstOrDefault(s => 
                    string.Equals(s.Name, tag, StringComparison.OrdinalIgnoreCase));
                if (section != null && section.Keywords != null)
                {
                    keywords.AddRange(section.Keywords);
                }
            }

            return keywords.Distinct().ToList();
        }

        /// <summary>
        /// Calculate Levenshtein distance between two strings (Feedback1)
        /// </summary>
        private int LevenshteinDistance(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return string.IsNullOrEmpty(b) ? 0 : b.Length;
            if (string.IsNullOrEmpty(b)) return a.Length;

            var dp = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) dp[0, j] = j;
            
            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = (a[i - 1] == b[j - 1]) ? 0 : 1;
                    dp[i, j] = Math.Min(
                        Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                        dp[i - 1, j - 1] + cost);
                }
            }
            
            return dp[a.Length, b.Length];
        }

        #endregion
    }
}

