# Document Template Handling / Enrichment - How It Works

## 📋 Overview

The template handling system converts "scratch documents" (rough documents with minimal formatting) into professionally formatted organizational templates. **No AI is used** - everything is rule-based and deterministic.

---

## 🔄 Complete Flow Diagram

```
User uploads document to SharePoint
         ↓
Webhook notification triggers ProcessSharePointFile
         ↓
┌─────────────────────────────────────────┐
│ STEP 1: EXTRACTION                      │
│ - Download file from SharePoint         │
│ - Extract text based on file type        │
│ - Extract images from document           │
│ - Optional: OCR for image files         │
└─────────────────────────────────────────┘
         ↓
┌─────────────────────────────────────────┐
│ STEP 2: SECTIONING (Rule-Based)        │
│ - Parse text to detect headings          │
│ - Split into logical sections            │
│ - Extract title                          │
│ - Generate summaries                     │
└─────────────────────────────────────────┘
         ↓
┌─────────────────────────────────────────┐
│ STEP 3: CLASSIFICATION (Keyword-Based)  │
│ - Analyze keywords                       │
│ - Classify: Technical/Support/Functional │
└─────────────────────────────────────────┘
         ↓
┌─────────────────────────────────────────┐
│ STEP 4: TEMPLATE BUILDING               │
│ - Create DocumentModel structure         │
│ - Apply organizational template          │
│ - Embed images                           │
│ - Generate DOCX file                     │
└─────────────────────────────────────────┘
         ↓
Upload enriched document to ProcessedDocs folder
         ↓
Update SharePoint metadata
         ↓
✅ Document ready for Copilot search
```

---

## 📝 Step-by-Step Process

### **STEP 1: Content Extraction** (`SimpleExtractor`)

**Purpose:** Extract raw text and images from various file formats

**Supported Formats:**
- **DOCX** - Uses OpenXML SDK
- **PPTX** - Extracts text from slides, images from slide parts
- **XLSX** - Extracts text from cells, handles shared strings
- **PDF** - Uses Spire.PDF (commercial license)
- **Images** - PNG, JPG, JPEG, GIF, BMP, TIFF (with optional OCR)

**What It Does:**
```csharp
// Example: DOCX extraction
var (text, images) = await _extractor.ExtractDocxAsync(fileStream);
// Returns:
// - text: All text content from document
// - images: List of image byte arrays
```

**Output:**
- `text`: String containing all extracted text
- `imagesBytes`: List of byte arrays (one per image)

---

### **STEP 2: Rule-Based Sectioning** (`HybridEnricher.SectionDocument()`)

**Purpose:** Split unstructured text into logical sections with headings

**How It Works:**

#### **2.1 Title Extraction**
```csharp
ExtractTitle(text, fileName)
```
- Checks first line of text
- If first line looks like a title (< 100 chars, starts with capital, no periods):
  - Uses first line as title
- Otherwise: Uses filename (without extension)

#### **2.2 Heading Detection** (`IsLikelyHeading()`)
Detects headings using rules:
- **First line rule:** If it's the first line and < 100 chars → heading
- **Length rule:** Lines > 80 chars are NOT headings
- **Punctuation rule:** Lines ending with `.` or `,` are NOT headings
- **Numbered list rule:** Matches `^\d+[\.\)]\s+[A-Z]` (e.g., "1. Introduction")
- **Capitalization rule:** > 50% uppercase letters → likely heading

**Example:**
```
Input text:
"Introduction
This is the introduction section.
It contains some content.

Features
The system has many features.
Feature 1: Login
Feature 2: Dashboard"
```

**Detected Sections:**
1. Heading: "Introduction"
   - Body: "This is the introduction section. It contains some content."
2. Heading: "Features"
   - Body: "The system has many features. Feature 1: Login Feature 2: Dashboard"

#### **2.3 Section Creation** (`CreateSection()`)
For each detected section:
- **Id:** `s1`, `s2`, `s3`, etc.
- **Heading:** Detected heading text
- **Summary:** First sentence (up to 200 chars) or first 40 words
- **Body:** All content under that heading

**Output:** `DocumentModel` object with:
```csharp
{
    Title: "Document Title",
    Sections: [
        { Id: "s1", Heading: "Introduction", Summary: "...", Body: "..." },
        { Id: "s2", Heading: "Features", Summary: "...", Body: "..." }
    ],
    Images: []
}
```

---

### **STEP 3: Document Classification** (`HybridEnricher.ClassifyDocument()`)

**Purpose:** Categorize document type using keyword matching (no AI)

**How It Works:**
1. Combines title + content into single text
2. Counts keyword matches in three categories:

**Technical Keywords:**
- `api`, `endpoint`, `integration`, `technical`, `developer`, `code`, `sdk`, `rest`, `graph`, `database`, `architecture`, `implementation`

**Support Keywords:**
- `support`, `troubleshooting`, `issue`, `problem`, `error`, `help`, `faq`, `guide`, `how to`, `fix`, `resolve`

**Functional Keywords:**
- `feature`, `functionality`, `user`, `workflow`, `process`, `business`, `requirement`, `use case`, `scenario`

3. Returns category with highest score:
   - `"Technical"` - If technical score is highest
   - `"Support"` - If support score is highest
   - `"Functional"` - Otherwise (default)

**Example:**
```
Document: "API Integration Guide - How to connect to REST endpoints"
Technical keywords found: 3 (api, endpoint, integration)
Support keywords found: 1 (guide)
Functional keywords found: 0
→ Classification: "Technical"
```

---

### **STEP 4: Template Building** (`TemplateBuilder.BuildDocxBytes()`)

**Purpose:** Convert structured `DocumentModel` into formatted DOCX file

**What It Creates:**

