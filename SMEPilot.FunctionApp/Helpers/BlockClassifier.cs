using System;
using System.Collections.Generic;
using System.Linq;
using SMEPilot.FunctionApp.Helpers;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Step 7: BlockClassifier
    /// Purpose: Label sections for smarter context detection
    /// Prevents: Mapping text from unrelated sections, wrong contextScore
    /// </summary>
    public class BlockClassifier
    {
        private readonly AliasManager _aliasManager;

        public BlockClassifier(AliasManager aliasManager)
        {
            _aliasManager = aliasManager ?? throw new ArgumentNullException(nameof(aliasManager));
        }

        /// <summary>
        /// Classifies all sections in the BOM tree
        /// </summary>
        public Dictionary<Helpers.StructureBuilder.SectionNode, List<string>> ClassifySections(Helpers.StructureBuilder.SectionNode bomTree)
        {
            var classifications = new Dictionary<StructureBuilder.SectionNode, List<string>>();

            foreach (var section in EnumerateSections(bomTree))
            {
                var labels = ClassifySection(section);
                if (labels.Count > 0)
                {
                    classifications[section] = labels;
                }
            }

            return classifications;
        }

        /// <summary>
        /// Classifies a single section based on heading and content
        /// </summary>
        public List<string> ClassifySection(Helpers.StructureBuilder.SectionNode section)
        {
            var labels = new List<string>();
            var heading = section.Heading?.ToLowerInvariant() ?? string.Empty;

            // Keyword-based classification
            if (heading.Contains("introduction") || heading.Contains("overview"))
                labels.Add("Introduction");
            if (heading.Contains("requirement") || heading.Contains("specification"))
                labels.Add("Requirements");
            if (heading.Contains("architecture") || heading.Contains("design"))
                labels.Add("Architecture");
            if (heading.Contains("data") && heading.Contains("entity"))
                labels.Add("DataEntities");
            if (heading.Contains("authentication") || heading.Contains("authorization"))
                labels.Add("Authentication");
            if (heading.Contains("api") || heading.Contains("endpoint"))
                labels.Add("API");
            if (heading.Contains("security") || heading.Contains("protection"))
                labels.Add("Security");
            if (heading.Contains("performance") || heading.Contains("scalability"))
                labels.Add("Performance");
            if (heading.Contains("test") || heading.Contains("testing"))
                labels.Add("Testing");
            if (heading.Contains("deployment") || heading.Contains("infrastructure"))
                labels.Add("Deployment");
            if (heading.Contains("user story") || heading.Contains("epic"))
                labels.Add("UserStories");
            if (heading.Contains("feature") || heading.Contains("functionality"))
                labels.Add("Features");
            if (heading.Contains("workflow") || heading.Contains("process"))
                labels.Add("Workflows");
            if (heading.Contains("business rule") || heading.Contains("rule"))
                labels.Add("BusinessRules");
            if (heading.Contains("success metric") || heading.Contains("kpi"))
                labels.Add("SuccessMetrics");
            if (heading.Contains("objective") || heading.Contains("goal"))
                labels.Add("Objectives");
            if (heading.Contains("scope"))
                labels.Add("Scope");
            if (heading.Contains("reference") || heading.Contains("appendix"))
                labels.Add("References");

            // Alias-based classification (check if section heading matches any field alias)
            foreach (var fieldName in _aliasManager.GetAllFieldNames())
            {
                if (_aliasManager.IsAlias(fieldName, heading))
                {
                    labels.Add(fieldName);
                }
            }

            // Virtual-heading boost: if section has very few paragraphs and short heading, it's likely a header section
            if (section.Paragraphs.Count <= 2 && section.Heading.Length < 50)
            {
                labels.Add("HeaderSection");
            }

            return labels.Distinct().ToList();
        }

        private IEnumerable<Helpers.StructureBuilder.SectionNode> EnumerateSections(Helpers.StructureBuilder.SectionNode node)
        {
            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var descendant in EnumerateSections(child))
                {
                    yield return descendant;
                }
            }
        }
    }
}

