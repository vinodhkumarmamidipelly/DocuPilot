using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SMEPilot.FunctionApp.Helpers;
using SMEPilot.FunctionApp.Models;
using SMEPilot.FunctionApp.Services;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;

namespace SMEPilot.FunctionApp.Functions
{
    /// <summary>
    /// Lightweight test endpoint for template-driven enrichment.
    /// 
    /// IMPORTANT:
    /// - Does NOT touch SharePoint, Graph, or metadata.
    /// - Does NOT change or invoke the main webhook pipeline.
    /// - Intended only for local / manual testing of enrichment behaviour.
    /// 
    /// Request format (application/json):
    /// {
    ///   "rawFileBase64": "BASE64_OF_DOCX",
    ///   "templateFileBase64": "BASE64_OF_DOTX_OR_DOCX_TEMPLATE",
    ///   "rawFileName": "Raw.docx",
    ///   "templateFileName": "UniversalOrgTemplate.dotx"
    /// }
    /// 
    /// Response:
    /// {
    ///   "success": true,
    ///   "message": "string",
    ///   "filledPlaceholderCount": 123,
    ///   "enrichedFileName": "Raw_enriched.docx",
    ///   "enrichedFileBase64": "BASE64_OF_RESULT"
    /// }
    /// </summary>
    public class TestTemplateEnrichment
    {
        private readonly DocumentExtractor _extractor;
        private readonly TemplateProcessor _templateProcessor;
        private readonly ILogger<TestTemplateEnrichment> _logger;
        private static Dictionary<string, FieldConfig>? _fieldMappingSpecCache;

        public TestTemplateEnrichment(
            DocumentExtractor extractor,
            TemplateProcessor templateProcessor,
            ILogger<TestTemplateEnrichment> logger)
        {
            _extractor = extractor;
            _templateProcessor = templateProcessor;
            _logger = logger;
        }

