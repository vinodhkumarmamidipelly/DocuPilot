# Code Changes Required - Simplified Requirements

## 📋 What Needs to Change

Based on manager's feedback:
- ❌ No database required
- ❌ No AI enrichment required (just template formatting)
- ✅ Template formatting only

---

## 🔧 Required Code Changes

### **1. Remove Database/Embedding Code**

**File:** `SMEPilot.FunctionApp/Functions/ProcessSharePointFile.cs`

**Remove Lines 607-641:**
```csharp
// 7. Create embeddings and store
Console.WriteLine($"🔍 [ENRICHMENT] Creating embeddings for {docModel.Sections.Count} sections...");
var embeddingCount = 0;
foreach (var s in docModel.Sections)
{
    // ... embedding generation code ...
    await _embeddingStore.UpsertEmbeddingAsync(embDoc);
}
```

**Action:** Remove entire embedding creation/storage block

---

### **2. Remove AI Enrichment Call**

**File:** `SMEPilot.FunctionApp/Functions/ProcessSharePointFile.cs`

**Remove Line 520 (in Hybrid Mode):**
```csharp
// Step 3: AI enrichment of section content only (minimal AI usage)
docModel = await _hybridEnricher.EnrichSectionsAsync(docModel, imageOcrs);
```

**Action:** Remove AI enrichment call, keep only sectioning

---

### **3. Simplify HybridEnricher**

**File:** `SMEPilot.FunctionApp/Helpers/HybridEnricher.cs`

**Options:**
- **Option A:** Remove `EnrichSectionsAsync()` method entirely
- **Option B:** Make it optional (skip if no AI configured)

**Keep:**
- `SectionDocument()` - Rule-based sectioning
- `ClassifyDocument()` - Keyword-based classification

---

### **4. Update ProcessSharePointFile Constructor**

**File:** `SMEPilot.FunctionApp/Functions/ProcessSharePointFile.cs`

**Current:**
```csharp
private readonly IEmbeddingStore _embeddingStore;
private readonly OpenAiHelper _openai;
```

**Change to:**
```csharp
// Make optional - not needed for template-only mode
private readonly IEmbeddingStore? _embeddingStore;
private readonly OpenAiHelper? _openai;
```

**Or remove entirely if not needed**

---

### **5. Update Program.cs**

**File:** `SMEPilot.FunctionApp/Program.cs`

**Remove or Make Optional:**
- Database service registration (MongoDB/Cosmos DB)
- Or keep but make optional (only register if connection string exists)

**Keep:**
- GraphHelper
- SimpleExtractor
- TemplateBuilder
- HybridEnricher (for sectioning only)

---

### **6. Update Configuration**

**File:** `SMEPilot.FunctionApp/local.settings.json`

**Remove (if not needed):**
- `AzureOpenAI_*` (if no AI enrichment)
- `Mongo_*` or `Cosmos_*` (if no database)

**Keep:**
- `Graph_*` (required)
- `EnrichedFolderRelativePath` (required)

---

## ✅ What Stays the Same

### **Keep These (Core Functionality):**
- ✅ `SimpleExtractor` - Extract text + images
- ✅ `HybridEnricher.SectionDocument()` - Rule-based sectioning
- ✅ `TemplateBuilder` - Apply template
- ✅ `GraphHelper` - SharePoint operations
- ✅ Webhook subscription - Automatic trigger
- ✅ Metadata update - SharePoint only

---

## 📝 Simplified Processing Flow

### **After Changes:**

```
1. Webhook notification
         ↓
2. Download document
         ↓
3. Extract text + images
         ↓
4. Rule-based sectioning (no AI)
         ↓
5. Apply template (formatting only)
         ↓
6. Upload formatted document
         ↓
7. Update metadata (SharePoint only)
         ↓
Done! (No embeddings, no AI enrichment)
```

---

## 🎯 Summary of Changes

### **Must Remove:**
1. ❌ Embedding generation code (lines 607-641)
2. ❌ AI enrichment call (line 520 in Hybrid Mode)
3. ❌ Database dependencies (optional - can keep for future)

### **Must Keep:**
1. ✅ Document extraction
2. ✅ Rule-based sectioning
3. ✅ Template application
4. ✅ SharePoint upload
5. ✅ Metadata update

### **Can Simplify:**
1. ⏳ Remove `_embeddingStore` dependency (or make optional)
2. ⏳ Remove `_openai` dependency (or make optional)
3. ⏳ Simplify `HybridEnricher` (remove AI enrichment method)

---

## ✅ Status

**Current:** Code has database and AI enrichment
**Required:** Remove database/embedding code, remove AI enrichment
**Result:** Template formatting only

**Ready to implement these changes?**

