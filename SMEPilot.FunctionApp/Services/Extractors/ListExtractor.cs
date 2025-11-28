using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SMEPilot.FunctionApp.Models;

namespace SMEPilot.FunctionApp.Services.Extractors
{
    /// <summary>
    /// Extracts lists (Features, Personas, etc.) from specific sections
    /// Uses section context + pattern matching
    /// </summary>
    public class ListExtractor
    {
        private readonly ILogger<ListExtractor>? _logger;

        public ListExtractor(ILogger<ListExtractor>? logger = null)
        {
            _logger = logger;
        }

        public ListData Extract(string fullText)
        {
            return new ListData
            {
                Features = ExtractListFromText(fullText, "3.2", "Feature", "#### Feature"),
                Personas = ExtractPersonasFromText(fullText), // Special handling for personas
                Workflows = ExtractListFromText(fullText, "2.2", "Workflow", "#### 2.2"),
                BusinessRules = ExtractListFromText(fullText, "2.3", "Business Rule", "Rule", "#### 2.3"),
                Entities = ExtractListFromText(fullText, "2.4", "Entity", "Data Entity"),
                Integrations = ExtractListFromText(fullText, "2.5", "Integration", "#### 2.5"),
                Epics = ExtractListFromText(fullText, "3.1", "Epic", "#### Epic"),
                UserStories = ExtractListFromText(fullText, "3.2", "User Story", "Story", "#### Feature"),
                Endpoints = ExtractListFromText(fullText, "6.3", "Endpoint"),
                Flows = ExtractListFromText(fullText, "7.2", "Flow"),
                ScopeItems = ExtractScopeItems(fullText),
                OutOfScopeItems = ExtractOutOfScopeItems(fullText),
                References = ExtractListItems(fullText, "References", "1.4")
            };
        }

        private List<string> ExtractPersonasFromText(string fullText)
        {
            var personas = new List<string>();
            
            // Try multiple approaches to find personas
            
            // Approach 1: Find "1.2 Target Users" or "Target Users" section
            var sectionStart = fullText.IndexOf("1.2", StringComparison.OrdinalIgnoreCase);
            if (sectionStart < 0)
                sectionStart = fullText.IndexOf("Target Users", StringComparison.OrdinalIgnoreCase);
            if (sectionStart < 0)
                sectionStart = fullText.IndexOf("User Personas", StringComparison.OrdinalIgnoreCase);
            if (sectionStart < 0)
                sectionStart = fullText.IndexOf("Personas", StringComparison.OrdinalIgnoreCase);
            
            if (sectionStart >= 0)
            {
                var remainingText = fullText.Substring(sectionStart);
                var nextSectionPattern = @"^##\s+[0-9]|^###\s+1\.3|^###\s+[0-9]";
                var nextSectionMatch = Regex.Match(remainingText.Substring(100), nextSectionPattern, RegexOptions.Multiline);
                int sectionEnd = nextSectionMatch.Success 
                    ? 100 + nextSectionMatch.Index 
                    : Math.Min(remainingText.Length, 5000);
                
                var sectionText = remainingText.Substring(0, sectionEnd);
                
                // Pattern 1: **Trainer Persona** or **Client Persona**
                var personaPattern1 = @"\*\*([A-Z][a-z]+\s+Persona)\*\*";
                var matches1 = Regex.Matches(sectionText, personaPattern1, RegexOptions.IgnoreCase);
                foreach (Match match in matches1)
                {
                    var persona = match.Groups[1].Value.Trim();
                    if (!personas.Contains(persona, StringComparer.OrdinalIgnoreCase))
                    {
                        personas.Add(persona);
                    }
                }
                
                // Pattern 2: "Persona 1: Name" or "Persona 1 Name"
                var personaPattern2 = @"(?:Persona|User\s+Persona)\s+\d+\s*:?\s*([A-Z][A-Za-z0-9\s\-&]+?)(?:\s*$|\s*\n|\s+Persona)";
                var matches2 = Regex.Matches(sectionText, personaPattern2, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                foreach (Match match in matches2)
                {
                    var persona = match.Groups[1].Value.Trim().Trim('*', '\\');
                    if (!string.IsNullOrWhiteSpace(persona) && persona.Length >= 3 && persona.Length < 100 &&
                        !personas.Contains(persona, StringComparer.OrdinalIgnoreCase))
                    {
                        personas.Add(persona);
                    }
                }
            }
            
            // Approach 2: Search entire document for persona patterns (if not found in section)
            if (personas.Count == 0)
            {
                var personaPattern3 = @"\*\*([A-Z][a-z]+\s+Persona)\*\*";
                var matches3 = Regex.Matches(fullText, personaPattern3, RegexOptions.IgnoreCase);
                foreach (Match match in matches3)
                {
                    var persona = match.Groups[1].Value.Trim();
                    if (!personas.Contains(persona, StringComparer.OrdinalIgnoreCase))
                    {
                        personas.Add(persona);
                    }
                }
            }
            
            _logger?.LogDebug("✅ [LIST] Extracted {Count} personas: {Personas}", personas.Count, string.Join(", ", personas));
            return personas;
        }

        private List<string> ExtractScopeItems(string fullText)
        {
            var items = new List<string>();
            
            // Find "Project Scope" or "In Scope" section
            var scopeStart = fullText.IndexOf("Project Scope", StringComparison.OrdinalIgnoreCase);
            if (scopeStart < 0)
                scopeStart = fullText.IndexOf("In Scope", StringComparison.OrdinalIgnoreCase);
            
            if (scopeStart < 0)
                return items;
            
            var remainingText = fullText.Substring(scopeStart);
            var nextSectionPattern = @"^##\s+|^###\s+[0-9]";
            var nextSectionMatch = Regex.Match(remainingText.Substring(200), nextSectionPattern, RegexOptions.Multiline);
            int sectionEnd = nextSectionMatch.Success 
                ? 200 + nextSectionMatch.Index 
                : Math.Min(remainingText.Length, 2000);
            
            var sectionText = remainingText.Substring(0, sectionEnd);
            
            // Extract items like "- **In Scope (MVP)**: OTP authentication, client management..."
            var scopePattern = @"\*\*In Scope[^:]*:\*\*\s*([^\n]+)";
            var match = Regex.Match(sectionText, scopePattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var scopeText = match.Groups[1].Value;
                // Split by comma
                var itemsArray = scopeText.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var item in itemsArray)
                {
                    var trimmed = item.Trim().Trim('*', '\\', ')', ']', '[', '(', '|');
                    if (!string.IsNullOrWhiteSpace(trimmed) && trimmed.Length >= 3 && trimmed.Length < 200)
                    {
                        items.Add(trimmed);
                    }
                }
            }
            
            _logger?.LogDebug("✅ [LIST] Extracted {Count} scope items", items.Count);
            return items;
        }