#### **4.1 Document Structure**
```
┌─────────────────────────────────┐
│ Title (Title style)              │
├─────────────────────────────────┤
│ Table of Contents (TOCHeading)   │
├─────────────────────────────────┤
│ Section 1 Heading (Heading1)     │
│ Summary: [first sentence]        │
│ [Section body content]           │
├─────────────────────────────────┤
│ Section 2 Heading (Heading1)    │
│ Summary: [first sentence]        │
│ [Section body content]           │
├─────────────────────────────────┤
│ Images: (Heading2)               │
│ [Image 1 with caption]           │
│ [Image 2 with caption]           │
└─────────────────────────────────┘
```

#### **4.2 Word Styles Applied**
- **Title:** `Title` style (Word built-in)
- **TOC Heading:** `TOCHeading` style
- **Section Headings:** `Heading1` style
- **Image Section:** `Heading2` style

#### **4.3 Image Embedding**
1. Detects image format (PNG, JPEG, GIF) by file signature
2. Adds image part to document
3. Creates drawing element with:
   - Fixed dimensions: 595x842 pixels (A4 ratio)
   - Centered alignment
   - Caption: "Figure {idx}: Image from original document"
4. **Note:** Currently uses reflection workaround (TODO: fix proper Drawing class)

#### **4.4 Output**
- Returns `byte[]` of complete DOCX file
- File name: `{originalName}_enriched.docx`

---

## 🎯 Key Characteristics

### **✅ What It Does:**
1. **Extracts** text and images from multiple formats
2. **Sections** content using rule-based heading detection
3. **Formats** into organizational template
4. **Classifies** documents using keyword matching
5. **Embeds** images with captions
6. **Generates** professional DOCX output

### **❌ What It Does NOT Do:**
1. **No AI enrichment** - Doesn't improve or expand content
2. **No database** - Doesn't store embeddings or metadata
3. **No semantic search** - Relies on SharePoint native search
4. **No content generation** - Only reformats existing content

---

## 📊 Example: Before & After

### **Before (Scratch Document):**
```
my-product-guide.docx

Introduction
This is a product guide. It has screenshots.

Features
The product has login. The product has dashboard.
[Screenshot 1]
[Screenshot 2]

Support
For help, contact support.
```

### **After (Enriched Document):**
```
my-product-guide_enriched.docx

┌─────────────────────────────────────────┐
│ My Product Guide                         │
│ (Title style)                            │
├─────────────────────────────────────────┤
│ Table of Contents (auto-generated)       │
├─────────────────────────────────────────┤
│ Introduction                             │
│ (Heading1 style)                         │
│ Summary: This is a product guide.        │
│ This is a product guide. It has          │
│ screenshots.                             │
├─────────────────────────────────────────┤
│ Features                                 │
│ (Heading1 style)                         │
│ Summary: The product has login.          │
│ The product has login. The product has   │
│ dashboard.                               │
├─────────────────────────────────────────┤
│ Support                                  │
│ (Heading1 style)                         │
│ Summary: For help, contact support.      │
│ For help, contact support.               │
├─────────────────────────────────────────┤
│ Images                                   │
│ (Heading2 style)                         │
│ [Figure 1: Screenshot 1 - embedded]      │
│ Figure 1: Image from original document   │
│ [Figure 2: Screenshot 2 - embedded]      │
│ Figure 2: Image from original document   │
└─────────────────────────────────────────┘
```

**Metadata Added:**
- `SMEPilot_Enriched`: `true`
- `SMEPilot_Status`: `Completed`
- `SMEPilot_Classification`: `Functional` (based on keywords)
- `SMEPilot_EnrichedFileUrl`: Link to enriched document

---

## 🔧 Configuration

### **Template Customization:**
Currently, template is hard-coded in `TemplateBuilder.cs`. To customize:

1. **Change styles:** Modify `ParagraphStyleId` values
2. **Change structure:** Modify document building logic
3. **Add sections:** Add new elements to document body
4. **Change image size:** Modify `widthEmu` and `heightEmu` values

### **Sectioning Rules:**
Customize in `HybridEnricher.cs`:
- **Heading detection:** Modify `IsLikelyHeading()` method
- **Summary generation:** Modify `GenerateSummary()` method
- **Classification keywords:** Modify keyword arrays in `ClassifyDocument()`

---

## 🚀 Benefits

1. **Consistent Formatting:** All documents follow same template
2. **Better Structure:** Content organized into logical sections
3. **Searchable:** Formatted documents work with SharePoint/Copilot search
4. **Professional:** Clean, organized output
5. **No Cost:** No AI API calls, no database costs
6. **Fast:** Rule-based processing is very fast

---

## ⚠️ Limitations

1. **Basic Sectioning:** May miss complex document structures
2. **Simple Classification:** Only 3 categories, keyword-based
3. **Fixed Image Size:** Images resized to fixed dimensions
4. **No TOC Generation:** TOC is placeholder text (not actual hyperlinks)
5. **Image Embedding:** Uses reflection workaround (needs fix)

---

## 🔮 Future Enhancements (Optional)

1. **Better Heading Detection:** Use ML for more accurate sectioning
2. **Custom Templates:** Allow organizations to define their own templates
3. **Dynamic TOC:** Generate actual table of contents with hyperlinks
4. **Image Optimization:** Resize/compress images before embedding
5. **Style Customization:** Allow style configuration via settings

---

## 📝 Summary

The template handling system is a **rule-based document formatter** that:
- Extracts content from multiple formats
- Organizes content into sections using pattern matching
- Applies consistent organizational template
- Generates professional DOCX output
- **No AI, no database, just formatting!**

The result is a consistently formatted document that's ready for SharePoint search and O365 Copilot integration.

