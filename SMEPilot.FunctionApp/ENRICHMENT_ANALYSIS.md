# Template Formatting & Branding Analysis

## Current Flow (After Webhook Received)

```
1. Webhook → ProcessSharePointFile
2. Download file from SharePoint
3. Extract structured data (DocumentExtractor)
   - Paragraphs, Tables, Images
4. Parse sections (DocumentEnricher.ParseSections)
   - Rule-based section detection
   - Section mapping to template tags
5. Build DocumentModel
   - Title, Sections, Images
6. Download/Copy Template
   - From SharePoint or local Templates folder
7. Build Content Map (TemplateProcessor.BuildContentMapFromTemplate)
   - Map sections to template placeholders
   - Multiple matching strategies (exact, fuzzy, content-based)
8. Fill Template (TemplateProcessor.FillTemplate)
   - Replace content controls (SDT elements)
   - Replace plain text placeholders
   - Insert images
   - Expand revision history tables
9. Upload enriched document to SharePoint
```

## Key Issues Identified

### 1. **Section-to-Placeholder Mapping Complexity**
**Location:** `TemplateProcessor.BuildContentMapFromTemplate()` (line 1664)

**Problems:**
- Multiple overlapping matching strategies (exact, fuzzy, content-based, keyword-based)
- Low confidence matches may fill placeholders incorrectly
- Document title sections sometimes incorrectly mapped
- No clear priority order when multiple matches exist

**Current Logic:**
```csharp
// Strategy 1: Exact match
// Strategy 2: Fuzzy/semantic match  
// Strategy 3: Content-based match
// Strategy 4: Keyword-based match
// Fallback: First available section
```

**Impact:** 
- Some placeholders may remain empty
- Wrong content may be inserted into placeholders
- Branding sections may get filled with wrong content

### 2. **Formatting Preservation Issues**
**Location:** `TemplateProcessor.ReplaceContentInSdt()` (line 977)

**Problems:**
- Formatting preservation only works if original content has formatting
- When replacing with new content, formatting may be lost
- Template styles may not be applied correctly
- Numbering lists may break when content is replaced

**Current Approach:**
```csharp
// Tries to preserve RunProperties and ParagraphProperties
// But only if original SDT had formatting
// New content gets default formatting
```

**Impact:**
- Branded styles from template may not be preserved
- Lists may lose numbering
- Headers/footers formatting may be inconsistent

### 3. **Template Branding Elements Not Fully Preserved**
**Location:** `TemplateProcessor.FillTemplate()` (line 646)

**Problems:**
- Headers and footers from template may not be preserved
- Logo/images in headers may be lost
- Template styles may be overwritten
- Page setup (margins, orientation) may not be preserved

**Current Behavior:**
- Only preserves numbering definitions (line 683)
- Does NOT explicitly preserve headers/footers
- Does NOT preserve page setup
- Does NOT preserve theme/styles

**Impact:**
- Branding (logos, company headers) may be lost
- Document appearance may not match template

### 4. **Content Control (SDT) Replacement Logic**
**Location:** `TemplateProcessor.ReplaceContentInSdt()` (line 977)

**Problems:**
- Complex logic for different SDT types (Block, Run, Cell)
- May not handle nested content controls correctly
- Text replacement may split across runs incorrectly
- Multi-paragraph content may lose formatting

**Current Logic:**
```csharp
if (sdt is SdtBlock) { /* Replace block content */ }
else if (sdt is SdtRun) { /* Replace run content */ }
else if (sdt is SdtCell) { /* Replace cell content */ }
else { /* Generic replacement */ }
```

**Impact:**
- Some placeholders may not be filled
- Formatting may be inconsistent
- Complex templates may break

### 5. **Revision History Table Expansion**
**Location:** `TemplateProcessor.ExpandRevisionHistoryTable()` (line 1363)

**Problems:**
- Assumes specific table structure
- May fail if table format differs
- Does not preserve table styling from template
- Hardcoded column assumptions

**Current Logic:**
- Finds first table with "Revision" in text
- Assumes 5 columns (Version, Date, Author, Changes, Approved By)
- May break if template has different structure

**Impact:**
- Revision history may not be populated correctly
- Table formatting may be lost

## Recommended Improvements

### Priority 1: Improve Template Branding Preservation

**Action Items:**
1. **Preserve Headers and Footers**
   ```csharp
   private void PreserveHeadersAndFooters(WordprocessingDocument outputDoc, string templatePath)
   {
       // Copy header parts from template
       // Copy footer parts from template
       // Preserve header/footer relationships
   }
   ```

2. **Preserve Page Setup**
   ```csharp
   private void PreservePageSetup(WordprocessingDocument outputDoc, string templatePath)
   {
       // Copy section properties (margins, orientation, page size)
       // Preserve page borders
   }
   ```

