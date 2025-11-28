using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SMEPilot.FunctionApp.Models
{
    /// <summary>
    /// Result of document enrichment operation
    /// Enhanced with confidence scores, diagnostics, and section mapping results (per Feedback1)
    /// </summary>
    public class EnrichmentResult
    {
        public bool Success { get; set; } = false;
        public string DocumentType { get; set; } = "";
        public string EnrichedPath { get; set; } = "";
        public string OutputPath { get; set; } = ""; // Feedback2 compatibility (maps to EnrichedPath)
        public string Status { get; set; } = ""; // Succeeded, ManualReview, Error
        public string ErrorMessage { get; set; } = "";
        public string Error { get; set; } = ""; // Feedback2 compatibility (maps to ErrorMessage)
        
        // Enhanced fields (Feedback1 recommendations)
        public string JobId { get; set; } = Guid.NewGuid().ToString();
        public string OriginalFileName { get; set; } = "";
        public string EnrichedFileName { get; set; } = "";
        public double Confidence { get; set; } = 0.0; // Overall confidence score (0.0-1.0)
        public Dictionary<string, object> ExtractedFields { get; set; } = new Dictionary<string, object>();
        public List<SectionMappingResult> Sections { get; set; } = new List<SectionMappingResult>();
        public Dictionary<string, object> Diagnostics { get; set; } = new Dictionary<string, object>();
        public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Result of mapping a section to a template tag with confidence score and reasons
    /// </summary>
    public class SectionMappingResult
    {
        public string Heading { get; set; } = "";
        public string TemplateTag { get; set; } = "";
        public double Score { get; set; } = 0.0; // Confidence score (0.0-1.0)
        public List<string> Reasons { get; set; } = new List<string>(); // Mapping decision reasons
    }

    /// <summary>
    /// Configuration for section mapping based on keywords
    /// </summary>
    public class MappingConfig
    {
        public List<SectionRule> Sections { get; set; } = new List<SectionRule>();
        public List<string> FunctionalKeywords { get; set; } = new List<string>();
        public List<string> TechnicalKeywords { get; set; } = new List<string>();
        public List<string> SupportKeywords { get; set; } = new List<string>();
        
        // Feedback1: Configurable auto-accept threshold for section mapping (0.0-1.0)
        // Scores below this threshold will be marked for ManualReview
        public double AutoAcceptThreshold { get; set; } = 0.6; // Default: 0.6 (60% confidence)
    }

    /// <summary>
    /// Rule for mapping content to a section
    /// </summary>
    public class SectionRule
    {
        public string Name { get; set; } = "";
        public List<string> Keywords { get; set; } = new List<string>();
        public bool Mandatory { get; set; } = false;
    }

    /// <summary>
    /// Rule mapping for header-based section detection
    /// </summary>
    public class RuleMapping
    {
        public Dictionary<string, string> HeaderMappings { get; set; } = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        
        public static RuleMapping Default()
        {
            return new RuleMapping
            {
                HeaderMappings = new Dictionary<string, string>
                {
                    {"symptom","Symptoms"},
                    {"error","Symptoms"},
                    {"issue","Symptoms"},
                    {"steps","Steps to Reproduce"},
                    {"reproduce","Steps to Reproduce"},
                    {"troubleshoot","Resolution"},
                    {"workaround","Resolution"},
                    {"architecture","Architecture"},
                    {"design","Architecture"},
                    {"overview","Overview"},
                    {"summary","Overview"},
                    {"user","User Scenarios"},
                    {"function","User Scenarios"},
                    {"screenshot","Screenshots"},
                    {"image","Screenshots"}
                }
            };
        }
    }
}

