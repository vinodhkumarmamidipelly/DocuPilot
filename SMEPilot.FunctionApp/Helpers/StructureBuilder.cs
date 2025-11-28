using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SMEPilot.FunctionApp.Models;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Step 2: StructureBuilder (HeadingParser + BOM)
    /// Purpose: Convert raw paragraphs → structured hierarchical tree (like HTML DOM)
    /// Prevents: Wrong content classification, scoring engine mixing unrelated content
    /// </summary>
    public class StructureBuilder
    {
        /// <summary>
        /// Represents a hierarchical section in the document
        /// </summary>
        public class SectionNode
        {
            public string Heading { get; set; } = string.Empty;
            public string Number { get; set; } = string.Empty; // e.g. "1.1"
            public int Level { get; set; }
            /// <summary>
            /// Paragraph index where this section's heading occurred in the raw document.
            /// This allows us to attach tables that are anchored at the heading paragraph.
            /// </summary>
            public int HeadingParagraphIndex { get; set; } = -1;
            public List<ParagraphDto> Paragraphs { get; set; } = new();
            public List<TableDto> Tables { get; set; } = new();
            public List<SectionNode> Children { get; set; } = new();
            public SectionNode? Parent { get; set; }
            public string HeadingPath { get; set; } = string.Empty; // e.g., "1.1 > Overview"
        }

        /// <summary>
        /// Builds hierarchical BOM tree from paragraphs
        /// </summary>
        public SectionNode BuildBOM(List<ParagraphDto> paragraphs, List<TableDto>? tables = null)
        {
            var root = new SectionNode { Heading = "Document", Level = 0 };
            var currentPath = new Stack<SectionNode>();
            currentPath.Push(root);

            tables ??= new List<TableDto>();

            foreach (var para in paragraphs)
            {
                var headingInfo = DetectHeading(para);

                if (headingInfo.IsHeading)
                {
                    // Pop stack until we find the right parent level
                    while (currentPath.Count > 1 && currentPath.Peek().Level >= headingInfo.Level)
                    {
                        currentPath.Pop();
                    }

                    var parent = currentPath.Peek();
                    var section = new SectionNode
                    {
                        Heading = headingInfo.Heading,
                        Number = headingInfo.Number,
                        Level = headingInfo.Level,
                        HeadingParagraphIndex = para.Index,
                        Parent = parent,
                        HeadingPath = string.IsNullOrEmpty(parent.HeadingPath)
                            ? (!string.IsNullOrEmpty(headingInfo.Number) ? $"{headingInfo.Number} {headingInfo.Heading}" : headingInfo.Heading)
                            : $"{parent.HeadingPath} > {(!string.IsNullOrEmpty(headingInfo.Number) ? headingInfo.Number + " " : string.Empty)}{headingInfo.Heading}"
                    };

                    parent.Children.Add(section);
                    currentPath.Push(section);
                }
                else
                {
                    // Add paragraph to current section
                    currentPath.Peek().Paragraphs.Add(para);
                }
            }

            // Attach tables to nearest section
            foreach (var table in tables)
            {
                var anchorSection = FindSectionForParagraph(root, table.AnchorParagraphIndex);
                if (anchorSection != null)
                {
                    anchorSection.Tables.Add(table);
                }
            }

            return root;
        }

        /// <summary>
        /// Detects if paragraph is a heading (explicit, numbered, or virtual)
        /// </summary>
        private (bool IsHeading, string Heading, int Level, string Number) DetectHeading(ParagraphDto para)
        {
            var styleId = para.StyleId?.Replace(" ", "").ToLowerInvariant() ?? string.Empty;
            var text = para.Text?.Trim() ?? string.Empty;

            // 1. Explicit heading via style (Heading1/2/3/4 etc.)
            if (styleId.StartsWith("heading", StringComparison.OrdinalIgnoreCase))
            {
                var level = 1;
                var levelStr = styleId.Replace("heading", "");
                if (!int.TryParse(levelStr, out level))
                {
                    if (styleId.Contains("heading1")) level = 1;
                    else if (styleId.Contains("heading2")) level = 2;
                    else if (styleId.Contains("heading3")) level = 3;
                    else if (styleId.Contains("heading4")) level = 4;
                }

                // Try to parse a numeric prefix like "1.1 Project Information"
                var numberedMatch = Regex.Match(text, @"^(\d+(?:\.\d+)*)\s+(.+)$");
                if (numberedMatch.Success)
                {
                    var numberPart = numberedMatch.Groups[1].Value;
                    var headingText = numberedMatch.Groups[2].Value.Trim();
                    return (true, headingText, level, numberPart);
                }

                return (true, text, level, string.Empty);
            }

            // 2. Numbered heading without explicit heading style (1., 1.1, 2.3.4, etc.)
            var numberedOnlyMatch = Regex.Match(text, @"^(\d+(?:\.\d+)*)\s+(.+)$");
            if (numberedOnlyMatch.Success)
            {
                var numberPart = numberedOnlyMatch.Groups[1].Value;
                var headingText = numberedOnlyMatch.Groups[2].Value.Trim();
                var level = numberPart.Split('.').Length;
                return (true, headingText, level, numberPart);
            }

            // 3. Virtual heading (only when there is no explicit style)
            if (string.IsNullOrEmpty(styleId) &&
                text.Length > 0 && text.Length < 100 &&
                !text.Contains('.') &&
                !text.Contains(',') &&
                char.IsUpper(text[0]))
            {
                if (text.Length < 50 && (text == text.ToUpper() || IsTitleCase(text)))
                {
                    return (true, text, 3, string.Empty); // Default to level 3 for virtual
                }
            }

            return (false, text, 0, string.Empty);
        }

        private bool IsTitleCase(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return words.All(w => w.Length > 0 && char.IsUpper(w[0]));
        }

        private SectionNode? FindSectionForParagraph(SectionNode root, int paragraphIndex)
        {
            // 1) Prefer a section that actually contains this paragraph as content
            foreach (var section in EnumerateSections(root))
            {
                if (section.Paragraphs.Any(p => p.Index == paragraphIndex))
                {
                    return section;
                }
            }

            // 2) Fall back to a section whose heading was at this paragraph index
            foreach (var section in EnumerateSections(root))
            {
                if (section.HeadingParagraphIndex == paragraphIndex)
                {
                    return section;
                }
            }

            // 3) As a last resort, attach to the root
            return root;
        }

        private IEnumerable<SectionNode> EnumerateSections(SectionNode node)
        {
            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var descendant in EnumerateSections(child))
                {
                    yield return descendant;
                }
            }
        }
    }
}

