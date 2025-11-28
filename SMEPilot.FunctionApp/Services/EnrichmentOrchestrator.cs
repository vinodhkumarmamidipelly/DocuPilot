// EnrichmentOrchestrator.cs
// Purpose: Orchestrates the enrichment process using Feedback2 components
// Renamed from Feedback2EnricherService for better naming

using SMEPilot.FunctionApp.Interfaces;
using SMEPilot.FunctionApp.Models;
using SMEPilot.FunctionApp.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SMEPilot.FunctionApp.Services
{
    /// <summary>
    /// Orchestrator service for document enrichment using Feedback2 components
    /// </summary>
    public class EnrichmentOrchestrator : IEnricherService
    {
        private readonly IContentExtractor _extractor;
        private readonly IContentNormalizer _normalizer;
        private readonly IDocumentClassifier _classifier;
        private readonly ITemplateRenderer _renderer;
        private readonly ILogger<EnrichmentOrchestrator>? _logger;

        public EnrichmentOrchestrator(ILogger<EnrichmentOrchestrator>? logger = null)
        {
            // Create default components
            _extractor = new OpenXmlExtractor();
            _normalizer = new DefaultNormalizer();
            _classifier = new RuleBasedClassifier();
            _renderer = new OpenXmlTemplateRenderer();
            _logger = logger;
        }

        public async Task<EnrichmentResult> EnrichAsync(DocumentContext ctx)
        {
            // Validate input file
            if (!File.Exists(ctx.LocalSourcePath))
                return new EnrichmentResult { Success = false, ErrorMessage = "Source file not found", Status = "Error", ProcessedAtUtc = DateTime.UtcNow };

            // Check file size
            var fi = new FileInfo(ctx.LocalSourcePath);
            if (fi.Length > ctx.MaxFileSizeBytes)
                return new EnrichmentResult { Success = false, ErrorMessage = $"File exceeds max size ({ctx.MaxFileSizeBytes} bytes)", Status = "Error", ProcessedAtUtc = DateTime.UtcNow };

            // Compute hash for idempotency check
            var hash = ContentHashHelper.ComputeSha256Hex(ctx.LocalSourcePath);

            // Use TempFileLease for safe temporary work area
            using var lease = new TempFileLease("smepilot");
            var workingSource = lease.GetPath(fi.Name);
            File.Copy(ctx.LocalSourcePath, workingSource, true);

            try
            {
                // Step 1: Extract
                var blocks = _extractor.Extract(workingSource);
                _logger?.LogInformation("Extracted {BlockCount} content blocks.", blocks.Count);

                // Step 2: Normalize
                var normalized = _normalizer.Normalize(blocks);
                _logger?.LogInformation("Normalized document with {NormalizedBlockCount} blocks.", normalized.Blocks.Count);

                // Step 3: Classify
                var docType = _classifier.Classify(normalized, ctx.FileName);
                normalized.DetectedType = docType;
                _logger?.LogInformation("Classified document as: {DocumentType}", docType);

                // Step 4: Map
                var mapper = new SectionMapper(ctx.MappingJsonPath);
                var sections = mapper.Map(normalized, ctx.MappingJsonPath);
                _logger?.LogInformation("Mapped {SectionCount} sections.", sections.Count);

                // Ensure output path unique
                var outFileName = Path.GetFileNameWithoutExtension(ctx.FileName) + $"-enriched-{DateTime.UtcNow:yyyyMMddHHmmss}.docx";
                var outputPath = lease.GetPath(outFileName);

                // Step 5: Render template
                _renderer.Render(ctx.TemplatePath, outputPath, sections, normalized.Title);
                _logger?.LogInformation("Rendered enriched document to: {OutputPath}", outputPath);

                return new EnrichmentResult 
                { 
                    Success = true, 
                    EnrichedPath = outputPath,
                    OutputPath = outputPath, // Feedback2 compatibility
                    DocumentType = docType,
                    Status = "Succeeded",
                    OriginalFileName = ctx.FileName,
                    EnrichedFileName = Path.GetFileName(outputPath),
                    ProcessedAtUtc = DateTime.UtcNow,
                    Diagnostics = new Dictionary<string, object> 
                    { 
                        { "ContentHash", hash },
                        { "BlockCount", blocks.Count },
                        { "SectionCount", sections.Count }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Enrichment failed for {FileName}: {ErrorMessage}", ctx.FileName, ex.Message);
                return new EnrichmentResult 
                { 
                    Success = false, 
                    ErrorMessage = ex.ToString(),
                    Error = ex.ToString(), // Feedback2 compatibility
                    Status = "Error",
                    ProcessedAtUtc = DateTime.UtcNow,
                    Diagnostics = new Dictionary<string, object> 
                    { 
                        { "Exception", ex.GetType().Name },
                        { "Message", ex.Message }
                    }
                };
            }
        }
    }
}

