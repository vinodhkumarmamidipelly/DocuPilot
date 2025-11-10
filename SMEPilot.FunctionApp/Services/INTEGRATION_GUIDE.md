# DocumentEnricherService - Complete Integration Guide

## Overview

This guide shows how to integrate `DocumentEnricherService` into your `ProcessSharePointFile.cs` function.

## Step 1: Initialize Setup (Optional but Recommended)

Add initialization in `Program.cs` to ensure template and config files are ready:

```csharp
// In Program.cs, after service registration
var logger = host.Services.GetRequiredService<ILogger<Program>>();
try
{
    var (templatePath, mappingPath) = SetupEnrichmentService.Initialize(
        baseDirectory: AppContext.BaseDirectory,
        logger: logger);
    Log.Information("✅ Enrichment service initialized: Template={Template}, Config={Config}", 
        templatePath, mappingPath);
}
catch (Exception ex)
{
    Log.Warning(ex, "⚠️ Enrichment service setup failed (will be created on first use)");
}
```

## Step 2: Integrate in ProcessSharePointFile.cs

### Option A: Replace Existing Enrichment (Recommended)

Replace the existing enrichment logic with `DocumentEnricherService`:

```csharp
// In ProcessSharePointFile.cs, after downloading the file

// 1. Setup paths
var baseDir = AppContext.BaseDirectory;
var templatePath = Path.Combine(baseDir, "Templates", "SMEPilot_OrgTemplate_RuleBased.dotx");
var mappingJsonPath = Path.Combine(baseDir, "Config", "mapping.json");
var outputPath = Path.ChangeExtension(localFilePath, "_formatted.docx");

// 2. Ensure template exists (will generate if missing)
if (!File.Exists(templatePath))
{
    var templateDir = Path.GetDirectoryName(templatePath);
    if (!string.IsNullOrEmpty(templateDir) && !Directory.Exists(templateDir))
        Directory.CreateDirectory(templateDir);
    TemplateGenerator.GenerateTemplate(templatePath);
    _logger.LogInformation("Generated template: {Path}", templatePath);
}

// 3. Create enrichment service
var enricher = new DocumentEnricherService(templatePath, mappingJsonPath, _logger);

// 4. Enrich document
var enrichmentResult = enricher.Enrich(
    inputPath: localFilePath,
    outputPath: outputPath,
    author: uploaderEmail ?? "System",
    images: null); // Images will be extracted from document automatically

// 5. Handle result
if (enrichmentResult.Success)
{
    // Upload enriched document back to SharePoint
    using var enrichedStream = File.OpenRead(outputPath);
    await _graph.UploadFileAsync(
        driveId: driveId,
        fileName: Path.GetFileName(outputPath),
        fileStream: enrichedStream,
        parentFolderPath: null);
    
    // Update SharePoint metadata
    await _graph.UpdateListItemFieldsAsync(
        driveId: driveId,
        itemId: itemId,
        fields: new Dictionary<string, object>
        {
            { "SMEPilot_Status", "Enriched" },
            { "SMEPilot_Enriched", true },
            { "SMEPilot_DocumentType", enrichmentResult.DocumentType }
        });
    
    _logger.LogInformation(
        "✅ Document enriched successfully: {Path}, Type: {DocType}",
        enrichmentResult.EnrichedPath,
        enrichmentResult.DocumentType);
    
    // Cleanup temporary files
    try
    {
        File.Delete(localFilePath);
        File.Delete(outputPath);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to cleanup temporary files");
    }
}
else
{
    _logger.LogError(
        "❌ Document enrichment failed: {Error}",
        enrichmentResult.ErrorMessage);
    
    // Update status to indicate failure
    await _graph.UpdateListItemFieldsAsync(
        driveId: driveId,
        itemId: itemId,
        fields: new Dictionary<string, object>
        {
            { "SMEPilot_Status", $"Enrichment Failed: {enrichmentResult.ErrorMessage}" }
        });
}
```

### Option B: Use as Alternative Enrichment Method

Keep existing enrichment and add this as an alternative:

```csharp
// Check if rule-based enrichment should be used
var useRuleBasedEnrichment = _cfg.GetEnvironmentVariable("UseRuleBasedEnrichment") == "true";

if (useRuleBasedEnrichment)
{
    // Use DocumentEnricherService
    var enricher = new DocumentEnricherService(templatePath, mappingJsonPath, _logger);
    var result = enricher.Enrich(localFilePath, outputPath, uploaderEmail ?? "System");
    // ... handle result
}
else
{
    // Use existing HybridEnricher + TemplateBuilder
    // ... existing code
}
```

## Step 3: Configuration

### Environment Variables (Optional)

Add to `local.settings.json` or Azure Function App Settings:

```json
{
  "Values": {
    "TemplatePath": "Templates/SMEPilot_OrgTemplate_RuleBased.dotx",
    "MappingJsonPath": "Config/mapping.json",
    "UseRuleBasedEnrichment": "true"
  }
}
```

### Customize mapping.json

Edit `Config/mapping.json` to match your organization's needs:

```json
{
  "FunctionalKeywords": ["your", "keywords", "here"],
  "TechnicalKeywords": ["your", "keywords", "here"],
  "SupportKeywords": ["your", "keywords", "here"],
  "Sections": [
    {
      "Name": "1. Introduction",
      "Keywords": ["introduction", "purpose"],
      "Mandatory": true
    },
    {
      "Name": "Custom Section Name",
      "Keywords": ["keyword1", "keyword2"],
      "Mandatory": false
    }
  ]
}
```

## Step 4: Testing

### Local Testing

1. **Generate Template** (first time):
   ```csharp
   var templatePath = Path.Combine("Templates", "SMEPilot_OrgTemplate_RuleBased.dotx");
   TemplateGenerator.GenerateTemplate(templatePath);
   ```

2. **Test Enrichment**:
   ```csharp
   var enricher = new DocumentEnricherService(templatePath, mappingJsonPath, logger);
   var result = enricher.Enrich("test_input.docx", "test_output.docx", "Test User");
   Console.WriteLine($"Success: {result.Success}, Type: {result.DocumentType}");
   ```

3. **Verify Output**:
   - Open `test_output.docx` in Word
   - Check TOC (right-click → Update Field)
   - Verify sections are populated
   - Check images are embedded

### Deployment Checklist

- [ ] `Config/mapping.json` is included in build
- [ ] Template is generated or included in deployment
- [ ] Paths are correct for Azure Function environment
- [ ] Logging is configured
- [ ] Error handling is in place

## Step 5: Monitoring

### Log Messages

The service logs:
- ✅ Success: "Document enrichment completed successfully"
- ❌ Errors: "Document enrichment failed: {ErrorMessage}"
- ⚠️ Warnings: Template generation, missing config, etc.

### SharePoint Metadata

After enrichment, check SharePoint columns:
- `SMEPilot_Status`: "Enriched" or error message
- `SMEPilot_Enriched`: `true` if successful
- `SMEPilot_DocumentType`: "Functional", "Technical", "Support", or "Generic"

## Troubleshooting

### Template Not Found
**Error**: `Template file not found`
**Solution**: Ensure template is generated or included in deployment

### Mapping Config Error
**Error**: `Failed to deserialize mapping configuration`
**Solution**: Validate JSON syntax in `mapping.json`

### Images Not Embedding
**Error**: Images missing or placeholders shown
**Solution**: Check image format support (PNG, JPEG, GIF, BMP)

### TOC Not Populating
**Issue**: TOC shows empty
**Solution**: Open in Word and right-click TOC → "Update Field"

## Performance Considerations

- **Template Generation**: One-time cost (~100ms)
- **Document Enrichment**: ~500ms - 2s depending on document size
- **Image Embedding**: ~100-500ms per image
- **Total**: Typically 1-3 seconds per document

## Best Practices

1. **Reuse Service Instance**: Create once, reuse for multiple documents
2. **Error Handling**: Always check `result.Success`
3. **Cleanup**: Delete temporary files after upload
4. **Logging**: Use structured logging for monitoring
5. **Configuration**: Keep `mapping.json` version-controlled

## Migration from Existing Enrichment

If you're replacing `HybridEnricher` + `TemplateBuilder`:

1. **Keep Both**: Run both and compare results
2. **Gradual Rollout**: Use feature flag to switch
3. **Monitor**: Compare document quality and processing time
4. **Switch**: Once validated, remove old code

---

**Status**: ✅ Ready for Integration  
**Last Updated**: 2024-12-19

