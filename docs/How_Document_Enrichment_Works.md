# 🧠 How Document Enrichment Works - AI-Powered Process

## ✅ **YES - We Use Azure OpenAI for Enrichment**

SMEPilot uses **two Azure OpenAI models** to enrich documents:

### 1. **GPT Model** (GPT-4, GPT-4o, or GPT-3.5-turbo)
   - **Purpose**: Sectioning, summarization, and content enhancement
   - **Deployment**: Configured via `AzureOpenAI_Deployment_GPT` setting

### 2. **Embedding Model** (text-embedding-ada-002 or text-embedding-3-small)
   - **Purpose**: Creates vector embeddings for semantic search
   - **Deployment**: Configured via `AzureOpenAI_Embedding_Deployment` setting

---

## 📋 Step-by-Step Enrichment Process

### **Step 1: Download Document**
```
User uploads: "scratch_document.docx" → SharePoint
Azure Function downloads the file
```

### **Step 2: Extract Content**
```
Extract:
- Text content from DOCX
- Images from DOCX
- (Optional: OCR text from images - currently skipped)
```

### **Step 3: AI Processing (GPT Model)**
```
Send to Azure OpenAI GPT:
- System Prompt: "You are a document enricher. Output ONLY valid JSON..."
- User Prompt: Raw text + Image OCRs
- Temperature: 0.2 (low = more consistent)
- Max Tokens: 1500

AI Returns:
{
  "title": "Enhanced Document Title",
  "sections": [
    {
      "id": "s1",
      "heading": "Introduction",
      "summary": "Brief 20-40 word summary",
      "body": "Enriched and expanded content"
    },
    ...
  ],
  "images": [...]
}
```

**What AI Does:**
- ✅ **Sections the document** into logical parts (up to 12 sections)
- ✅ **Generates headings** for each section
- ✅ **Creates summaries** (20-40 words per section)
- ✅ **Enriches body text** - expands and improves the original content
- ✅ **Structures everything** into a standard format

### **Step 4: Build New Document**
```
TemplateBuilder creates new DOCX:
- Title (from AI)
- Table of Contents (auto-generated)
- Sections with headings (from AI)
- Summaries (from AI)
- Enriched body text (from AI)
- Images (from original document)
```

### **Step 5: Create Embeddings**
```
For each section:
- Generate embedding vector (1536 dimensions)
- Store in CosmosDB for semantic search
- Used later for Copilot queries
```

### **Step 6: Upload Enriched Document**
```
Save to SharePoint:
- Folder: ProcessedDocs/
- Filename: original_name_enriched.docx
- Update metadata: SMEPilot_Enriched = true
```

---

## 🤖 AI Prompts Used

### **System Prompt (to GPT):**
```
"You are a document enricher. Output ONLY valid JSON matching the schema:
{title:"...",sections:[{id:"s1",heading:"...",summary:"...",body:"..."}],images:[{id:"img1",alt:"..."}]}
Create up to 12 sections. Summary 20-40 words. Do not add extra text."
```

### **User Prompt (to GPT):**
```
RawText:
[Extracted text from document]

ImageOCRs:
[OCR text from images, if any]

Return valid JSON only.
```

---

## 🔄 Current Status

### **✅ Working (Mock Mode):**
- Code is ready
- Runs without Azure OpenAI (uses mock responses)
- Successfully processed `GeneratedReport.pdf`

### **⚠️ Needs Configuration:**
- Azure OpenAI credentials not configured yet
- Currently using mock AI responses

### **To Enable Real AI:**
1. Create Azure OpenAI resource in Azure Portal
2. Deploy GPT model (gpt-4, gpt-4o, or gpt-35-turbo)
3. Deploy Embedding model (text-embedding-ada-002)
4. Update `local.settings.json`:
   ```json
   {
     "AzureOpenAI_Endpoint": "https://your-resource.openai.azure.com/",
     "AzureOpenAI_Key": "your-api-key",
     "AzureOpenAI_Deployment_GPT": "gpt-4",
     "AzureOpenAI_Embedding_Deployment": "text-embedding-ada-002"
   }
   ```

---

## 📊 Example: Before vs After

### **Before (Scratch Document):**
```
Title: Product Guide
Content: "This is a product. It has features. Here are screenshots."
```

### **After (AI Enriched):**
```
Title: Comprehensive Product Guide: Features and Implementation

Table of Contents:
1. Introduction
2. Key Features
3. Implementation Guide
...

Section 1: Introduction
Summary: This guide provides a comprehensive overview of the product's core features and implementation strategies.

Body: [AI-enhanced, expanded content with proper structure]

Section 2: Key Features
Summary: The product offers robust functionality including...
Body: [AI-enhanced content]
...
```

---

## 💡 How It's Different from Simple Copying

**Without AI:**
- Just copies text → No enhancement
- No structure → No sections
- No summaries → Users must read everything

**With AI (SMEPilot):**
- ✅ **Intelligently sections** content
- ✅ **Generates summaries** for quick understanding
- ✅ **Enriches text** - expands and improves
- ✅ **Creates structure** - headings, organization
- ✅ **Standardizes format** - consistent template

---

## 🎯 Summary

**YES, we use Azure OpenAI** for:
1. **Document sectioning** (GPT)
2. **Content summarization** (GPT)
3. **Text enrichment** (GPT)
4. **Semantic search** (Embeddings)

**Currently:** Running in mock mode (no real AI calls)
**Next Step:** Configure Azure OpenAI credentials to enable real AI enrichment

