using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SMEPilot.FunctionApp.Services;

namespace SMEPilot.FunctionApp.Services
{
    /// <summary>
    /// Example usage of DocumentEnricherService in ProcessSharePointFile.cs
    /// This shows how to integrate the enrichment service with SharePoint file processing
    /// </summary>
    public class DocumentEnricherServiceUsageExample
    {
        /// <summary>
        /// Example: How to use DocumentEnricherService in ProcessSharePointFile.cs
        /// 
        /// Add this to your ProcessSharePointFile.cs Run method after downloading the file
        /// </summary>
        public static async Task<EnrichmentResult> ExampleUsage(
            string downloadedFilePath,
            string author,
            ILogger logger)
        {
            try
            {
                // 1. Define paths
                var templatePath = Path.Combine(
                    Environment.GetEnvironmentVariable("TEMPLATE_PATH") ?? "Templates",
                    "SMEPilot_OrgTemplate_RuleBased.dotx");
                
                var mappingJsonPath = Path.Combine(
                    Environment.GetEnvironmentVariable("CONFIG_PATH") ?? "Config",
                    "mapping.json");

                // 2. Ensure template exists (generate if needed)
                if (!File.Exists(templatePath))
                {
                    logger.LogWarning("Template not found at {Path}, generating...", templatePath);
                    var templateDir = Path.GetDirectoryName(templatePath);
                    if (!string.IsNullOrEmpty(templateDir) && !Directory.Exists(templateDir))
                    {
                        Directory.CreateDirectory(templateDir);
                    }
                    Helpers.TemplateGenerator.GenerateTemplate(templatePath);
                    logger.LogInformation("Template generated at {Path}", templatePath);
                }

                // 3. Create enrichment service
                var enricher = new DocumentEnricherService(mappingJsonPath, templatePath);

                // 4. Define output path (add _formatted suffix)
                var outputPath = Path.ChangeExtension(downloadedFilePath, "_formatted.docx");
                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // 5. Extract images if needed (optional - can pass null)
                // var images = await ExtractImagesFromDocument(downloadedFilePath);
                List<byte[]>? images = null;

                // 6. Enrich document
                var result = enricher.EnrichFile(
                    inputPath: downloadedFilePath,
                    outputPath: outputPath,
                    author: author);

                if (result.Success)
                {
                    logger.LogInformation(
                        "✅ Document enriched successfully: {OutputPath}, Type: {DocType}, Status: {Status}",
                        result.EnrichedPath,
                        result.DocumentType,
                        result.Status);
                }
                else
                {
                    logger.LogError(
                        "❌ Document enrichment failed: {ErrorMessage}",
                        result.ErrorMessage);
                }

                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception during document enrichment");
                return new EnrichmentResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Status = "Failed"
                };
            }
        }

        /// <summary>
        /// Example: Integration in ProcessSharePointFile.cs Run method
        /// 
        /// Replace the existing enrichment logic with this:
        /// </summary>
        /*
        // In ProcessSharePointFile.cs, after downloading the file:

        // Download file from SharePoint
        var localFilePath = await _graph.DownloadFileAsync(driveId, itemId);
        
        // Use DocumentEnricherService for rule-based enrichment
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "SMEPilot_OrgTemplate_RuleBased.dotx");
        var mappingJsonPath = Path.Combine(AppContext.BaseDirectory, "Config", "mapping.json");
        var outputPath = Path.ChangeExtension(localFilePath, "_formatted.docx");
        
        var enricher = new DocumentEnricherService(templatePath, mappingJsonPath, _logger);
        var enrichmentResult = enricher.Enrich(
            inputPath: localFilePath,
            outputPath: outputPath,
            author: uploaderEmail ?? "System",
            images: null); // Images will be extracted from document
        
        if (enrichmentResult.Success)
        {
            // Upload enriched document back to SharePoint
            await _graph.UploadFileAsync(
                driveId: driveId,
                fileName: Path.GetFileName(outputPath),
                fileStream: File.OpenRead(outputPath),
                parentFolderPath: null);
            
            // Update metadata
            await _graph.UpdateListItemFieldsAsync(
                driveId: driveId,
                itemId: itemId,
                fields: new Dictionary<string, object>
                {
                    { "SMEPilot_Status", "Enriched" },
                    { "SMEPilot_Enriched", true },
                    { "SMEPilot_DocumentType", enrichmentResult.DocumentType }
                });
            
            _logger.LogInformation("✅ Document enriched and uploaded: {Path}", enrichmentResult.EnrichedPath);
        }
        else
        {
            _logger.LogError("❌ Enrichment failed: {Error}", enrichmentResult.ErrorMessage);
        }
        */
    }
}

