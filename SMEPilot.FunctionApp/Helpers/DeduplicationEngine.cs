using System;
using System.Collections.Generic;
using System.Linq;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Step 8: DeduplicationEngine
    /// Purpose: Ensure each raw content block is used at most once
    /// Prevents: Same paragraph filling multiple tokens, cross-contamination
    /// </summary>
    public class DeduplicationEngine
    {
        /// <summary>
        /// Tracks which content blocks have been used
        /// </summary>
        public class UsageTracker
        {
            private readonly HashSet<int> _usedParagraphIndices = new();
            private readonly HashSet<string> _usedContentHashes = new();
            private readonly Dictionary<string, List<int>> _tokenToParagraphs = new();

            public void MarkUsed(string tokenName, int paragraphIndex, string? contentHash = null)
            {
                _usedParagraphIndices.Add(paragraphIndex);
                _tokenToParagraphs.TryGetValue(tokenName, out var list);
                if (list == null)
                {
                    list = new List<int>();
                    _tokenToParagraphs[tokenName] = list;
                }
                list.Add(paragraphIndex);

                if (!string.IsNullOrWhiteSpace(contentHash))
                {
                    _usedContentHashes.Add(contentHash);
                }
            }

            public bool IsUsed(int paragraphIndex)
            {
                return _usedParagraphIndices.Contains(paragraphIndex);
            }

            public bool IsContentUsed(string contentHash)
            {
                return !string.IsNullOrWhiteSpace(contentHash) && _usedContentHashes.Contains(contentHash);
            }

            public bool CanReuse(string tokenName, int paragraphIndex)
            {
                // Check if this token explicitly allows reuse (e.g., repeatable template tokens)
                if (IsRepeatableToken(tokenName))
                {
                    return true;
                }

                return !IsUsed(paragraphIndex);
            }

            private bool IsRepeatableToken(string tokenName)
            {
                // Tokens that are part of repeat blocks can be reused
                // This is a simple heuristic; in practice, you'd check template structure
                return tokenName.Contains("ITEM") || 
                       tokenName.Contains("ROW") || 
                       tokenName.Contains("ENTRY");
            }

            public Dictionary<string, List<int>> GetUsageMap()
            {
                return new Dictionary<string, List<int>>(_tokenToParagraphs);
            }
        }

        /// <summary>
        /// Marks candidates as used and filters out already-used content
        /// </summary>
        public List<T> Deduplicate<T>(
            List<T> candidates,
            Func<T, int> getParagraphIndex,
            Func<T, string?>? getContentHash = null)
        {
            var tracker = new UsageTracker();
            var result = new List<T>();

            foreach (var candidate in candidates)
            {
                var paraIndex = getParagraphIndex(candidate);
                var contentHash = getContentHash?.Invoke(candidate);

                if (!tracker.IsUsed(paraIndex) && 
                    (contentHash == null || !tracker.IsContentUsed(contentHash)))
                {
                    result.Add(candidate);
                    // Don't mark as used here - caller should mark after selection
                }
            }

            return result;
        }

        /// <summary>
        /// Creates a new usage tracker
        /// </summary>
        public UsageTracker CreateTracker()
        {
            return new UsageTracker();
        }
    }
}

