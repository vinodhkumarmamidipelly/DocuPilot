# Table of Contents (TOC) and Page Numbers Implementation

## Your Requirement

**What You Need:**
1. ✅ TOC should be **added automatically** if original document doesn't contain one
2. ✅ TOC should include **headings** from the document
3. ✅ TOC should include **page numbers**
4. ✅ TOC should be **picked from pages** (generated from document content)

**Question:** Is this possible with OpenXML?

**Answer:** ✅ **Yes, with some considerations** - OpenXML can insert TOC field, but Word needs to update it to show content.

---

## Current Implementation Status

### ✅ What We're Already Doing

**TOC Field Insertion:**
```csharp
// DocumentEnricherService.cs - InsertTocField()
var instr = new Run(new FieldCode(" TOC \\o \"1-3\" \\h \\z \\u "));
```

**What This Does:**
- ✅ Inserts TOC field code
- ✅ Configures to include Heading 1-3
- ✅ Creates hyperlinks (`\h`)
- ✅ Uses heading styles (`\u`)

**Current Limitation:**
- ⚠️ TOC field is inserted, but **content is not populated** until Word updates it
- ⚠️ Page numbers may not be automatically added to headers/footers

---

## How OpenXML Handles TOC

### ✅ What OpenXML CAN Do

1. **Insert TOC Field Code**
   - ✅ Can insert Word TOC field
   - ✅ Can configure which heading levels to include
   - ✅ Can set TOC options (hyperlinks, formatting)

2. **Ensure Headings Are Styled**
   - ✅ Can apply Heading1, Heading2, Heading3 styles
   - ✅ Headings are what TOC uses to generate entries

3. **Set Auto-Update**
   - ✅ Can set `UpdateFieldsOnOpen` property
   - ✅ Word will update TOC when document opens

4. **Add Page Numbers**
   - ✅ Can add PAGE field to headers/footers
   - ✅ Page numbers will appear in TOC automatically

### ⚠️ What OpenXML CANNOT Do Directly

1. **Calculate Page Numbers**
   - ⚠️ OpenXML cannot calculate which page a heading is on
   - ⚠️ Word calculates this when TOC field is updated

2. **Generate TOC Content**
   - ⚠️ OpenXML cannot generate the actual TOC entries
   - ⚠️ Word generates TOC when field is updated

**Why:** TOC generation requires Word's layout engine to:
- Calculate page breaks
- Determine page numbers
- Generate TOC entries with page references

---

## Solution: Proper TOC Implementation

### Approach 1: TOC Field with Auto-Update (Recommended)

**How It Works:**
1. Insert TOC field code
2. Ensure headings are properly styled
3. Set `UpdateFieldsOnOpen` property
4. Word automatically updates TOC when document opens

**Implementation:**

```csharp
// 1. Insert TOC Field
private void InsertTocField(Body body)
{
    // TOC Heading
    var tocHeading = CreateParagraphWithStyle("Table of Contents", "Heading1");
    body.AppendChild(tocHeading);
    
    // TOC Field Code
    var tocParagraph = new Paragraph();
    var tocRun = new Run();
    
    // Field start
    tocRun.Append(new FieldChar() { FieldCharType = FieldCharValues.Begin });
    
    // TOC field code: \o "1-3" = Heading levels 1-3
    //                  \h = Hyperlinks
    //                  \z = Hide in Web view
    //                  \u = Use heading styles
    tocRun.Append(new FieldCode(" TOC \\o \"1-3\" \\h \\z \\u ") 
    { 
        Space = SpaceProcessingModeValues.Preserve 
    });
    
    // Field separator
    tocRun.Append(new FieldChar() { FieldCharType = FieldCharValues.Separate });
    
    // Placeholder text (visible until Word updates)
    tocRun.Append(new Text("TOC will appear here when fields are updated in Word."));
    
    // Field end
    tocRun.Append(new FieldChar() { FieldCharType = FieldCharValues.End });
    
    tocParagraph.Append(tocRun);
    body.AppendChild(tocParagraph);
}

// 2. Set Auto-Update Property
private void SetUpdateFieldsOnOpen(WordprocessingDocument doc)
{
    var settingsPart = doc.MainDocumentPart?.DocumentSettingsPart;
    if (settingsPart == null)
    {
        settingsPart = doc.MainDocumentPart.AddNewPart<DocumentSettingsPart>();
    }
    
    var settings = settingsPart.Settings ?? new Settings();
    settings.UpdateFieldsOnOpen = new UpdateFieldsOnOpen() { Val = true };
    settingsPart.Settings = settings;
}

// 3. Ensure Headings Are Styled
private void EnsureHeadingStyles(Body body)
{
    foreach (var paragraph in body.Descendants<Paragraph>())
    {
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val;
        
        // If it looks like a heading but isn't styled, apply heading style
        if (IsHeading(paragraph) && !IsHeadingStyle(styleId))
        {
            paragraph.ParagraphProperties = new ParagraphProperties(
                new ParagraphStyleId() { Val = "Heading1" }  // or Heading2, Heading3
            );
        }
    }
}
```

