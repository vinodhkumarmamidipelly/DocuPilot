using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Step 3: AliasManager
    /// Purpose: Provide synonyms for template fields so matching is consistent
    /// Prevents: Wrong-field mappings, low-confidence matches
    /// </summary>
    public class AliasManager
    {
        private readonly Dictionary<string, List<string>> _aliases;
        private readonly Dictionary<string, List<string>> _learnedSynonyms;

        public AliasManager(Dictionary<string, List<string>>? staticAliases = null)
        {
            _aliases = staticAliases ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            _learnedSynonyms = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Loads aliases from FieldMappingSpec.json or custom alias file
        /// </summary>
        public static AliasManager LoadFromConfig(string? configPath = null)
        {
            var aliases = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (string.IsNullOrWhiteSpace(configPath))
                {
                    var basePath = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
                    configPath = Path.Combine(basePath, "Config", "FieldMappingSpec.json");
                }

                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var spec = JsonConvert.DeserializeObject<Dictionary<string, FieldConfig>>(json);

                    if (spec != null)
                    {
                        foreach (var kvp in spec)
                        {
                            var fieldName = kvp.Key;
                            var cfg = kvp.Value;

                            var aliasList = new List<string>();

                            // Collect LabelPatterns
                            if (cfg.LabelPatterns != null)
                            {
                                aliasList.AddRange(cfg.LabelPatterns);
                            }

                            // Collect LinePatterns
                            if (cfg.LinePatterns != null)
                            {
                                aliasList.AddRange(cfg.LinePatterns);
                            }

                            if (aliasList.Count > 0)
                            {
                                aliases[fieldName] = aliasList;
                            }
                        }
                    }
                }
            }
            catch
            {
                // If loading fails, return empty alias manager
            }

            return new AliasManager(aliases);
        }

        /// <summary>
        /// Checks if a text matches any alias for a field
        /// </summary>
        public bool IsAlias(string fieldName, string text)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(fieldName))
                return false;

            var textLower = text.ToLowerInvariant().Trim();
            var allAliases = ExpandSynonyms(fieldName);

            return allAliases.Any(alias => 
                textLower.Contains(alias.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Expands all synonyms for a field (static + learned)
        /// </summary>
        public List<string> ExpandSynonyms(string fieldName)
        {
            var result = new List<string>();

            // Add static aliases
            if (_aliases.TryGetValue(fieldName, out var staticList))
            {
                result.AddRange(staticList);
            }

            // Add learned synonyms
            if (_learnedSynonyms.TryGetValue(fieldName, out var learnedList))
            {
                result.AddRange(learnedList);
            }

            // Add field name itself as fallback
            if (result.Count == 0)
            {
                result.Add(fieldName.Replace("_", " ").ToLowerInvariant());
            }

            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Learn a new synonym from human correction
        /// </summary>
        public void LearnSynonym(string fieldName, string synonym)
        {
            if (string.IsNullOrWhiteSpace(fieldName) || string.IsNullOrWhiteSpace(synonym))
                return;

            if (!_learnedSynonyms.ContainsKey(fieldName))
            {
                _learnedSynonyms[fieldName] = new List<string>();
            }

            if (!_learnedSynonyms[fieldName].Contains(synonym, StringComparer.OrdinalIgnoreCase))
            {
                _learnedSynonyms[fieldName].Add(synonym);
            }
        }

        /// <summary>
        /// Returns all field names that have static or learned aliases.
        /// Useful for diagnostics and block classification.
        /// </summary>
        public IEnumerable<string> GetAllFieldNames()
        {
            return _aliases.Keys
                .Concat(_learnedSynonyms.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private class FieldConfig
        {
            public string? Type { get; set; }
            public List<string>? LabelPatterns { get; set; }
            public List<string>? LinePatterns { get; set; }
        }
    }
}

