# Fix CosmosDB Connection & View Data

## 🔴 Current Issue

1. **CosmosDB Connection Failing** - Logs show:
   - `FormatException: The input is not a valid Base-64 string`
   - `AccountKey Length: 90` (should be 88)
   - `Mock: Would upsert embedding...` (NOT actually storing)

2. **Data Not Visible** - You're on Quickstart page, not Data Explorer

---

## ✅ Step 1: Fix Connection String

**Updated `local.settings.json`** to match your emulator's AccountKey:
```
AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==
```

**If still failing, try this format (with semicolon at end):**
```json
"Cosmos_ConnectionString": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;"
```

---

## ✅ Step 2: Navigate to Data Explorer

**You're currently on "Quickstart" page - need to go to "Explorer":**

1. In the browser where CosmosDB Emulator is open
2. **Click "Explorer"** in the left sidebar (under "Quickstart")
3. You should see:
   ```
   Databases
      └─ SMEPilotDB
           └─ Embeddings
   ```

4. **Click on "Embeddings"** to see documents

---

## ✅ Step 3: Restart Function App

After updating `local.settings.json`:

1. **Stop Function App** (Shift+F5 in Visual Studio)
2. **Start again** (F5)
3. **Check logs** - Should see:
   ```
   ✅ [COSMOS] CosmosDB client initialized successfully
   ```
   (NOT `❌ Failed to initialize`)

---

## ✅ Step 4: Process a New Document

**Important:** Documents processed while CosmosDB was failing won't be in DB.

1. **Upload a NEW document** to SharePoint
2. **Wait for processing** (check logs)
3. **Look for:** 
   ```
   ✅ Embedding created and stored for section: [Name]
   ```
   (NOT `Mock: Would upsert embedding...`)

4. **Refresh Data Explorer** - Click refresh button or navigate to `Embeddings` again

---

## 🎯 Expected Results

**After fix, logs should show:**
```
✅ [COSMOS] CosmosDB client initialized successfully
✅ Embedding created and stored for section: [Name]
```

**Data Explorer should show:**
- Database: `SMEPilotDB`
- Container: `Embeddings`
- Documents: List of embedding documents (one per section)

**Each document contains:**
```json
{
  "id": "guid",
  "TenantId": "8e8a9805-15bc-44e4-9fd5-8bf2cd01fe09",
  "FileUrl": "https://...",
  "Heading": "Section Title",
  "Summary": "...",
  "Embedding": [0.123, 0.456, ...] // 1536 numbers
}
```

---

## 🔍 Troubleshooting

**If connection still fails:**

1. **Check AccountKey in emulator:**
   - Look at Quickstart page
   - Copy the exact `Primary Key` value
   - Make sure it matches in `local.settings.json`

2. **Try alternative format:**
   Use separate endpoint/key instead of connection string (code update needed)

3. **Verify emulator is running:**
   - Check system tray for CosmosDB icon
   - Should be green/running

4. **Check for extra spaces:**
   - In `local.settings.json`, make sure no spaces before/after connection string
   - No line breaks

---

## 📝 Quick Checklist

- [ ] Updated `local.settings.json` with correct AccountKey
- [ ] Restarted Function App
- [ ] Logs show `✅ [COSMOS] CosmosDB client initialized successfully`
- [ ] Clicked "Explorer" tab (not Quickstart)
- [ ] Navigated to `SMEPilotDB` → `Embeddings`
- [ ] Uploaded new document and verified storage
- [ ] See documents with `Embedding` arrays

---

## 🚀 Next: Test QueryAnswer

Once data is stored, test the QueryAnswer endpoint to verify semantic search works!

