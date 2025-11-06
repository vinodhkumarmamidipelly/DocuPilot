# Step 2: O365 Copilot Integration - Quick Start

## ⚡ Fast Track (5 Minutes)

### **Step 1: Verify SharePoint Indexing**

1. **Go to:** https://admin.microsoft.com/Adminportal/Home#/MicrosoftSearch
2. **Check:** "Content sources" → Should see "SharePoint" listed
3. **Verify:** Your site `https://onblick.sharepoint.com/sites/DocEnricher-PoC` is included

**If not listed:**
- Click "Add content source" → Select "SharePoint" → Add your site URL → Save

---

### **Step 2: Test in Teams**

1. **Open Microsoft Teams**
2. **Click "Copilot"** (or type `/copilot`)
3. **Ask:**
   ```
   "Show me documents in DocEnricher-PoC site"
   ```

**Expected:** Copilot lists your documents

---

### **Step 3: Test Specific Question**

**In Teams, ask Copilot:**
```
"What are the types of alerts?"
```

**Expected:** 
- Copilot finds your enriched documents
- Provides answer based on content
- Shows source links

---

## ✅ That's It!

**If Copilot can find your documents, integration is working!**

---

## 🔍 Troubleshooting

### **Copilot says "I don't have access"**

1. **Check site permissions:**
   - Site Settings → Site Permissions
   - Ensure "Everyone" or employee groups have read access

2. **Wait 24-48 hours** for indexing to complete

3. **Verify site is indexed:**
   - Admin Center → Search & intelligence → Content sources
   - Ensure SharePoint site is listed

---

## 📊 What's Working

- ✅ **QueryAnswer API** - Semantic search working
- ✅ **Embeddings** - Stored in CosmosDB
- ✅ **Documents** - Enriched and in SharePoint
- ⏳ **Copilot Access** - Needs indexing (automatic, but takes time)

---

## 🎯 Next: Test and Verify

1. **Wait 24-48 hours** for full indexing
2. **Test in Teams** with various questions
3. **Share with team** to test org-wide access

---

**That's the quick version! For detailed steps, see `STEP2_COPILOT_INTEGRATION.md`**