        /// <summary>
        /// Attempts to resolve a list-type field directly from the rich semantic tree
        /// (StructuredSections). This is used for fields whose values are naturally
        /// modeled as lists under a label, e.g. "Project Objectives".
        ///
        /// Strategy:
        /// - Derive candidate labels from field name and config labelPatterns.
        /// - Search all RichSections/fields for a matching label.
        /// - If the matching field's Value is a List&lt;string&gt;, return it.
        /// - If it's a List&lt;RichField&gt;, flatten to strings using "Name: Value" when needed.
        /// </summary>
        private List<string> ExtractListFromStructuredSections(
            string fieldName,
            FieldConfig? config,
            List<RichSectionExtractor.RichSection> structuredSections)
        {
            var items = new List<string>();
            if (structuredSections == null || structuredSections.Count == 0)
            {
                return items;
            }

            // Normalize field name
            var normalizedFieldName = NormalizePlaceholderName(fieldName);

            // Candidate labels from config + token-derived label
            var labelCandidates = new List<string>();
            if (config?.LabelPatterns != null && config.LabelPatterns.Count > 0)
            {
                labelCandidates.AddRange(config.LabelPatterns.Where(lp => !string.IsNullOrWhiteSpace(lp)));
            }

            labelCandidates.Add(
                normalizedFieldName
                    .Trim('#', '/')
                    .Replace("_", " "));

            // Add variations for better matching
            var additionalCandidates = new List<string>();
            foreach (var candidate in labelCandidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                var baseCandidate = candidate.Trim().ToLowerInvariant();
                additionalCandidates.Add(baseCandidate);

                // Add variation without spaces
                if (baseCandidate.Contains(" "))
                {
                    additionalCandidates.Add(baseCandidate.Replace(" ", ""));
                }

                // Add variation with underscores
                if (!baseCandidate.Contains("_"))
                {
                    additionalCandidates.Add(baseCandidate.Replace(" ", "_"));
                }
            }

            var normalizedLabels = additionalCandidates
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct()
                .ToList();

            if (normalizedLabels.Count == 0)
            {
                return items;
            }

            foreach (var section in structuredSections)
            {
                if (section.Value == null || section.Value.Count == 0)
                    continue;

                var sectionName = section.Name ?? string.Empty;
                var sectionLower = sectionName.ToLowerInvariant();

                // 1) If the section name itself matches the label (e.g. "Glossary",
                //    "Data Entities"), treat the entire section body as the list.
                // Improved: use word boundaries for better matching
                var sectionMatches = normalizedLabels.Any(lbl =>
                {
                    if (string.IsNullOrWhiteSpace(sectionLower) || string.IsNullOrWhiteSpace(lbl))
                        return false;

                    // Exact match (highest priority)
                    if (string.Equals(sectionLower, lbl, StringComparison.OrdinalIgnoreCase))
                        return true;

                    // Word boundary match (e.g., "data entities" matches "entities" as a word)
                    var wordPattern = @"\b" + Regex.Escape(lbl) + @"\b";
                    if (Regex.IsMatch(sectionLower, wordPattern, RegexOptions.IgnoreCase))
                        return true;

                    // Contains match (fallback for multi-word labels)
                    return sectionLower.Contains(lbl, StringComparison.OrdinalIgnoreCase);
                });

                if (sectionMatches)
                {
                    foreach (var field in section.Value)
                    {
                        if (field.Value is string s && !string.IsNullOrWhiteSpace(s))
                        {
                            items.Add(s.Trim());
                        }
                        else if (field.Value is List<string> listStrings)
                        {
                            items.AddRange(listStrings.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
                        }
                        else if (field.Value is List<RichSectionExtractor.RichField> childFields)
                        {
                            foreach (var cf in childFields)
                            {
                                if (cf.Value is string vs && !string.IsNullOrWhiteSpace(vs))
                                {
                                    items.Add(vs.Trim());
                                }
                                else if (cf.Value is List<string> nestedList)
                                {
                                    items.AddRange(nestedList.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
                                }
                            }
                        }
                    }

                    if (items.Count > 0)
                    {
                        return items;
                    }
                }

                // 2) Otherwise, look for a matching child field label within the section
                foreach (var field in section.Value)
                {
                    var name = field.Name ?? string.Empty;
                    var lower = name.ToLowerInvariant();

                    // Improved matching with word boundaries
                    var isMatch = normalizedLabels.Any(lbl =>
                    {
                        if (string.IsNullOrWhiteSpace(lbl))
                            return false;

                        // Exact match (highest priority)
                        if (string.Equals(lower, lbl, StringComparison.OrdinalIgnoreCase))
                            return true;

                        // Word boundary match
                        var wordPattern = @"\b" + Regex.Escape(lbl) + @"\b";
                        if (Regex.IsMatch(lower, wordPattern, RegexOptions.IgnoreCase))
                            return true;

                        // Contains match (fallback)
                        return lower.Contains(lbl, StringComparison.OrdinalIgnoreCase);
                    });

                    // Also consider matches inside child field labels (e.g. "In Scope (MVP)"
                    // and "Out of Scope (Future)" under a parent "Project Scope").
                    List<RichSectionExtractor.RichField>? childFieldsForMatch = null;
                    bool childLabelMatch = false;
                    if (!isMatch && field.Value is List<RichSectionExtractor.RichField> childFieldsTmp)
                    {
                        childFieldsForMatch = childFieldsTmp;
                        childLabelMatch = childFieldsTmp.Any(cf =>
                        {
                            var childName = cf.Name ?? string.Empty;
                            var childLower = childName.ToLowerInvariant();
                            return normalizedLabels.Any(lbl =>
                            {
                                if (string.IsNullOrWhiteSpace(childLower) || string.IsNullOrWhiteSpace(lbl))
                                    return false;

                                // Exact match
                                if (string.Equals(childLower, lbl, StringComparison.OrdinalIgnoreCase))
                                    return true;

                                // Word boundary match
                                var wordPattern = @"\b" + Regex.Escape(lbl) + @"\b";
                                if (Regex.IsMatch(childLower, wordPattern, RegexOptions.IgnoreCase))
                                    return true;

                                // Contains match
                                return childLower.Contains(lbl, StringComparison.OrdinalIgnoreCase);
                            });
                        });
                    }

                    // Additionally, for some documents the label text may live only in the
                    // value string (e.g. "Out of Scope (Future): Group chats, ...") instead
                    // of the Name. In that case, treat a leading label-pattern match in the
                    // value as a hit as well so simple lists like OUT_SCOPE_ITEMS can bind.
                    bool valueLabelMatch = false;
                    string? rawValueText = null;
                    if (!isMatch && !childLabelMatch && field.Value is string valueText && !string.IsNullOrWhiteSpace(valueText))
                    {
                        rawValueText = valueText;
                        var valueLower = valueText.ToLowerInvariant();
                        valueLabelMatch = normalizedLabels.Any(lbl =>
                        {
                            if (string.IsNullOrWhiteSpace(lbl))
                                return false;

                            // We only care about labels that appear at the start of the line;
                            // this avoids accidentally matching inner fragments of long sentences.
                            return valueLower.StartsWith(lbl, StringComparison.OrdinalIgnoreCase);
                        });
                    }

                    if (!isMatch && !childLabelMatch && !valueLabelMatch)
                        continue;

                    switch (field.Value)
                    {
                        // Direct string value on the matched field itself (e.g. a bullet
                        // "- In Scope (MVP): OTP authentication, ..."). In this case we
                        // only want the underlying value text, not the label prefix.
                        case string s when !string.IsNullOrWhiteSpace(s):
                        {
                            var text = s.Trim();

                            // If the label lives in the value (valueLabelMatch), strip the
                            // leading "Label: " portion using the configured LabelPatterns
                            // when available. This keeps values clean while remaining
                            // schema/config-driven (no field-name hard-coding).
                            if (valueLabelMatch && config?.LabelPatterns != null && config.LabelPatterns.Count > 0)
                            {
                                foreach (var lp in config.LabelPatterns.Where(lp => !string.IsNullOrWhiteSpace(lp)))
                                {
                                    var core = Regex.Escape(lp.Trim());
                                    // Allow optional qualifier in parentheses between the core label
                                    // and the colon, e.g. "Out of Scope (Future): ..."
                                    var pattern = @"^\s*" + core + @"(?:\s*\([^)]*\))?\s*:\s*(.*)$";
                                    var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                                    if (m.Success)
                                    {
                                        text = m.Groups[1].Value.Trim();
                                        break;
                                    }
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                items.Add(text);
                            }
                            break;
                        }

                        case List<string> listStrings:
                            items.AddRange(listStrings.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()));
                            break;

                        case List<RichSectionExtractor.RichField> childFields:
                            // Prefer children whose labels match our normalized labels (e.g. "In Scope"
                            // vs "Out of Scope"). If none match, fall back to all children to keep
                            // behaviour compatible for sections that don't use nested labels.
                            var candidates = childFieldsForMatch ?? childFields;
                            var matchingChildren = candidates
                                .Where(cf =>
                                {
                                    var childName = cf.Name ?? string.Empty;
                                    var childLower = childName.ToLowerInvariant();
                                    return normalizedLabels.Any(lbl =>
                                    {
                                        if (string.IsNullOrWhiteSpace(childLower) || string.IsNullOrWhiteSpace(lbl))
                                            return false;

                                        // Exact match
                                        if (string.Equals(childLower, lbl, StringComparison.OrdinalIgnoreCase))
                                            return true;

                                        // Word boundary match
                                        var wordPattern = @"\b" + Regex.Escape(lbl) + @"\b";
                                        if (Regex.IsMatch(childLower, wordPattern, RegexOptions.IgnoreCase))
                                            return true;

                                        // Contains match
                                        return childLower.Contains(lbl, StringComparison.OrdinalIgnoreCase);
                                    });
                                })
                                .ToList();

                            if (matchingChildren.Count == 0)
                            {
                                matchingChildren = childFields;
                            }

                            foreach (var cf in matchingChildren)
                            {
                                if (cf.Value is string vs && !string.IsNullOrWhiteSpace(vs))
                                {
                                    // For list fields we only want the actual value text, not the
                                    // child label prefix (e.g. "In Scope (MVP):" / "Out of Scope (Future):").
                                    // The label itself is exposed via configuration / diagnostics.
                                    var text = vs.Trim();

                                    if (config?.LabelPatterns != null && config.LabelPatterns.Count > 0)
                                    {
                                        foreach (var lp in config.LabelPatterns.Where(lp => !string.IsNullOrWhiteSpace(lp)))
                                        {
                                            var core = Regex.Escape(lp.Trim());
                                            // Allow optional qualifier in parentheses between the core label
                                            // and the colon, e.g. "Out of Scope (Future): ..."
                                            var pattern = @"^\s*" + core + @"(?:\s*\([^)]*\))?\s*:\s*(.*)$";
                                            var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                                            if (m.Success)
                                            {
                                                text = m.Groups[1].Value.Trim();
                                                break;
                                            }
                                        }
                                    }

                                    if (!string.IsNullOrWhiteSpace(text))
                                    {
                                        items.Add(text);
                                    }
                                }
                                else if (cf.Value is List<string> nestedList)
                                {
                                    items.AddRange(nestedList.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
                                }
                            }
                            break;
                    }

                    if (items.Count > 0)
                    {
                        return items;
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// Extracts structured list records from the rich semantic tree for list-type
        /// fields that expose multiple element fields (e.g. BUSINESS_RULES with
        /// RULE_NAME, RULE_ACTIONS, RULE_CONDITIONS, RULE_DESCRIPTION).
        ///
        /// This walks the matching section's nested RichField tree and, for each
        /// leaf "group" (a named field whose Value is a List&lt;string&gt;), creates
        /// one record object. ElementFields are then populated generically:
        /// - "*NAME"   → group label (e.g. "OTP Rules")
        /// - "*ACTION" → joined bullet text
        /// - others    → empty string (placeholder)
        ///
        /// This keeps the logic schema-driven (based on ElementFields) and does
        /// not hard-code any particular parent field name like BUSINESS_RULES.
        /// </summary>
        private List<Dictionary<string, object?>> ExtractStructuredListRecordsFromSections(
            string fieldName,
            FieldConfig? config,
            List<RichSectionExtractor.RichSection> structuredSections)
        {
            var records = new List<Dictionary<string, object?>>();
            if (structuredSections == null || structuredSections.Count == 0)
            {
                return records;
            }

            // Normalise field name and build candidate labels (same strategy as
            // ExtractListFromStructuredSections) so we can find the right section
            // root for this list field.
            var normalizedFieldName = NormalizePlaceholderName(fieldName);

            var labelCandidates = new List<string>();
            if (config?.LabelPatterns != null && config.LabelPatterns.Count > 0)
            {
                labelCandidates.AddRange(config.LabelPatterns.Where(lp => !string.IsNullOrWhiteSpace(lp)));
            }

            // Base candidate derived from the token name (e.g. "BUSINESS_RULES" -> "Business Rules")
            var baseTokenLabel = normalizedFieldName
                .Trim('#', '/')
                .Replace("_", " ");

            if (!string.IsNullOrWhiteSpace(baseTokenLabel))
            {
                labelCandidates.Add(baseTokenLabel);

                // Also add the last word as an additional candidate (e.g. "Rules" from
                // "Business Rules"). This allows generic containers such as BUSINESS_RULES
                // to bind to all concrete "* Rules" sections (Authentication Rules,
                // Client Management Rules, etc.) without hard-coding field names.
                var parts = baseTokenLabel
                    .Split(new[] { ' ', '_' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length > 1)
                {
                    var last = parts[^1];
                    if (!string.IsNullOrWhiteSpace(last) && last.Length > 3)
                    {
                        labelCandidates.Add(last);
                    }
                }
            }

            var additionalCandidates = new List<string>();
            foreach (var candidate in labelCandidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                var baseCandidate = candidate.Trim().ToLowerInvariant();
                additionalCandidates.Add(baseCandidate);

                if (baseCandidate.Contains(" "))
                {
                    additionalCandidates.Add(baseCandidate.Replace(" ", ""));
                }

                if (!baseCandidate.Contains("_"))
                {
                    additionalCandidates.Add(baseCandidate.Replace(" ", "_"));
                }
            }

            var normalizedLabels = additionalCandidates
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct()
                .ToList();

            if (normalizedLabels.Count == 0)
            {
                return records;
            }

            // Helper: build records from a TableDto attached under the matching
            // section/field. This is used for table-shaped list fields such as
            // version history / change logs, and is driven entirely by the
            // ElementFields schema (no hard-coded field names).
            void BuildRecordsFromTable(TableDto table)
            {
                if (table?.Rows == null || table.Rows.Count < 2)
                    return;

                var headerRow = table.Rows[0];
                if (headerRow == null || headerRow.Count == 0)
                    return;

                var dataRows = table.Rows.Skip(1).ToList();
                if (dataRows.Count == 0)
                    return;

                var elementNames = config?.ElementFields ?? new List<string>();
                if (elementNames.Count == 0)
                    return;

                static string NormalizeToken(string input)
                {
                    var lower = (input ?? string.Empty).ToLowerInvariant();
                    return Regex.Replace(lower, "[^a-z0-9]+", string.Empty);
                }

                static string GetElementCore(string element)
                {
                    var lower = (element ?? string.Empty).ToLowerInvariant();
                    var parts = lower.Split('_', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length <= 1)
                        return NormalizeToken(lower);

                    // Drop the first segment (generic prefix like VERSION_, CHANGE_, TABLE_, etc.)
                    var core = string.Join("_", parts.Skip(1));
                    return NormalizeToken(core);
                }

                var normalizedHeaders = headerRow
                    .Select(h => NormalizeToken(h ?? string.Empty))
                    .ToList();

                var columnForElement = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var rawName in elementNames.Where(n => !string.IsNullOrWhiteSpace(n)))
                {
                    var elementName = rawName.Trim();
                    var core = GetElementCore(elementName);
                    if (string.IsNullOrEmpty(core))
                        continue;

                    var bestIndex = -1;
                    var bestScore = 0;

                    for (int col = 0; col < normalizedHeaders.Count; col++)
                    {
                        var hNorm = normalizedHeaders[col];
                        if (string.IsNullOrEmpty(hNorm))
                            continue;

                        var score = 0;
                        if (hNorm == core)
                        {
                            score = 3;
                        }
                        else if (hNorm.Contains(core, StringComparison.Ordinal) ||
                                 core.Contains(hNorm, StringComparison.Ordinal))
                        {
                            score = 2;
                        }

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestIndex = col;
                        }
                    }

                    if (bestIndex >= 0)
                    {
                        columnForElement[elementName] = bestIndex;
                    }
                }

                if (columnForElement.Count == 0)
                    return;

                foreach (var row in dataRows)
                {
                    if (row == null || row.Count == 0)
                        continue;

                    var record = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var rawName in elementNames.Where(n => !string.IsNullOrWhiteSpace(n)))
                    {
                        var elementName = rawName.Trim();

                        if (columnForElement.TryGetValue(elementName, out var colIndex) &&
                            colIndex >= 0 && colIndex < row.Count)
                        {
                            record[elementName] = (row[colIndex] ?? string.Empty).Trim();
                        }
                        else
                        {
                            record[elementName] = string.Empty;
                        }
                    }

                    if (record.Count > 0)
                    {
                        records.Add(record);
                    }
                }
            }

            // Helper: search a field tree for any attached TableDto instances and
            // build records from them. Returns true if at least one table was used.
            bool TryCollectFromTables(IEnumerable<RichSectionExtractor.RichField> fields)
            {
                var found = false;

                void Walk(RichSectionExtractor.RichField field)
                {
                    if (field == null)
                        return;

                    switch (field.Value)
                    {
                        case TableDto table:
                            BuildRecordsFromTable(table);
                            found = true;
                            break;

                        case List<RichSectionExtractor.RichField> children:
                            foreach (var child in children)
                            {
                                Walk(child);
                            }
                            break;
                    }
                }

                foreach (var f in fields)
                {
                    Walk(f);
                }

                return found;
            }

            // Helper: recursively walk a RichField tree and produce one record
            // per leaf group. A "leaf group" is either:
            // - a named field whose Value is List<string>  (e.g. "OTP Rules" + bullets)
            // - a named field whose Value is a scalar string (e.g. "User" + "id, role, ...")
            void CollectLeafGroups(RichSectionExtractor.RichField field, string? parentLabel = null)
            {
                if (field == null)
                    return;

                switch (field.Value)
                {
                    case List<RichSectionExtractor.RichField> children:
                        // If this is a named parent whose children are all anonymous scalar
                        // lines, treat them as a single leaf group (steps/bullets) rather
                        // than separate records. This covers shapes like:
                        //   "Client Management Feature" -> bullet list of criteria
                        //   "Trainer Workflow: Client Onboarding" -> ordered list of steps.
                        if (!string.IsNullOrWhiteSpace(field.Name) &&
                            children.All(c => string.IsNullOrWhiteSpace(c.Name) &&
                                              c.Value is string sChild &&
                                              !string.IsNullOrWhiteSpace(sChild)))
                        {
                            var groupLabel = field.Name;
                            var leafItems = children
                                .Select(c => (c.Value as string)?.Trim())
                                .Where(v => !string.IsNullOrWhiteSpace(v))
                                .ToList();

                            if (leafItems.Count == 0)
                                return;

                            var groupCombinedText = string.Join("\n", leafItems);

                            var groupRecord = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                            var groupElementNames = config?.ElementFields ?? new List<string>();

                            var groupLabelElements = groupElementNames
                                .Where(n => !string.IsNullOrWhiteSpace(n))
                                .Select(n => n.Trim())
                                .Where(n =>
                                {
                                    var lower = n.ToLowerInvariant();
                                    return lower.Contains("name") || lower.Contains("term");
                                })
                                .ToList();

                            var groupValueElements = groupElementNames
                                .Where(n => !string.IsNullOrWhiteSpace(n))
                                .Select(n => n.Trim())
                                .Where(n => !groupLabelElements.Contains(n, StringComparer.OrdinalIgnoreCase))
                                .ToList();

                            // Prefer a value field that looks like "steps" or "description"
                            // for the combined text; otherwise fall back to the first.
                            string? groupPrimaryValueElement = groupValueElements
                                .FirstOrDefault(v =>
                                {
                                    var lower = v.ToLowerInvariant();
                                    return lower.Contains("steps") || lower.Contains("description");
                                })
                                ?? groupValueElements.FirstOrDefault();

                            foreach (var rawName in groupElementNames.Where(n => !string.IsNullOrWhiteSpace(n)))
                            {
                                var childName = rawName.Trim();

                                if (groupLabelElements.Contains(childName, StringComparer.OrdinalIgnoreCase))
                                {
                                    groupRecord[childName] = groupLabel.Trim();
                                }
                                else
                                {
                                    groupRecord[childName] =
                                        groupPrimaryValueElement != null &&
                                        string.Equals(childName, groupPrimaryValueElement, StringComparison.OrdinalIgnoreCase)
                                            ? groupCombinedText
                                            : string.Empty;
                                }
                            }

                            if (groupRecord.Count > 0)
                            {
                                records.Add(groupRecord);
                            }
                        }
                        else
                        {
                            foreach (var child in children)
                            {
                                // Propagate the most specific non-empty label downwards so that
                                // anonymous bullets/lines can still inherit a meaningful name
                                // from their parent heading.
                                var nextParent = string.IsNullOrWhiteSpace(field.Name)
                                    ? parentLabel
                                    : field.Name;
                                CollectLeafGroups(child, nextParent);
                            }
                        }
                        break;

                    case List<string> leafItems:
                        var label = !string.IsNullOrWhiteSpace(field.Name)
                            ? field.Name
                            : (parentLabel ?? string.Empty);
                        if (string.IsNullOrWhiteSpace(label))
                            return;

                        var nonEmptyItems = leafItems
                            .Where(v => !string.IsNullOrWhiteSpace(v))
                            .Select(v => v.Trim())
                            .ToList();

                        var combinedText = string.Join("\n", nonEmptyItems);

                        var record = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        var elementNames = config?.ElementFields ?? new List<string>();

                        // Split element fields into "label-like" and "value-like" buckets
                        // based purely on their own names. Any field whose name contains
                        // "name" or "term" is treated as a label carrier (e.g. RULE_NAME,
                        // METRIC_NAME, GLOSSARY_TERM). All other fields are value slots.
                        var labelElements = elementNames
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .Select(n => n.Trim())
                            .Where(n =>
                            {
                                var lower = n.ToLowerInvariant();
                                return lower.Contains("name") || lower.Contains("term");
                            })
                            .ToList();

                        var valueElements = elementNames
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .Select(n => n.Trim())
                            .Where(n => !labelElements.Contains(n, StringComparer.OrdinalIgnoreCase))
                            .ToList();

                        string? primaryValueElement = valueElements
                            .FirstOrDefault(v =>
                            {
                                var lower = v.ToLowerInvariant();
                                return lower.Contains("steps") || lower.Contains("description");
                            })
                            ?? valueElements.FirstOrDefault();

                        foreach (var rawName in elementNames.Where(n => !string.IsNullOrWhiteSpace(n)))
                        {
                            var childName = rawName.Trim();

                            if (labelElements.Contains(childName, StringComparer.OrdinalIgnoreCase))
                            {
                                // Label-like fields (e.g. RULE_NAME, METRIC_NAME, GLOSSARY_TERM)
                                // carry the group label.
                                record[childName] = label.Trim();
                            }
                            else
                            {
                                // Assign the combined text only to the primary value-like
                                // element field; subsequent ones stay as empty placeholders.
                                record[childName] =
                                    primaryValueElement != null &&
                                    string.Equals(childName, primaryValueElement, StringComparison.OrdinalIgnoreCase)
                                        ? combinedText
                                        : string.Empty;
                            }
                        }

                        if (record.Count > 0)
                        {
                            records.Add(record);
                        }
                        break;

                    case string scalarText:
                        var labelScalar = !string.IsNullOrWhiteSpace(field.Name)
                            ? field.Name
                            : (parentLabel ?? string.Empty);
                        var valueScalar = scalarText?.Trim() ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(labelScalar) && string.IsNullOrWhiteSpace(valueScalar))
                            return;

                        var recordScalar = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        var elementNamesScalar = config?.ElementFields ?? new List<string>();

                        // Same strategy as above for scalar fields: label-like elements
                        // (containing "name" or "term") carry the label, and the first
                        // value-like element carries the raw value text.
                        var labelElementsScalar = elementNamesScalar
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .Select(n => n.Trim())
                            .Where(n =>
                            {
                                var lower = n.ToLowerInvariant();
                                return lower.Contains("name") || lower.Contains("term");
                            })
                            .ToList();

                        var valueElementsScalar = elementNamesScalar
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .Select(n => n.Trim())
                            .Where(n => !labelElementsScalar.Contains(n, StringComparer.OrdinalIgnoreCase))
                            .ToList();

                        string? primaryValueElementScalar = valueElementsScalar
                            .FirstOrDefault(v =>
                            {
                                var lower = v.ToLowerInvariant();
                                return lower.Contains("steps") || lower.Contains("description");
                            })
                            ?? valueElementsScalar.FirstOrDefault();

                        foreach (var rawName in elementNamesScalar.Where(n => !string.IsNullOrWhiteSpace(n)))
                        {
                            var childName = rawName.Trim();

                            if (labelElementsScalar.Contains(childName, StringComparer.OrdinalIgnoreCase))
                            {
                                // Prefer the field label; fall back to value text.
                                recordScalar[childName] = string.IsNullOrWhiteSpace(labelScalar)
                                    ? valueScalar
                                    : labelScalar.Trim();
                            }
                            else
                            {
                                recordScalar[childName] =
                                    primaryValueElementScalar != null &&
                                    string.Equals(childName, primaryValueElementScalar, StringComparison.OrdinalIgnoreCase)
                                        ? valueScalar
                                        : string.Empty;
                            }
                        }

                        if (recordScalar.Count > 0)
                        {
                            records.Add(recordScalar);
                        }
                        break;
                }
            }

            foreach (var section in structuredSections)
            {
                if (section.Value == null || section.Value.Count == 0)
                    continue;

                var sectionName = section.Name ?? string.Empty;
                var sectionLower = sectionName.ToLowerInvariant();

                bool sectionMatches = normalizedLabels.Any(lbl =>
                {
                    if (string.IsNullOrWhiteSpace(sectionLower) || string.IsNullOrWhiteSpace(lbl))
                        return false;

                    var labelLower = lbl.ToLowerInvariant();

                    if (string.Equals(sectionLower, labelLower, StringComparison.OrdinalIgnoreCase))
                        return true;

                    // Direct word-boundary check on the full label
                    var wordPattern = @"\b" + Regex.Escape(labelLower) + @"\b";
                    if (Regex.IsMatch(sectionLower, wordPattern, RegexOptions.IgnoreCase))
                        return true;

                    if (sectionLower.Contains(labelLower, StringComparison.OrdinalIgnoreCase))
                        return true;

                    // Token-level matching with simple singular/plural handling to keep
                    // things robust for cases like FEATURES → "Feature 1: ...".
                    var labelWords = labelLower
                        .Split(new[] { ' ', '_' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(w => w.Length > 2)
                        .ToArray();

                    if (labelWords.Length == 0)
                        return false;

                    bool allMatch = labelWords.All(w =>
                    {
                        var token = w;

                        // Try the token as-is
                        var tokenPattern = @"\b" + Regex.Escape(token) + @"\b";
                        if (Regex.IsMatch(sectionLower, tokenPattern, RegexOptions.IgnoreCase) ||
                            sectionLower.Contains(token, StringComparison.OrdinalIgnoreCase))
                            return true;

                        // Try singular form
                        var singularUpper = ToSingular(token.ToUpperInvariant());
                        var singular = singularUpper.ToLowerInvariant();
                        if (!string.Equals(singular, token, StringComparison.OrdinalIgnoreCase))
                        {
                            var singularPattern = @"\b" + Regex.Escape(singular) + @"\b";
                            if (Regex.IsMatch(sectionLower, singularPattern, RegexOptions.IgnoreCase) ||
                                sectionLower.Contains(singular, StringComparison.OrdinalIgnoreCase))
                                return true;
                        }

                        // Try plural form
                        var plural = token.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                            ? token
                            : token + "s";
                        if (!string.Equals(plural, token, StringComparison.OrdinalIgnoreCase))
                        {
                            var pluralPattern = @"\b" + Regex.Escape(plural) + @"\b";
                            if (Regex.IsMatch(sectionLower, pluralPattern, RegexOptions.IgnoreCase) ||
                                sectionLower.Contains(plural, StringComparison.OrdinalIgnoreCase))
                                return true;
                        }

                        return false;
                    });

                    return allMatch;
                });

                if (sectionMatches)
                {
                    // If there is a table under this section, prefer using it to
                    // build structured records (e.g. VERSION_HISTORY, CHANGE_LOG).
                    if (!TryCollectFromTables(section.Value))
                    {
                        // Treat the entire section as a single logical group so that
                        // a heading with anonymous child bullets (e.g.
                        // "Authentication Feature" → list of acceptance lines,
                        // "Trainer Workflow: Client Onboarding" → list of steps)
                        // produces ONE record per heading rather than one record per
                        // bullet. We do this by wrapping the section in a synthetic
                        // RichField whose Value is the section's field list.
                        var synthetic = new RichSectionExtractor.RichField
                        {
                            Name = section.Name,
                            Value = section.Value
                        };

                        CollectLeafGroups(synthetic, section.Name);
                    }
                }
                else
                {
                    // No direct section heading match; fall back to searching for a
                    // matching label inside this section's field tree. This covers
                    // cases like PERSONAS where the document uses a label such as
                    // "User Personas" under a broader heading ("Target Users").
                    void SearchFieldsForLabel(RichSectionExtractor.RichField field)
                    {
                        if (field == null)
                            return;

                        var fieldName = field.Name ?? string.Empty;
                        var fieldLower = fieldName.ToLowerInvariant();

                        bool fieldMatches = normalizedLabels.Any(lbl =>
                        {
                            if (string.IsNullOrWhiteSpace(fieldLower) || string.IsNullOrWhiteSpace(lbl))
                                return false;

                            var labelLower = lbl.ToLowerInvariant();

                            if (string.Equals(fieldLower, labelLower, StringComparison.OrdinalIgnoreCase))
                                return true;

                            var wordPattern = @"\b" + Regex.Escape(labelLower) + @"\b";
                            if (Regex.IsMatch(fieldLower, wordPattern, RegexOptions.IgnoreCase))
                                return true;

                            if (fieldLower.Contains(labelLower, StringComparison.OrdinalIgnoreCase))
                                return true;

                            var labelWords = labelLower
                                .Split(new[] { ' ', '_' }, StringSplitOptions.RemoveEmptyEntries)
                                .Where(w => w.Length > 2)
                                .ToArray();

                            if (labelWords.Length == 0)
                                return false;

                            bool allMatch = labelWords.All(w =>
                            {
                                var token = w;

                                var tokenPattern = @"\b" + Regex.Escape(token) + @"\b";
                                if (Regex.IsMatch(fieldLower, tokenPattern, RegexOptions.IgnoreCase) ||
                                    fieldLower.Contains(token, StringComparison.OrdinalIgnoreCase))
                                    return true;

                                var singularUpper = ToSingular(token.ToUpperInvariant());
                                var singular = singularUpper.ToLowerInvariant();
                                if (!string.Equals(singular, token, StringComparison.OrdinalIgnoreCase))
                                {
                                    var singularPattern = @"\b" + Regex.Escape(singular) + @"\b";
                                    if (Regex.IsMatch(fieldLower, singularPattern, RegexOptions.IgnoreCase) ||
                                        fieldLower.Contains(singular, StringComparison.OrdinalIgnoreCase))
                                        return true;
                                }

                                var plural = token.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                                    ? token
                                    : token + "s";
                                if (!string.Equals(plural, token, StringComparison.OrdinalIgnoreCase))
                                {
                                    var pluralPattern = @"\b" + Regex.Escape(plural) + @"\b";
                                    if (Regex.IsMatch(fieldLower, pluralPattern, RegexOptions.IgnoreCase) ||
                                        fieldLower.Contains(plural, StringComparison.OrdinalIgnoreCase))
                                        return true;
                                }

                                return false;
                            });

                            return allMatch;
                        });

                        if (fieldMatches)
                        {
                            // Prefer any table attached under this field; otherwise
                            // fall back to label + bullet/leaf based extraction.
                            if (!TryCollectFromTables(new[] { field }))
                            {
                                CollectLeafGroups(field, field.Name);
                            }

                            return;
                        }

                        if (field.Value is List<RichSectionExtractor.RichField> childFields)
                        {
                            foreach (var child in childFields)
                            {
                                SearchFieldsForLabel(child);
                            }
                        }
                    }

                    foreach (var field in section.Value)
                    {
                        SearchFieldsForLabel(field);
                    }
                }
            }

            // Post-process 1: coalesce consecutive records that share the same
            // label-like element (e.g. WORKFLOWS / UX_FLOWS where each step was
            // initially emitted as a separate row). We merge them into a single
            // record per label and concatenate the primary value field
            // (e.g. "*STEPS" or "*DESCRIPTION") with newlines.
            if (config?.ElementFields != null && config.ElementFields.Count > 1 && records.Count > 1)
            {
                var elementNamesAll = config.ElementFields
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => n.Trim())
                    .ToList();

                if (elementNamesAll.Count > 1)
                {
                    var labelElementsAll = elementNamesAll
                        .Where(n =>
                        {
                            var lower = n.ToLowerInvariant();
                            return lower.Contains("name") || lower.Contains("term");
                        })
                        .ToList();

                    var valueElementsAll = elementNamesAll
                        .Where(n => !labelElementsAll.Contains(n, StringComparer.OrdinalIgnoreCase))
                        .ToList();

                    var primaryValueElementAll = valueElementsAll
                        .FirstOrDefault(v =>
                        {
                            var lower = v.ToLowerInvariant();
                            return lower.Contains("steps") || lower.Contains("description");
                        })
                        ?? valueElementsAll.FirstOrDefault();

                    if (labelElementsAll.Count > 0 && !string.IsNullOrWhiteSpace(primaryValueElementAll))
                    {
                        var labelKey = labelElementsAll[0];
                        var merged = new List<Dictionary<string, object?>>(records.Count);

                        var groups = records.GroupBy(r =>
                        {
                            if (r.TryGetValue(labelKey, out var v) && v is string s && !string.IsNullOrWhiteSpace(s))
                                return s.Trim();
                            return string.Empty;
                        }, StringComparer.OrdinalIgnoreCase);

                        foreach (var group in groups)
                        {
                            var labelValue = group.Key;
                            var groupRecords = group.ToList();

                            // If there is no meaningful label or only a single record,
                            // keep the records as-is.
                            if (string.IsNullOrWhiteSpace(labelValue) || groupRecords.Count == 1)
                            {
                                merged.AddRange(groupRecords);
                                continue;
                            }

                            // Merge: start from the first record and fold in the rest.
                            var baseRecord = new Dictionary<string, object?>(groupRecords[0], StringComparer.OrdinalIgnoreCase);

                            // Concatenate the primary value field across all records.
                            var combinedValueParts = groupRecords
                                .Select(r =>
                                {
                                    if (r.TryGetValue(primaryValueElementAll, out var v) && v is string s && !string.IsNullOrWhiteSpace(s))
                                        return s.Trim();
                                    return null;
                                })
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .ToList();

                            if (combinedValueParts.Count > 0)
                            {
                                baseRecord[primaryValueElementAll] = string.Join("\n", combinedValueParts);
                            }

                            // For all other value-like elements, prefer the first
                            // non-empty value across the group.
                            foreach (var ve in valueElementsAll.Where(v => !string.Equals(v, primaryValueElementAll, StringComparison.OrdinalIgnoreCase)))
                            {
                                var firstNonEmpty = groupRecords
                                    .Select(r =>
                                    {
                                        if (r.TryGetValue(ve, out var v) && v is string s && !string.IsNullOrWhiteSpace(s))
                                            return s.Trim();
                                        return null;
                                    })
                                    .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

                                if (!string.IsNullOrWhiteSpace(firstNonEmpty))
                                {
                                    baseRecord[ve] = firstNonEmpty;
                                }
                            }

                            merged.Add(baseRecord);
                        }

                        records = merged;
                    }
                }
            }

            // Post-process 2: populate any "*INDEX" element fields with the zero-based
            // record position. This is purely positional and keyed by the element
            // name containing "index", so it remains generic (e.g. PERSONA_INDEX,
            // RULE_INDEX, STEP_INDEX, etc.). Existing non-empty values are preserved.
            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var keys = record.Keys.ToList();
                foreach (var key in keys)
                {
                    if (key.IndexOf("index", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var current = record[key];
                        if (current == null || (current is string s && string.IsNullOrWhiteSpace(s)))
                        {
                            record[key] = i;
                        }
                    }
                }
            }

            return records;
        }

        private class TestEnrichRequest
        {
            public string? RawFileBase64 { get; set; }
            public string? TemplateFileBase64 { get; set; }
            public string? RawFileName { get; set; }
            public string? TemplateFileName { get; set; }
        }

        private class TestEnrichResponse
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public RawDocumentTreeDto? RawDocumentTree { get; set; }

            /// <summary>
            /// Sanitised, semantic token list for diagnostics. This is derived from
            /// the raw extracted tokens plus inferred field types, so that group /
            /// list / table structures are clearer in the JSON.
            /// </summary>
            public List<TemplateTokenViewDto>? TemplateTokens { get; set; }

            // FieldMappings is a per-field diagnostic object:
            // {
            //   "PROJECT_NAME": { "value": "...", "source": "...", "logic": "...", "failedReason": "..." },
            //   ...
            // }
            public Dictionary<string, FieldResult>? FieldMappings { get; set; }
        }

        private class RawDocumentTreeDto
        {
            public string FileName { get; set; } = string.Empty;
            [JsonIgnore]
            public List<ParagraphDto> Paragraphs { get; set; } = new();
            [JsonIgnore]
            public List<TableDto> Tables { get; set; } = new();
            public List<ImageInfoDto> Images { get; set; } = new();
            [JsonIgnore]
            public List<SectionViewDto> Sections { get; set; } = new();
            /// <summary>
            /// Rich, hierarchical view of the document by sections and label/value children.
            /// This is where the \"1.1 Project Information\" → Project Name/Description/Objectives tree lives.
            /// </summary>
            public List<RichSectionExtractor.RichSection> StructuredSections { get; set; } = new();
        }

        private class ImageInfoDto
        {
            public string Id { get; set; } = string.Empty;
            public int AnchorParagraphIndex { get; set; }
            public int SizeBytes { get; set; }
        }

        private class SectionViewDto
        {
            public string Heading { get; set; } = string.Empty;
            public List<string> Paragraphs { get; set; } = new();
        }

        private class TemplateTokenDto
        {
            /// <summary>
            /// Raw token name as extracted from the template, e.g. "PROJECT_NAME",
            /// "#PERSONAS", "/PERSONAS".
            /// </summary>
            public string Name { get; set; } = string.Empty;

            /// <summary>
            /// Raw token kind, e.g. PlainTextBrackets / ContentControl. This is
            /// primarily useful for debugging the extractor.
            /// </summary>
            public string Type { get; set; } = string.Empty;

            /// <summary>
            /// Raw pattern as it appears in the template, e.g. "[PROJECT_NAME]".
            /// </summary>
            public string Pattern { get; set; } = string.Empty;
        }

        /// <summary>
        /// Sanitised, semantic view of a template token that is easier to reason
        /// about when inspecting JSON:
        /// - Group markers like #ITEMS and /ITEMS are collapsed into a single
        ///   logical field (Name = "ITEMS", Type = "list").
        /// - Element/column fields that belong to a group are exposed via
        ///   ElementFields instead of as separate top-level tokens.
        /// - Type reflects the inferred semantic type (scalar/list/table), not
        ///   the low-level bracket style.
        /// </summary>
        private class TemplateTokenViewDto
        {
            public string Name { get; set; } = string.Empty;

            /// <summary>
            /// Semantic type: "scalar", "list", "table", etc. May be null if
            /// not inferred.
            /// </summary>
            public string? Type { get; set; }

            /// <summary>
            /// Representative pattern for this logical field, usually taken from
            /// the opening group marker (e.g. "[#PERSONAS]") or the scalar token
            /// (e.g. "[PROJECT_NAME]").
            /// </summary>
            public string Pattern { get; set; } = string.Empty;

            /// <summary>
            /// For list/table-style fields inferred from grouping markers, this
            /// contains the logical child fields (e.g. PERSONA_NAME, PERSONA_ROLE)
            /// as small objects so that nested structures can be represented in
            /// a future-safe way.
            /// </summary>
            public List<TemplateElementFieldViewDto>? ElementFields { get; set; }
        }

        /// <summary>
        /// Lightweight view of an element/column field that belongs to a group-
        /// style logical token. This mirrors the shape you requested:
        /// { "Name": "...", "Type": "...", "Pattern": "..." }.
        /// </summary>
        private class TemplateElementFieldViewDto
        {
            public string Name { get; set; } = string.Empty;
            public string? Type { get; set; }
            public string Pattern { get; set; } = string.Empty;
        }

        private class FieldConfig
        {
            public string? Type { get; set; }
            public List<string>? Sources { get; set; }
            public List<string>? LabelPatterns { get; set; }
            public List<string>? LinePatterns { get; set; }
            public List<string>? ExcludeLinePatterns { get; set; }
            public List<string>? SplitOn { get; set; }
            public Dictionary<string, List<string>>? HeaderPatterns { get; set; }
            public List<string>? RegexPatterns { get; set; }

            /// <summary>
            /// For group-style fields inferred from template tokens (e.g. #PERSONAS ...
            /// /PERSONAS), this captures the logical "columns" or child element fields
            /// that belong to the group (e.g. PERSONA_NAME, PERSONA_ROLE, ...).
            /// This is derived purely from token structure, not from config JSON.
            /// </summary>
            public List<string>? ElementFields { get; set; }

            /// <summary>
            /// When true, allows this field to be populated by the heuristic scalar
            /// fallback. By default, heuristics are disabled for all fields; only
            /// fields that explicitly opt-in via config can use it.
            /// </summary>
            public bool AllowHeuristic { get; set; } = false;
        }

        private class FieldResult
        {
            public object? Value { get; set; }
            public string? Source { get; set; }
            public string? Logic { get; set; }
            public string? FailedReason { get; set; }
            public double? Score { get; set; }
            public string? MatchType { get; set; }

            /// <summary>
            /// The textual label or heading that was used to match this field
            /// in the document, when available. For example, for VERSION_NUMBER
            /// this will typically be "Version".
            /// </summary>
            public string? Text { get; set; }
        }

        private class ParagraphInfo
        {
            public ParagraphDto Para { get; set; } = new ParagraphDto();
            public string Lower { get; set; } = string.Empty;
        }

        private class MultipartFilesResult
        {
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public byte[]? RawBytes { get; set; }
            public string? RawFileName { get; set; }
            public byte[]? TemplateBytes { get; set; }
            public string? TemplateFileName { get; set; }
        }

        // Test HTTP endpoint removed: keeping implementation code for potential reuse,
        // but disabling the Azure Function trigger so it is no longer exposed.
        // [Function("TestTemplateEnrichment")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            var response = req.CreateResponse();
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            try
            {
                byte[] rawBytes;
                byte[] templateBytes;
                string rawFileName;
                string templateFileName;

                // Detect content type and support both JSON (base64) and multipart/form-data (file upload)
                req.Headers.TryGetValues("Content-Type", out var contentTypeValues);
                var contentType = contentTypeValues is not null ? System.Linq.Enumerable.FirstOrDefault(contentTypeValues) : null;

                if (!string.IsNullOrWhiteSpace(contentType) &&
                    contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
                {
                    // Handle multipart file upload from Postman
                    var multipart = await ReadMultipartFilesAsync(req, contentType);
                    if (!multipart.Success || multipart.RawBytes == null || multipart.TemplateBytes == null)
                    {
                        response.StatusCode = HttpStatusCode.BadRequest;
                        await response.WriteStringAsync(JsonConvert.SerializeObject(new TestEnrichResponse
                        {
                            Success = false,
                            Message = multipart.ErrorMessage ?? "Invalid multipart/form-data payload."
                        }));
                        return response;
                    }

                    rawBytes = multipart.RawBytes;
                    templateBytes = multipart.TemplateBytes;
                    rawFileName = string.IsNullOrWhiteSpace(multipart.RawFileName) ? "Raw.docx" : multipart.RawFileName!;
                    templateFileName = string.IsNullOrWhiteSpace(multipart.TemplateFileName) ? "Template.dotx" : multipart.TemplateFileName!;
                }
                else
                {
                    // Fallback: original JSON + base64 flow
                    var body = await new StreamReader(req.Body).ReadToEndAsync();
                    if (string.IsNullOrWhiteSpace(body))
                    {
                        response.StatusCode = HttpStatusCode.BadRequest;
                        await response.WriteStringAsync(JsonConvert.SerializeObject(new TestEnrichResponse
                        {
                            Success = false,
                            Message = "Request body is empty."
                        }));
                        return response;
                    }

                    TestEnrichRequest? input;
                    try
                    {
                        input = JsonConvert.DeserializeObject<TestEnrichRequest>(body);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize TestTemplateEnrichment request.");
                        response.StatusCode = HttpStatusCode.BadRequest;
                        await response.WriteStringAsync(JsonConvert.SerializeObject(new TestEnrichResponse
                        {
                            Success = false,
                            Message = "Invalid JSON payload."
                        }));
                        return response;
                    }

                    if (input == null ||
                        string.IsNullOrWhiteSpace(input.RawFileBase64) ||
                        string.IsNullOrWhiteSpace(input.TemplateFileBase64))
                    {
                        response.StatusCode = HttpStatusCode.BadRequest;
                        await response.WriteStringAsync(JsonConvert.SerializeObject(new TestEnrichResponse
                        {
                            Success = false,
                            Message = "Both 'rawFileBase64' and 'templateFileBase64' are required."
                        }));
                        return response;
                    }

                    try
                    {
                        rawBytes = Convert.FromBase64String(input.RawFileBase64);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Invalid base64 for raw file.");
                        response.StatusCode = HttpStatusCode.BadRequest;
                        await response.WriteStringAsync(JsonConvert.SerializeObject(new TestEnrichResponse
                        {
                            Success = false,
                            Message = "Invalid base64 for 'rawFileBase64'."
                        }));
                        return response;
                    }

                    try
                    {
                        templateBytes = Convert.FromBase64String(input.TemplateFileBase64);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Invalid base64 for template file.");
                        response.StatusCode = HttpStatusCode.BadRequest;
                        await response.WriteStringAsync(JsonConvert.SerializeObject(new TestEnrichResponse
                        {
                            Success = false,
                            Message = "Invalid base64 for 'templateFileBase64'."
                        }));
                        return response;
                    }

                    rawFileName = string.IsNullOrWhiteSpace(input.RawFileName)
                        ? "Raw.docx"
                        : input.RawFileName;

                    templateFileName = string.IsNullOrWhiteSpace(input.TemplateFileName)
                        ? "Template.dotx"
                        : input.TemplateFileName;
                }

                using var lease = new TempFileLease("test-enrich");

                var rawPath = lease.GetPath(rawFileName);
                var templatePath = lease.GetPath(templateFileName);

                await File.WriteAllBytesAsync(rawPath, rawBytes);
                await File.WriteAllBytesAsync(templatePath, templateBytes);

                _logger.LogInformation("📄 [TEST] Saved raw file to {Path}", rawPath);
                _logger.LogInformation("📄 [TEST] Saved template file to {Path}", templatePath);

                // ===== PIPELINE: 12 Steps =====
                
                // Step 1: Pre-Sanitizer - Clean raw document
                using var inputStream = File.OpenRead(rawPath);
                var (rawParas, tables, extractedImages) = _extractor.ExtractDocxStructured(inputStream);
                if (rawParas == null) rawParas = new List<ParagraphDto>();
                
                // Sanitize paragraphs
                var paras = rawParas.Select(p => new ParagraphDto
                {
                    Index = p.Index,
                    Text = PreSanitizer.NormalizeParagraph(p.Text ?? string.Empty),
                    StyleId = p.StyleId
                }).ToList();

                // Step 2: StructureBuilder - Build hierarchical BOM tree
                var structureBuilder = new StructureBuilder();
                var bomTree = structureBuilder.BuildBOM(paras, tables);

                // Step 3: AliasManager - Load runtime aliases
                var aliasManager = AliasManager.LoadFromConfig();

                // Step 4: StructuredExtractors - Detect higher-level structures
                var keyValuePairs = StructuredExtractors.ExtractKeyValueLines(paras);
                var lists = StructuredExtractors.ExtractLists(paras);
                var bulletBlocks = StructuredExtractors.ExtractBulletBlocks(paras);
                var embeddedContent = StructuredExtractors.ExtractEmbeddedContent(paras);

                // Step 5: SignalGenerator - Compute features for candidates
                var signalGenerator = new SignalGenerator(aliasManager);

                // Step 6: ScoringEngine - Convert features to scores
                var scoringEngine = new ScoringEngine();

                // Step 7: BlockClassifier - Label sections
                var blockClassifier = new BlockClassifier(aliasManager);
                var sectionClassifications = blockClassifier.ClassifySections(bomTree);

                // Step 8: DeduplicationEngine - Track usage
                var deduplicationEngine = new DeduplicationEngine();
                var usageTracker = deduplicationEngine.CreateTracker();

                // Build simple section view for backward compatibility
                // Legacy flat sections view kept only for debugging; mapping logic
                // now uses StructuredSections as the single source of truth.
                var sections = new List<SectionViewDto>();

                // Step 3 (continued): Shape raw document tree DTO
                var richExtractor = new RichSectionExtractor();
                var richSections = richExtractor.BuildRichSections(bomTree);

                var rawTree = new RawDocumentTreeDto
                {
                    FileName = rawFileName,
                    Paragraphs = paras,
                    Tables = tables ?? new List<TableDto>(),
                    Images = (extractedImages ?? new List<ExtractedImage>()).ConvertAll(img => new ImageInfoDto
                    {
                        Id = img.Id,
                        AnchorParagraphIndex = img.AnchorParagraphIndex,
                        SizeBytes = img.Bytes?.Length ?? 0
                    }),
                    Sections = sections,
                    StructuredSections = richSections
                };

                // Step 9: TemplateCompiler + TemplateMapper - Extract tokens and map
                var templateTokens = ExtractTemplateTokens(templatePath);
                var fieldSpec = LoadFieldMappingSpec();

                // Primary pass: deterministic label/section-based matcher.
                var tokenMatches = BuildTokenMatches(rawTree, templateTokens, fieldSpec);

                // Step 6 (continued): Build higher-level field mappings
                var fieldMappings = BuildFieldMappings(rawTree, templateTokens, tokenMatches, fieldSpec);

                // Build a sanitised, semantic view of the tokens for JSON output,
                // using the enriched fieldSpec (which now knows about list/table
                // types and ElementFields inferred from the tokens themselves).
                var tokenView = BuildTemplateTokenView(templateTokens, fieldSpec);

                // Step 10: NAFiller + StrictValidator - Handle missing/invalid fields
                var naFiller = new NAFiller();
                var requiredFields = new HashSet<string>(); // Could be loaded from config
                var optionalFields = new HashSet<string>(fieldMappings.Keys);
                var validatedMappings = naFiller.FillMissingOptional(
                    fieldMappings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value ?? string.Empty),
                    requiredFields,
                    optionalFields);
                var validationResult = naFiller.Validate(validatedMappings, requiredFields);

                // Step 12: Post-Sanitizer - Cleanup (for text values)
                foreach (var key in validatedMappings.Keys.ToList())
                {
                    if (validatedMappings[key] is string strValue)
                    {
                        validatedMappings[key] = PostSanitizer.Cleanup(strValue);
                    }
                }

                var result = new TestEnrichResponse
                {
                    Success = true,
                    Message = "Extraction successful.",
                    RawDocumentTree = rawTree,
                    TemplateTokens = tokenView,
                    FieldMappings = fieldMappings
                };

                response.StatusCode = HttpStatusCode.OK;
                await response.WriteStringAsync(JsonConvert.SerializeObject(result));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [TEST] Unexpected error in TestTemplateEnrichment.");
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync(JsonConvert.SerializeObject(new TestEnrichResponse
                {
                    Success = false,
                    Message = $"Unexpected error: {ex.Message}"
                }));
                return response;
            }
        }

        /// <summary>
        /// Minimal multipart/form-data parser for two file parts: rawFile and templateFile.
        /// Intended for local testing via Postman; not a full general-purpose parser.
        /// </summary>
        private async Task<MultipartFilesResult> ReadMultipartFilesAsync(HttpRequestData req, string contentType)
        {
            var result = new MultipartFilesResult();

            try
            {
                var boundaryIndex = contentType.IndexOf("boundary=", StringComparison.OrdinalIgnoreCase);
                if (boundaryIndex < 0)
                {
                    result.Success = false;
                    result.ErrorMessage = "Missing boundary in Content-Type header.";
                    return result;
                }

                var boundary = contentType.Substring(boundaryIndex + "boundary=".Length).Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(boundary))
                {
                    result.Success = false;
                    result.ErrorMessage = "Invalid boundary in Content-Type header.";
                    return result;
                }

                using var ms = new MemoryStream();
                await req.Body.CopyToAsync(ms);
                var bodyBytes = ms.ToArray();

                // Use Latin1 so bytes map 1:1 to chars (safe for binary content round-trip)
                var bodyText = Encoding.Latin1.GetString(bodyBytes);
                var delimiter = "--" + boundary;

                var segments = bodyText.Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var segmentRaw in segments)
                {
                    var segment = segmentRaw.Trim('\r', '\n');
                    if (segment == "--")
                    {
                        continue; // closing boundary
                    }

                    var headerEndIndex = segment.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                    if (headerEndIndex <= 0)
                    {
                        continue;
                    }

                    var headersText = segment.Substring(0, headerEndIndex);
                    var contentText = segment.Substring(headerEndIndex + 4);

                    // Trim the final CRLF that precedes the next boundary
                    contentText = contentText.TrimEnd('\r', '\n');

                    // Parse Content-Disposition
                    var contentDispositionLine = string.Empty;
                    var headerLines = headersText.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in headerLines)
                    {
                        if (line.StartsWith("Content-Disposition", StringComparison.OrdinalIgnoreCase))
                        {
                            contentDispositionLine = line;
                            break;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(contentDispositionLine))
                    {
                        continue;
                    }

                    string? name = null;
                    string? fileName = null;

                    var nameIndex = contentDispositionLine.IndexOf("name=\"", StringComparison.OrdinalIgnoreCase);
                    if (nameIndex >= 0)
                    {
                        var start = nameIndex + "name=\"".Length;
                        var end = contentDispositionLine.IndexOf('"', start);
                        if (end > start)
                        {
                            name = contentDispositionLine.Substring(start, end - start);
                        }
                    }

                    var fileNameIndex = contentDispositionLine.IndexOf("filename=\"", StringComparison.OrdinalIgnoreCase);
                    if (fileNameIndex >= 0)
                    {
                        var start = fileNameIndex + "filename=\"".Length;
                        var end = contentDispositionLine.IndexOf('"', start);
                        if (end > start)
                        {
                            fileName = contentDispositionLine.Substring(start, end - start);
                        }
                    }

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var contentBytes = Encoding.Latin1.GetBytes(contentText);

                    if (string.Equals(name, "rawFile", StringComparison.OrdinalIgnoreCase))
                    {
                        result.RawBytes = contentBytes;
                        result.RawFileName = fileName ?? "Raw.docx";
                    }
                    else if (string.Equals(name, "templateFile", StringComparison.OrdinalIgnoreCase))
                    {
                        result.TemplateBytes = contentBytes;
                        result.TemplateFileName = fileName ?? "Template.dotx";
                    }
                }

                if (result.RawBytes == null || result.TemplateBytes == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Expected form-data fields 'rawFile' and 'templateFile' with files attached.";
                    return result;
                }

                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse multipart/form-data payload in TestTemplateEnrichment.");
                result.Success = false;
                result.ErrorMessage = $"Failed to parse multipart/form-data payload: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Very simple section builder: groups paragraphs by heading style.
        /// This is intentionally lightweight and does NOT apply enrichment or mapping logic.
        /// </summary>
        // Legacy paragraph-based section builder is no longer used for mapping; kept
        // only for potential debugging. All mapping logic now reads from StructuredSections.
        private List<SectionViewDto> BuildSectionsFromParagraphs(List<ParagraphDto> paras) => new();

        /// <summary>
        /// Loads field mapping specification for functional/technical specs from Config/FieldMappingSpec.json.
        /// This is used ONLY by this test endpoint and does not affect the main pipeline.
        /// </summary>
        private Dictionary<string, FieldConfig> LoadFieldMappingSpec()
        {
            if (_fieldMappingSpecCache != null)
            {
                return _fieldMappingSpecCache;
            }

            try
            {
                var basePath = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
                var configPath = Path.Combine(basePath, "Config", "FieldMappingSpec.json");

                if (!File.Exists(configPath))
                {
                    _logger.LogWarning("⚠️ [TEST] FieldMappingSpec.json not found at {Path}. Using empty spec.", configPath);
                    _fieldMappingSpecCache = new Dictionary<string, FieldConfig>(StringComparer.OrdinalIgnoreCase);
                    return _fieldMappingSpecCache;
                }

                var json = File.ReadAllText(configPath);
                var dict = JsonConvert.DeserializeObject<Dictionary<string, FieldConfig>>(json);
                _fieldMappingSpecCache = dict ?? new Dictionary<string, FieldConfig>(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ [TEST] Failed to load FieldMappingSpec.json. Using empty spec.");
                _fieldMappingSpecCache = new Dictionary<string, FieldConfig>(StringComparer.OrdinalIgnoreCase);
            }

            return _fieldMappingSpecCache;
        }

        /// <summary>
        /// Extracts all placeholders / tokens from the template file without performing any matching logic.
        /// </summary>
        private List<TemplateTokenDto> ExtractTemplateTokens(string templatePath)
        {
            var tokens = new List<TemplateTokenDto>();

            if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
            {
                return tokens;
            }

            try
            {
                using var doc = WordprocessingDocument.Open(templatePath, false);
                var mainPart = doc.MainDocumentPart;
                if (mainPart?.Document?.Body == null)
                    return tokens;

                var body = mainPart.Document.Body;

                // Track by name to avoid duplicates
                var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 1) SDT content controls (by tag)
                var sdtList = body.Descendants<SdtElement>().ToList();
                foreach (var sdt in sdtList)
                {
                    var tag = sdt.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;
                    if (!string.IsNullOrWhiteSpace(tag) && !seenNames.Contains(tag))
                    {
                        seenNames.Add(tag);
                        tokens.Add(new TemplateTokenDto
                        {
                            Name = tag,
                            Type = "ContentControl",
                            Pattern = $"[{tag}]"
                        });
                    }
                }

                // 2) Plain text placeholders in paragraphs
                var paragraphs = body.Descendants<Paragraph>().ToList();

                foreach (var para in paragraphs)
                {
                    var paraText = string.Concat(para.Descendants<Text>().Select(t => t.Text ?? string.Empty));
                    if (string.IsNullOrWhiteSpace(paraText))
                    {
                        continue;
                    }

                    // [TAG] or [instruction]
                    var bracketMatches = Regex.Matches(paraText, @"\[([^\]]+)\]");
                    foreach (Match match in bracketMatches)
                    {
                        var tag = match.Groups[1].Value.Trim();
                        if (string.IsNullOrWhiteSpace(tag) || tag.Length > 200)
                            continue;

                        // Skip markdown-style links [text](http...)
                        if (tag.Contains("(") && tag.Contains("http"))
                            continue;

                        if (!seenNames.Contains(tag))
                        {
                            seenNames.Add(tag);
                            tokens.Add(new TemplateTokenDto
                            {
                                Name = tag,
                                Type = "PlainTextBrackets",
                                Pattern = $"[{tag}]"
                            });
                        }
                    }

                    // {{TAG}}
                    var doubleBraceMatches = Regex.Matches(paraText, @"\{\{([A-Z_][A-Z0-9_()| ]*)\}\}");
                    foreach (Match match in doubleBraceMatches)
                    {
                        var tag = match.Groups[1].Value.Trim();
                        if (string.IsNullOrWhiteSpace(tag))
                            continue;

                        if (!seenNames.Contains(tag))
                        {
                            seenNames.Add(tag);
                            tokens.Add(new TemplateTokenDto
                            {
                                Name = tag,
                                Type = "PlainTextDoubleBraces",
                                Pattern = "{{" + tag + "}}"
                            });
                        }
                    }

                    // {TAG}
                    var singleBraceMatches = Regex.Matches(paraText, @"\{([A-Z_][A-Z0-9_()| ]*)\}");
                    foreach (Match match in singleBraceMatches)
                    {
                        var tag = match.Groups[1].Value.Trim();
                        if (string.IsNullOrWhiteSpace(tag))
                            continue;

                        if (!seenNames.Contains(tag))
                        {
                            seenNames.Add(tag);
                            tokens.Add(new TemplateTokenDto
                            {
                                Name = tag,
                                Type = "PlainTextSingleBraces",
                                Pattern = "{" + tag + "}"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ [TEST] Failed to extract template tokens from {TemplatePath}", templatePath);
            }

            return tokens;
        }

        /// <summary>
        /// Enhanced token matching using SignalGenerator + ScoringEngine (Step 5+6)
        /// </summary>
        private Dictionary<string, string> BuildTokenMatchesWithScoring(
            RawDocumentTreeDto raw,
            List<TemplateTokenDto> tokens,
            Dictionary<string, FieldConfig> fieldSpec,
            SignalGenerator signalGenerator,
            ScoringEngine scoringEngine,
            Helpers.StructureBuilder.SectionNode bomTree,
            DeduplicationEngine.UsageTracker usageTracker)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (raw == null || tokens == null || tokens.Count == 0)
                return result;

            var paragraphs = BuildVirtualParagraphsFromStructuredSections(raw.StructuredSections ?? new List<RichSectionExtractor.RichSection>());

            // Infer logical group/collection field names from template markers like
            // #GROUP_NAME or /GROUP_NAME. Scoring is only applied to scalar-style
            // fields, never to group containers.
            var groupFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in tokens)
            {
                var tName = t.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(tName))
                    continue;

                if (tName.StartsWith("#", StringComparison.Ordinal) ||
                    tName.StartsWith("/", StringComparison.Ordinal))
                {
                    var groupName = tName.TrimStart('#', '/').Trim();
                    if (!string.IsNullOrWhiteSpace(groupName))
                    {
                        groupFieldNames.Add(groupName);
                    }
                }
            }

            foreach (var token in tokens)
            {
                var name = token.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name) || name.StartsWith("#") || name.StartsWith("/"))
                {
                    result[name] = string.Empty;
                    continue;
                }

                // Never let scoring drive IDs or logical collection/table fields.
                // IDs are globally unsafe to guess, and group containers should be
                // populated via structural extraction rather than scalar scoring.
                if (name.EndsWith("_ID", StringComparison.OrdinalIgnoreCase) ||
                    groupFieldNames.Contains(name))
                {
                    result[name] = string.Empty;
                    continue;
                }

                // Generate features for all candidates
                var candidates = signalGenerator.GenerateFeaturesForAll(name, paragraphs, bomTree);

                // Filter out already-used candidates
                candidates = candidates.Where(c => usageTracker.CanReuse(name, c.ParagraphIndex)).ToList();

                // Find best candidate
                var best = scoringEngine.FindBestCandidate(candidates);
                // Second-layer filling: be reasonably permissive so that we populate
                // many currently-empty fields, but still require non-trivial confidence.
                fieldSpec.TryGetValue(name, out var cfg);
                var threshold = 0.3; // Keep original threshold to preserve existing matches
                if (best != null && best.Score > threshold)
                {
                    var value = best.Features.CandidateText;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        result[name] = value;
                        usageTracker.MarkUsed(name, best.Features.ParagraphIndex);
                    }
                    else
                    {
                        result[name] = string.Empty;
                    }
                }
                else
                {
                    // Fallback to simple matching
                    // Reuse cfg from above scope
                    result[name] = TryMatchLabelStyle(name, paragraphs.Select(p => new ParagraphInfo { Para = p, Lower = (p.Text ?? "").ToLowerInvariant() }).ToList(), 
                        cfg);
                }
            }

            return result;
        }

        /// <summary>
        /// Combines primary (deterministic) and secondary (scoring-based) token matches.
        /// For each token:
        /// - If primary has a non-empty value, keep it.
        /// - Otherwise, if secondary has a non-empty value, use that.
        /// - Otherwise, leave it empty.
        /// This guarantees that scoring never overwrites already-populated values.
        /// </summary>
        private Dictionary<string, string> MergeTokenMatches(
            Dictionary<string, string> primary,
            Dictionary<string, string> secondary)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            primary ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            secondary ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var allKeys = new HashSet<string>(primary.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var key in secondary.Keys)
            {
                allKeys.Add(key);
            }

            foreach (var key in allKeys)
            {
                if (primary.TryGetValue(key, out var primaryVal) &&
                    !string.IsNullOrWhiteSpace(primaryVal))
                {
                    result[key] = primaryVal;
                    continue;
                }

                if (secondary.TryGetValue(key, out var secondaryVal) &&
                    !string.IsNullOrWhiteSpace(secondaryVal))
                {
                    result[key] = secondaryVal;
                }
                else
                {
                    result[key] = string.Empty;
                }
            }

            return result;
        }

        /// <summary>
        /// Simple heuristic matching: for each template token, try to find a value in the raw document tree.
        /// - Field-style tokens (PROJECT_NAME, VERSION_NUMBER, etc.) look for paragraphs like "Project Name: value".
        /// - Section-style tokens (BUSSINESS_CONTEXT, SUCCESS_METRICS, etc.) try to match section headings.
        /// Many tokens will legitimately return empty string when no obvious match exists.
        /// Uses FieldMappingSpec.json where available for label patterns; otherwise falls back to token-derived labels.
        /// </summary>
        private Dictionary<string, string> BuildTokenMatches(
            RawDocumentTreeDto raw,
            List<TemplateTokenDto> tokens,
            Dictionary<string, FieldConfig> fieldSpec)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (raw == null || tokens == null || tokens.Count == 0)
            {
                return result;
            }

            var structured = raw.StructuredSections ?? new List<RichSectionExtractor.RichSection>();

            // Build a virtual paragraph view from StructuredSections so that the
            // existing label/section-style matching logic can operate on a single
            // canonical source instead of raw paragraphs/sections.
            var virtualParas = BuildVirtualParagraphsFromStructuredSections(structured);

            var paraInfos = virtualParas
                .Select(p => new ParagraphInfo
                {
                    Para = p,
                    Lower = (p.Text ?? string.Empty).ToLowerInvariant()
                })
                .ToList();

            foreach (var token in tokens)
            {
                var name = token.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                // Skip pure group markers (#GROUP, /GROUP)
                if (name.StartsWith("#") || name.StartsWith("/"))
                {
                    result[name] = string.Empty;
                    continue;
                }

                var value = string.Empty;

                // Look up field config if it exists (for label or heading patterns)
                fieldSpec.TryGetValue(name, out var cfg);

                // 1) Try label-style paragraphs: "Label: value"
                value = TryMatchLabelStyle(name, paraInfos, cfg);

                // 2) If still empty, and this is not an *_ID style field, try to map
                //    to a whole section by heading using StructuredSections (not legacy sections view).
                if (string.IsNullOrWhiteSpace(value) &&
                    !name.EndsWith("_ID", StringComparison.OrdinalIgnoreCase) &&
                    raw.StructuredSections != null && raw.StructuredSections.Count > 0)
                {
                    value = TryMatchSectionStyleFromStructured(name, raw.StructuredSections, cfg);
                }

                result[name] = value ?? string.Empty;
            }

            return result;
        }

