# SMEPilot - Simplified Requirements

## 📋 Updated Requirements

### **What's Required:**
1. ✅ **Template Formatting Only** - Convert scratch documents to organizational template
2. ✅ **No Database** - Work without any database
3. ✅ **No AI Enrichment** - Just formatting and styling
4. ✅ **Copilot Ready** - Once formatted, Copilot can use them via SharePoint search

### **What's NOT Required:**
- ❌ Database (MongoDB/Cosmos DB)
- ❌ AI content enrichment
- ❌ Semantic search/embeddings
- ❌ Custom QueryAnswer endpoint

---

## 🎯 Simplified Workflow

```
User uploads scratch document
         ↓
Webhook notification
         ↓
Extract content (text + images)
         ↓
Apply organizational template
         ↓
Format sections (rule-based)
         ↓
Upload formatted document
         ↓
Update SharePoint metadata
         ↓
Copilot can search (via SharePoint)
```

---

## ✅ What We Need to Change

### **1. Remove Database Dependencies**
- Remove MongoDB/Cosmos DB code
- Remove embedding generation
- Remove embedding storage
- Use SharePoint metadata only

### **2. Remove AI Enrichment**
- Remove Azure OpenAI calls
- Remove content expansion
- Keep only template formatting
- Keep rule-based sectioning

### **3. Simplify Processing**
- Extract → Template → Upload
- No embeddings
- No AI enrichment
- Just formatting

---

## 🚀 Will It Work?

**Yes!** The simplified approach will work:

1. ✅ **Template Application** - Already implemented in `TemplateBuilder`
2. ✅ **Rule-Based Sectioning** - Already implemented in `HybridEnricher.SectionDocument()`
3. ✅ **No Database** - Can remove database code
4. ✅ **No AI** - Can remove AI enrichment code
5. ✅ **Copilot Integration** - SharePoint's native search will work with formatted documents

**Result:** Simpler, faster, cheaper solution that meets the actual requirement!

---

## 📝 Next Steps

1. **Remove database code** from ProcessSharePointFile
2. **Remove AI enrichment** - keep only template formatting
3. **Simplify HybridEnricher** - remove AI calls, keep sectioning
4. **Test template application** - verify formatting works
5. **Verify Copilot can search** - test with formatted documents

---

**This simplified approach is actually better - simpler, faster, and meets the real requirement!**

