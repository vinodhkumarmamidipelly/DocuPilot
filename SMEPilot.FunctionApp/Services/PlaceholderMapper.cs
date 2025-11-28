using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SMEPilot.FunctionApp.Models;
using TemplatePlaceholder = SMEPilot.FunctionApp.Services.TemplateProcessor.TemplatePlaceholder;

namespace SMEPilot.FunctionApp.Services
{
    /// <summary>
    /// Maps extracted structured data to template placeholders using exact rules
    /// No fuzzy matching - returns empty if no match found
    /// </summary>
    public class PlaceholderMapper
    {
        private readonly ILogger<PlaceholderMapper>? _logger;

        public PlaceholderMapper(ILogger<PlaceholderMapper>? logger = null)
        {
            _logger = logger;
        }

        private DocumentModel? _docModel; // Store document model for section-based mapping

        public Dictionary<string, string> Map(ExtractedData data, List<TemplatePlaceholder> placeholders, DocumentModel? docModel = null)
        {
            _docModel = docModel; // Store for section-based mapping
            var mapping = new Dictionary<string, string>();

            foreach (var placeholder in placeholders)
            {
                var name = NormalizePlaceholderName(placeholder.Name);
                var value = MapPlaceholder(name, data, placeholder);
                mapping[placeholder.Name] = value ?? "";
            }

            return mapping;
        }

        // Overload for backward compatibility
        public Dictionary<string, string> Map(ExtractedData data, List<TemplatePlaceholder> placeholders)
        {
            return Map(data, placeholders, null);
        }

        private string NormalizePlaceholderName(string name)
        {
            return name.ToLowerInvariant()
                .Replace("_", " ")
                .Replace("(", " ")
                .Replace(")", " ")
                .Replace("|", " ")
                .Replace("[", "")
                .Replace("]", "")
                .Trim();
        }

        private string? MapPlaceholder(string name, ExtractedData data, TemplatePlaceholder placeholder)
        {
            // Metadata - exact match
            if (name.Contains("project_name") || name.Contains("project name"))
            {
                var value = data.Metadata.ProjectName;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Project Name: {Value}", placeholder.Name, value);
                    return value;
                }
            }

            if (name.Contains("version") && !name.Contains("history"))
            {
                var value = data.Metadata.Version;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Version: {Value}", placeholder.Name, value);
                    return value;
                }
            }