        private string TryMatchLabelStyle(string tokenName, List<ParagraphInfo> paraInfos, FieldConfig? config)
        {
            if (paraInfos == null || paraInfos.Count == 0)
                return string.Empty;

            // Normalize token name (remove parentheses, special chars)
            var normalizedTokenName = NormalizePlaceholderName(tokenName);

            // Prefer label patterns from config; otherwise derive from token name (PROJECT_NAME -> "project name")
            var labelPatterns = config?.LabelPatterns != null && config.LabelPatterns.Count > 0
                ? config.LabelPatterns                                                                                                                                                                                                                                                                                                                                                                         
                : new List<string>
                {
                    normalizedTokenName
                        .Trim('#', '/')
                        .Replace("_", " ")
                        .ToLowerInvariant()
                };

            // Also add variations: with/without spaces, different cases
            var additionalPatterns = new List<string>();
            foreach (var pattern in labelPatterns)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                    continue;

                var basePattern = pattern.Trim().ToLowerInvariant();
                additionalPatterns.Add(basePattern);

                // Add variation without spaces (e.g., "project name" -> "projectname")
                if (basePattern.Contains(" "))
                {
                    additionalPatterns.Add(basePattern.Replace(" ", ""));
                }

                // Add variation with underscores (e.g., "project name" -> "project_name")
                if (!basePattern.Contains("_"))
                {
                    additionalPatterns.Add(basePattern.Replace(" ", "_"));
                }
            }

