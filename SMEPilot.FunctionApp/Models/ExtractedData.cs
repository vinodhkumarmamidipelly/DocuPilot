namespace SMEPilot.FunctionApp.Models
{
    /// <summary>
    /// Structured data extracted from raw document
    /// </summary>
    public class ExtractedData
    {
        public MetadataData Metadata { get; set; } = new();
        public SectionData Sections { get; set; } = new();
        public ListData Lists { get; set; } = new();
    }

    /// <summary>
    /// Metadata extracted from document header
    /// </summary>
    public class MetadataData
    {
        public string? ProjectName { get; set; }
        public string? Version { get; set; }
        public string? Date { get; set; }
        public string? Status { get; set; }
        public string? Author { get; set; }
        public string? Reviewer { get; set; }
        public string? Approver { get; set; }
    }

    /// <summary>
    /// Content sections extracted from document
    /// </summary>
    public class SectionData
    {
        public string? Overview { get; set; }
        public string? BusinessContext { get; set; }
        public string? ProjectDescription { get; set; }
        public string? ProjectObjectives { get; set; }
        public string? BusinessGoals { get; set; }
        public string? Scope { get; set; }
        public string? OutOfScope { get; set; }
    }

    /// <summary>
    /// Lists extracted from document sections
    /// </summary>
    public class ListData
    {
        public List<string> Features { get; set; } = new();
        public List<string> Personas { get; set; } = new();
        public List<string> Workflows { get; set; } = new();
        public List<string> BusinessRules { get; set; } = new();
        public List<string> Entities { get; set; } = new();
        public List<string> Integrations { get; set; } = new();
        public List<string> Epics { get; set; } = new();
        public List<string> UserStories { get; set; } = new();
        public List<string> Endpoints { get; set; } = new();
        public List<string> Flows { get; set; } = new();
        public List<string> ScopeItems { get; set; } = new();
        public List<string> OutOfScopeItems { get; set; } = new();
        public List<string> References { get; set; } = new();
    }
}

