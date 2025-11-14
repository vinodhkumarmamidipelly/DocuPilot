using System;
using System.Collections.Generic;
using System.Linq;
using SMEPilot.FunctionApp.Models;
using Microsoft.Extensions.Logging;

namespace SMEPilot.FunctionApp.Services
{
    /// <summary>
    /// Simplified content mapper - uses direct, predictable mapping instead of complex heuristics
    /// </summary>
    public static class SimplifiedContentMapper
    {
        /// <summary>
        /// Builds content map using simple, direct mapping approach
        /// Maps sections by explicit markers first, then by position/heading
        /// </summary>
        public static Dictionary<string, string> BuildContentMap(
            DocumentModel docModel, 
            string? documentType,
            List<string>? availableTemplateTags = null,
            ILogger? logger = null)
        {
            var contentMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var usedSections = new HashSet<int>();

            // Document metadata
            if (!string.IsNullOrWhiteSpace(docModel.Title))
                contentMap["DocumentTitle"] = docModel.Title;

            if (!string.IsNullOrWhiteSpace(documentType))
                contentMap["DocumentType"] = documentType;

            if (docModel.Sections == null || docModel.Sections.Count == 0)
            {
                logger?.LogWarning("⚠️ [MAPPER] No sections in DocumentModel");
                return contentMap;
            }

            logger?.LogInformation("📋 [MAPPER] Mapping {SectionCount} sections to template tags", docModel.Sections.Count);

            // Step 1: Find sections with explicit markers (most reliable)
            for (int i = 0; i < docModel.Sections.Count; i++)
            {
                var section = docModel.Sections[i];
                var heading = section.Heading ?? "";
                var body = section.Body ?? "";
                if (string.IsNullOrWhiteSpace(body)) continue;

                var headingLower = heading.ToLowerInvariant();
                var bodyLower = body.ToLowerInvariant();

                string? targetTag = null;
                string contentToMap = body;

                // Check for explicit markers in body text (most reliable)
                if (bodyLower.Contains("functional overview:") || bodyLower.Contains("functional details:"))
                {
                    targetTag = "Functional";
                    contentToMap = ExtractAfterMarker(body, new[] { "Functional Overview:", "Functional Details:" });
                }
                else if (bodyLower.Contains("technical implementation:") || bodyLower.Contains("technical details:"))
                {
                    targetTag = "Technical";
                    contentToMap = ExtractAfterMarker(body, new[] { "Technical Implementation:", "Technical Details:" });
                }
                else if (bodyLower.Contains("troubleshooting:") || bodyLower.Contains("known issues:"))
                {
                    targetTag = "Findings";
                }
                else if (bodyLower.Contains("references:") || bodyLower.Contains("reference:") || 
                         bodyLower.Contains("http://") || bodyLower.Contains("https://"))
                {
                    targetTag = "References";
                }
                // Check heading (simpler matching)
                else if (headingLower.Contains("overview") || headingLower.Contains("summary") || headingLower.Contains("introduction"))
                {
                    targetTag = "Overview";
                }
                else if (headingLower.Contains("functional") && !headingLower.Contains("technical"))
                {
                    targetTag = "Functional";
                }
                else if (headingLower.Contains("technical") || headingLower.Contains("implementation"))
                {
                    targetTag = "Technical";
                }
                else if (headingLower.Contains("reference") || headingLower.Contains("link"))
                {
                    targetTag = "References";
                }

                if (targetTag != null)
                {
                    // Only map if template has this tag (if availableTemplateTags provided)
                    if (availableTemplateTags == null || availableTemplateTags.Contains(targetTag, StringComparer.OrdinalIgnoreCase))
                    {
                        if (contentMap.ContainsKey(targetTag))
                        {
                            contentMap[targetTag] = contentMap[targetTag] + "\n\n" + contentToMap;
                        }
                        else
                        {
                            contentMap[targetTag] = contentToMap;
                        }
                        usedSections.Add(i);
                        logger?.LogDebug("✅ [MAPPER] Mapped section {Index} ({Heading}) → {Tag}", i, heading, targetTag);
                    }
                }
            }

            // Step 2: Map remaining sections by position (simple fallback)
            // First unmapped section → Overview (if not already filled)
            if (!contentMap.ContainsKey("Overview"))
            {
                for (int i = 0; i < docModel.Sections.Count; i++)
                {
                    if (usedSections.Contains(i)) continue;
                    var body = docModel.Sections[i].Body ?? "";
                    if (body.Length > 50)
                    {
                        contentMap["Overview"] = body;
                        usedSections.Add(i);
                        logger?.LogDebug("✅ [MAPPER] Mapped first unmapped section → Overview");
                        break;
                    }
                }
            }

            // Remaining sections → Technical or Functional based on content keywords
            for (int i = 0; i < docModel.Sections.Count; i++)
            {
                if (usedSections.Contains(i)) continue;
                var section = docModel.Sections[i];
                var body = section.Body ?? "";
                if (string.IsNullOrWhiteSpace(body)) continue;

                var bodyLower = body.ToLowerInvariant();
                string? targetTag = null;

                // Simple keyword detection
                if (bodyLower.Contains("api") || bodyLower.Contains("endpoint") || bodyLower.Contains("cron") || 
                    bodyLower.Contains("webhook") || bodyLower.Contains("microservice") || bodyLower.Contains("service"))
                {
                    targetTag = "Technical";
                }
                else if (bodyLower.Contains("functional") || bodyLower.Contains("feature") || bodyLower.Contains("workflow"))
                {
                    targetTag = "Functional";
                }

                if (targetTag != null)
                {
                    // Only map if template has this tag and it's not already filled
                    if ((availableTemplateTags == null || availableTemplateTags.Contains(targetTag, StringComparer.OrdinalIgnoreCase)) &&
                        !contentMap.ContainsKey(targetTag))
                    {
                        contentMap[targetTag] = body;
                        usedSections.Add(i);
                        logger?.LogDebug("✅ [MAPPER] Mapped section {Index} → {Tag} (keyword-based)", i, targetTag);
                    }
                }
            }

            logger?.LogInformation("✅ [MAPPER] Content map built: {Count} entries ({Keys})", 
                contentMap.Count, string.Join(", ", contentMap.Keys));

            return contentMap;
        }

        private static string ExtractAfterMarker(string text, string[] markers)
        {
            var textLower = text.ToLowerInvariant();
            foreach (var marker in markers)
            {
                var markerLower = marker.ToLowerInvariant();
                var index = textLower.IndexOf(markerLower);
                if (index >= 0)
                {
                    var startIndex = index + marker.Length;
                    return text.Substring(startIndex).Trim();
                }
            }
            return text;
        }
    }
}

