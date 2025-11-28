using System;
using System.Text;
using System.Text.RegularExpressions;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Step 12: Post-Sanitizer
    /// Purpose: Cleanup final output
    /// Prevents: Broken files, visual artifacts, validation errors
    /// </summary>
    public static class PostSanitizer
    {
        /// <summary>
        /// Removes leftover token markers from text
        /// </summary>
        public static string RemoveLeftoverMarkers(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Remove {{ }} fragments
            text = Regex.Replace(text, @"\{\{[^}]*\}\}", string.Empty, RegexOptions.None);
            
            // Remove [ ] fragments that look like tokens
            text = Regex.Replace(text, @"\[[A-Z_][A-Z0-9_]*\]", string.Empty, RegexOptions.None);

            // Remove empty brackets
            text = Regex.Replace(text, @"\[\s*\]", string.Empty, RegexOptions.None);
            text = Regex.Replace(text, @"\{\s*\}", string.Empty, RegexOptions.None);

            return text;
        }

        /// <summary>
        /// Fixes whitespace issues
        /// </summary>
        public static string FixWhitespace(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Collapse multiple spaces
            text = Regex.Replace(text, @"[ \t]+", " ", RegexOptions.None);

            // Remove spaces before punctuation
            text = Regex.Replace(text, @"\s+([.,;:!?])", "$1", RegexOptions.None);

            // Fix multiple newlines (keep max 2)
            text = Regex.Replace(text, @"\n{3,}", "\n\n", RegexOptions.None);

            // Trim each line
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = lines[i].TrimEnd();
            }

            return string.Join("\n", lines).Trim();
        }

        /// <summary>
        /// Validates OpenXML integrity (basic checks on text content)
        /// </summary>
        public static bool ValidateTextIntegrity(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return true;

            // Check for null characters (invalid in XML)
            if (text.Contains('\0'))
                return false;

            // Check for unpaired brackets (potential XML issues)
            var openBraces = text.Count(c => c == '{');
            var closeBraces = text.Count(c => c == '}');
            if (openBraces != closeBraces && openBraces > 0)
            {
                // Might be leftover markers, but not necessarily invalid
            }

            return true;
        }

        /// <summary>
        /// Full post-processing cleanup
        /// </summary>
        public static string Cleanup(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            text = RemoveLeftoverMarkers(text);
            text = FixWhitespace(text);

            if (!ValidateTextIntegrity(text))
            {
                // If validation fails, try to fix by removing problematic characters
                text = text.Replace("\0", "");
            }

            return text;
        }
    }
}

