using System;
using System.IO;
using Microsoft.Extensions.Logging;
using SMEPilot.FunctionApp.Helpers;

namespace SMEPilot.FunctionApp.Services
{
    /// <summary>
    /// Setup utility for DocumentEnricherService
    /// Ensures template and config files are in place
    /// </summary>
    public static class SetupEnrichmentService
    {
        /// <summary>
        /// Initializes the enrichment service by ensuring template and config files exist
        /// Call this during application startup or first use
        /// </summary>
        /// <param name="baseDirectory">Base directory for templates and config (default: AppContext.BaseDirectory)</param>
        /// <param name="logger">Optional logger</param>
        /// <returns>Paths to template and config files</returns>
        public static (string templatePath, string mappingJsonPath) Initialize(
            string? baseDirectory = null,
            ILogger? logger = null)
        {
            baseDirectory ??= AppContext.BaseDirectory;

            var templatesDir = Path.Combine(baseDirectory, "Templates");
            var configDir = Path.Combine(baseDirectory, "Config");

            // Ensure directories exist
            if (!Directory.Exists(templatesDir))
            {
                Directory.CreateDirectory(templatesDir);
                logger?.LogInformation("Created Templates directory: {Path}", templatesDir);
            }

            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
                logger?.LogInformation("Created Config directory: {Path}", configDir);
            }

            var templatePath = Path.Combine(templatesDir, "SMEPilot_OrgTemplate_RuleBased.dotx");
            var mappingJsonPath = Path.Combine(configDir, "mapping.json");

            // Generate template if it doesn't exist
            if (!File.Exists(templatePath))
            {
                try
                {
                    TemplateGenerator.GenerateTemplate(templatePath);
                    logger?.LogInformation("Generated template: {Path}", templatePath);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Failed to generate template: {Path}", templatePath);
                    throw;
                }
            }
            else
            {
                logger?.LogDebug("Template already exists: {Path}", templatePath);
            }

            // Verify mapping.json exists
            if (!File.Exists(mappingJsonPath))
            {
                logger?.LogWarning("Mapping configuration not found: {Path}. Using default.", mappingJsonPath);
                // mapping.json should be included in project, but if missing, service will throw
            }
            else
            {
                logger?.LogDebug("Mapping configuration found: {Path}", mappingJsonPath);
            }

            return (templatePath, mappingJsonPath);
        }

        /// <summary>
        /// Validates that all required files exist and are accessible
        /// </summary>
        public static bool ValidateSetup(string templatePath, string mappingJsonPath, ILogger? logger = null)
        {
            var isValid = true;

            if (!File.Exists(templatePath))
            {
                logger?.LogError("Template file not found: {Path}", templatePath);
                isValid = false;
            }

            if (!File.Exists(mappingJsonPath))
            {
                logger?.LogError("Mapping configuration not found: {Path}", mappingJsonPath);
                isValid = false;
            }

            if (isValid)
            {
                logger?.LogInformation("✅ Enrichment service setup validated successfully");
            }

            return isValid;
        }
    }
}

