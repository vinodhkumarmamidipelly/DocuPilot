using System.Collections.Generic;
using System.Threading.Tasks;
using SMEPilot.FunctionApp.Models;

namespace SMEPilot.FunctionApp.Interfaces
{
    /// <summary>
    /// Interfaces from Feedback2 - added alongside existing code for future use
    /// </summary>

    /// <summary>
    /// Interface for content extraction
    /// </summary>
    public interface IContentExtractor
    {
        IReadOnlyList<ContentBlock> Extract(string docxPath);
    }

    /// <summary>
    /// Interface for content normalization
    /// </summary>
    public interface IContentNormalizer
    {
        NormalizedDocument Normalize(IReadOnlyList<ContentBlock> blocks);
    }

    /// <summary>
    /// Interface for document classification
    /// </summary>
    public interface IDocumentClassifier
    {
        string Classify(NormalizedDocument doc, string? fileName = null);
    }

    /// <summary>
    /// Interface for section mapping
    /// </summary>
    public interface ISectionMapper
    {
        Dictionary<string, MappedSection> Map(NormalizedDocument doc, string mappingJsonPath);
    }

    /// <summary>
    /// Interface for template rendering
    /// </summary>
    public interface ITemplateRenderer
    {
        /// <summary>
        /// Renders a new document by copying templatePath -> outputPath and filling with sections.
        /// </summary>
        void Render(string templatePath, string outputPath, Dictionary<string, MappedSection> sections, string? title = null);
    }

    /// <summary>
    /// Interface for enrichment service
    /// </summary>
    public interface IEnricherService
    {
        /// <summary>
        /// Process the downloaded file and produce an enriched DOCX file path (local).
        /// Returns EnrichmentResult with output path and status.
        /// </summary>
        Task<EnrichmentResult> EnrichAsync(DocumentContext ctx);
    }
}