        private List<string> ExtractOutOfScopeItems(string fullText)
        {
            var items = new List<string>();
            
            // Find "Out of Scope" section
            var scopeStart = fullText.IndexOf("Out of Scope", StringComparison.OrdinalIgnoreCase);
            if (scopeStart < 0)
                return items;
            
            var remainingText = fullText.Substring(scopeStart);
            var nextSectionPattern = @"^##\s+|^###\s+[0-9]";
            var nextSectionMatch = Regex.Match(remainingText.Substring(200), nextSectionPattern, RegexOptions.Multiline);
            int sectionEnd = nextSectionMatch.Success 
                ? 200 + nextSectionMatch.Index 
                : Math.Min(remainingText.Length, 2000);
            
            var sectionText = remainingText.Substring(0, sectionEnd);
            
            // Extract items like "- **Out of Scope (Future)**: Group chats, voice/video calls..."
            var scopePattern = @"\*\*Out of Scope[^:]*:\*\*\s*([^\n]+)";
            var match = Regex.Match(sectionText, scopePattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var scopeText = match.Groups[1].Value;
                // Split by comma
                var itemsArray = scopeText.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var item in itemsArray)
                {
                    var trimmed = item.Trim().Trim('*', '\\', ')', ']', '[', '(', '|');
                    if (!string.IsNullOrWhiteSpace(trimmed) && trimmed.Length >= 3 && trimmed.Length < 200)
                    {
                        items.Add(trimmed);
                    }
                }
            }
            
            _logger?.LogDebug("✅ [LIST] Extracted {Count} out of scope items", items.Count);
            return items;
        }

