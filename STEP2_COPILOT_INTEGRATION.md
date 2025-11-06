# Step 2: O365 Copilot Integration - Complete Guide

## 🎯 Goal

Make your enriched SMEPilot documents searchable by O365 Copilot in Teams, so employees can ask questions and get answers from your documentation.

---

## ✅ Prerequisites

1. ✅ **Backend working** - QueryAnswer endpoint tested and working
2. ✅ **Documents enriched** - Enriched documents in SharePoint `ProcessedDocs` folder
3. ✅ **Embeddings stored** - CosmosDB has embeddings for semantic search
4. ⚠️ **Admin Access** - You need Microsoft 365 Admin Center access

---

## 🔧 Method 1: Microsoft Search Connector (Recommended)

### **How It Works:**

```
Enriched Documents in SharePoint
    ↓
Microsoft Search Connector indexes them
    ↓
Microsoft Search (used by Copilot) finds them
    ↓
Employee asks question in Teams
    ↓
Copilot searches Microsoft Search
    ↓
Finds SMEPilot documents
    ↓
Uses QueryAnswer API for precise answers
```

---

## 📋 Step-by-Step Configuration

### **Step 1: Access Microsoft Search Admin Center**

1. **Go to:** https://admin.microsoft.com/Adminportal/Home#/MicrosoftSearch
   - Or: Admin Center → Settings → Search & intelligence → Microsoft Search

2. **Sign in** with admin credentials

---

### **Step 2: Verify SharePoint Documents Are Indexed**

**Good News:** SharePoint documents are **automatically indexed** by Microsoft Search by default!

**Verify:**
1. In Microsoft Search Admin Center, go to **"Indexing"** or **"Content sources"**
2. You should see **"SharePoint"** listed as a content source
3. Your SharePoint site should be included

**If SharePoint is NOT indexed:**
1. Go to **"Content sources"**
2. Click **"Add content source"**
3. Select **"SharePoint"**
4. Add your site: `https://onblick.sharepoint.com/sites/DocEnricher-PoC`
5. Save

---

### **Step 3: Verify Copilot Can Access SharePoint**

**Test in Teams:**

1. **Open Microsoft Teams**
2. **Click "Copilot"** (or use `/copilot` command)
3. **Ask a test question:**
   ```
   "What documents are in the DocEnricher-PoC site?"
   ```

**Expected:** Copilot should mention your SharePoint documents

**If Copilot says "I don't have access":**
- Check SharePoint site permissions
- Ensure site is indexed (Step 2)
- Wait 24-48 hours for indexing to complete

---

### **Step 4: Test QueryAnswer Integration**

**Option A: Direct API Call (For Testing)**

Since QueryAnswer is working, you can test it directly:

```powershell
$queryBody = @{
    tenantId = "default"
    question = "What are the types of alerts?"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:7071/api/QueryAnswer" -Method Post -Body $queryBody -ContentType "application/json"
```

**Option B: Via Teams/Copilot (After Indexing)**

Once documents are indexed:
1. Open Teams
2. Ask Copilot: "What are the types of alerts?"
3. Copilot should reference your SMEPilot documents

---

## 🔧 Method 2: Custom Search Connector (Advanced)

If you need more control, create a custom connector:

### **Step 1: Create Custom Connector**

1. **Go to:** Microsoft Search Admin Center → **"Connectors"** → **"Add connector"**
2. **Select:** "Custom connector" or "SharePoint Online"
3. **Configure:**
   - **Name:** SMEPilot Documents
   - **Source:** SharePoint site URL
   - **Scope:** `/sites/DocEnricher-PoC/Shared Documents/ProcessedDocs`
   - **Schedule:** Daily or hourly refresh

### **Step 2: Map Fields**

Map SharePoint columns to Search properties:
- `Title` → Search Title
- `Content` → Searchable Content
- `SMEPilot_Enriched` → Custom Property
- `FileUrl` → URL

### **Step 3: Test Connection**

1. Click **"Test connection"**
2. Verify documents are found
3. Save connector

---

## 🧪 Testing Copilot Integration