            if (name.Contains("date") && !name.Contains("updated") && !name.Contains("history"))
            {
                var value = data.Metadata.Date;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Date: {Value}", placeholder.Name, value);
                    return value;
                }
            }

            if (name.Contains("status"))
            {
                var value = data.Metadata.Status;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Status: {Value}", placeholder.Name, value);
                    return value;
                }
            }

            if (name.Contains("author") && !name.Contains("approved"))
            {
                var value = data.Metadata.Author;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Author: {Value}", placeholder.Name, value);
                    return value;
                }
            }

            if (name.Contains("reviewer"))
            {
                var value = data.Metadata.Reviewer;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Reviewer: {Value}", placeholder.Name, value);
                    return value;
                }
            }

            if (name.Contains("approver"))
            {
                var value = data.Metadata.Approver;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Approver: {Value}", placeholder.Name, value);
                    return value;
                }
            }

            // Content sections - exact match
            if (name.Contains("overview") || name.Contains("high-level overview") || name.Contains("executive summary"))
            {
                var value = data.Sections.Overview;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Overview: {Length} chars", placeholder.Name, value.Length);
                    return value;
                }
            }

            if (name.Contains("business context"))
            {
                var value = data.Sections.BusinessContext;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Business Context: {Length} chars", placeholder.Name, value.Length);
                    return value;
                }
            }

            if (name.Contains("project description"))
            {
                var value = data.Sections.ProjectDescription;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Project Description: {Length} chars", placeholder.Name, value.Length);
                    return value;
                }
            }

            if (name.Contains("project objectives"))
            {
                var value = data.Sections.ProjectObjectives;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Project Objectives: {Length} chars", placeholder.Name, value.Length);
                    return value;
                }
            }

            if (name.Contains("business goals"))
            {
                var value = data.Sections.BusinessGoals;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Business Goals: {Length} chars", placeholder.Name, value.Length);
                    return value;
                }
            }

            if (name.Contains("scope") && !name.Contains("out of scope") && !name.Contains("item"))
            {
                var value = data.Sections.Scope;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Scope: {Length} chars", placeholder.Name, value.Length);
                    return value;
                }
            }

            if (name.Contains("out of scope"))
            {
                var value = data.Sections.OutOfScope;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Out of Scope: {Length} chars", placeholder.Name, value.Length);
                    return value;
                }
            }

            // Lists - try extracted list first, then fall back to section content
            if (name.Contains("persona name") || name.Contains("persona") || name.Contains("target users"))
            {
                // Try extracted list first
                var value = ExtractByIndex(data.Lists.Personas, placeholder);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Persona (from list): {Value}", placeholder.Name, value);
                    return value;
                }
                
                // Fallback: Use section content if document model available
                if (_docModel != null)
                {
                    var sectionContent = FindSectionContent(_docModel, "persona", "target users", "user types", "user personas");
                    if (!string.IsNullOrWhiteSpace(sectionContent))
                    {
                        _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Persona (from section): {Length} chars", placeholder.Name, sectionContent.Length);
                        return sectionContent;
                    }
                }
            }

            if (name.Contains("feature name") || name.Contains("feature"))
            {
                // Try extracted list first
                var value = ExtractByIndex(data.Lists.Features, placeholder);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Feature (from list): {Value}", placeholder.Name, value);
                    return value;
                }
                
                // Fallback: Use section content
                if (_docModel != null)
                {
                    var sectionContent = FindSectionContent(_docModel, "feature", "core features", "functional requirements", "4.1");
                    if (!string.IsNullOrWhiteSpace(sectionContent))
                    {
                        _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Feature (from section): {Length} chars", placeholder.Name, sectionContent.Length);
                        return sectionContent;
                    }
                }
            }

            if (name.Contains("workflow name") || name.Contains("workflow"))
            {
                // Try extracted list first
                var value = ExtractByIndex(data.Lists.Workflows, placeholder);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Workflow (from list): {Value}", placeholder.Name, value);
                    return value;
                }
                
                // Fallback: Use section content
                if (_docModel != null)
                {
                    var sectionContent = FindSectionContent(_docModel, "workflow", "user workflows", "2.2");
                    if (!string.IsNullOrWhiteSpace(sectionContent))
                    {
                        _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Workflow (from section): {Length} chars", placeholder.Name, sectionContent.Length);
                        return sectionContent;
                    }
                }
            }

            if (name.Contains("rule name") || (name.Contains("business rule") && name.Contains("name")))
            {
                var value = ExtractByIndex(data.Lists.BusinessRules, placeholder);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Business Rule: {Value}", placeholder.Name, value);
                    return value;
                }
            }

            if (name.Contains("entity name"))
            {
                var value = ExtractByIndex(data.Lists.Entities, placeholder);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Entity: {Value}", placeholder.Name, value);
                    return value;
                }
            }

            if (name.Contains("integration name"))
            {
                var value = ExtractByIndex(data.Lists.Integrations, placeholder);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Integration: {Value}", placeholder.Name, value);
                    return value;
                }
            }

            if (name.Contains("epic name"))
            {
                var value = ExtractByIndex(data.Lists.Epics, placeholder);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Epic: {Value}", placeholder.Name, value);
                    return value;
                }
            }

            if (name.Contains("story title") || name.Contains("user story") && name.Contains("title"))
            {
                var value = ExtractByIndex(data.Lists.UserStories, placeholder);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → User Story: {Value}", placeholder.Name, value);
                    return value;
                }
            }

            if (name.Contains("endpoint name"))
            {
                var value = ExtractByIndex(data.Lists.Endpoints, placeholder);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Endpoint: {Value}", placeholder.Name, value);
                    return value;
                }
            }

            if (name.Contains("flow name"))
            {
                var value = ExtractByIndex(data.Lists.Flows, placeholder);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Flow: {Value}", placeholder.Name, value);
                    return value;
                }
            }

            if (name.Contains("scope item"))
            {
                var value = ExtractByIndex(data.Lists.ScopeItems, placeholder);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Scope Item: {Value}", placeholder.Name, value);
                    return value;
                }
            }

            if (name.Contains("out of scope item"))
            {
                var value = ExtractByIndex(data.Lists.OutOfScopeItems, placeholder);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Out of Scope Item: {Value}", placeholder.Name, value);
                    return value;
                }
            }

            if (name.Contains("reference"))
            {
                var value = ExtractByIndex(data.Lists.References, placeholder);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _logger?.LogDebug("✅ [MAP] '{Placeholder}' → Reference: {Value}", placeholder.Name, value);
                    return value;
                }
            }

            // Not found - return null (will be empty in template)
            _logger?.LogDebug("⚠️ [MAP] No match found for '{Placeholder}'", placeholder.Name);
            return null;
        }

        /// <summary>
        /// Finds section content by searching for keywords in headings or body
        /// Returns entire section content (heading + body) if found
        /// </summary>
        private string? FindSectionContent(DocumentModel docModel, params string[] keywords)
        {
            if (docModel?.Sections == null || docModel.Sections.Count == 0)
                return null;

            foreach (var section in docModel.Sections)
            {
                var heading = section.Heading ?? "";
                var body = section.Body ?? "";
                var combined = (heading + " " + body).ToLowerInvariant();

                // Check if any keyword matches
                foreach (var keyword in keywords)
                {
                    if (combined.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        // Return heading + body
                        var content = (!string.IsNullOrWhiteSpace(heading) ? heading + "\n\n" : "") + body;
                        if (!string.IsNullOrWhiteSpace(content) && content.Length > 20) // Minimum content length
                        {
                            _logger?.LogDebug("✅ [MAP] Found section content for keywords '{Keywords}': {Length} chars", 
                                string.Join(", ", keywords), content.Length);
                            return content;
                        }
                    }
                }
            }

            return null;
        }

        private string? ExtractByIndex(List<string> list, TemplatePlaceholder placeholder)
        {
            if (list == null || list.Count == 0)
                return null;

            // Try to extract index from placeholder context
            // e.g., "Persona 1: [Persona Name]" → index 0
            // e.g., "Feature 2: [Feature Name]" → index 1
            var index = ExtractIndexFromContext(placeholder);

            if (index >= 0 && index < list.Count)
                return list[index];

            // If no index found, return first item (common case)
            return list.FirstOrDefault();
        }

        private int ExtractIndexFromContext(TemplatePlaceholder placeholder)
        {
            // Look for patterns like "Persona 1:", "Feature 2:", etc. in placeholder context
            // For now, we'll use the placeholder's section context if available
            // This is a simplified version - can be enhanced based on template structure
            
            // Check if placeholder name contains a number
            var numberMatch = Regex.Match(placeholder.Name, @"\d+");
            if (numberMatch.Success)
            {
                if (int.TryParse(numberMatch.Value, out var index))
                {
                    return index - 1; // Convert to 0-based index
                }
            }

            // Default: return -1 to indicate no index found (will use first item)
            return -1;
        }
    }
}