            labelPatterns = additionalPatterns.Distinct().ToList();

            foreach (var info in paraInfos)
            {
                var paraText = info.Para.Text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(paraText))
                    continue;

                // Important: a single Word paragraph can contain multiple logical lines
                // separated by manual line breaks. Split by line so that e.g.
                // "Version: 1.0\nDate: December 2024\n..." yields distinct matches.
                var lines = paraText
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var rawLabel in labelPatterns)
                {
                    var label = rawLabel?.Trim().ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(label))
                        continue;

                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        var lowerLine = line.ToLowerInvariant();
                        
                        // Try matching with word boundaries first (more precise), then fallback to simple match
                        // Match patterns like "label:", "label :", "Label:", etc.
                        var labelPatternWithBoundary = @"\b" + Regex.Escape(label) + @"\s*:";
                        var labelPatternSimple = Regex.Escape(label) + @"\s*:";
                        
                        var labelMatch = Regex.Match(lowerLine, labelPatternWithBoundary, RegexOptions.IgnoreCase);
                        if (!labelMatch.Success)
                        {
                            // Fallback to simple match without word boundaries (less strict, preserves existing behavior)
                            labelMatch = Regex.Match(lowerLine, labelPatternSimple, RegexOptions.IgnoreCase);
                        }
                        
