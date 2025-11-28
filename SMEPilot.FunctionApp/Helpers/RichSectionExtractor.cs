using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SMEPilot.FunctionApp.Models;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Builds a rich hierarchical representation of sections and their field-like children
    /// from the BOM tree produced by StructureBuilder.
    /// </summary>
    public class RichSectionExtractor
    {
        public class RichSection
        {
            public string Section { get; set; } = string.Empty; // e.g. "1.1"
            public string Name { get; set; } = string.Empty;    // e.g. "Project Information"
            public List<RichField> Value { get; set; } = new();
        }

        public class RichField
        {
            public string Section { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public object? Value { get; set; } // string or List<RichField> or List<string>
        }

        /// <summary>
        /// Builds rich sections from the BOM root node.
        /// 
        /// Design:
        /// - Every real section node (Level > 0) becomes one RichSection entry.
        /// - We rely on paragraph/list structure inside each section to express
        ///   nesting (e.g. User Personas → Trainer / Client, Business Rules blocks,
        ///   Data Entities, Epics, Workflows, etc.).
        /// - We DO NOT synthesize extra parent/child RichSection copies, so each
        ///   SectionNode is represented exactly once and the full document tree
        ///   is preserved without collapsing.
        /// </summary>
        public List<RichSection> BuildRichSections(StructureBuilder.SectionNode bomRoot)
        {
            var sections = new List<RichSection>();

            if (bomRoot == null)
                return sections;

            foreach (var node in EnumerateSections(bomRoot))
            {
                if (node.Level <= 0)
                    continue;

                var fields = BuildFieldsForSection(node);
                if (fields == null || fields.Count == 0)
                    continue;

                sections.Add(new RichSection
                {
                    Section = node.Number ?? string.Empty,
                    Name = node.Heading ?? string.Empty,
                    Value = fields
                });
            }

            return sections;
        }

        private IEnumerable<StructureBuilder.SectionNode> EnumerateSections(StructureBuilder.SectionNode node)
        {
            if (node == null)
                yield break;

            yield return node;

            if (node.Children == null)
                yield break;

            foreach (var child in node.Children)
            {
                foreach (var descendant in EnumerateSections(child))
                {
                    yield return descendant;
                }
            }
        }

        /// <summary>
        /// Builds fields for a section using both its paragraphs and any tables
        /// that StructureBuilder has attached to it. Paragraph-derived fields are
        /// processed first, then table fields are appended so that all raw content
        /// for the section is represented under StructuredSections.
        /// </summary>
        private List<RichField> BuildFieldsForSection(StructureBuilder.SectionNode sectionNode)
        {
            var fields = BuildFieldsForSection(sectionNode.Paragraphs);

            // Attach tables so that all raw document data is reachable through
            // StructuredSections. We store the full TableDto (AnchorParagraphIndex + Rows)
            // so that the representation under StructuredSections matches the shape used
            // in RawDocumentTree.Tables, just nested under the appropriate section.
            if (sectionNode.Tables != null && sectionNode.Tables.Count > 0)
            {
                foreach (var table in sectionNode.Tables)
                {
                    if (table.Rows == null || table.Rows.Count == 0)
                        continue;

                    fields.Add(new RichField
                    {
                        Section = sectionNode.Number ?? string.Empty,
                        Name = "Table",
                    Value = table
                    });
                }
            }

            return fields;
        }

        private List<RichField> BuildFieldsForSection(List<ParagraphDto> paragraphs)
        {
            var fields = new List<RichField>();
            if (paragraphs == null || paragraphs.Count == 0)
                return fields;

            int index = 0;
            fields = BuildFieldsAtLevel(paragraphs, ref index, 0);

            // Normalise any anonymous lines that actually look like "Label: Value" pairs
            // into proper named fields before we do higher-level collapsing. This acts
            // as a safety net in cases where ParagraphDto/numbering caused a line like
            // "Out of Scope (Future): x, y, z" to come through with Name == "".
            fields = NormalizeAnonymousLabelLines(fields);

            // First pass: "Label" + following bullets → label.Value = list
            // (for already-named labels such as "Project Scope", "User Personas", etc.).
            fields = CollapseLabelWithFollowingBullets(fields);

            // Second pass: empty labels followed by structured child blocks
            // (e.g. "User Personas" → [Trainer Persona, Client Persona])
            fields = CollapseStructuredChildBlocks(fields);

            // Third pass: simplify persona blocks into string lists
            FlattenPersonaBlocks(fields);

            return fields;
        }

        /// <summary>
        /// Walks a list of fields (recursively) and upgrades any anonymous entries
        /// whose string Value looks like a "Label: Value" pair into proper
        /// Name/Value fields. This keeps the raw tree shape generic and ensures
        /// that label-like bullets (e.g. "Out of Scope (Future): ...") are
        /// consistently represented as named children.
        /// </summary>
        private List<RichField> NormalizeAnonymousLabelLines(List<RichField> fields)
        {
            if (fields == null || fields.Count == 0)
                return fields ?? new List<RichField>();

            foreach (var f in fields)
            {
                if (string.IsNullOrWhiteSpace(f.Name) && f.Value is string s && !string.IsNullOrWhiteSpace(s))
                {
                    var text = s.Trim();
                    if (ShouldTreatAsLabel(text))
                    {
                        var kvMatch = Regex.Match(text, @"^([^:]+):\s*(.*)$");
                        if (kvMatch.Success)
                        {
                            f.Name = kvMatch.Groups[1].Value.Trim();
                            f.Value = kvMatch.Groups[2].Value.Trim();
                        }
                    }
                }
                else if (f.Value is List<RichField> childList)
                {
                    NormalizeAnonymousLabelLines(childList);
                }
            }

            return fields;
        }

        /// <summary>
        /// Recursively builds fields based purely on list level (bullet/numbered hierarchy)
        /// and simple "Label: Value" parsing, so the tree mirrors the exact document structure.
        /// </summary>
        private List<RichField> BuildFieldsAtLevel(List<ParagraphDto> paragraphs, ref int index, int level)
        {
            var result = new List<RichField>();

            while (index < paragraphs.Count)
            {
                var para = paragraphs[index];
                var text = (para.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    index++;
                    continue;
                }

                var paraLevel = para.ListLevel ?? 0;

                // If this paragraph belongs to a higher-level parent, stop and return to caller
                if (paraLevel < level)
                {
                    break;
                }

                // If this paragraph is deeper than current level, treat it as children
                // of the previous field
                if (paraLevel > level)
                {
                    if (result.Count == 0)
                    {
                        // No parent yet; treat as same level
                        paraLevel = level;
                    }
                    else
                    {
                        var parent = result[result.Count - 1];
                        if (parent.Value is not List<RichField> childList)
                        {
                            childList = new List<RichField>();
                            parent.Value = childList;
                        }

                        var children = BuildFieldsAtLevel(paragraphs, ref index, paraLevel);
                        childList.AddRange(children);
                        continue;
                    }
                }

                // Now paraLevel == level → create a field from this line
                var field = ParseLineToField(text);
                result.Add(field);
                index++;
            }

            return result;
        }

        /// <summary>
        /// Collapses sequences of:
        ///   [Label with empty Value] + [following fields]
        /// into a nested structure so that the label owns its subsequent items.
        /// Generic, does not depend on specific label text.
        /// - If the following items are anonymous bullets, produces List&lt;string&gt;.
        /// - If they are named (or mixed), produces List&lt;RichField&gt;.
        /// </summary>
        private List<RichField> CollapseLabelWithFollowingBullets(List<RichField> fields)
        {
            if (fields == null || fields.Count == 0)
                return fields;

            var result = new List<RichField>();

            int i = 0;
            while (i < fields.Count)
            {
                var current = fields[i];

                // Only consider labels with empty or whitespace value
                if (!string.IsNullOrWhiteSpace(current.Name) &&
                    (current.Value == null || (current.Value is string s && string.IsNullOrWhiteSpace(s))))
                {
                    var bulletItems = new List<string>();
                    var childFields = new List<RichField>();
                    int j = i + 1;

                    while (j < fields.Count)
                    {
                        var next = fields[j];

                        // Boundary: next label with empty/whitespace value starts a new group
                        if (!string.IsNullOrWhiteSpace(next.Name) &&
                            (next.Value == null || (next.Value is string ns && string.IsNullOrWhiteSpace(ns))))
                        {
                            break;
                        }

                        // Anonymous bullet/line → by default becomes a string item, but if
                        // the text itself looks like a "Label: Value" pair we promote it
                        // to a proper named child field so shapes like
                        // "Out of Scope (Future): x, y, z" are not lost.
                        if (string.IsNullOrWhiteSpace(next.Name))
                        {
                            if (next.Value is string vs && !string.IsNullOrWhiteSpace(vs))
                            {
                                var text = vs.Trim();

                                if (ShouldTreatAsLabel(text))
                                {
                                    var kvMatch = Regex.Match(text, @"^([^:]+):\s*(.*)$");
                                    if (kvMatch.Success)
                                    {
                                        var label = kvMatch.Groups[1].Value.Trim();
                                        var after = kvMatch.Groups[2].Value.Trim();

                                        childFields.Add(new RichField
                                        {
                                            Section = string.Empty,
                                            Name = label,
                                            Value = after
                                        });

                                        j++;
                                        continue;
                                    }
                                }

                                bulletItems.Add(text);
                                j++;
                                continue;
                            }

                            // Empty/invalid → stop grouping
                            break;
                        }

                        // Named child field (e.g. "In Scope (MVP)", "Trainer", "Client")
                        childFields.Add(next);
                        j++;
                    }

                    if (bulletItems.Count > 0 || childFields.Count > 0)
                    {
                        if (childFields.Count > 0)
                        {
                            // If we also have plain bullets, wrap them as anonymous child fields
                            foreach (var bi in bulletItems)
                            {
                                childFields.Add(new RichField
                                {
                                    Section = string.Empty,
                                    Name = string.Empty,
                                    Value = bi
                                });
                            }

                            current.Value = childFields;
                        }
                        else
                        {
                            // Only plain bullets → keep as simple list of strings
                            current.Value = bulletItems;
                        }

                        result.Add(current);
                        i = j;
                        continue;
                    }
                }

                // Default: keep field as-is
                result.Add(current);
                i++;
            }

            return result;
        }

        /// <summary>
        /// Second-stage collapse: for labels that still have empty Value but are followed
        /// by already-structured blocks (Value is a list), attach those blocks as children.
        /// Example:
        ///   "User Personas" (empty)
        ///   "Trainer Persona" (Value = list)
        ///   "Client Persona"  (Value = list)
        /// → "User Personas".Value = [Trainer Persona, Client Persona].
        /// </summary>
        private List<RichField> CollapseStructuredChildBlocks(List<RichField> fields)
        {
            if (fields == null || fields.Count == 0)
                return fields;

            var result = new List<RichField>();
            int i = 0;

            while (i < fields.Count)
            {
                var current = fields[i];

                bool isEmptyLabel =
                    !string.IsNullOrWhiteSpace(current.Name) &&
                    (current.Value == null || (current.Value is string s && string.IsNullOrWhiteSpace(s)));

                if (isEmptyLabel)
                {
                    var structuredChildren = new List<RichField>();
                    int j = i + 1;

                    while (j < fields.Count)
                    {
                        var next = fields[j];

                        // Stop if next is anonymous or plain scalar – only lists count as "structured blocks"
                        if (string.IsNullOrWhiteSpace(next.Name))
                            break;

                        if (next.Value is IList list && next.Value is not string)
                        {
                            // Generic rule: only treat this as a child block if it appears to
                            // belong to the same "label family" as the parent (e.g.
                            // "User Personas" → "Trainer Persona", "Client Persona").
                            // We derive a key token from the parent (usually the last word,
                            // singularised) and require the child label to share that token.
                            if (!IsSameLabelFamily(current.Name, next.Name))
                            {
                                break;
                            }

                            structuredChildren.Add(next);
                            j++;
                            continue;
                        }

                        break;
                    }

                    if (structuredChildren.Count > 0)
                    {
                        current.Value = structuredChildren;
                        result.Add(current);
                        i = j;
                        continue;
                    }
                }

                result.Add(current);
                i++;
            }

            return result;
        }

        /// <summary>
        /// Returns true when parent and child labels look like they belong to the same
        /// "family", e.g. "User Personas" (parent) and "Trainer Persona" (child).
        /// This is purely lexical: we take the parent's last significant word,
        /// normalise plurals, and require the child to contain the same token.
        /// </summary>
        private static bool IsSameLabelFamily(string parentName, string childName)
        {
            var parentKey = ExtractKeyToken(parentName);
            if (string.IsNullOrEmpty(parentKey))
                return false;

            var childTokens = TokenizeLabel(childName)
                .Select(NormalizeToken)
                .Where(t => !string.IsNullOrEmpty(t));

            return childTokens.Any(t => t.Equals(parentKey, StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<string> TokenizeLabel(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Array.Empty<string>();

            // Split on whitespace, colon and basic punctuation.
            return Regex.Split(name, @"[\s:,-]+")
                        .Where(t => !string.IsNullOrWhiteSpace(t));
        }

        private static string? ExtractKeyToken(string name)
        {
            var tokens = TokenizeLabel(name).ToList();
            if (tokens.Count == 0)
                return null;

            // Use the last token as the key (e.g. "Personas", "Goals").
            var last = NormalizeToken(tokens[^1]);
            return string.IsNullOrEmpty(last) ? null : last;
        }

        private static string NormalizeToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return string.Empty;

            var t = token.Trim().Trim('.', ',', ';').ToLowerInvariant();

            // Simple plural normalisation: "personas" → "persona", "metrics" → "metric".
            if (t.EndsWith("s", StringComparison.Ordinal) && t.Length > 4)
            {
                t = t[..^1];
            }

            return t;
        }

        /// <summary>
        /// Flattens persona blocks so that their internals are simple string lists.
        /// Example:
        ///   Trainer Persona → ["Age: 25-45", "Tech-savvy, manages 10-50 clients", ...]
        /// This keeps JSON smaller and easier to consume while preserving all text.
        /// </summary>
        private void FlattenPersonaBlocks(List<RichField> fields)
        {
            if (fields == null || fields.Count == 0)
                return;

            foreach (var field in fields)
            {
                if (field.Value is List<RichField> childFields)
                {
                    // Recurse first so nested persona blocks are processed
                    FlattenPersonaBlocks(childFields);

                    // Heuristic: flatten only *individual* persona blocks (Trainer Persona, Client Persona, etc.),
                    // not their parent group "User Personas" (note the plural).
                    var nameLower = field.Name.ToLowerInvariant();
                    bool isSinglePersona =
                        nameLower.Contains("persona") &&
                        !nameLower.Contains("personas"); // avoid flattening the parent "User Personas"

                    if (isSinglePersona)
                    {
                        var lines = new List<string>();

                        foreach (var cf in childFields)
                        {
                            switch (cf.Value)
                            {
                                case string vs when !string.IsNullOrWhiteSpace(vs):
                                    if (!string.IsNullOrWhiteSpace(cf.Name))
                                    {
                                        lines.Add($"{cf.Name}: {vs}");
                                    }
                                    else
                                    {
                                        lines.Add(vs);
                                    }
                                    break;

                                case List<string> list:
                                    lines.AddRange(list.Where(x => !string.IsNullOrWhiteSpace(x)));
                                    break;
                            }
                        }

                        field.Value = lines;
                    }
                }
            }
        }

        private RichField ParseLineToField(string text)
        {
            // Generic "Label: Value" detection with guards so we don't
            // accidentally split relational/cardinality shapes like
            // "User → TrainerProfile (1:1)" or "1:1 Chat Channels".
            if (ShouldTreatAsLabel(text))
            {
                var kvMatch = Regex.Match(text, @"^([^:]+):\s*(.*)$");
                if (kvMatch.Success)
                {
                    var label = kvMatch.Groups[1].Value.Trim();
                    var after = kvMatch.Groups[2].Value.Trim();
                    return new RichField
                    {
                        Name = label,
                        Value = after // may be empty string for pure label
                    };
                }
            }

            // Otherwise it's a plain bullet/line
            return new RichField
            {
                Name = string.Empty,
                Value = text
            };
        }

        // PostProcessSection is no longer needed now that we build hierarchy from list levels.

        /// <summary>
        /// Collects following paragraphs until blank or another label-like line.
        /// Returns (plain items, nested label-value fields, new index).
        /// </summary>
        private (List<string> PlainItems, List<RichField> NestedFields, int NextIndex)
            CollectFollowingLines(List<ParagraphDto> paragraphs, int startIndex)
        {
            var plainItems = new List<string>();
            var nestedFields = new List<RichField>();

            int j = startIndex;
            for (; j < paragraphs.Count; j++)
            {
                var next = paragraphs[j];
                var nextText = (next.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(nextText))
                {
                    break;
                }

                // Check for label/value pattern first (with same guards as ParseLineToField)
                if (ShouldTreatAsLabel(nextText))
                {
                    var kvMatch = Regex.Match(nextText, @"^([^:]+):\s*(.*)$");
                    if (kvMatch.Success)
                    {
                        var nestedLabel = kvMatch.Groups[1].Value.Trim();
                        var nestedValue = kvMatch.Groups[2].Value.Trim();

                        // Pure label line like "Client Persona:" or "User Goals:" → start of a new block
                        if (string.IsNullOrWhiteSpace(nestedValue))
                        {
                            break;
                        }

                        // Proper "Label: Value" nested field (e.g. "Age: 25-45")
                        nestedFields.Add(new RichField
                        {
                            Name = nestedLabel,
                            Value = nestedValue
                        });
                        continue;
                    }
                }

                // Plain bullet/description line, keep as item
                plainItems.Add(nextText);
            }

            return (plainItems, nestedFields, j);
        }

        /// <summary>
        /// Determines whether a line should be interpreted as a "Label: Value"
        /// pair, avoiding false positives for shapes like "1:1" or "1:many"
        /// (cardinality) and cases where the only colon is inside parentheses.
        /// </summary>
        private static bool ShouldTreatAsLabel(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var idx = text.IndexOf(':');
            // We require at least one character before the colon so we don't treat
            // leading punctuation as labels, but we now allow the colon to appear
            // at the very end of the line so pure labels like "Project Scope:"
            // and "User Personas:" are recognised as labels with empty values.
            if (idx <= 0)
                return false;

            // Guard 1: digit:digit (e.g. "1:1", "10:30") → treat as part of value
            if (idx < text.Length - 1 &&
                char.IsDigit(text[idx - 1]) &&
                char.IsDigit(text[idx + 1]))
            {
                return false;
            }

            // Guard 2: colon inside any "( ... )" group (e.g. "User → X (1:many)").
            // We walk all parenthesis pairs and, if the colon falls strictly between
            // an opening and its closing paren, we consider this a non-label line.
            int searchStart = 0;
            while (true)
            {
                var openParen = text.IndexOf('(', searchStart);
                if (openParen < 0 || openParen >= idx)
                {
                    break;
                }

                var closeParen = text.IndexOf(')', openParen + 1);
                if (closeParen < 0)
                {
                    break;
                }

                if (idx > openParen && idx < closeParen)
                {
                    // The colon is inside this "( ... )" span – don't treat as label.
                    return false;
                }

                // Continue searching after this pair.
                searchStart = closeParen + 1;
            }

            return true;
        }
    }
}



