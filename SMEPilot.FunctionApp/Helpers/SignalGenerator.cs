using System;
using System.Collections.Generic;
using System.Linq;
using SMEPilot.FunctionApp.Models;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Step 5: SignalGenerator
    /// Purpose: Compute "features" for each candidate piece of content
    /// Prevents: Blind fuzzy matching, wrong decisions by ScoringEngine
    /// </summary>
    public class SignalGenerator
    {
        private readonly AliasManager _aliasManager;

        public SignalGenerator(AliasManager aliasManager)
        {
            _aliasManager = aliasManager ?? throw new ArgumentNullException(nameof(aliasManager));
        }

        /// <summary>
        /// Generates feature set for a candidate content snippet
        /// </summary>
        public FeatureSet GenerateFeatures(
            string tokenName,
            string candidateText,
            int paragraphIndex,
            string? headingPath = null,
            bool isTableCell = false,
            bool isListItem = false)
        {
            var features = new FeatureSet
            {
                TokenName = tokenName,
                CandidateText = candidateText,
                ParagraphIndex = paragraphIndex,
                HeadingPath = headingPath ?? string.Empty,
                IsTableCell = isTableCell,
                IsListItem = isListItem
            };

            // 1. Alias hit
            features.AliasHit = _aliasManager.IsAlias(tokenName, candidateText);

            // 2. Normalized exact match
            var normalizedCandidate = candidateText.ToLowerInvariant().Trim();
            var normalizedToken = tokenName.Replace("_", " ").ToLowerInvariant();
            features.NormalizedExact = normalizedCandidate.Contains(normalizedToken) ||
                                      normalizedToken.Contains(normalizedCandidate);

            // 3. Fuzzy similarity (simple Levenshtein-based)
            features.Fuzzy = ComputeFuzzySimilarity(normalizedToken, normalizedCandidate);

            // 4. Pattern match (date, email, number, etc.)
            features.PatternMatch = DetectPatternMatch(tokenName, candidateText);

            // 5. Context match (heading path relevance)
            features.ContextMatch = CheckContextMatch(tokenName, headingPath);

            return features;
        }

        /// <summary>
        /// Generates features for all candidates from paragraphs
        /// </summary>
        public List<FeatureSet> GenerateFeaturesForAll(
            string tokenName,
            List<ParagraphDto> paragraphs,
            Helpers.StructureBuilder.SectionNode? bomTree = null)
        {
            var results = new List<FeatureSet>();

            foreach (var para in paragraphs)
            {
                var text = para.Text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                // Get heading path from BOM if available
                string? headingPath = null;
                if (bomTree != null)
                {
                    headingPath = FindHeadingPathForParagraph(bomTree, para.Index);
                }

                var features = GenerateFeatures(
                    tokenName,
                    text,
                    para.Index,
                    headingPath,
                    isTableCell: false,
                    isListItem: false
                );

                results.Add(features);
            }

            return results;
        }

        private int ComputeFuzzySimilarity(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
                return 0;

            if (s1 == s2)
                return 100;

            // Simple word overlap similarity
            var words1 = s1.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(w => w.ToLowerInvariant())
                          .ToHashSet();
            var words2 = s2.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(w => w.ToLowerInvariant())
                          .ToHashSet();

            if (words1.Count == 0 || words2.Count == 0)
                return 0;

            var intersection = words1.Intersect(words2).Count();
            var union = words1.Union(words2).Count();

            return (int)((double)intersection / union * 100);
        }

        private bool DetectPatternMatch(string tokenName, string candidateText)
        {
            var tokenLower = tokenName.ToLowerInvariant();

            // Date patterns
            if (tokenLower.Contains("date") || tokenLower.Contains("time"))
            {
                return System.Text.RegularExpressions.Regex.IsMatch(
                    candidateText,
                    @"\d{1,2}[/-]\d{1,2}[/-]\d{2,4}|\d{4}-\d{2}-\d{2}");
            }

            // Email patterns
            if (tokenLower.Contains("email") || tokenLower.Contains("mail"))
            {
                return System.Text.RegularExpressions.Regex.IsMatch(
                    candidateText,
                    @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b");
            }

            // Number patterns
            if (tokenLower.Contains("number") || tokenLower.Contains("count") || tokenLower.Contains("version"))
            {
                return System.Text.RegularExpressions.Regex.IsMatch(candidateText, @"\d+");
            }

            return false;
        }

        private bool CheckContextMatch(string tokenName, string? headingPath)
        {
            if (string.IsNullOrWhiteSpace(headingPath))
                return false;

            var tokenWords = tokenName.Replace("_", " ").ToLowerInvariant()
                                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Where(w => w.Length > 2)
                                    .ToHashSet();

            var headingLower = headingPath.ToLowerInvariant();

            return tokenWords.Any(word => headingLower.Contains(word));
        }

        private string? FindHeadingPathForParagraph(Helpers.StructureBuilder.SectionNode node, int paragraphIndex)
        {
            if (node.Paragraphs.Any(p => p.Index == paragraphIndex))
            {
                return node.HeadingPath;
            }

            foreach (var child in node.Children)
            {
                var path = FindHeadingPathForParagraph(child, paragraphIndex);
                if (path != null)
                    return path;
            }

            return null;
        }

        public class FeatureSet
        {
            public string TokenName { get; set; } = string.Empty;
            public string CandidateText { get; set; } = string.Empty;
            public int ParagraphIndex { get; set; }
            public string HeadingPath { get; set; } = string.Empty;
            public bool IsTableCell { get; set; }
            public bool IsListItem { get; set; }

            // Features
            public bool AliasHit { get; set; }
            public bool NormalizedExact { get; set; }
            public int Fuzzy { get; set; } // 0-100
            public bool PatternMatch { get; set; }
            public bool ContextMatch { get; set; }
        }
    }
}

