using System;
using System.Text;
using System.Text.RegularExpressions;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Step 1: Pre-Sanitizer
    /// Purpose: Clean the raw document so downstream stages don't get confused.
    /// Prevents: Wrong heading detection, wrong token extraction, broken fuzzy-matching
    /// </summary>
    public static class PreSanitizer
    {
        /// <summary>
        /// Normalizes text while preserving formatting metadata structure
        /// </summary>
        public static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var sb = new StringBuilder(text);

            // Normalize Unicode (remove non-printable control characters except common ones)
            for (int i = sb.Length - 1; i >= 0; i--)
            {
                var ch = sb[i];
                // Keep: tab, newline, carriage return, space
                if (char.IsControl(ch) && ch != '\t' && ch != '\n' && ch != '\r' && ch != ' ')
                {
                    sb.Remove(i, 1);
                }
            }

            // Normalize whitespace (collapse multiple spaces, but preserve line breaks)
            var normalized = Regex.Replace(sb.ToString(), @"[ \t]+", " ", RegexOptions.None);
            
            // Normalize line breaks (CRLF -> LF, but keep LF)
            normalized = normalized.Replace("\r\n", "\n").Replace("\r", "\n");

            // Standardize punctuation spacing (remove spaces before colons/semicolons if inconsistent)
            normalized = Regex.Replace(normalized, @"\s+([:;])", "$1", RegexOptions.None);
            normalized = Regex.Replace(normalized, @"([:;])\s*", "$1 ", RegexOptions.None);

            // Normalize numbering patterns (1. -> 1., 1) -> 1., etc.)
            normalized = Regex.Replace(normalized, @"(\d+)\s*\)\s+", "$1. ", RegexOptions.None);

            return normalized.Trim();
        }

        /// <summary>
        /// Normalizes paragraph text while preserving structure
        /// </summary>
        public static string NormalizeParagraph(string paragraphText)
        {
            if (string.IsNullOrWhiteSpace(paragraphText))
                return string.Empty;

            var normalized = NormalizeText(paragraphText);

            // Remove trailing whitespace from each line but keep line structure
            var lines = normalized.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = lines[i].TrimEnd();
            }

            return string.Join("\n", lines);
        }
    }
}

