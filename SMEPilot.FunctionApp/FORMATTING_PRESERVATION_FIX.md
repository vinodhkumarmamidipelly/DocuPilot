# Formatting Preservation Fix Plan

## Problem Summary

**Current Behavior:**
- Content is extracted as plain text strings
- New Run/Paragraph elements are created from text
- **ALL formatting is lost**: bold, italic, colors, tables, images, lists, hyperlinks

**Required Behavior (per requirement.md):**
- Extract sections as OpenXML elements (preserve structure)
- Clone elements with `CloneNode(true)` to preserve formatting
- Insert cloned elements into template placeholders
- Preserve: Bold, Italic, Underline, Colors, Tables, Nested Tables, Bullets, Numbering, Images, Image Position, Hyperlinks, Alignment, Paragraph Spacing

## Solution Approach

### Phase 1: Extract Sections as OpenXML Elements

Instead of extracting as plain text, extract as actual OpenXML elements:

```csharp
// NEW: Extract section as OpenXML elements
public List<OpenXmlElement> ExtractSectionAsElements(
    WordprocessingDocument sourceDoc, 
    string sectionHeading)
{
    var elements = new List<OpenXmlElement>();
    var body = sourceDoc.MainDocumentPart.Document.Body;
    
    // Find section by heading
    bool inSection = false;
    foreach (var elem in body.Elements())
    {
        if (IsHeading(elem, sectionHeading))
        {
            inSection = true;
            continue; // Skip heading itself
        }
        
        if (inSection)
        {
            if (IsNextHeading(elem))
                break; // End of section
            
            // Clone element with all formatting
            elements.Add(elem.CloneNode(true));
        }
    }
    
    return elements;
}
```

### Phase 2: Insert Cloned Elements into Template

Replace `ReplaceSdtContent(string newText)` with:

```csharp
// NEW: Insert OpenXML elements preserving formatting
private bool ReplaceSdtContentWithElements(
    SdtElement sdt, 
    List<OpenXmlElement> elements,
    WordprocessingDocument targetDoc,
    WordprocessingDocument sourceDoc)
{
    if (sdt is SdtBlock sdtBlock && sdtBlock.SdtContentBlock != null)
    {
        sdtBlock.SdtContentBlock.RemoveAllChildren();
        
        foreach (var elem in elements)
        {
            // Clone element
            var cloned = elem.CloneNode(true);
            
            // Handle images: Copy image parts and update relationships
            if (elem is Paragraph para)
            {
                cloned = CopyImageParts(para, sourceDoc, targetDoc);
            }
            // Handle tables: Copy table structure
            else if (elem is Table table)
            {
                cloned = CopyTableWithFormatting(table, sourceDoc, targetDoc);
            }
            
            sdtBlock.SdtContentBlock.AppendChild(cloned);
        }
        return true;
    }
    // ... handle other SDT types
}
```

### Phase 3: Handle Image Parts and Relationships

Images need special handling because they're stored as separate parts:

```csharp
private OpenXmlElement CopyImageParts(
    Paragraph para, 
    WordprocessingDocument sourceDoc,
    WordprocessingDocument targetDoc)
{
    var cloned = para.CloneNode(true) as Paragraph;
    var mainPart = targetDoc.MainDocumentPart;
    
    // Find all images in paragraph
    var drawings = cloned.Descendants<Drawing>().ToList();
    foreach (var drawing in drawings)
    {
        var blip = drawing.Descendants<A.Blip>().FirstOrDefault();
        if (blip?.Embed != null)
        {
            var imageRelId = blip.Embed.Value;
            
            // Get image part from source
            var sourceImagePart = sourceDoc.MainDocumentPart
                .GetPartById(imageRelId) as ImagePart;
            
            if (sourceImagePart != null)
            {
                // Copy image part to target
                var targetImagePart = mainPart.AddImagePart(sourceImagePart.ContentType);
                using (var stream = sourceImagePart.GetStream())
                {
                    targetImagePart.FeedData(stream);
                }
                
                // Update relationship ID
                var newRelId = mainPart.GetIdOfPart(targetImagePart);
                blip.Embed = newRelId;
            }
        }
    }
    
    return cloned;
}
```

### Phase 4: Handle Tables with Formatting

Tables need to preserve cell formatting, borders, shading:

```csharp
private Table CopyTableWithFormatting(
    Table sourceTable,
    WordprocessingDocument sourceDoc,
    WordprocessingDocument targetDoc)
{
    // Clone table structure
    var cloned = sourceTable.CloneNode(true) as Table;
    
    // Process each cell to handle nested images/tables
    foreach (var row in cloned.Elements<TableRow>())
    {
        foreach (var cell in row.Elements<TableCell>())
        {
            // Copy images in cells
            var cellParas = cell.Elements<Paragraph>().ToList();
            foreach (var para in cellParas)
            {
                CopyImageParts(para, sourceDoc, targetDoc);
            }
            
            // Handle nested tables
            var nestedTables = cell.Elements<Table>().ToList();
            foreach (var nestedTable in nestedTables)
            {
                CopyTableWithFormatting(nestedTable, sourceDoc, targetDoc);
            }
        }
    }
    
    return cloned;
}
```

## Implementation Steps

1. **Modify DocumentExtractor** to extract sections as OpenXML elements
   - Add method: `ExtractSectionAsElements(sourceDoc, sectionHeading)`
   - Return list of OpenXmlElement (paragraphs, tables, etc.)

2. **Modify TemplateProcessor** to accept OpenXML elements
   - Change `ReplaceSdtContent(string)` to `ReplaceSdtContentWithElements(List<OpenXmlElement>)`
   - Add image part copying logic
   - Add table copying logic

3. **Update BuildContentMapFromTemplate** to use element extraction
   - Instead of building text strings, build lists of OpenXML elements
   - Store elements in content map instead of strings

4. **Update FillTemplate** to use new method
   - Pass OpenXML elements instead of text strings
   - Handle image relationships properly

## Testing Checklist

- [ ] Bold/italic/underline preserved
- [ ] Colors preserved (text color, highlight color)
- [ ] Tables preserved with borders and shading
- [ ] Nested tables preserved
- [ ] Bullet lists preserved
- [ ] Numbered lists preserved
- [ ] Images preserved with correct size and position
- [ ] Hyperlinks preserved
- [ ] Paragraph alignment preserved
- [ ] Paragraph spacing preserved
- [ ] Section order preserved
- [ ] Remaining content has all formatting

## Files to Modify

1. `DocumentExtractor.cs` - Add element extraction methods
2. `TemplateProcessor.cs` - Replace text-based insertion with element-based
3. `DocumentEnricher.cs` - Update to work with OpenXML elements
4. `ProcessSharePointFile.cs` - Update to pass elements instead of text