                        if (!labelMatch.Success)
                            continue;

                        var idx = labelMatch.Index;
                        var valueStart = idx + labelMatch.Length;
                        if (valueStart >= line.Length)
                            continue;

                        var after = line.Substring(valueStart);

                        // Improved: stop at the next "Label:" pattern with better word boundary handling
                        // Handles cases like "Version: 1.0Date: December 2024Status: Draft..."
                        // Also handles common separators (newline, semicolon, etc.)
                        var nextLabelMatch = Regex.Match(
                            after,
                            @"\b([A-Z][A-Za-z0-9]{0,30}(?:\s+[A-Z][A-Za-z0-9]{0,30}){0,3})\s*:",
                            RegexOptions.None);

                        string value;
                        if (nextLabelMatch.Success)
                        {
                            value = after.Substring(0, nextLabelMatch.Index).Trim();
                        }
                        else
                        {
                            // Also stop at common separators if they appear early
                            var separatorMatch = Regex.Match(after, @"^([^;\n\r]+?)(?:[;\n\r]|$)", RegexOptions.Multiline);
                            value = separatorMatch.Success ? separatorMatch.Groups[1].Value.Trim() : after.Trim();
                        }

                        // Clean up value: remove trailing punctuation that might be part of formatting
                        value = Regex.Replace(value, @"[.,;]+$", "").Trim();

                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            return value;
                        }
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Normalizes placeholder names by removing/replacing special characters that can cause matching issues.
        /// Handles cases like "AUTHOR_NAME(S)" -> "AUTHOR_NAME" or "AUTHOR_NAMES"
        /// </summary>
        private string NormalizePlaceholderName(string placeholderName)
        {
            if (string.IsNullOrWhiteSpace(placeholderName))
                return placeholderName;

            var normalized = placeholderName.Trim();

            // Only remove parentheses and their contents (e.g., "AUTHOR_NAME(S)" -> "AUTHOR_NAME")
            // Keep other special characters to preserve existing matches
            normalized = Regex.Replace(normalized, @"\([^)]*\)", "");

            // Normalize multiple underscores/spaces but preserve the structure
            normalized = Regex.Replace(normalized, @"[_ ]+", "_");
            normalized = normalized.Trim('_', ' ');

            return normalized;
        }

        private string TryMatchSectionStyle(string tokenName, List<SectionViewDto> sections, FieldConfig? config)
        {
            if (sections == null || sections.Count == 0)
                return string.Empty;

            // Normalize token name
            var normalizedTokenName = NormalizePlaceholderName(tokenName);

            // Build label candidates from config aliases (if any) plus the token name itself
            var labelCandidates = new List<string>();
            if (config?.LabelPatterns != null && config.LabelPatterns.Count > 0)
            {
                labelCandidates.AddRange(config.LabelPatterns.Where(lp => !string.IsNullOrWhiteSpace(lp)));
            }

            labelCandidates.Add(
                normalizedTokenName
                .Trim('#', '/')
                .Replace("_", " ")
            );

            // Add variations (with/without spaces, different cases)
            var additionalCandidates = new List<string>();
            foreach (var candidate in labelCandidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                var baseCandidate = candidate.Trim();
                additionalCandidates.Add(baseCandidate);

                // Add variation without spaces
                if (baseCandidate.Contains(" "))
                {
                    additionalCandidates.Add(baseCandidate.Replace(" ", ""));
                }

                // Add variation with underscores
                if (!baseCandidate.Contains("_"))
                {
                    additionalCandidates.Add(baseCandidate.Replace(" ", "_"));
                }
            }

            labelCandidates = additionalCandidates.Distinct().ToList();

            foreach (var rawLabel in labelCandidates)
            {
                var label = rawLabel.Trim();
            if (string.IsNullOrWhiteSpace(label))
                    continue;

            var labelWords = label
                    .ToLowerInvariant()
                    .Split(new[] { ' ', '_' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 2)
                .ToArray();

            if (labelWords.Length == 0)
                    continue;

            foreach (var section in sections)
            {
                var headingLower = (section.Heading ?? string.Empty).ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(headingLower))
                        continue;

                    // Check for exact matches first (higher priority)
                    bool exactMatch = headingLower.Equals(label, StringComparison.OrdinalIgnoreCase) ||
                                     headingLower.Contains(label, StringComparison.OrdinalIgnoreCase);

                    if (!exactMatch)
                    {
                        // Word-by-word matching: try with word boundaries first, then fallback to simple contains
                        // This preserves existing behavior while adding precision where possible
                        bool allMatch = labelWords.All(w =>
                        {
                            // First try with word boundaries (more precise)
                            var wordPatternWithBoundary = @"\b" + Regex.Escape(w) + @"\b";
                            if (Regex.IsMatch(headingLower, wordPatternWithBoundary, RegexOptions.IgnoreCase))
                                return true;

                            // Fallback to simple contains (preserves existing matches)
                            if (headingLower.Contains(w, StringComparison.OrdinalIgnoreCase))
                                return true;

                            // Check singular/plural variations (with word boundaries)
                            var singularUpper = ToSingular(w.ToUpperInvariant());
                            var singular = singularUpper.ToLowerInvariant();
                            if (w != singular)
                            {
                                var singularPattern = @"\b" + Regex.Escape(singular) + @"\b";
                                if (Regex.IsMatch(headingLower, singularPattern, RegexOptions.IgnoreCase))
                                    return true;
                                
                                // Fallback to simple contains
                                if (headingLower.Contains(singular, StringComparison.OrdinalIgnoreCase))
                                    return true;
                            }

                            // Check plural form
                            var plural = w + "s";
                            if (w != plural && !w.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                            {
                                var pluralPattern = @"\b" + Regex.Escape(plural) + @"\b";
                                if (Regex.IsMatch(headingLower, pluralPattern, RegexOptions.IgnoreCase))
                                    return true;
                                
                                // Fallback to simple contains
                                if (headingLower.Contains(plural, StringComparison.OrdinalIgnoreCase))
                                    return true;
                            }

                            return false;
                        });

                        if (!allMatch)
                    continue;
                    }

                if (section.Paragraphs != null && section.Paragraphs.Count > 0)
                {
                        return string.Join(
                            "\n\n",
                            section.Paragraphs.Where(p => !string.IsNullOrWhiteSpace(p))
                        );
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Section-style matcher that operates on StructuredSections by projecting them
        /// into a lightweight SectionViewDto list and delegating to TryMatchSectionStyle.
        /// This keeps all matching logic driven from StructuredSections while reusing the
        /// existing, generic section-heading matcher.
        /// </summary>
        private string TryMatchSectionStyleFromStructured(
            string tokenName,
            List<RichSectionExtractor.RichSection> structuredSections,
            FieldConfig? config)
        {
            if (structuredSections == null || structuredSections.Count == 0)
                return string.Empty;

            var sections = new List<SectionViewDto>();
            foreach (var rich in structuredSections)
            {
                var heading = string.IsNullOrWhiteSpace(rich.Name) ? "Section" : rich.Name;
                var body = CollectSectionText(rich);
                sections.Add(new SectionViewDto
                {
                    Heading = heading,
                    Paragraphs = string.IsNullOrWhiteSpace(body)
                        ? new List<string>()
                        : new List<string> { body }
                });
            }

            return TryMatchSectionStyle(tokenName, sections, config);
        }

        /// <summary>
        /// Builds higher-level field mappings (scalars, lists, tables) from raw document tree and token matches.
        /// Returns a diagnostic object per field: value + source + logic + failedReason.
        /// The mapping is driven entirely by FieldMappingSpec.json (fieldSpec).
        /// </summary>
        private Dictionary<string, FieldResult> BuildFieldMappings(
            RawDocumentTreeDto raw,
            List<TemplateTokenDto> tokens,
            Dictionary<string, string> tokenMatches,
            Dictionary<string, FieldConfig> fieldSpec)
        {
            if (raw == null)
            {
                return new Dictionary<string, FieldResult>(StringComparer.OrdinalIgnoreCase);
            }

            var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            var structuredSections = raw.StructuredSections ?? new List<RichSectionExtractor.RichSection>();
            var paragraphs = BuildVirtualParagraphsFromStructuredSections(structuredSections);
            var tablesFromStructured = CollectTablesFromStructuredSections(structuredSections);

            // 1) Infer field types (scalar/list/table) from template tokens and grouping markers
            //    where not already specified in FieldMappingSpec.json. This allows us to keep the
            //    engine generic and drive structure from the template itself.
            InferFieldTypesFromTokens(fieldSpec, tokens);

            // 2) Ensure we have entries for all template tokens and infer a default type
            //    of "scalar" for any token that does not have a corresponding config entry.
            //    This keeps structural decisions driven by the template; config is used
            //    only for label/header patterns and aliases.
            if (tokens != null)
            {
                foreach (var token in tokens)
                {
                    var name = token.Name;
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    // Skip group markers like #ITEMS or /ITEMS
                    if (name.StartsWith("#", StringComparison.Ordinal) ||
                        name.StartsWith("/", StringComparison.Ordinal))
                        continue;

                    // If config already defines this field (possibly with patterns), don't overwrite it
                    if (!fieldSpec.ContainsKey(name))
                    {
                        fieldSpec[name] = new FieldConfig
                        {
                            Type = "scalar"
                        };
                    }
                }
            }

            // 3) For fields coming from config, infer a type when none has been set yet:
            //    - table when HeaderPatterns are present
            //    - list  when LinePatterns or SplitOn are present
            //    - scalar otherwise.
            foreach (var kvp in fieldSpec)
            {
                var cfg = kvp.Value;
                if (cfg == null || !string.IsNullOrWhiteSpace(cfg.Type))
                {
                    continue;
                }

                if (cfg.HeaderPatterns != null && cfg.HeaderPatterns.Count > 0)
                {
                    cfg.Type = "table";
                }
                else if ((cfg.LinePatterns != null && cfg.LinePatterns.Count > 0) ||
                         (cfg.SplitOn != null && cfg.SplitOn.Count > 0))
                {
                    cfg.Type = "list";
                }
                else
                {
                    cfg.Type = "scalar";
                }
            }

            // 4) Build raw values by iterating over all fields defined in spec
            foreach (var kvp in fieldSpec)
            {
                var fieldName = kvp.Key;
                var cfg = kvp.Value;
                if (cfg == null || string.IsNullOrWhiteSpace(cfg.Type))
                {
                    continue;
                }

                var type = cfg.Type!.Trim().ToLowerInvariant();

                switch (type)
                {
                    case "scalar":
                    {
                        // Prefer tokenMatches; many scalar fields come directly from token-based label extraction
                        if (tokenMatches != null && tokenMatches.TryGetValue(fieldName, out var value))
                        {
                            map[fieldName] = value ?? string.Empty;
                        }
                        else
                        {
                            map[fieldName] = string.Empty;
                        }

                        // Optional regex post-processing for scalar values
                        map[fieldName] = ApplyRegexPostProcessing(map[fieldName], cfg);
                        break;
                    }

                    case "list":
                    {
                        // If list is defined with linePatterns in config, treat as paragraph-derived list
                        if (cfg.LinePatterns != null && cfg.LinePatterns.Count > 0)
                        {
                            map[fieldName] = ExtractScopeItems(paragraphs, cfg);
                        }
                        else
                        {
                            // For complex list schemas (multiple ElementFields) backed by a rich
                            // hierarchical section (e.g. Business Rules → Rule Name + Actions),
                            // first try to build structured records directly from StructuredSections.
                            if (cfg.ElementFields != null && cfg.ElementFields.Count > 1)
                            {
                                var structuredRecords = ExtractStructuredListRecordsFromSections(fieldName, cfg, structuredSections);
                                if (structuredRecords.Count > 0)
                                {
                                    map[fieldName] = structuredRecords;
                                }
                                else
                                {
                                    map[fieldName] = new List<object?>();
                                }
                            }
                            else
                            {
                                // Preferred: try to resolve simple list fields from the rich semantic tree
                                // built by RichSectionExtractor (StructuredSections). This keeps mapping
                                // logic aligned with the actual document structure (e.g. Project Objectives).
                                var structuredItems = ExtractListFromStructuredSections(fieldName, cfg, structuredSections);
                                if (structuredItems.Count > 0)
                                {
                                    map[fieldName] = structuredItems;
                                }
                                else
                                {
                                    // Heuristic fallbacks (label blocks / delimited scalars) are only safe
                                    // when the field has some explicit configuration hints (labels, patterns,
                                    // headers, etc.). For fields inferred purely from the template structure
                                    // (i.e. no config other than Type/ElementFields), using these broad
                                    // heuristics can easily over-match unrelated content (e.g. API_ENDPOINTS
                                    // picking up a single KPI line).
                                    bool hasStrongConfig =
                                        (cfg.LabelPatterns != null && cfg.LabelPatterns.Count > 0) ||
                                        (cfg.LinePatterns != null && cfg.LinePatterns.Count > 0) ||
                                        (cfg.HeaderPatterns != null && cfg.HeaderPatterns.Count > 0) ||
                                        (cfg.RegexPatterns != null && cfg.RegexPatterns.Count > 0) ||
                                        (cfg.SplitOn != null && cfg.SplitOn.Count > 0);

                                    if (hasStrongConfig)
                                    {
                                        // Fallback: label+following-lines pattern in flat paragraphs
                                        // e.g. a paragraph "Project Objectives:" followed by several bullet lines.
                                        var labelBlockItems = ExtractListFromLabelBlock(fieldName, cfg, paragraphs);
                                        if (labelBlockItems.Count > 0)
                                        {
                                            map[fieldName] = labelBlockItems;
                                        }
                                        else
                                        {
                                            // Final fallback: treat token match as a delimited scalar list
                            if (tokenMatches != null && tokenMatches.TryGetValue(fieldName, out var rawValue) &&
                                !string.IsNullOrWhiteSpace(rawValue))
                            {
                                var delims = (cfg.SplitOn ?? new List<string> { ",", ";" })
                                    .Where(p => !string.IsNullOrEmpty(p))
                                    .ToArray();

                                var listValues = rawValue
                                    .Split(delims, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(v => v.Trim())
                                    .Where(v => !string.IsNullOrWhiteSpace(v))
                                    .ToList();

                                map[fieldName] = listValues;
                            }
                            else
                            {
                                map[fieldName] = new List<string>();
                            }
                        }
                                    }
                                    else
                                    {
                                        // Purely template-inferred list with no config hints and no structured
                                        // match → leave empty instead of guessing. This keeps complex tokens
                                        // like API_ENDPOINTS/DB_TABLES from being incorrectly populated from
                                        // unrelated paragraphs when we only know their name from the template.
                                        map[fieldName] = new List<string>();
                                    }
                                }
                            }
                        }

                        // Optional regex post-processing for list values
                        map[fieldName] = ApplyRegexPostProcessing(map[fieldName], cfg);
                        break;
                    }

                    case "table":
                    {
                        // For tables backed by real DOCX tables, rely entirely on headerPatterns in config.
                        // (Semantic tables modeled via StructuredSections can be supported generically later
                        //  via additional config, without hard-coding specific field names here.)
                        map[fieldName] = ExtractTableByConfig(tablesFromStructured, cfg);
                        break;
                    }

                    default:
                        // Unknown type - skip
                        break;
                }
            }

            // 4b) Generic promotion: for any table-valued field whose rows have column
            //     names that also exist as scalar fields, populate those scalar fields
            //     from the first data row when they are still empty.
            PromoteTableColumnsToScalars(map);

            // 4c) Generic de-duplication across scalar and list fields. Earlier fields
            //     in fieldSpec "claim" their content; later fields see only remaining,
            //     unused values. This prevents the same sentence/list item from being
            //     reused across multiple fields (e.g. SCOPE_ITEMS vs IN_SCOPE_FEATURES).
            DeduplicateFieldContents(map, fieldSpec);

            // 4d) For complex list schemas (multiple ElementFields) that still have
            //     no real data after structured extraction, standardise behaviour by
            //     returning a single placeholder object with all element keys present
            //     and empty string values. This keeps schemas like DB_TABLES or
            //     API_ENDPOINTS predictable even when the raw document has no rows.
            EnsurePlaceholderForEmptyLists(map, fieldSpec);

            // 5) Build an initial content-usage set so that heuristic fallbacks do not
            //    blindly reuse the same text for many different scalar fields. This is
            //    generic (content-based), not token-name based.
            var usedContentValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in map)
            {
                if (entry.Value is string s && !string.IsNullOrWhiteSpace(s))
                {
                    usedContentValues.Add(NormalizeContentForDedup(s));
                }
            }

            // Track heuristic scores for provenance (scalar fields filled by heuristic fallback)
            var heuristicScores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // 6) Generic scalar fallback (safe, opt-in, config-aware, structure-driven)
            //    For scalar fields that are still empty and have AllowHeuristic=true,
            //    we attempt a best-effort match against the rich StructuredSections
            //    tree, while also respecting content-level deduplication via
            //    usedContentValues.
            ApplyHeuristicScalarFallbacks(map, fieldSpec, raw, usedContentValues, heuristicScores);

            // 7) Apply any post-processing heuristics that depend on the overall document,
            //    e.g. inferring DOCUMENT_TYPE from the top-level heading when not explicitly labeled.
            ApplyDocumentTypeHeuristic(map, fieldSpec, raw);

            // 8) Wrap raw values into diagnostic objects (value + source + logic + failureReason + score)
            var results = BuildFieldResults(map, fieldSpec, raw, heuristicScores);
            return results;
        }

        /// <summary>
        /// For list fields with a structured schema (ElementFields.Count > 1) that
        /// ended up with no records, inject a single placeholder object containing
        /// all element field keys mapped to empty strings. This ensures that consumers
        /// can always rely on the list shape (e.g. DB_TABLES, API_ENDPOINTS) even
        /// when the source document has no matching rows.
        /// </summary>
        private void EnsurePlaceholderForEmptyLists(
            Dictionary<string, object?> map,
            Dictionary<string, FieldConfig> fieldSpec)
        {
            if (map == null || map.Count == 0 || fieldSpec == null || fieldSpec.Count == 0)
                return;

            foreach (var kvp in fieldSpec)
            {
                var fieldName = kvp.Key;
                var cfg = kvp.Value;
                if (cfg == null)
                    continue;

                if (!string.Equals(cfg.Type, "list", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Only apply to record-style lists: schemas with multiple element fields.
                if (cfg.ElementFields == null || cfg.ElementFields.Count <= 1)
                    continue;

                if (!map.TryGetValue(fieldName, out var currentValue))
                {
                    // No entry at all – treat as empty.
                }
                else if (currentValue is System.Collections.IEnumerable enumerable &&
                         currentValue is not string)
                {
                    // If there is at least one element, we leave it as-is.
                    if (enumerable.Cast<object?>().Any())
                        continue;
                }
                else
                {
                    // Non-enumerable value for a list field – do not try to reshape it here.
                    continue;
                }

                var placeholder = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var rawName in cfg.ElementFields.Where(n => !string.IsNullOrWhiteSpace(n)))
                {
                    placeholder[rawName.Trim()] = string.Empty;
                }

                if (placeholder.Count == 0)
                    continue;

                // Use a list of dictionaries, consistent with other structured list outputs.
                map[fieldName] = new List<Dictionary<string, object?>> { placeholder };
            }
        }

        /// <summary>
        /// Promotes table columns into scalar fields in a generic way:
        /// - For every field whose value is a list of row dictionaries
        /// - Look at the first row; for each column key K
        /// - If there is a scalar field K with an empty value, fill it from that cell.
        /// This has no hard-coded field names and works for any table group that shares
        /// column names with scalar tokens (e.g. VERSION_*, CHANGE_*).
        /// </summary>
        private void PromoteTableColumnsToScalars(Dictionary<string, object?> map)
        {
            if (map == null || map.Count == 0)
                return;

            // Snapshot keys to avoid modifying the dictionary while iterating
            foreach (var entry in map.ToList())
            {
                if (entry.Value is not IEnumerable<Dictionary<string, string>> tableRowsEnumerable)
                    continue;

                var firstRow = tableRowsEnumerable.FirstOrDefault();
                if (firstRow == null || firstRow.Count == 0)
                    continue;

                foreach (var kvp in firstRow)
                {
                    var columnName = kvp.Key;
                    var cellValue = kvp.Value;

                    if (string.IsNullOrWhiteSpace(columnName) ||
                        string.IsNullOrWhiteSpace(cellValue))
                    {
                        continue;
                    }

                    // Only promote into fields that exist in the map and are currently empty
                    if (!map.TryGetValue(columnName, out var existing))
                    {
                        continue;
                    }

                    var hasValue = false;
                    if (existing is string s)
                    {
                        hasValue = !string.IsNullOrWhiteSpace(s);
                    }
                    else if (existing is System.Collections.IEnumerable enumerable &&
                             existing is not string)
                    {
                        hasValue = enumerable.Cast<object?>().Any();
                    }

                    if (hasValue)
                    {
                        continue;
                    }

                    map[columnName] = cellValue;
                }
            }
        }

        /// <summary>
        /// Generic content-level de-duplication across scalar and list fields.
        /// The first field (based on fieldSpec order) "claims" a piece of text;
        /// later fields will have that same text removed from their values.
        /// This avoids reusing the same content for multiple fields (for example,
        /// SCOPE_ITEMS vs IN_SCOPE_FEATURES).
        /// </summary>
        private void DeduplicateFieldContents(
            Dictionary<string, object?> map,
            Dictionary<string, FieldConfig> fieldSpec)
        {
            if (map == null || map.Count == 0 || fieldSpec == null || fieldSpec.Count == 0)
                return;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in fieldSpec)
            {
                var fieldName = kvp.Key;
                if (!map.TryGetValue(fieldName, out var value) || value is null)
                    continue;

                switch (value)
                {
                    case string s:
                    {
                        var norm = NormalizeContentForDedup(s);
                        if (string.IsNullOrWhiteSpace(norm))
                        {
                            map[fieldName] = string.Empty;
                            break;
                        }

                        if (seen.Contains(norm))
                        {
                            map[fieldName] = string.Empty;
                        }
                        else
                        {
                            seen.Add(norm);
                            map[fieldName] = s;
                        }

                        break;
                    }

                    case IEnumerable<string> listStr:
                    {
                        var newList = new List<string>();
                        foreach (var item in listStr)
                        {
                            if (string.IsNullOrWhiteSpace(item))
                                continue;

                            var norm = NormalizeContentForDedup(item);
                            if (string.IsNullOrWhiteSpace(norm) || seen.Contains(norm))
                                continue;

                            seen.Add(norm);
                            newList.Add(item);
                        }

                        map[fieldName] = newList;
                        break;
                    }

                    case IEnumerable<object?> listObj:
                    {
                        // Only dedupe string items; keep non-string objects as-is.
                        var newList = new List<object?>();
                        foreach (var obj in listObj)
                        {
                            if (obj is string itemStr)
                            {
                                if (string.IsNullOrWhiteSpace(itemStr))
                                    continue;

                                var norm = NormalizeContentForDedup(itemStr);
                                if (string.IsNullOrWhiteSpace(norm) || seen.Contains(norm))
                                    continue;

                                seen.Add(norm);
                                newList.Add(itemStr);
                            }
                            else
                            {
                                newList.Add(obj);
                            }
                        }

                        map[fieldName] = newList;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Infers high-level field types (scalar/list/table) from the template tokens and group
        /// markers where not already defined in FieldMappingSpec.json.
        ///
        /// Heuristics:
        /// - #GROUP_NAME ... /GROUP_NAME defines a logical collection field GROUP_NAME.
        /// - If there is one child token associated with the group, treat GROUP_NAME as a list.
        /// - If there are multiple child tokens with a common root (e.g. VERSION_*), treat
        ///   GROUP_NAME as a table with those columns.
        /// - Explicit types in FieldMappingSpec always win and are never overridden.
        /// </summary>
        private void InferFieldTypesFromTokens(
            Dictionary<string, FieldConfig> fieldSpec,
            List<TemplateTokenDto> tokens)
        {
            if (tokens == null || tokens.Count == 0)
            {
                return;
            }

            // Identify group names and their child element fields from #GROUP_NAME ...
            // /GROUP_NAME markers in the template token stream.
            //
            // Example:
            //   #PERSONAS
            //     PERSONA_INDEX
            //     PERSONA_NAME
            //     PERSONA_ROLE
            //   /PERSONAS
            //
            // Produces:
            //   group PERSONAS with ElementFields:
            //     [PERSONA_INDEX, PERSONA_NAME, PERSONA_ROLE]
            //
            var groupToElements = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var groupStack = new Stack<string>();

            foreach (var token in tokens)
            {
                var rawName = token.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(rawName))
                    continue;

                if (rawName.StartsWith("#", StringComparison.Ordinal))
                {
                    var groupName = rawName.TrimStart('#', '/').Trim();
                    if (!string.IsNullOrWhiteSpace(groupName))
                    {
                        if (!groupToElements.ContainsKey(groupName))
                        {
                            groupToElements[groupName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        }

                        groupStack.Push(groupName);
                    }

                    continue;
                }

                if (rawName.StartsWith("/", StringComparison.Ordinal))
                {
                    var groupName = rawName.TrimStart('#', '/').Trim();
                    if (!string.IsNullOrWhiteSpace(groupName) &&
                        groupStack.Count > 0 &&
                        string.Equals(groupStack.Peek(), groupName, StringComparison.OrdinalIgnoreCase))
                    {
                        groupStack.Pop();
                    }

                    continue;
                }

                // Normal token: if we are currently inside a group, treat this as a
                // child/element field of the innermost group.
                if (groupStack.Count > 0)
                {
                    var currentGroup = groupStack.Peek();
                    if (!string.IsNullOrWhiteSpace(currentGroup))
                    {
                        if (!groupToElements.TryGetValue(currentGroup, out var elements))
                        {
                            elements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            groupToElements[currentGroup] = elements;
                        }

                        elements.Add(rawName);
                    }
                }
            }

            if (groupToElements.Count == 0)
                return;

            foreach (var kvp in groupToElements)
            {
                var groupName = kvp.Key;
                var elementSet = kvp.Value;

                if (!fieldSpec.TryGetValue(groupName, out var cfg) || cfg == null)
                {
                    cfg = new FieldConfig();
                    fieldSpec[groupName] = cfg;
                }

                // Respect explicit type from config (e.g. VERSION_HISTORY, CHANGE_LOG tables)
                if (!string.IsNullOrWhiteSpace(cfg.Type))
                {
                    continue;
                }

                // Generic rule: any #GROUP_NAME ... [/GROUP_NAME] represents a repeatable
                // collection, so its logical field GROUP_NAME is treated as a list by default.
                // This keeps type inference driven purely by tokens, without any knowledge
                // of specific business fields.
                cfg.Type = "list";

                // Persist the element/child field schema so that list/object mappers can
                // understand the intended shape of each list (simple string list vs.
                // object list with multiple scalar properties).
                if (elementSet.Count > 0)
                {
                    cfg.ElementFields = elementSet
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
            }

            // Apply inferred group → element relationships back onto fieldSpec so that
            // downstream components (including the sanitised TemplateTokens view) can
            // understand which fields are logical lists and what their element fields are.
            foreach (var kvp in groupToElements)
            {
                var groupName = kvp.Key;
                var elements = kvp.Value;

                if (!fieldSpec.TryGetValue(groupName, out var cfg) || cfg == null)
                {
                    cfg = new FieldConfig();
                    fieldSpec[groupName] = cfg;
                }

                if (string.IsNullOrWhiteSpace(cfg.Type))
                {
                    cfg.Type = "list";
                }

                if (cfg.ElementFields == null)
                {
                    cfg.ElementFields = elements.ToList();
                }
                else
                {
                    foreach (var e in elements)
                    {
                        if (!cfg.ElementFields.Contains(e, StringComparer.OrdinalIgnoreCase))
                        {
                            cfg.ElementFields.Add(e);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Builds a sanitised, semantic view of the template tokens for JSON output.
        /// This collapses group markers like #ITEMS ... /ITEMS into a single logical
        /// entry and hides element fields that are only meaningful within a group.
        /// </summary>
        private List<TemplateTokenViewDto> BuildTemplateTokenView(
            List<TemplateTokenDto> rawTokens,
            Dictionary<string, FieldConfig> fieldSpec)
        {
            var result = new List<TemplateTokenViewDto>();

            if (rawTokens == null || rawTokens.Count == 0)
            {
                return result;
            }

            // Map logical field name → representative pattern. For group-style
            // markers we prefer the opening marker (e.g. "[#PERSONAS]") so that
            // the list semantics are obvious.
            var patternByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var t in rawTokens)
            {
                var rawName = t.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(rawName))
                    continue;

                var baseName = rawName.TrimStart('#', '/').Trim();
                if (string.IsNullOrWhiteSpace(baseName))
                    continue;

                var pattern = t.Pattern ?? $"[{baseName}]";

                if (!patternByName.ContainsKey(baseName))
                {
                    patternByName[baseName] = pattern;
                }

                // Prefer the opening group marker pattern if present.
                if (rawName.StartsWith("#", StringComparison.Ordinal))
                {
                    patternByName[baseName] = pattern;
                }
            }

            // Collect all element field names so we can avoid emitting them as
            // top-level tokens (they will appear under ElementFields instead).
            var elementFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cfg in fieldSpec.Values)
            {
                if (cfg?.ElementFields == null) continue;

                foreach (var e in cfg.ElementFields)
                {
                    if (!string.IsNullOrWhiteSpace(e))
                    {
                        elementFieldNames.Add(e);
                    }
                }
            }

            foreach (var kvp in fieldSpec.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                var name = kvp.Key;
                var cfg = kvp.Value ?? new FieldConfig();

                // Hide element/column fields from the top-level token list; they
                // will be visible via the parent group's ElementFields instead.
                if (elementFieldNames.Contains(name))
                {
                    continue;
                }

                var view = new TemplateTokenViewDto
                {
                    Name = name,
                    Type = string.IsNullOrWhiteSpace(cfg.Type) ? null : cfg.Type,
                    Pattern = patternByName.TryGetValue(name, out var p) ? p : $"[{name}]",
                    ElementFields = cfg.ElementFields != null && cfg.ElementFields.Count > 0
                        ? cfg.ElementFields
                            .Where(e => !string.IsNullOrWhiteSpace(e))
                            .Select(e =>
                            {
                                // For each element field, look up its own config and pattern
                                fieldSpec.TryGetValue(e, out var elementCfg);
                                return new TemplateElementFieldViewDto
                                {
                                    Name = e,
                                    Type = string.IsNullOrWhiteSpace(elementCfg?.Type) ? null : elementCfg.Type,
                                    Pattern = patternByName.TryGetValue(e, out var ep) ? ep : $"[{e}]"
                                };
                            })
                            .ToList()
                        : null
                };

                result.Add(view);
            }

            return result;
        }

        private static string ToSingular(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return name;

            if (name.EndsWith("IES", StringComparison.OrdinalIgnoreCase) && name.Length > 3)
            {
                return name.Substring(0, name.Length - 3) + "Y";
            }

            if (name.EndsWith("S", StringComparison.OrdinalIgnoreCase) &&
                !name.EndsWith("SS", StringComparison.OrdinalIgnoreCase) &&
                name.Length > 1)
            {
                return name.Substring(0, name.Length - 1);
            }

            return name;
        }

        /// <summary>
        /// Best-effort check to see if a value looks like a file name or path
        /// (e.g. "something.md", "foo/bar.docx"), used to avoid mapping raw
        /// filenames into high-level scalar fields such as DOCUMENT_TYPE.
        /// </summary>
        private bool LooksLikeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var trimmed = value.Trim();

            // Ignore very long blobs of text – they are unlikely to be a single file name.
            if (trimmed.Length > 260)
                return false;

            var lower = trimmed.ToLowerInvariant();

            // Common document-style extensions
            var extensions = new[]
            {
                ".md", ".docx", ".dotx", ".doc", ".pdf",
                ".txt", ".json", ".mmd"
            };

            var hasKnownExtension = extensions.Any(ext =>
                lower.EndsWith(ext, StringComparison.Ordinal));

            // Also treat simple path-like strings with a dot as filenames
            var hasDot = trimmed.Contains('.');
            var hasPathSeparator = trimmed.Contains('/') || trimmed.Contains('\\');

            return hasKnownExtension || (hasDot && hasPathSeparator);
        }

        /// <summary>
        /// Best-effort heuristic to infer DOCUMENT_TYPE from the raw document when no explicit
        /// "Document Type:" label is present. For example, if the top heading is
        /// "FUNCTIONAL SPECIFICATION DOCUMENT (FSD)", we can safely map DOCUMENT_TYPE to
        /// "Functional Specification Document".
        /// </summary>
        private void ApplyDocumentTypeHeuristic(
            Dictionary<string, object?> fieldMappings,
            Dictionary<string, FieldConfig> fieldSpec,
            RawDocumentTreeDto raw)
        {
            // If DOCUMENT_TYPE is explicitly configured as a scalar, we respect that,
            // but still allow this heuristic to override obviously bad values such as
            // raw filenames (e.g. \"...requirements_mvp.md\").
            if (!fieldSpec.TryGetValue("DOCUMENT_TYPE", out var cfg) ||
                cfg == null ||
                !string.Equals(cfg.Type, "scalar", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (fieldMappings.TryGetValue("DOCUMENT_TYPE", out var existing) &&
                existing is string existingStr &&
                !string.IsNullOrWhiteSpace(existingStr))
            {
                // Allow override when the existing value is clearly wrong:
                // - looks like a filename, or
                // - is very long / multi-line (over-captured paragraph/block).
                var tooLong = existingStr.Length > 200 || existingStr.Contains('\n');
                if (!LooksLikeFileName(existingStr) && !tooLong)
                {
                    // Already populated via label/section logic with a reasonable value.
                    return;
                }
            }

            var inferred = InferDocumentTypeFromHeadings(raw);
            if (!string.IsNullOrWhiteSpace(inferred))
            {
                fieldMappings["DOCUMENT_TYPE"] = inferred;
            }
        }

        private string InferDocumentTypeFromHeadings(RawDocumentTreeDto raw)
        {
            if (raw == null)
                return string.Empty;

            // Prefer the first StructuredSection name if available
            var heading = raw.StructuredSections?
                              .FirstOrDefault()?.Name
                          ?? string.Empty;

            heading = heading.Trim();
            if (string.IsNullOrWhiteSpace(heading))
                return string.Empty;

            // Strip any trailing acronym in parentheses, e.g. "(FSD)"
            var parenIndex = heading.IndexOf('(');
            if (parenIndex > 0)
            {
                heading = heading.Substring(0, parenIndex).Trim();
            }

            var upper = heading.ToUpperInvariant();

            // Specific pattern: "FUNCTIONAL SPECIFICATION DOCUMENT"
            if (upper.Contains("FUNCTIONAL SPECIFICATION DOCUMENT"))
            {
                return "Functional Specification Document";
            }

            // Generic fallback: if it contains the word "DOCUMENT", use the cleaned heading as-is
            if (upper.Contains("DOCUMENT"))
            {
                return heading;
            }

            return string.Empty;
        }

        /// <summary>
        /// Heuristic scalar fallback using StructuredSections for scalar fields.
        /// 
        /// Rules:
        /// - Only for scalar fields whose current value is null/empty.
        /// - Never used for *_ID style fields (DOCUMENT_ID, ENTITY_ID, etc.).
        /// - Skips table column helper fields such as VERSION_LABEL, CHANGE_DATE, etc.,
        ///   which are meant to be derived from their parent table mappings.
        /// - Matching is based on token overlap between field name/aliases and
        ///   section/field labels in StructuredSections.
        /// </summary>
        private void ApplyHeuristicScalarFallbacks(
            Dictionary<string, object?> map,
            Dictionary<string, FieldConfig> fieldSpec,
            RawDocumentTreeDto raw,
            HashSet<string> usedContentValues,
            Dictionary<string, int> heuristicScores)
        {
            if (raw == null)
                return;

            var structuredSections = raw.StructuredSections ?? new List<RichSectionExtractor.RichSection>();

            if (structuredSections.Count == 0)
            {
                return;
            }

            foreach (var kvp in fieldSpec)
            {
                var fieldName = kvp.Key;
                var cfg = kvp.Value;
                if (cfg == null || string.IsNullOrWhiteSpace(cfg.Type))
                    continue;

                // Only scalar fields
                if (!string.Equals(cfg.Type, "scalar", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Heuristic fallback is only allowed for fields that explicitly opt-in
                // via config (AllowHeuristic = true). This keeps the engine conservative
                // and prevents accidental guessing for most fields.
                if (!(cfg.AllowHeuristic))
                    continue;

                // Never guess IDs
                if (fieldName.EndsWith("_ID", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip table column helper fields (e.g. VERSION_LABEL, CHANGE_DATE, ...)
                // which are driven by VERSION_HISTORY / CHANGE_LOG table extraction.
                if (IsTableColumnField(fieldName, fieldSpec))
                    continue;

                // Skip if we already have a non-empty value
                if (map.TryGetValue(fieldName, out var existingObj) &&
                    existingObj is string existingStr &&
                    !string.IsNullOrWhiteSpace(existingStr))
                {
                    continue;
                }

                var labelTokens = BuildHeuristicTokens(fieldName, cfg);
                if (labelTokens.Count == 0)
                    continue;

                // Core tokens are derived from the field name itself (e.g. FEATURE_NAME → "feature"),
                // so that we only accept candidate labels that share the same semantic anchor and
                // avoid generic labels like "Project Name" being reused for many other fields.
                var coreTokens = BuildCoreTokens(fieldName, labelTokens);
                if (coreTokens.Count == 0)
                    continue;

                string? bestValue = null;
                int bestScore = 0;

                // 1) Search rich structured sections / fields
                foreach (var section in structuredSections)
                {
                    var sectionLabel = $"{section.Section} {section.Name}".Trim();

                    // Section heading as candidate (e.g. "1.3 Success Metrics")
                    TryUpdateBestCandidate(
                            labelTokens,
                            coreTokens,
                        sectionLabel,
                        CollectSectionText(section),
                        ref bestValue,
                        ref bestScore);

                    // Child fields inside this section
                    if (section.Value == null)
                        continue;

                    foreach (var field in section.Value)
                    {
                        TryUpdateBestCandidate(
                            labelTokens,
                            coreTokens,
                            field.Name ?? string.Empty,
                            CollectRichFieldText(field),
                            ref bestValue,
                            ref bestScore);
                    }
                }

                // Require at least one shared token between field and candidate label
                if (bestScore > 0 && !string.IsNullOrWhiteSpace(bestValue))
                {
                    var normalized = NormalizeContentForDedup(bestValue!);

                    // Avoid over-extraction: if the heuristic value is excessively long,
                    // it likely represents an entire section or multi-paragraph block
                    // rather than a single scalar field. In that case, skip it.
                    const int MaxHeuristicLength = 300;
                    if (normalized.Length > MaxHeuristicLength)
                        continue;

                    // Content-level deduplication: if this exact text has already been used
                    // for some other field, do not reuse it for this one.
                    if (!usedContentValues.Contains(normalized))
                    {
                        usedContentValues.Add(normalized);
                        map[fieldName] = normalized;
                        heuristicScores[fieldName] = bestScore;
                    }
                }
            }
        }

        /// <summary>
        /// Normalizes content for deduplication: trims and collapses trivial whitespace
        /// differences so that logically identical strings compare equal.
        /// </summary>
        private string NormalizeContentForDedup(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            // Trim and normalize internal whitespace to a single space
            var trimmed = value.Trim();
            return System.Text.RegularExpressions.Regex.Replace(trimmed, "\\s+", " ");
        }

        /// <summary>
        /// Returns true when a scalar field name is used as a logical column
        /// in any table-backed field (FieldConfig.Type == "table").
        /// This lets us skip heuristics for helper tokens like VERSION_LABEL.
        /// </summary>
        private bool IsTableColumnField(string fieldName, Dictionary<string, FieldConfig> fieldSpec)
        {
            foreach (var kvp in fieldSpec)
            {
                var cfg = kvp.Value;
                if (cfg == null ||
                    !string.Equals(cfg.Type, "table", StringComparison.OrdinalIgnoreCase) ||
                    cfg.HeaderPatterns == null)
                {
                    continue;
                }

                foreach (var columnKey in cfg.HeaderPatterns.Keys)
                {
                    if (string.Equals(columnKey, fieldName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }


        /// <summary>
        /// Builds a token set for heuristic matching from the field name plus any
        /// LabelPatterns (aliases) defined in config.
        /// </summary>
        private HashSet<string> BuildHeuristicTokens(string fieldName, FieldConfig? config)
        {
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Base: token name converted to words
            var labelBase = fieldName
                .Trim('#', '/')
                .Replace("_", " ");

            foreach (var t in TokenizeForHeuristics(labelBase))
            {
                tokens.Add(t);
            }

            // Aliases from config (if any)
            if (config?.LabelPatterns != null)
            {
                foreach (var lp in config.LabelPatterns)
                {
                    if (string.IsNullOrWhiteSpace(lp))
                        continue;

                    foreach (var t in TokenizeForHeuristics(lp))
                    {
                        tokens.Add(t);
                    }
                }
            }

            return tokens;
        }

        /// <summary>
        /// Builds a smaller set of "core" tokens for a field, derived from the field
        /// name itself. Example:
        ///   FEATURE_NAME            → { "feature" }
        ///   WORKFLOW_DESCRIPTION    → { "workflow", "description" }
        ///   PROJECT_OBJECTIVE       → { "project", "objective" }
        ///
        /// We keep tokens that are:
        /// - at least 5 characters long, to de-emphasise generic words like "id", "name"
        /// - already present in the broader heuristic token set.
        /// </summary>
        private static readonly HashSet<string> HeuristicGenericSuffixTokens = new(StringComparer.OrdinalIgnoreCase)
        {
            // Generic suffix words that appear in many labels and field names but do not
            // carry domain meaning on their own. We exclude these from "core" tokens so
            // that fields don't match *only* on words like "name" or "description".
            "name",
            "names",
            "description",
            "descriptions",
            "label",
            "labels",
            "value",
            "values",
            "title",
            "titles",
            "text",
            "texts",
            "notes",
            "note",
            "details"
        };

        private HashSet<string> BuildCoreTokens(string fieldName, HashSet<string> allTokens)
        {
            var core = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(fieldName) || allTokens.Count == 0)
            {
                return core;
            }

            var parts = fieldName
                .Trim('#', '/')
                .Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.ToLowerInvariant())
                .ToList();

            foreach (var part in parts)
            {
                if (part.Length < 5)
                {
                    continue;
                }

                if (HeuristicGenericSuffixTokens.Contains(part))
                {
                    continue;
                }

                if (allTokens.Contains(part))
                {
                    core.Add(part);
                }
            }

            return core;
        }

        /// <summary>
        /// Tokenizes a label into lowercase alphanumeric words with length ≥ 3.
        /// </summary>
        private IEnumerable<string> TokenizeForHeuristics(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<string>();

            var lower = text.ToLowerInvariant();
            var parts = Regex.Split(lower, "[^a-z0-9]+");
            return parts.Where(p => p.Length >= 3);
        }

        /// <summary>
        /// Updates the best candidate if the label/value pair has a higher token overlap.
        /// </summary>
        private void TryUpdateBestCandidate(
            HashSet<string> fieldTokens,
            HashSet<string> coreTokens,
            string candidateLabel,
            string candidateValue,
            ref string? bestValue,
            ref int bestScore)
        {
            if (string.IsNullOrWhiteSpace(candidateLabel) || string.IsNullOrWhiteSpace(candidateValue))
                return;

            var candTokens = TokenizeForHeuristics(candidateLabel).ToList();
            if (candTokens.Count == 0)
                return;

            // Require that the candidate label share at least one "core" token with the field.
            // This avoids generic labels like "Project Name" or "Project Description" being
            // reused for many unrelated fields such as FEATURE_NAME, RULE_NAME, etc.
            if (!candTokens.Any(t => coreTokens.Contains(t)))
                return;

            var score = candTokens.Count(t => fieldTokens.Contains(t));

            if (score <= 0)
                return;

            // Prefer strictly higher scores; keep first in case of ties for determinism
            if (score > bestScore)
            {
                bestScore = score;
                bestValue = candidateValue;
            }
        }

        /// <summary>
        /// Builds a synthetic list of paragraphs from StructuredSections so that existing
        /// label-style matching logic can operate on a single canonical source.
        /// Each rich field becomes one or more virtual paragraphs of the form
        ///   "Label: Value" or "Value" for unlabeled content.
        /// </summary>
        private List<ParagraphDto> BuildVirtualParagraphsFromStructuredSections(
            List<RichSectionExtractor.RichSection> structuredSections)
        {
            var result = new List<ParagraphDto>();
            if (structuredSections == null || structuredSections.Count == 0)
                return result;

            int index = 0;

            foreach (var section in structuredSections)
            {
                if (section.Value == null)
                    continue;

                foreach (var field in section.Value)
                {
                    BuildVirtualParagraphsForField(field, ref index, result);
                }
            }

            return result;
        }

        private void BuildVirtualParagraphsForField(
            RichSectionExtractor.RichField field,
            ref int index,
            List<ParagraphDto> output)
        {
            if (field == null)
                return;

            var label = field.Name ?? string.Empty;

            switch (field.Value)
            {
                case string s when !string.IsNullOrWhiteSpace(s):
                    {
                        var text = string.IsNullOrWhiteSpace(label)
                            ? s
                            : $"{label}: {s}";
                        output.Add(new ParagraphDto
                        {
                            Index = index++,
                            Text = text,
                            StyleId = null
                        });
                        break;
                    }

                case List<string> listStrings:
                    {
                        foreach (var item in listStrings.Where(v => !string.IsNullOrWhiteSpace(v)))
                        {
                            var text = string.IsNullOrWhiteSpace(label)
                                ? item
                                : $"{label}: {item}";
                            output.Add(new ParagraphDto
                            {
                                Index = index++,
                                Text = text,
                                StyleId = null
                            });
                        }
                        break;
                    }

                case List<RichSectionExtractor.RichField> childFields:
                    {
                        foreach (var child in childFields)
                        {
                            // For nested fields, prefer the child label as the primary label.
                            BuildVirtualParagraphsForField(child, ref index, output);
                        }
                        break;
                    }

                case TableDto table:
                    {
                        // Represent each table row as a synthetic paragraph by joining cells.
                        if (table.Rows != null && table.Rows.Count > 0)
                        {
                            foreach (var row in table.Rows)
                            {
                                var rowText = string.Join(" | ", row ?? new List<string>());
                                if (string.IsNullOrWhiteSpace(rowText))
                                    continue;

                                var text = string.IsNullOrWhiteSpace(label)
                                    ? rowText
                                    : $"{label}: {rowText}";
                                output.Add(new ParagraphDto
                                {
                                    Index = index++,
                                    Text = text,
                                    StyleId = null
                                });
                            }
                        }
                        break;
                    }
            }
        }

        /// <summary>
        /// Applies optional regex-based post-processing for scalar or list values, driven by FieldConfig.
        /// - For scalar strings, returns the first regex match (or group 1 if present).
        /// - For lists of strings, applies the same transformation per item and drops empties.
        /// Other value types are returned unchanged.
        /// </summary>
        private object? ApplyRegexPostProcessing(object? value, FieldConfig? config)
        {
            if (config == null || config.RegexPatterns == null || config.RegexPatterns.Count == 0)
                return value;

            var patterns = config.RegexPatterns
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            if (patterns.Count == 0 || value == null)
                return value;

            if (value is string s)
            {
                var transformed = ApplyRegexToString(s, patterns);
                return transformed ?? s;
            }

            if (value is IEnumerable<string> list)
            {
                var result = new List<string>();
                foreach (var item in list)
                {
                    if (string.IsNullOrWhiteSpace(item))
                        continue;

                    var transformed = ApplyRegexToString(item, patterns) ?? item;
                    if (!string.IsNullOrWhiteSpace(transformed))
                    {
                        result.Add(transformed);
                    }
                }

                return result;
            }

            return value;
        }

        private string? ApplyRegexToString(string input, List<string> patterns)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            foreach (var pattern in patterns)
            {
                try
                {
                    var rx = new Regex(pattern, RegexOptions.IgnoreCase);
                    var m = rx.Match(input);
                    if (m.Success)
                    {
                        if (m.Groups.Count > 1 && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
                        {
                            return m.Groups[1].Value.Trim();
                        }
                        return m.Value.Trim();
                    }
                }
                catch
                {
                    // Invalid regex pattern: ignore and continue
                }
            }

            return null;
        }

        /// <summary>
        /// Collects all TableDto instances from StructuredSections so that table-based
        /// mapping can operate on the same shape as RawDocumentTree.Tables while using
        /// StructuredSections as the single source of truth.
        /// </summary>
        private List<TableDto> CollectTablesFromStructuredSections(
            List<RichSectionExtractor.RichSection> structuredSections)
        {
            var result = new List<TableDto>();
            if (structuredSections == null || structuredSections.Count == 0)
                return result;

            foreach (var section in structuredSections)
            {
                if (section.Value == null)
                    continue;

                foreach (var field in section.Value)
                {
                    CollectTablesFromField(field, result);
                }
            }

            return result;
        }

        private void CollectTablesFromField(RichSectionExtractor.RichField field, List<TableDto> output)
        {
            if (field.Value is TableDto table && table.Rows != null && table.Rows.Count > 0)
            {
                output.Add(table);
            }
            else if (field.Value is List<RichSectionExtractor.RichField> childFields)
            {
                foreach (var cf in childFields)
                {
                    CollectTablesFromField(cf, output);
                }
            }
        }

        /// <summary>
        /// Collects all text for a rich section by flattening its fields.
        /// </summary>
        private string CollectSectionText(RichSectionExtractor.RichSection section)
        {
            if (section == null || section.Value == null || section.Value.Count == 0)
                return string.Empty;

            var lines = new List<string>();
            foreach (var field in section.Value)
            {
                var text = CollectRichFieldText(field);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    lines.Add(text);
                }
            }

            return string.Join("\n\n", lines);
        }

        /// <summary>
        /// Flattens a RichField into human-readable text while preserving labels
        /// where useful. This keeps the RawDocumentTree semantic while still
        /// giving scalar fields a deterministic string value.
        /// </summary>
        private string CollectRichFieldText(RichSectionExtractor.RichField field)
        {
            if (field == null)
                return string.Empty;

            switch (field.Value)
            {
                case string s when !string.IsNullOrWhiteSpace(s):
                    return s;

                case List<string> listStr:
                {
                    var items = listStr.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                    if (items.Count == 0)
                        return string.Empty;

                    return string.Join("\n", items);
                }

                case List<RichSectionExtractor.RichField> childFields:
                {
                    var lines = new List<string>();
                    foreach (var cf in childFields)
                    {
                        var text = CollectRichFieldText(cf);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            lines.Add(text);
                        }
                    }

                    if (lines.Count == 0)
                        return string.Empty;

                    var joined = string.Join("\n", lines);
                    if (!string.IsNullOrWhiteSpace(field.Name))
                    {
                        return $"{field.Name}: {joined}";
                    }

                    return joined;
                }

                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Builds per-field diagnostic results: value, source, logic, and failure reason.
        /// This is for test/debugging only so we can see how each field was populated.
        /// </summary>
        private Dictionary<string, FieldResult> BuildFieldResults(
            Dictionary<string, object?> fieldMappings,
            Dictionary<string, FieldConfig> fieldSpec,
            RawDocumentTreeDto raw,
            Dictionary<string, int> heuristicScores)
        {
            var results = new Dictionary<string, FieldResult>(StringComparer.OrdinalIgnoreCase);

            // Element/child field names (e.g. TABLE_NAME, ENDPOINT_URL, PERSONA_NAME)
            // so we can avoid emitting separate top-level FieldMappings entries for
            // them. They will instead appear only inside their parent list/table
            // field's Value objects.
            var elementFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cfg in fieldSpec.Values)
            {
                if (cfg?.ElementFields == null) continue;

                foreach (var e in cfg.ElementFields)
                {
                    if (!string.IsNullOrWhiteSpace(e))
                    {
                        elementFieldNames.Add(e);
                    }
                }
            }

            foreach (var kvp in fieldSpec)
            {
                var fieldName = kvp.Key;
                var cfg = kvp.Value;
                if (cfg == null || string.IsNullOrWhiteSpace(cfg.Type))
                {
                    continue;
                }

                // Skip element-only child fields from the top-level FieldMappings
                // output; they are represented structurally under their parent
                // list/table field according to cfg.ElementFields.
                if (elementFieldNames.Contains(fieldName))
                {
                    continue;
                }

                fieldMappings.TryGetValue(fieldName, out var value);
                var type = cfg.Type!.Trim().ToLowerInvariant();

                // Scalar fallback: if nothing was mapped via the primary pipeline but
                // we have label patterns, try to synthesise a value directly from the
                // structured section whose heading matches those labels. This is
                // generic and works for summary-style fields such as
                // USER_STORY_ACCEPTANCE, BUSINESS_CONTEXT, etc.
                if (string.Equals(type, "scalar", StringComparison.OrdinalIgnoreCase) &&
                    (value == null || (value is string sv0 && string.IsNullOrWhiteSpace(sv0))) &&
                    cfg.LabelPatterns != null &&
                    cfg.LabelPatterns.Count > 0 &&
                    raw.StructuredSections != null &&
                    raw.StructuredSections.Count > 0)
                {
                    var fallback = TryMatchSectionStyleFromStructured(
                        fieldName,
                        raw.StructuredSections,
                        cfg);

                    if (!string.IsNullOrWhiteSpace(fallback))
                    {
                        value = fallback;
                    }
                }

                bool hasValue = false;

                switch (type)
                {
                    case "scalar":
                        hasValue = value is string s && !string.IsNullOrWhiteSpace(s);
                        break;

                    case "list":
                        if (value is IEnumerable<string> listStr)
                        {
                            hasValue = listStr.Any();
                        }
                        else if (value is IEnumerable<object> listObj)
                        {
                            hasValue = listObj.Any();
                        }
                        break;

                    case "table":
                        if (value is IEnumerable<Dictionary<string, string>> tableRows)
                        {
                            hasValue = tableRows.Any();
                        }
                        else if (value is IEnumerable<object> tableObj)
                        {
                            hasValue = tableObj.Any();
                        }
                        break;
                }

                // Simple source/logic description based on config
                string source;
                string logic;
                string? text = null;

                // For list fields that have an explicit element schema inferred from the
                // template (ElementFields), present the value as a list of objects instead
                // of a flat list of strings in the JSON output. This keeps FieldMappings
                // structurally aligned with the TemplateTokens view, where each list field
                // exposes its child tokens as ElementFields.
                if (string.Equals(type, "list", StringComparison.OrdinalIgnoreCase) &&
                    cfg.ElementFields != null &&
                    cfg.ElementFields.Count > 0 &&
                    value is IEnumerable<string> flatList)
                {
                    // Generic representation: one object per list entry, keyed by the first
                    // element field name. This works for simple lists (single element field)
                    // and provides a consistent "list of objects" shape without making any
                    // field-specific assumptions. More advanced multi-column shaping can be
                    // added later without breaking this contract.
                    var primaryElementName = cfg.ElementFields[0];
                    var objectRows = flatList
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => (object?)new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            [primaryElementName] = v.Trim()
                        })
                        .ToList();

                    // If there is no data for this list, still emit a single placeholder
                    // row so that the schema is visible to downstream consumers (e.g. a
                    // DB_TABLES or API_ENDPOINTS row with empty child fields).
                    if (objectRows.Count == 0)
                    {
                        var placeholder = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        foreach (var childName in cfg.ElementFields.Where(n => !string.IsNullOrWhiteSpace(n)))
                        {
                            placeholder[childName] = string.Empty;
                        }

                        objectRows.Add(placeholder);
                    }

                    value = objectRows;
                }

                if (cfg.LabelPatterns != null && cfg.LabelPatterns.Count > 0)
                {
                    source = "headerLabel/paragraph";
                    logic = "label";
                    // Only expose the primary label as Text when we actually
                    // mapped a value for this field. This prevents cases like
                    // IN_SCOPE_FEATURES / OUT_SCOPE_FEATURES where the content
                    // is intentionally empty (deduped to SCOPE_ITEMS), but a
                    // misleading Text would still be shown.
                    if (hasValue)
                    {
                        text = cfg.LabelPatterns.FirstOrDefault(lp => !string.IsNullOrWhiteSpace(lp));
                    }
                }
                else if (cfg.LinePatterns != null && cfg.LinePatterns.Count > 0)
                {
                    source = "paragraphLines";
                    logic = "line-split";
                }
                else if (cfg.HeaderPatterns != null && cfg.HeaderPatterns.Count > 0)
                {
                    source = "tableHeaders";
                    logic = "header-match";
                }
                else
                {
                    source = "unknown";
                    logic = "unknown";
                }

                // Attach index information where possible (paragraph or table anchors)
                string? indexInfo = null;
                var paragraphs = BuildVirtualParagraphsFromStructuredSections(
                    raw.StructuredSections ?? new List<RichSectionExtractor.RichSection>());

                if (hasValue)
                {
                    switch (type)
                    {
                        case "scalar":
                            if (value is string sv && !string.IsNullOrWhiteSpace(sv))
                            {
                                var matchPara = paragraphs.FirstOrDefault(p =>
                                    !string.IsNullOrWhiteSpace(p.Text) &&
                                    p.Text!.Contains(sv, StringComparison.Ordinal));
                                if (matchPara != null)
                                {
                                    indexInfo = $"paragraphIndex={matchPara.Index}";
                                }
                            }
                            break;

                        case "list":
                            if (value is IEnumerable<string> listStr)
                            {
                                var indices = new HashSet<int>();
                                foreach (var item in listStr)
                                {
                                    if (string.IsNullOrWhiteSpace(item)) continue;
                                    var matchPara = paragraphs.FirstOrDefault(p =>
                                        !string.IsNullOrWhiteSpace(p.Text) &&
                                        p.Text!.Contains(item, StringComparison.Ordinal));
                                    if (matchPara != null)
                                    {
                                        indices.Add(matchPara.Index);
                                    }
                                }

                                if (indices.Count > 0)
                                {
                                    indexInfo = $"paragraphIndices=[{string.Join(",", indices.OrderBy(i => i))}]";
                                }
                            }
                            break;

                        case "table":
                            // Use table anchors from StructuredSections where possible
                            var tableAnchors = raw.StructuredSections?
                                .SelectMany(sec => sec.Value ?? new List<RichSectionExtractor.RichField>())
                                .Where(f => f.Value is TableDto)
                                .Select(f => ((TableDto)f.Value!).AnchorParagraphIndex)
                                    .Distinct()
                                    .OrderBy(i => i)
                                .ToList() ?? new List<int>();
                            if (tableAnchors.Count > 0)
                                {
                                indexInfo = $"tableAnchorIndices=[{string.Join(",", tableAnchors)}]";
                            }
                            break;
                    }
                }

                if (!string.IsNullOrEmpty(indexInfo))
                {
                    source = $"{source} ({indexInfo})";
                }

                var failedReason = hasValue
                    ? null
                    : "No value matched using configured rules.";

                // Basic provenance score:
                // - For scalar/list/table with a value: default score 1.0
                // - If filled via heuristic fallback, use the heuristic token-overlap score.
                double? score = null;
                string matchType = logic;

                if (hasValue)
                {
                    score = 1.0;
                }

                if (heuristicScores != null &&
                    heuristicScores.TryGetValue(fieldName, out var hScore) &&
                    hScore > 0)
                {
                    score = hScore;
                    matchType = "heuristic";
                }

                results[fieldName] = new FieldResult
                {
                    Value = value,
                    Source = source,
                    Logic = logic,
                    FailedReason = failedReason,
                    Score = score,
                    MatchType = matchType,
                    Text = text
                };
            }

            return results;
        }

        /// <summary>
        /// Extracts fine-grained scope items from paragraphs using configuration.
        /// </summary>
        private List<string> ExtractScopeItems(List<ParagraphDto> paragraphs, FieldConfig? config)
        {
            var result = new List<string>();
            if (paragraphs == null || paragraphs.Count == 0)
            {
                return result;
            }

            // If no config, we can't reliably extract scope items
            if (config == null || (config.LinePatterns == null || config.LinePatterns.Count == 0))
            {
                return result;
            }

            var linePatterns = config.LinePatterns
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.ToLowerInvariant())
                .ToList();

            var excludePatterns = (config.ExcludeLinePatterns ?? new List<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.ToLowerInvariant())
                .ToList();

            var splitDelimiters = (config.SplitOn ?? new List<string> { ",", ";" })
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            foreach (var para in paragraphs)
            {
                var text = para.Text ?? string.Empty;
                var lower = text.ToLowerInvariant();

                // Skip excluded lines
                if (excludePatterns.Any(ep => lower.Contains(ep)))
                {
                    continue;
                }

                // Check if line matches any configured scope marker
                var matchedPattern = linePatterns.FirstOrDefault(lp => lower.Contains(lp));
                if (matchedPattern == null)
                {
                    continue;
                }

                // Extract part after ':' if available, otherwise after marker phrase
                var colonIndex = text.IndexOf(':'); // original case-preserving text
                string listPortion;
                if (colonIndex >= 0)
                {
                    // If there is a colon but nothing after it, there's nothing to extract
                    if (colonIndex + 1 >= text.Length)
                    {
                        continue;
                    }

                    listPortion = text.Substring(colonIndex + 1).Trim();
                }
                else
                {
                    // Fallback: take text after matched marker phrase
                    var markerIndex = lower.IndexOf(matchedPattern, StringComparison.Ordinal);
                    if (markerIndex >= 0)
                    {
                        var afterMarkerIndex = markerIndex + matchedPattern.Length;
                        if (afterMarkerIndex < text.Length)
                        {
                            listPortion = text.Substring(afterMarkerIndex).Trim();
                        }
                        else
                        {
                            listPortion = string.Empty;
                        }
                    }
                    else
                    {
                        listPortion = string.Empty;
                    }
                }

                if (string.IsNullOrWhiteSpace(listPortion))
                {
                    continue;
                }

                // Split by comma/semicolon into individual items
                var items = listPortion
                    .Split(splitDelimiters, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x) && x != ":")
                    .ToList();

                if (items.Count > 0)
                {
                    result.AddRange(items);
                }
            }

            return result;
        }

        /// <summary>
        /// Generic section-to-list extractor for list-typed fields that represent a logical
        /// section of the document (e.g. SUCCESS_METRICS, OBJECTIVE, DATA_ENTITIES, EPICS).
        /// It attempts to find a section whose heading matches the field name (or its aliases)
        /// and returns that section's paragraphs as individual list items.
        /// </summary>
        private List<string> ExtractListFromSection(string fieldName, FieldConfig? config, List<SectionViewDto> sections)
        {
            var items = new List<string>();
            if (sections == null || sections.Count == 0)
            {
                return items;
            }

            // Build label candidates from config aliases (if any) plus the token name itself.
            var labelCandidates = new List<string>();
            if (config?.LabelPatterns != null && config.LabelPatterns.Count > 0)
            {
                labelCandidates.AddRange(config.LabelPatterns.Where(lp => !string.IsNullOrWhiteSpace(lp)));
            }

            labelCandidates.Add(
                fieldName
                    .Trim('#', '/')
                    .Replace("_", " ")
            );

            foreach (var rawLabel in labelCandidates)
            {
                var label = rawLabel.Trim();
                if (string.IsNullOrWhiteSpace(label))
                    continue;

                var labelWords = label
                    .ToLowerInvariant()
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 2)
                    .ToArray();

                if (labelWords.Length == 0)
                    continue;

                foreach (var section in sections)
                {
                    var headingLower = (section.Heading ?? string.Empty).ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(headingLower))
                        continue;

                    // Improved: word boundary matching with plural/singular handling
                    bool allMatch = labelWords.All(w =>
                    {
                        // Check for exact word match with word boundaries
                        var wordPattern = @"\b" + Regex.Escape(w) + @"\b";
                        if (Regex.IsMatch(headingLower, wordPattern, RegexOptions.IgnoreCase))
                            return true;

                        // Check singular/plural variations
                        var singularUpper = ToSingular(w.ToUpperInvariant());
                        var singular = singularUpper.ToLowerInvariant();
                        if (w != singular)
                        {
                            var singularPattern = @"\b" + Regex.Escape(singular) + @"\b";
                            if (Regex.IsMatch(headingLower, singularPattern, RegexOptions.IgnoreCase))
                                return true;
                        }

                        // Check plural form
                        var plural = w + "s";
                        if (w != plural && !w.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                        {
                            var pluralPattern = @"\b" + Regex.Escape(plural) + @"\b";
                            if (Regex.IsMatch(headingLower, pluralPattern, RegexOptions.IgnoreCase))
                                return true;
                        }

                        return false;
                    });

                    if (!allMatch)
                        continue;

                    if (section.Paragraphs != null && section.Paragraphs.Count > 0)
                    {
                        items.AddRange(
                            section.Paragraphs
                                .Where(p => !string.IsNullOrWhiteSpace(p))
                                .Select(p => p.Trim())
                        );
                    }

                    if (items.Count > 0)
                    {
                        return items;
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// Label+following-lines extractor for list-typed fields that are introduced by a label
        /// paragraph followed by several bullet/compact lines inside the same section.
        /// Example pattern:
        ///   "Project Objectives:"
        ///   "Enable trainers to efficiently manage multiple clients"
        ///   "Provide clients with easy-to-use workout and meal logging tools"
        /// </summary>
        private List<string> ExtractListFromLabelBlock(string fieldName, FieldConfig? config, List<ParagraphDto> paragraphs)
        {
            var items = new List<string>();
            if (paragraphs == null || paragraphs.Count == 0)
            {
                return items;
            }

            // Normalize field name
            var normalizedFieldName = NormalizePlaceholderName(fieldName);

            // Candidate labels: config label patterns + token-derived label
            var labelCandidates = new List<string>();
            if (config?.LabelPatterns != null && config.LabelPatterns.Count > 0)
            {
                labelCandidates.AddRange(config.LabelPatterns.Where(lp => !string.IsNullOrWhiteSpace(lp)));
            }

            labelCandidates.Add(
                normalizedFieldName
                    .Trim('#', '/')
                    .Replace("_", " ")
            );

            // Add variations for better matching
            var additionalCandidates = new List<string>();
            foreach (var candidate in labelCandidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                var baseCandidate = candidate.Trim().ToLowerInvariant();
                additionalCandidates.Add(baseCandidate);

                // Add variation without spaces
                if (baseCandidate.Contains(" "))
                {
                    additionalCandidates.Add(baseCandidate.Replace(" ", ""));
                }

                // Add variation with underscores
                if (!baseCandidate.Contains("_"))
                {
                    additionalCandidates.Add(baseCandidate.Replace(" ", "_"));
                }
            }

            // Normalize label candidates
            var normalizedLabels = additionalCandidates
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct()
                .ToList();

            if (normalizedLabels.Count == 0)
            {
                return items;
            }

            for (int i = 0; i < paragraphs.Count; i++)
            {
                var para = paragraphs[i];
                var text = (para.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                var lower = text.ToLowerInvariant();

                // Improved: Check if this paragraph is a label line with word boundary matching
                // Handles cases like "Project Objectives:" or "Project Objectives :"
                var isLabelLine = false;
                foreach (var label in normalizedLabels)
                {
                    // Match at start of line with optional whitespace before colon
                    var labelPattern = @"^" + Regex.Escape(label) + @"\s*:";
                    if (Regex.IsMatch(lower, labelPattern, RegexOptions.IgnoreCase))
                    {
                        isLabelLine = true;
                        break;
                    }

                    // Also check for exact match (without colon)
                    if (string.Equals(lower.TrimEnd(':'), label, StringComparison.OrdinalIgnoreCase))
                    {
                        isLabelLine = true;
                        break;
                    }
                }

                if (!isLabelLine)
                    continue;

                // Collect following paragraphs until blank or another label-like line
                for (int j = i + 1; j < paragraphs.Count; j++)
                {
                    var next = paragraphs[j];
                    var nextText = (next.Text ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(nextText))
                    {
                        break;
                    }

                    var nextLower = nextText.ToLowerInvariant();

                    // Improved: Better detection of next label-like line
                    // Stop if we hit another label-like line (something with a colon and short-ish)
                    // Also check for common label patterns
                    var isNextLabel = nextText.EndsWith(":", StringComparison.Ordinal) ||
                        (nextText.Contains(":", StringComparison.Ordinal) && nextText.Length <= 80);

                    // Check if it matches any of our label patterns (likely a new section)
                    if (!isNextLabel)
                    {
                        foreach (var label in normalizedLabels)
                        {
                            var labelPattern = @"^" + Regex.Escape(label) + @"\s*:";
                            if (Regex.IsMatch(nextLower, labelPattern, RegexOptions.IgnoreCase))
                            {
                                isNextLabel = true;
                                break;
                            }
                        }
                    }

                    if (isNextLabel)
                    {
                        break;
                    }

                    items.Add(nextText);
                }

                if (items.Count > 0)
                {
                    return items;
                }
            }

            return items;
        }

        /// <summary>
        /// Extracts table rows from tables based on header semantics and configuration.
        /// The logical column names are taken from config.HeaderPatterns keys.
        /// </summary>
        private List<Dictionary<string, string>> ExtractTableByConfig(List<TableDto> tables, FieldConfig? config)
        {
            var list = new List<Dictionary<string, string>>();
            if (tables == null || tables.Count == 0)
            {
                return list;
            }

            if (config == null || config.HeaderPatterns == null || config.HeaderPatterns.Count == 0)
            {
                return list;
            }

            foreach (var table in tables)
            {
                if (table.Rows == null || table.Rows.Count < 2)
                {
                    continue;
                }

                var header = table.Rows[0]
                    .Select(c => (c ?? string.Empty).Trim())
                    .ToList();

                if (header.Count == 0)
                {
                    continue;
                }

                var headerLower = header.Select(h => h.ToLowerInvariant()).ToList();

                // Map every logical field in config.HeaderPatterns to a column index
                var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var hp in config.HeaderPatterns)
                {
                    var logicalName = hp.Key;
                    var patterns = hp.Value;
                    if (patterns == null || patterns.Count == 0)
                    {
                        continue;
                    }

                    foreach (var pattern in patterns)
                    {
                        var pat = pattern.ToLowerInvariant();
                        var idx = headerLower.FindIndex(h => h.Contains(pat));
                        if (idx >= 0)
                        {
                            columnIndex[logicalName] = idx;
                            break;
                        }
                    }
                }

                // At minimum, require at least one mapped column
                if (columnIndex.Count == 0)
                {
                    continue;
                }

                for (int i = 1; i < table.Rows.Count; i++)
                {
                    var row = table.Rows[i];
                    if (row == null || row.Count == 0)
                    {
                        continue;
                    }

                    string GetCell(int index)
                    {
                        return index >= 0 && index < row.Count
                            ? (row[index] ?? string.Empty).Trim()
                            : string.Empty;
                    }

                    var entry = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var col in columnIndex)
                    {
                        entry[col.Key] = GetCell(col.Value);
                    }

                    // Skip completely empty entries
                    if (entry.Values.All(v => string.IsNullOrWhiteSpace(v)))
                    {
                        continue;
                    }

                    list.Add(entry);
                }

                // Use first matching table only
                if (list.Count > 0)
                {
                    break;
                }
            }

            return list;
        }

    }
}