        private List<string> ExtractListFromText(string fullText, string sectionNumber, params string[] itemTypes)
        {
            var items = new List<string>();

            if (string.IsNullOrWhiteSpace(fullText))
                return items;

            // Find section by number (e.g., "3.2") or heading pattern
            string? sectionText = null;
            var sectionStart = -1;
            
            // Try to find section by number first
            var sectionNumberPattern = $@"^##\s+{Regex.Escape(sectionNumber)}|^###\s+{Regex.Escape(sectionNumber)}|^\d+\.\d+\s+";
            var sectionMatch = Regex.Match(fullText, sectionNumberPattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (sectionMatch.Success)
            {
                sectionStart = sectionMatch.Index;
            }
            else
            {
                // Try direct search for section number
                sectionStart = fullText.IndexOf(sectionNumber, StringComparison.OrdinalIgnoreCase);
            }

            if (sectionStart >= 0)
            {
                // Find end of section (next major heading)
                var remainingText = fullText.Substring(sectionStart);
                var nextSectionPattern = @"^##\s+[0-9]|^###\s+\d+\.\d+";
                var nextSectionMatch = Regex.Match(remainingText.Substring(100), nextSectionPattern, RegexOptions.Multiline);
                int sectionEnd = nextSectionMatch.Success 
                    ? 100 + nextSectionMatch.Index 
                    : Math.Min(remainingText.Length, 10000); // Limit to 10000 chars
                sectionText = remainingText.Substring(0, sectionEnd);
            }

            if (string.IsNullOrWhiteSpace(sectionText))
            {
                _logger?.LogDebug("⚠️ [LIST] Section {SectionNumber} not found in text", sectionNumber);
                return items;
            }

            // Extract items like "#### Feature 1: OTP Authentication" → "OTP Authentication"
            foreach (var itemType in itemTypes)
            {
                // Pattern: "#### Feature 1: Name" or "Feature 1: Name" or "* **Feature 1:** Name"
                var pattern = $@"(?:####|###|##|\*)\s*{Regex.Escape(itemType)}\s*\d+\s*:?\s*([^\n]+)";
                var matches = Regex.Matches(sectionText, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                foreach (Match match in matches)
                {
                    var itemName = match.Groups[1].Value.Trim().Trim('*', '\\', ')', ']', '[', '(', '|', '_');
                    if (!string.IsNullOrWhiteSpace(itemName) && itemName.Length >= 3 && itemName.Length < 200)
                    {
                        if (!items.Contains(itemName, StringComparer.OrdinalIgnoreCase))
                        {
                            items.Add(itemName);
                        }
                    }
                }
                
                // Also try pattern without "####" for plain text
                pattern = $@"{Regex.Escape(itemType)}\s*\d+\s*:?\s*([^\n]+)";
                matches = Regex.Matches(sectionText, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                foreach (Match match in matches)
                {
                    var itemName = match.Groups[1].Value.Trim().Trim('*', '\\', ')', ']', '[', '(', '|', '_');
                    if (!string.IsNullOrWhiteSpace(itemName) && itemName.Length >= 3 && itemName.Length < 200)
                    {
                        if (!items.Contains(itemName, StringComparer.OrdinalIgnoreCase))
                        {
                            items.Add(itemName);
                        }
                    }
                }
            }
            
            // If no items found with itemType patterns, try generic numbered list extraction
            if (items.Count == 0)
            {
                // Pattern: "#### Epic 1: Name" or "#### Feature 1: Name"
                var genericPattern = @"(?:####|###)\s+([A-Z][a-z]+)\s+\d+\s*:?\s*([^\n]+)";
                var matches = Regex.Matches(sectionText, genericPattern, RegexOptions.Multiline);
                foreach (Match match in matches)
                {
                    var itemType = match.Groups[1].Value;
                    var itemName = match.Groups[2].Value.Trim().Trim('*', '\\', ')', ']', '[', '(', '|', '_');
                    
                    // Check if this itemType matches any of our target types
                    bool matchesType = itemTypes.Any(t => itemType.Contains(t, StringComparison.OrdinalIgnoreCase) || t.Contains(itemType, StringComparison.OrdinalIgnoreCase));
                    
                    if (matchesType && !string.IsNullOrWhiteSpace(itemName) && itemName.Length >= 3 && itemName.Length < 200)
                    {
                        if (!items.Contains(itemName, StringComparer.OrdinalIgnoreCase))
                        {
                            items.Add(itemName);
                        }
                    }
                }
            }
            
            _logger?.LogDebug("✅ [LIST] Extracted {Count} {ItemType} items from section {SectionNumber}: {Items}", 
                items.Count, string.Join("/", itemTypes), sectionNumber, string.Join(", ", items.Take(5)));
            
            return items;
        }
        
        private List<string> ExtractListItems(string fullText, string sectionKeyword, string? sectionNumber = null)
        {
            var items = new List<string>();

            // Find section by keyword
            var sectionStart = fullText.IndexOf(sectionKeyword, StringComparison.OrdinalIgnoreCase);
            if (sectionStart < 0)
            {
                _logger?.LogDebug("⚠️ [LIST] Section '{SectionKeyword}' not found", sectionKeyword);
                return items;
            }

            // Extract text from section start to next major section (## or numbered heading)
            var sectionEnd = fullText.IndexOf("\n##", sectionStart + sectionKeyword.Length);
            if (sectionEnd < 0)
                sectionEnd = fullText.Length;

            var sectionText = fullText.Substring(sectionStart, sectionEnd - sectionStart);

            // Extract bullet points or numbered items
            var patterns = new[]
            {
                @"^[-*]\s+(.+?)(?=\n[-*]|\n\d+\.|\n##|$)",  // Bullet points
                @"^\d+\.\s+(.+?)(?=\n\d+\.|\n[-*]|\n##|$)",  // Numbered list
                @"^-\s+\*\*(.+?)\*\*",  // Bold bullet points
            };

            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(sectionText, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                foreach (Match match in matches)
                {
                    var item = match.Groups[1].Value.Trim().Trim('*', '\\');
                    if (!string.IsNullOrWhiteSpace(item) && item.Length >= 3 && item.Length < 500)
                    {
                        // Avoid duplicates and document title
                        if (!items.Contains(item, StringComparer.OrdinalIgnoreCase) &&
                            !item.Contains("FUNCTIONAL SPECIFICATION", StringComparison.OrdinalIgnoreCase))
                        {
                            items.Add(item);
                        }
                    }
                }
            }

            _logger?.LogDebug("✅ [LIST] Extracted {Count} items from '{SectionKeyword}'", items.Count, sectionKeyword);
            return items;
        }
    }
}