**Result:**
- ✅ TOC field inserted
- ✅ Word updates TOC automatically when document opens
- ✅ Headings and page numbers appear in TOC

---

### Approach 2: Add Page Numbers to Headers/Footers

**How It Works:**
1. Add PAGE field to header or footer
2. Page numbers appear automatically
3. TOC includes page numbers when updated

**Implementation:**

```csharp
// Add Page Numbers to Footer
private void AddPageNumbers(WordprocessingDocument doc)
{
    var mainPart = doc.MainDocumentPart;
    
    // Get or create footer
    FooterPart footerPart = mainPart.FooterParts?.FirstOrDefault();
    if (footerPart == null)
    {
        footerPart = mainPart.AddNewPart<FooterPart>();
        var footer = new Footer();
        footerPart.Footer = footer;
        
        // Link footer to document
        var sectionProperties = mainPart.Document.Body
            .Descendants<SectionProperties>()
            .FirstOrDefault();
        
        if (sectionProperties != null)
        {
            var footerReference = new FooterReference()
            {
                Type = HeaderFooterValues.Default,
                Id = mainPart.GetIdOfPart(footerPart)
            };
            sectionProperties.Append(footerReference);
        }
    }
    
    var footer = footerPart.Footer ?? new Footer();
    
    // Create paragraph with centered page number
    var pageNumberParagraph = new Paragraph(
        new ParagraphProperties(
            new Justification() { Val = JustificationValues.Center }
        ),
        new Run(
            new FieldChar() { FieldCharType = FieldCharValues.Begin },
            new FieldCode(" PAGE ") { Space = SpaceProcessingModeValues.Preserve },
            new FieldChar() { FieldCharType = FieldCharValues.Separate },
            new Text("1"),  // Placeholder
            new FieldChar() { FieldCharType = FieldCharValues.End }
        )
    );
    
    footer.Append(pageNumberParagraph);
    footerPart.Footer = footer;
}
```

**Result:**
- ✅ Page numbers appear in footer
- ✅ TOC includes page numbers when updated

---

## Complete Implementation

### Enhanced TOC and Page Numbers

```csharp
public void EnrichDocumentWithTOC(string inputPath, string outputPath, string templatePath)
{
    // Copy template
    File.Copy(templatePath, outputPath, true);
    
    using (var doc = WordprocessingDocument.Open(outputPath, true))
    {
        var mainPart = doc.MainDocumentPart;
        var body = mainPart.Document.Body;
        
        // 1. Extract content from raw document
        var content = ExtractContent(inputPath);
        
        // 2. Ensure headings are properly styled
        EnsureHeadingStyles(body);
        
        // 3. Insert TOC field (if not exists)
        if (!HasTOC(body))
        {
            InsertTocField(body);
        }
        
        // 4. Add page numbers to footer
        AddPageNumbers(doc);
        
        // 5. Set auto-update for TOC
        SetUpdateFieldsOnOpen(doc);
        
        // 6. Save document
        mainPart.Document.Save();
    }
}

// Check if TOC already exists
private bool HasTOC(Body body)
{
    return body.Descendants<FieldCode>()
        .Any(fc => fc.Text.Contains("TOC"));
}

// Ensure headings are styled
private void EnsureHeadingStyles(Body body)
{
    var paragraphs = body.Descendants<Paragraph>().ToList();
    
    foreach (var para in paragraphs)
    {
        var text = para.InnerText.Trim();
        
        // Detect heading patterns
        if (IsHeadingText(text))
        {
            var level = DetectHeadingLevel(text);
            var styleId = $"Heading{level}";
            
            // Apply heading style
            para.ParagraphProperties = new ParagraphProperties(
                new ParagraphStyleId() { Val = styleId }
            );
        }
    }
}

// Detect if text is a heading
private bool IsHeadingText(string text)
{
    // Check if text is short and looks like a heading
    if (text.Length > 100) return false;
    
    // Check for common heading patterns
    var headingPatterns = new[] { "Overview", "Introduction", "Functional", "Technical", 
                                   "Troubleshooting", "Conclusion", "References" };
    
    return headingPatterns.Any(pattern => 
        text.StartsWith(pattern, StringComparison.OrdinalIgnoreCase));
}

// Detect heading level (1, 2, or 3)
private int DetectHeadingLevel(string text)
{
    // Simple heuristic: main sections = Heading1, subsections = Heading2
    var mainSections = new[] { "Overview", "Functional", "Technical", "Troubleshooting" };
    
    if (mainSections.Any(s => text.StartsWith(s, StringComparison.OrdinalIgnoreCase)))
        return 1;
    
    return 2;  // Default to Heading2
}
```

---

## What Happens When Document Opens

### Automatic TOC Update

1. **User Opens Document in Word**
2. **Word Detects `UpdateFieldsOnOpen = true`**
3. **Word Updates All Fields:**
   - Calculates page breaks
   - Determines page numbers for each heading
   - Generates TOC entries with page numbers
   - Updates PAGE fields in headers/footers

4. **Result:**
   - ✅ TOC shows all headings
   - ✅ TOC shows page numbers
   - ✅ TOC has hyperlinks to sections
   - ✅ Page numbers appear in footer