3. **Preserve Styles and Themes**
   ```csharp
   private void PreserveStylesAndThemes(WordprocessingDocument outputDoc, string templatePath)
   {
       // Copy styles part
       // Copy theme part
       // Preserve style relationships
   }
   ```

### Priority 2: Improve Section-to-Placeholder Mapping

**Action Items:**
1. **Add Confidence Scoring**
   - Score each match (0.0 - 1.0)
   - Only use matches above threshold (e.g., 0.6)
   - Log low-confidence matches for review

2. **Improve Matching Algorithm**
   ```csharp
   private (string? tag, double confidence) FindBestMatch(
       string placeholderName, 
       DocumentModel docModel, 
       List<string> availableTags)
   {
       // 1. Exact name match (confidence: 1.0)
       // 2. Semantic similarity (confidence: 0.8-0.9)
       // 3. Keyword match (confidence: 0.6-0.7)
       // 4. Content analysis (confidence: 0.5-0.6)
       // Return best match with confidence score
   }
   ```

3. **Add Template-Driven Mapping**
   - Read template metadata/instructions
   - Use template-defined mapping rules
   - Support custom mapping configuration

### Priority 3: Improve Formatting Preservation

**Action Items:**
1. **Preserve Template Styles When Inserting Content**
   ```csharp
   private void InsertContentWithTemplateStyle(
       SdtElement sdt, 
       string content, 
       string? templateStyleId)
   {
       // Apply template style to new content
       // Preserve paragraph properties
       // Preserve run properties
   }
   ```

2. **Handle Complex Formatting**
   - Preserve bold/italic/underline
   - Preserve colors and fonts
   - Preserve list formatting
   - Preserve table formatting

### Priority 4: Improve Content Control Replacement

**Action Items:**
1. **Better SDT Type Handling**
   - Handle nested SDTs
   - Handle SDTs with complex content
   - Preserve SDT properties

2. **Improve Text Replacement**
   - Handle tokens split across runs
   - Preserve formatting when replacing
   - Handle multi-paragraph replacements

### Priority 5: Add Validation and Diagnostics

**Action Items:**
1. **Template Validation**
   - Validate template structure
   - Check for required placeholders
   - Verify branding elements exist

2. **Content Mapping Validation**
   - Verify all required placeholders are filled
   - Check content quality (not empty, not just title)
   - Validate formatting preservation

3. **Diagnostic Logging**
   - Log all placeholder matches with confidence
   - Log formatting preservation status
   - Log branding element preservation status

## Implementation Plan

### Phase 1: Branding Preservation (Week 1)
- [ ] Implement header/footer preservation
- [ ] Implement page setup preservation
- [ ] Implement styles/theme preservation
- [ ] Add tests for branding preservation

### Phase 2: Mapping Improvements (Week 2)
- [ ] Add confidence scoring to matching
- [ ] Improve matching algorithm
- [ ] Add template-driven mapping support
- [ ] Add validation for placeholder filling

### Phase 3: Formatting Improvements (Week 3)
- [ ] Improve style preservation when inserting content
- [ ] Handle complex formatting scenarios
- [ ] Improve SDT replacement logic
- [ ] Add formatting validation

### Phase 4: Testing and Refinement (Week 4)
- [ ] Add comprehensive tests
- [ ] Test with various template formats
- [ ] Test with various document types
- [ ] Performance optimization

## Code Locations to Modify

1. **TemplateProcessor.cs**
   - `FillTemplate()` - Add branding preservation calls
   - `BuildContentMapFromTemplate()` - Improve matching
   - `ReplaceContentInSdt()` - Improve formatting preservation
   - `ExpandRevisionHistoryTable()` - Improve table handling

2. **New Methods to Add:**
   - `PreserveHeadersAndFooters()`
   - `PreservePageSetup()`
   - `PreserveStylesAndThemes()`
   - `FindBestMatch()` (with confidence scoring)
   - `ValidateTemplateStructure()`
   - `ValidateContentMapping()`

## Testing Strategy

1. **Unit Tests:**
   - Test each matching strategy
   - Test formatting preservation
   - Test branding preservation

2. **Integration Tests:**
   - Test full enrichment flow
   - Test with various templates
   - Test with various document types

3. **Manual Testing:**
   - Test with real templates
   - Verify branding is preserved
   - Verify content is correctly mapped

## Success Criteria

1. ✅ All branding elements (headers, footers, logos) are preserved
2. ✅ All template styles are applied correctly
3. ✅ 95%+ of placeholders are filled correctly
4. ✅ Formatting is preserved in 90%+ of cases
5. ✅ Revision history tables are populated correctly
6. ✅ No regression in existing functionality



