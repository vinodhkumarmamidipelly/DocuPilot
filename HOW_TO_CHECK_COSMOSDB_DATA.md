# How to Check CosmosDB Data

## ✅ Quick Method: Azure Cosmos DB Emulator Data Explorer

### **Step 1: Open Data Explorer**

1. Open your web browser
2. Navigate to:
   ```
   https://localhost:8081/_explorer/index.html
   ```

3. **If you see SSL warning:**
   - Click **"Advanced"** or **"Show Details"**
   - Click **"Proceed to localhost (unsafe)"** or **"Accept Risk and Continue"**
   - This is normal for local emulator - it uses a self-signed certificate

### **Step 2: Navigate to Your Data**

1. In the left sidebar, you'll see:
   ```
   📊 Databases
      └─ SMEPilotDB        ← Your database (created automatically)
           └─ Embeddings  ← Your container (created automatically)
   ```

2. **Click on `Embeddings`** to see all stored documents

### **Step 3: View Documents**

1. **List View:** You'll see a list of documents (one per section)
   - Each row shows: `id`, `TenantId`, `FileId`, `Heading`, etc.

2. **Click on any document** to see full details:
   ```json
   {
     "id": "unique-guid-here",
     "TenantId": "8e8a9805-15bc-44e4-9fd5-8bf2cd01fe09",
     "FileId": "sharepoint-file-id",
     "FileUrl": "https://onblick.sharepoint.com/...",
     "SectionId": "s1",
     "Heading": "Section Title",
     "Summary": "Section summary text...",
     "Body": "Full section body text...",
     "Embedding": [0.123, 0.456, 0.789, ...],  // 1536 numbers
     "CreatedAt": "2025-11-05T15:30:00Z"
   }
   ```

### **Step 4: Verify Embeddings Were Stored**

✅ **Success Indicators:**
- ✅ Database `SMEPilotDB` exists
- ✅ Container `Embeddings` exists
- ✅ You see documents listed (one per section of your document)
- ✅ Each document has an `Embedding` field with 1536 numbers
- ✅ `FileUrl` points to your SharePoint document

❌ **If Empty:**
- Check Function App logs - should see `✅ [COSMOS] CosmosDB client initialized successfully`
- Check if you see `Embedding created and stored` (not "Mock")
- Verify CosmosDB Emulator is running (system tray icon)

---

## 🔍 Alternative: Query Using SQL

### **In Data Explorer:**

1. Click on `Embeddings` container
2. Click **"New SQL Query"** tab at the top
3. Run these queries:

**See all documents:**
```sql
SELECT * FROM c
```

**Count total embeddings:**
```sql
SELECT COUNT(1) as Total FROM c
```

**See documents by file:**
```sql
SELECT c.id, c.Heading, c.FileUrl, ARRAY_LENGTH(c.Embedding) as EmbeddingSize 
FROM c 
WHERE c.FileUrl LIKE '%your-filename%'
```

**See all headings:**
```sql
SELECT c.Heading, c.Summary 
FROM c
```

---

## 📊 What Each Document Represents

Each document in CosmosDB = **One section** from your enriched document.

For example, if your document has 10 sections, you'll see **10 documents** in CosmosDB, each with:
- Unique `id` (GUID)
- Same `FileId` and `FileUrl` (linking back to original document)
- Different `SectionId` (s1, s2, s3, ...)
- Different `Heading`, `Summary`, `Body`
- Different `Embedding` (vector representing that section's content)

---

## 🎯 Quick Checklist

- [ ] CosmosDB Emulator is running (system tray icon)
- [ ] Opened `https://localhost:8081/_explorer/index.html`
- [ ] See `SMEPilotDB` database
- [ ] See `Embeddings` container
- [ ] Clicked on `Embeddings` - see list of documents
- [ ] Clicked on a document - see full JSON with `Embedding` array
- [ ] Verified `Embedding` array has 1536 numbers

---

## 🚀 Next Step: Test QueryAnswer

Once you see data in CosmosDB, test the QueryAnswer endpoint:

```powershell
$queryBody = @{
    tenantId = "8e8a9805-15bc-44e4-9fd5-8bf2cd01fe09"
    question = "What is this document about?"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:7071/api/QueryAnswer" -Method Post -Body $queryBody -ContentType "application/json"
```

**Expected:** Should return an answer with sources (file URLs), not "No relevant documents found"