---

## If Original Document Has No TOC

### Detection and Addition

```csharp
// Check if original document has TOC
private bool OriginalDocumentHasTOC(string inputPath)
{
    using (var doc = WordprocessingDocument.Open(inputPath, false))
    {
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null) return false;
        
        // Check for TOC field
        return body.Descendants<FieldCode>()
            .Any(fc => fc.Text.Contains("TOC"));
    }
}

// Add TOC if missing
public void EnrichDocument(string inputPath, string outputPath, string templatePath)
{
    // Check if original has TOC
    bool hasTOC = OriginalDocumentHasTOC(inputPath);
    
    // Copy template or original
    if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
        File.Copy(templatePath, outputPath, true);
    else
        File.Copy(inputPath, outputPath, true);
    
    using (var doc = WordprocessingDocument.Open(outputPath, true))
    {
        var body = doc.MainDocumentPart.Document.Body;
        
        // Add TOC if original didn't have one
        if (!hasTOC)
        {
            InsertTocField(body);
        }
        
        // Always ensure headings are styled
        EnsureHeadingStyles(body);
        
        // Always add page numbers
        AddPageNumbers(doc);
        
        // Always set auto-update
        SetUpdateFieldsOnOpen(doc);
        
        doc.MainDocumentPart.Document.Save();
    }
}
```

---

## Requirements Coverage

### Your Requirements vs. Implementation

| Requirement | OpenXML Capability | Implementation |
|-------------|-------------------|----------------|
| **Add TOC if missing** | ✅ Yes | Check for TOC, insert if not found |
| **Include headings** | ✅ Yes | Ensure headings are styled, TOC picks them up |
| **Include page numbers** | ✅ Yes | Add PAGE field, TOC includes page numbers |
| **Pick from pages** | ✅ Yes | Word generates TOC from document content |

**Coverage:** ✅ **100%** - All requirements can be met

---

## Important Notes

### ⚠️ TOC Content Generation

**Key Point:** OpenXML **inserts the TOC field**, but **Word generates the content**.

**Why:**
- TOC generation requires Word's layout engine
- Page numbers depend on document layout
- Word calculates page breaks and positions

**Solution:**
- ✅ Insert TOC field with proper configuration
- ✅ Set `UpdateFieldsOnOpen = true`
- ✅ Word automatically generates TOC when document opens

**Result:**
- ✅ TOC appears with headings and page numbers
- ✅ Works automatically when user opens document
- ✅ No manual update needed (if `UpdateFieldsOnOpen` is set)

---

## Alternative: Programmatic TOC Generation

### If You Need TOC Content Immediately

**Option:** Generate TOC entries programmatically (more complex)

```csharp
// Generate TOC entries manually
private void GenerateTOCEntries(Body body, List<HeadingInfo> headings)
{
    var tocParagraph = new Paragraph(new Run(new Text("Table of Contents")));
    body.Append(tocParagraph);
    
    foreach (var heading in headings)
    {
        // Create TOC entry: "Heading Text ................... Page Number"
        var entry = $"{heading.Text} {new string('.', 50 - heading.Text.Length)} {heading.PageNumber}";
        var entryParagraph = new Paragraph(new Run(new Text(entry)));
        body.Append(entryParagraph);
    }
}

// Extract headings with page numbers (requires layout calculation)
private List<HeadingInfo> ExtractHeadingsWithPageNumbers(Body body)
{
    // This is complex - requires calculating page breaks
    // OpenXML doesn't provide this directly
    // Would need to estimate or use Word Interop (not recommended)
}
```

**Limitation:**
- ⚠️ OpenXML cannot calculate page numbers directly
- ⚠️ Would need Word Interop (requires Word installed)
- ⚠️ Not recommended for server-side processing

**Recommendation:** ✅ Use TOC field with auto-update (Approach 1)

---

## Summary

### ✅ Yes, It's Possible

**What OpenXML Can Do:**
1. ✅ Insert TOC field if original document doesn't have one
2. ✅ Configure TOC to include headings (levels 1-3)
3. ✅ Add page numbers to headers/footers
4. ✅ Set auto-update so Word generates TOC content

**How It Works:**
1. OpenXML inserts TOC field code
2. OpenXML ensures headings are properly styled
3. OpenXML adds PAGE field to footer
4. Word updates TOC when document opens (if `UpdateFieldsOnOpen = true`)
5. TOC shows headings with page numbers

**Result:**
- ✅ TOC automatically generated from document content
- ✅ Headings included in TOC
- ✅ Page numbers included in TOC
- ✅ Works even if original document had no TOC

---

## Implementation Checklist

- [ ] Check if original document has TOC
- [ ] Insert TOC field if missing
- [ ] Ensure headings are styled (Heading1, Heading2, Heading3)
- [ ] Add PAGE field to footer for page numbers
- [ ] Set `UpdateFieldsOnOpen = true` for auto-update
- [ ] Test TOC generation when document opens in Word

---

**This implementation ensures TOC is automatically generated with headings and page numbers, even if the original document didn't have one.**

