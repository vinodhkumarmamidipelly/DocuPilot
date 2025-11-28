using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SMEPilot.FunctionApp.Models;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Step 4: StructuredExtractors
    /// Purpose: Detect higher-level structures that paragraphs alone can't express
    /// Prevents: Misinterpreting table rows as normal paragraphs, loss of structured data
    /// </summary>
    public static class StructuredExtractors
    {
        /// <summary>
        /// Extracts key-value pairs from paragraphs (e.g., "Label: Value")
        /// </summary>
        public static List<KeyValuePair<string, string>> ExtractKeyValueLines(List<ParagraphDto> paragraphs)
        {
            var result = new List<KeyValuePair<string, string>>();
            var kvPattern = new Regex(@"^([^:]+):\s*(.+)$", RegexOptions.Multiline);

            foreach (var para in paragraphs)
            {
                var text = para.Text ?? string.Empty;
                var lines = text.Split('\n');

                foreach (var line in lines)
                {
                    var match = kvPattern.Match(line.Trim());
                    if (match.Success)
                    {
                        var key = match.Groups[1].Value.Trim();
                        var value = match.Groups[2].Value.Trim();
                        if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                        {
                            result.Add(new KeyValuePair<string, string>(key, value));
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Extracts bullet/numbered lists from paragraphs
        /// </summary>
        public static List<ListItem> ExtractLists(List<ParagraphDto> paragraphs)
        {
            var result = new List<ListItem>();
            var bulletPattern = new Regex(@"^[\u2022\u25CF\u25CB\u25A0\-\*]\s+(.+)$");
            var numberedPattern = new Regex(@"^(\d+)[\.\)]\s+(.+)$");

            foreach (var para in paragraphs)
            {
                var text = para.Text ?? string.Empty;
                var lines = text.Split('\n');

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed))
                        continue;

                    var bulletMatch = bulletPattern.Match(trimmed);
                    if (bulletMatch.Success)
                    {
                        result.Add(new ListItem
                        {
                            Text = bulletMatch.Groups[1].Value.Trim(),
                            Type = ListItemType.Bullet,
                            Index = result.Count
                        });
                        continue;
                    }

                    var numberedMatch = numberedPattern.Match(trimmed);
                    if (numberedMatch.Success)
                    {
                        if (int.TryParse(numberedMatch.Groups[1].Value, out var number))
                        {
                            result.Add(new ListItem
                            {
                                Text = numberedMatch.Groups[2].Value.Trim(),
                                Type = ListItemType.Numbered,
                                Index = number
                            });
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Extracts bullet blocks (consecutive bullet items)
        /// </summary>
        public static List<List<string>> ExtractBulletBlocks(List<ParagraphDto> paragraphs)
        {
            var blocks = new List<List<string>>();
            var currentBlock = new List<string>();
            var bulletPattern = new Regex(@"^[\u2022\u25CF\u25CB\u25A0\-\*]\s+(.+)$");

            foreach (var para in paragraphs)
            {
                var text = para.Text ?? string.Empty;
                var lines = text.Split('\n');

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    var match = bulletPattern.Match(trimmed);

                    if (match.Success)
                    {
                        currentBlock.Add(match.Groups[1].Value.Trim());
                    }
                    else
                    {
                        if (currentBlock.Count > 0)
                        {
                            blocks.Add(new List<string>(currentBlock));
                            currentBlock.Clear();
                        }
                    }
                }
            }

            if (currentBlock.Count > 0)
            {
                blocks.Add(currentBlock);
            }

            return blocks;
        }

        /// <summary>
        /// Extracts embedded content metadata (images, tables already handled separately)
        /// </summary>
        public static List<EmbeddedContent> ExtractEmbeddedContent(List<ParagraphDto> paragraphs)
        {
            var result = new List<EmbeddedContent>();
            var urlPattern = new Regex(@"https?://[^\s]+", RegexOptions.IgnoreCase);
            var emailPattern = new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b");

            foreach (var para in paragraphs)
            {
                var text = para.Text ?? string.Empty;

                // Extract URLs
                foreach (Match match in urlPattern.Matches(text))
                {
                    result.Add(new EmbeddedContent
                    {
                        Type = EmbeddedContentType.Url,
                        Value = match.Value,
                        ParagraphIndex = para.Index
                    });
                }

                // Extract emails
                foreach (Match match in emailPattern.Matches(text))
                {
                    result.Add(new EmbeddedContent
                    {
                        Type = EmbeddedContentType.Email,
                        Value = match.Value,
                        ParagraphIndex = para.Index
                    });
                }
            }

            return result;
        }

        public class ListItem
        {
            public string Text { get; set; } = string.Empty;
            public ListItemType Type { get; set; }
            public int Index { get; set; }
        }

        public enum ListItemType
        {
            Bullet,
            Numbered
        }

        public class EmbeddedContent
        {
            public EmbeddedContentType Type { get; set; }
            public string Value { get; set; } = string.Empty;
            public int ParagraphIndex { get; set; }
        }

        public enum EmbeddedContentType
        {
            Url,
            Email
        }
    }
}

