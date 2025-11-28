using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SMEPilot.FunctionApp.Models;

namespace SMEPilot.FunctionApp.Services.Extractors
{
    /// <summary>
    /// Extracts content sections directly from full text using regex patterns
    /// NO dependency on section parsing - works with raw text
    /// </summary>
    public class SectionExtractor
    {
        private readonly ILogger<SectionExtractor>? _logger;

        public SectionExtractor(ILogger<SectionExtractor>? logger = null)
        {
            _logger = logger;
        }

        public SectionData Extract(string fullText)
        {
            return new SectionData
            {
                Overview = FindSectionInText(fullText, "1. PROJECT OVERVIEW", "1 PROJECT OVERVIEW", "project overview", "overview", "executive summary", "summary", "introduction", "2.1", "2\\.1"),
                BusinessContext = FindSectionInText(fullText, "2.3", "2\\.3", "business context", "context"),
                ProjectDescription = FindSectionInText(fullText, "1.1", "1\\.1", "project description", "project information", "description"),
                ProjectObjectives = FindSectionInText(fullText, "1.1", "1\\.1", "project objectives", "objectives"),
                BusinessGoals = FindSectionInText(fullText, "3.4", "3\\.4", "business goals", "goals"),
                Scope = FindSectionInText(fullText, "1.1", "1\\.1", "project scope", "scope", "in scope"),
                OutOfScope = FindSectionInText(fullText, "1.1", "1\\.1", "out of scope", "out of scope")
            };
        }

        private string? FindSectionInText(string fullText, params string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(fullText))
                return null;

            // Try to find section heading using various patterns
            int sectionStart = -1;
            string? foundHeading = null;

            foreach (var keyword in keywords)
            {
                // Pattern 1: Numbered heading (e.g., "2.1 Overview" or "2.1. Overview")
                var numberedPattern = $@"^\d+\.\d+\s*\.?\s*{Regex.Escape(keyword)}";
                var numberedMatch = Regex.Match(fullText, numberedPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                if (numberedMatch.Success)
                {
                    sectionStart = numberedMatch.Index;
                    foundHeading = numberedMatch.Value;
                    break;
                }

                // Pattern 2: Markdown heading (e.g., "## Overview")
                var markdownPattern = $@"^##\s+{Regex.Escape(keyword)}";
                var markdownMatch = Regex.Match(fullText, markdownPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                if (markdownMatch.Success)
                {
                    sectionStart = markdownMatch.Index;
                    foundHeading = markdownMatch.Value;
                    break;
                }

                // Pattern 3: Plain heading (e.g., "Overview" or "PROJECT OVERVIEW")
                var plainPattern = $@"^(?:#+\s+)?{Regex.Escape(keyword)}(?:\s|$|:)";
                var plainMatch = Regex.Match(fullText, plainPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                if (plainMatch.Success && (sectionStart < 0 || plainMatch.Index < sectionStart))
                {
                    sectionStart = plainMatch.Index;
                    foundHeading = plainMatch.Value;
                }
            }

            if (sectionStart < 0)
            {
                _logger?.LogDebug("⚠️ [SECTION] Section not found for keywords: {Keywords}", string.Join(", ", keywords));
                return null;
            }

            // Find the end of this section (next heading or end of document)
            var remainingText = fullText.Substring(sectionStart);
            
            // Find next heading (numbered, markdown, or plain)
            var nextHeadingPattern = @"(?:^\d+\.\d+[\.\)]?\s+[A-Z]|^##\s+[A-Z]|^#+\s+[A-Z])";
            var nextHeadingMatch = Regex.Match(remainingText.Substring(foundHeading?.Length ?? 0), nextHeadingPattern, RegexOptions.Multiline);
            
            int sectionEnd = nextHeadingMatch.Success 
                ? foundHeading.Length + nextHeadingMatch.Index 
                : Math.Min(remainingText.Length, 2000); // Limit to 2000 chars max

            var sectionContent = remainingText.Substring(foundHeading?.Length ?? 0, sectionEnd - (foundHeading?.Length ?? 0))
                .Trim();

            if (string.IsNullOrWhiteSpace(sectionContent))
            {
                _logger?.LogDebug("⚠️ [SECTION] Section '{Heading}' has no content", foundHeading);
                return null;
            }

            // Limit to first 1000 chars
            if (sectionContent.Length > 1000)
            {
                // Try to get first paragraph
                var firstParagraph = sectionContent.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(firstParagraph) && firstParagraph.Length <= 1000)
                    sectionContent = firstParagraph;
                else
                    sectionContent = sectionContent.Substring(0, 1000) + "...";
            }

            _logger?.LogDebug("✅ [SECTION] Found section '{Heading}' ({Length} chars)", 
                foundHeading, sectionContent.Length);
            return sectionContent;
        }
    }
}

