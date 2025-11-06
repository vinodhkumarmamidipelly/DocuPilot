# CosmosDB Emulator Configuration Guide

## ✅ CosmosDB Emulator Connection String

The **CosmosDB Emulator** uses a fixed connection string:

```
AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jr3g==;
```

**Note:** This is the default emulator connection string. If you changed settings, adjust accordingly.

---

## Step 1: Verify Configuration ✅

**Already Done:** Updated `local.settings.json` with emulator connection string.

**Database & Container:**
- Database: `SMEPilotDB` (created automatically)
- Container: `Embeddings` (created automatically)
- Partition Key: `TenantId` (from your tenant ID)

---

## Step 2: View Data in CosmosDB Emulator

### **Option A: Azure Cosmos DB Emulator Data Explorer** (Easiest)

1. **Open browser:** Navigate to:
   ```
   https://localhost:8081/_explorer/index.html
   ```

2. **SSL Certificate Warning:**
   - If you see a security warning, click **"Advanced"** → **"Proceed to localhost (unsafe)"**
   - This is normal for local emulator

3. **View Data:**
   - Database: `SMEPilotDB` (will appear after first document is inserted)
   - Container: `Embeddings`
   - Click on container → See stored embeddings

### **Option B: Azure Data Studio** (Recommended for Querying)

1. **Install Azure Data Studio:**
   - Download: https://aka.ms/azuredatastudio
   - Install **Cosmos DB extension**: Extensions → Search "Cosmos DB" → Install

2. **Connect to Emulator:**
   - Click **"Add Connection"**
   - Connection Type: **Cosmos DB**
   - Account URI: `https://localhost:8081`
   - Account Key: `C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jr3g==`
   - Database: `SMEPilotDB`
   - Container: `Embeddings`

3. **Query Data:**
   - Open **"New Query"**
   - Run: `SELECT * FROM c`
   - See all embeddings

---

## Step 3: Test Embedding Storage

### **After updating `local.settings.json`:**

1. **Restart Function App** (F5 in Visual Studio)

2. **Upload a document** to SharePoint (or trigger manually)

3. **Check logs** - Should see:
   ```
   ✅ Embedding created and stored for section: [Section Name]
   ```
   (Instead of "Mock: Would upsert embedding...")

4. **Verify in Data Explorer:**
   - Open `https://localhost:8081/_explorer/index.html`
   - Navigate to: `SMEPilotDB` → `Embeddings`
   - Should see documents with structure:
     ```json
     {
       "id": "guid-here",
       "TenantId": "8e8a9805-15bc-44e4-9fd5-8bf2cd01fe09",
       "FileId": "file-id",
       "FileUrl": "https://...",
       "Heading": "Section Title",
       "Summary": "Section summary...",
       "Embedding": [0.123, 0.456, ...], // 1536 numbers
       "SectionIndex": 0
     }
     ```

---

## Step 4: Query Data Manually

### **Using Azure Data Studio or Emulator UI:**

**Query all embeddings:**
```sql
SELECT * FROM c
```

**Query by tenant:**
```sql
SELECT * FROM c WHERE c.TenantId = '8e8a9805-15bc-44e4-9fd5-8bf2cd01fe09'
```

**Query specific file:**
```sql
SELECT * FROM c WHERE c.FileId = 'your-file-id'
```

**Count embeddings:**
```sql
SELECT COUNT(1) as TotalEmbeddings FROM c
```

**View embedding dimensions:**
```sql
SELECT c.id, c.Heading, ARRAY_LENGTH(c.Embedding) as EmbeddingSize FROM c
```

---

## 🎯 Expected Data Structure

Each embedding document contains:
- `id`: Unique identifier (GUID)
- `TenantId`: Your tenant ID (`8e8a9805-15bc-44e4-9fd5-8bf2cd01fe09`) - **Partition Key**
- `FileId`: SharePoint file ID
- `FileUrl`: SharePoint file URL
- `SectionId`: Section identifier
- `Heading`: Section heading
- `Summary`: Section summary (20-40 words)
- `Body`: Section body text
- `Embedding`: Array of **1536 float numbers** (vector from text-embedding-ada-002)
- `CreatedAt`: Timestamp

---

## 🔍 Troubleshooting

### **Emulator Not Starting?**
- Check Windows Firewall settings
- Run as Administrator
- Check if port 8081 is available
- Look for Azure Cosmos DB Emulator icon in system tray

### **Connection String Not Working?**
- Verify emulator is running (system tray icon)
- Try: `https://localhost:8081/_explorer/index.html` in browser
- Check if emulator shows "Running" status
- Restart Function App after updating `local.settings.json`

### **No Data Appearing?**
- Verify `Cosmos_ConnectionString` is set correctly
- Restart Function App after updating `local.settings.json`
- Check Function App logs for errors:
  - Should see: `✅ Embedding created and stored`
  - NOT: `Mock: Would upsert embedding...`
- Upload a new document to trigger embedding creation

### **Database/Container Not Created?**
- Don't worry! They're created automatically on first insert
- Upload a document and embeddings will be stored
- Then check Data Explorer again

---

## ✅ Success Indicators

After configuration:
- ✅ Function App logs show "Embedding created and stored" (not "Mock")
- ✅ Data Explorer shows `SMEPilotDB` database
- ✅ `Embeddings` container exists
- ✅ Documents appear after uploading a file
- ✅ Each document has 1536-dimension embedding array
- ✅ QueryAnswer endpoint returns results (not "No relevant documents found")

---

## 📝 Quick Test Steps

1. **Restart Function App** (F5)
2. **Upload a document** to SharePoint
3. **Check logs** - Look for "Embedding created and stored"
4. **Open Data Explorer:** `https://localhost:8081/_explorer/index.html`
5. **Navigate:** `SMEPilotDB` → `Embeddings` → Click on a document
6. **Verify:** Document has `Embedding` array with 1536 numbers

---

## 🎯 What You'll See

**Before Configuration:**
- Logs: `Mock: Would upsert embedding for...`
- Data Explorer: Empty (no database)

**After Configuration:**
- Logs: `✅ Embedding created and stored for section: [Name]`
- Data Explorer: `SMEPilotDB` → `Embeddings` → Documents with embeddings

---

## 🚀 Next Steps After CosmosDB Setup

1. ✅ Test embedding storage (upload document)
2. ✅ Test QueryAnswer endpoint (should find documents)
3. ✅ Deploy Function App to Azure (production)
4. ✅ Configure Microsoft Search Connector (Copilot integration)