### **Test 1: Basic Search**

**In Teams, ask Copilot:**
```
"Show me documents about alerts"
```

**Expected:** Copilot lists your enriched documents

---

### **Test 2: Specific Question**

**In Teams, ask Copilot:**
```
"What are the types of alerts?"
```

**Expected:** 
- Copilot finds relevant documents
- Provides answer based on content
- Shows source links

---

### **Test 3: Verify QueryAnswer is Used**

**Check Function App logs** when asking Copilot:
- Should see `QueryAnswer` endpoint being called
- Should see tenant detection working
- Should see embeddings being queried

---

## 🔍 Troubleshooting

### **Issue: Copilot says "I don't have access"**

**Solutions:**
1. **Check site permissions:**
   - Site must be accessible to all employees (or Copilot service account)
   - Go to Site Settings → Site Permissions
   - Ensure "Everyone" or appropriate groups have read access

2. **Wait for indexing:**
   - SharePoint indexing can take 24-48 hours
   - Check indexing status in Search Admin Center

3. **Verify site is indexed:**
   - Admin Center → Search & intelligence → Content sources
   - Ensure your SharePoint site is listed

---

### **Issue: Copilot finds documents but answers are generic**

**This is expected** - Copilot uses basic search, not semantic search.

**Solution:** 
- QueryAnswer endpoint provides semantic search (already working!)
- For full integration, you'd need a Teams Bot or Copilot extension
- For now, documents are searchable, which is the main goal

---

### **Issue: Documents not appearing in search**

**Check:**
1. **Document location:** Are they in `ProcessedDocs` folder?
2. **File permissions:** Are documents readable by all employees?
3. **Indexing status:** Check Search Admin Center for errors
4. **File format:** Ensure `.docx` files are supported (they are by default)

---

## 🎯 Alternative: Teams Bot (Future Enhancement)

For **direct QueryAnswer integration** via Teams, you'd need:

1. **Create Teams Bot** using Bot Framework
2. **Bot receives messages** from Teams
3. **Bot calls QueryAnswer API** with user's question
4. **Bot returns answer** to Teams

**This is Phase 3** - For now, Microsoft Search Connector is sufficient.

---

## ✅ Success Criteria

**You'll know it's working when:**

1. ✅ **Documents appear in Microsoft Search**
   - Search for document title in SharePoint
   - Should find enriched documents

2. ✅ **Copilot can find documents**
   - Ask Copilot: "Show me SMEPilot documents"
   - Should list your documents

3. ✅ **Copilot provides answers**
   - Ask specific questions
   - Should reference document content

4. ✅ **QueryAnswer API is ready**
   - Already tested and working ✅
   - Can be called directly or via future Teams Bot

---

## 📊 Current Status

| Component | Status |
|-----------|--------|
| **QueryAnswer API** | ✅ Working |
| **Embeddings Storage** | ✅ Working |
| **SharePoint Documents** | ✅ Enriched |
| **Microsoft Search Indexing** | ⏳ Needs verification |
| **Copilot Access** | ⏳ Needs testing |

---

## 🚀 Next Steps After Configuration

1. **Wait 24-48 hours** for indexing to complete
2. **Test in Teams** - Ask Copilot questions
3. **Verify documents appear** in search results
4. **Monitor Function App logs** - Check if QueryAnswer is called
5. **Gather feedback** - Test with other employees

---

## 📝 Quick Checklist

- [ ] Access Microsoft Search Admin Center
- [ ] Verify SharePoint is indexed
- [ ] Test Copilot in Teams
- [ ] Ask test questions
- [ ] Verify documents appear in search
- [ ] Check Function App logs for QueryAnswer calls
- [ ] Document any issues for troubleshooting

---

## 🎉 Expected Result

**Employee in Teams:**
> "What are the types of alerts?"

**Copilot:**
> "Based on SMEPilot documentation, there are different types of alerts including immediate alerts and scheduled alerts. [Detailed answer]"
> 
> Source: [Link to enriched document]

---

**Ready to configure? Start with Step 1 above!**

