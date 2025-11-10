# DocumentEnricherService - Rule-Based Document Enrichment

## Overview

`DocumentEnricherService` is a rule-based document enrichment module that reformats documents using an organizational template and keyword-based section mapping. **No AI dependencies** - all logic is deterministic.

## Features

✅ **Template-Based Formatting** - Uses organizational .dotx template  
✅ **Keyword-Based Section Mapping** - Maps content to sections based on keywords  
✅ **Document Classification** - Detects document type (Functional/Technical/Support/Generic)  
✅ **Image Embedding** - Automatically embeds images in Screenshots section  
✅ **TOC Generation** - Creates functional Table of Contents  
✅ **Revision History** - Auto-generates revision tracking table  
✅ **Mandatory Sections** - Fills missing sections with placeholders  

## Folder Structure

```
SMEPilot.FunctionApp/
├── Services/
│   ├── DocumentEnricherService.cs          # Main enrichment service
│   └── DocumentEnricherService_Usage_Example.cs  # Usage examples
├── Config/
│   └── mapping.json                        # Keyword mapping configuration
├── Templates/
│   └── SMEPilot_OrgTemplate_RuleBased.dotx # Organizational template (auto-generated)
└── Helpers/
    └── TemplateGenerator.cs                # Template generator utility
```

## Quick Start

### 1. Generate Template (First Time)

```csharp
using SMEPilot.FunctionApp.Helpers;

// Generate template if it doesn't exist
var templatePath = Path.Combine("Templates", "SMEPilot_OrgTemplate_RuleBased.dotx");
if (!File.Exists(templatePath))
{
    TemplateGenerator.GenerateTemplate(templatePath);
}
```

### 2. Basic Usage

```csharp
using SMEPilot.FunctionApp.Services;

// Initialize service
var templatePath = Path.Combine("Templates", "SMEPilot_OrgTemplate_RuleBased.dotx");
var mappingJsonPath = Path.Combine("Config", "mapping.json");
var enricher = new DocumentEnricherService(templatePath, mappingJsonPath, logger);

// Enrich document
var result = enricher.Enrich(
    inputPath: "raw_document.docx",
    outputPath: "enriched_document.docx",
    author: "John Doe",
    images: null); // Optional: pass images or let service extract from document

if (result.Success)
{
    Console.WriteLine($"Document enriched: {result.EnrichedPath}");
    Console.WriteLine($"Document Type: {result.DocumentType}");
    Console.WriteLine($"Status: {result.Status}");
}
else
{
    Console.WriteLine($"Error: {result.ErrorMessage}");
}
```

## Integration with ProcessSharePointFile.cs

### Example Integration

```csharp
// After downloading file from SharePoint
var localFilePath = await _graph.DownloadFileAsync(driveId, itemId);

// Setup paths
var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "SMEPilot_OrgTemplate_RuleBased.dotx");
var mappingJsonPath = Path.Combine(AppContext.BaseDirectory, "Config", "mapping.json");
var outputPath = Path.ChangeExtension(localFilePath, "_formatted.docx");

// Ensure template exists
if (!File.Exists(templatePath))
{
    var templateDir = Path.GetDirectoryName(templatePath);
    if (!string.IsNullOrEmpty(templateDir) && !Directory.Exists(templateDir))
        Directory.CreateDirectory(templateDir);
    TemplateGenerator.GenerateTemplate(templatePath);
}

// Create enricher and enrich
var enricher = new DocumentEnricherService(templatePath, mappingJsonPath, _logger);
var enrichmentResult = enricher.Enrich(
    inputPath: localFilePath,
    outputPath: outputPath,
    author: uploaderEmail ?? "System",
    images: null);

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
    
    _logger.LogInformation("✅ Document enriched: {Path}", enrichmentResult.EnrichedPath);
}
```

## Configuration (mapping.json)

The `mapping.json` file defines:
- **Keywords for document classification** (Functional/Technical/Support)
- **Section rules** with keywords and mandatory flags

### Example Configuration

```json
{
  "FunctionalKeywords": ["requirement", "workflow", "module", "process", "feature"],
  "TechnicalKeywords": ["install", "setup", "config", "parameter", "deploy", "step"],
  "SupportKeywords": ["issue", "error", "fix", "troubleshoot", "faq"],
  "Sections": [
    {
      "Name": "1. Introduction",
      "Keywords": ["introduction", "purpose", "overview"],
      "Mandatory": true
    },
    {
      "Name": "3. Functional Details",
      "Keywords": ["workflow", "business", "requirement"],
      "Mandatory": false
    }
  ]
}
```

### Section Rules

- **Name**: Section heading text
- **Keywords**: List of keywords to match content
- **Mandatory**: If true, section is always included (with placeholder if no content)

## Document Structure

The enriched document follows this structure:

1. **Cover Page**
   - Document Title
   - Document Type (classification)
   - Author
   - Date
   - Version

2. **Table of Contents**
   - Auto-generated TOC field
   - Includes Heading1, Heading2, Heading3

3. **Sections** (from mapping.json)
   - 1. Introduction (mandatory)
   - 2. Overview (mandatory)
   - 3. Functional Details (optional)
   - 4. Technical Details (optional)
   - 5. Screenshots (optional, auto-populated with images)
   - 6. Troubleshooting & FAQ (optional)

4. **Revision History**
   - Table with Date, Author, Change Summary
   - Auto-populated with initial entry

## Document Classification

Documents are classified by keyword scoring:

- **Functional**: Highest score in functional keywords
- **Technical**: Highest score in technical keywords
- **Support**: Highest score in support keywords
- **Generic**: Fallback if no keywords match

## Image Handling

- Images are automatically extracted from input document
- Embedded in "5. Screenshots" section
- Aspect ratio preserved
- Captions added automatically
- Supports PNG, JPEG, GIF, BMP

## Error Handling

The service includes comprehensive error handling:
- File not found errors
- Invalid configuration errors
- Image embedding failures (graceful fallback)
- All errors logged and returned in `EnrichmentResult`

## Output

The `EnrichmentResult` object contains:

```csharp
public class EnrichmentResult
{
    public bool Success { get; set; }
    public string DocumentType { get; set; }      // "Functional", "Technical", "Support", "Generic"
    public string EnrichedPath { get; set; }      // Path to enriched document
    public string Status { get; set; }            // "Formatted" or "Failed"
    public string ErrorMessage { get; set; }      // Error message if failed
}
```

## Best Practices

1. **Template Generation**: Generate template once and reuse
2. **Configuration**: Customize `mapping.json` per organization needs
3. **Error Handling**: Always check `result.Success` before proceeding
4. **Logging**: Pass logger for debugging and monitoring
5. **Path Management**: Use `AppContext.BaseDirectory` for deployment paths

## Troubleshooting

### Template Not Found
- Ensure `Templates` folder exists
- Generate template using `TemplateGenerator.GenerateTemplate()`

### Mapping Configuration Errors
- Validate JSON syntax
- Ensure all required fields are present
- Check file path is correct

### Image Embedding Issues
- Check image format support (PNG, JPEG, GIF, BMP)
- Verify image file size
- Check SharePoint session errors (see SHAREPOINT_SESSION_FIX.md)

## See Also

- `TemplateBuilder.cs` - Alternative template builder (builds from scratch)
- `HybridEnricher.cs` - Rule-based sectioning logic
- `SHAREPOINT_SESSION_FIX.md` - Fix for SharePoint session errors

---

**Status:** ✅ Ready for use  
**Version:** 1.0  
**Last Updated:** 2024-12-19

