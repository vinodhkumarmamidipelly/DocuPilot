# Database Switching Guide - MongoDB vs Cosmos DB

## 🎯 Overview

The application now supports **both MongoDB and Cosmos DB** with easy switching via configuration. No code changes needed!

---

## ✅ Architecture

**Interface-Based Design:**
- `IEmbeddingStore` interface defines the contract
- `MongoHelper` implements MongoDB (for testing)
- `CosmosHelper` implements Cosmos DB (for production)
- Functions use `IEmbeddingStore` - don't care which implementation

**Automatic Selection:**
- `Program.cs` automatically chooses based on configuration
- Priority: MongoDB connection string > Cosmos DB connection string

---

## 🔧 Configuration

### For Testing (MongoDB)

**In `local.settings.json`:**
```json
{
  "Values": {
    // MongoDB Configuration (for testing)
    "Mongo_ConnectionString": "mongodb://username:password@your-vm-ip:27017/SMEPilotDB?authSource=admin",
    "Mongo_Database": "SMEPilotDB",
    "Mongo_Container": "Embeddings",
    
    // Comment out or remove Cosmos DB settings
    // "Cosmos_ConnectionString": "...",
  }
}
```

**Result:** Application uses MongoDB ✅

---

### For Production (Cosmos DB)

**In Azure Function App Configuration:**
```
Cosmos_ConnectionString = AccountEndpoint=https://your-account.documents.azure.com:443/;AccountKey=...
Cosmos_Database = SMEPilotDB
Cosmos_Container = Embeddings

// Don't set Mongo_ConnectionString
```

**Result:** Application uses Cosmos DB ✅

---

## 🔄 How Switching Works

### Priority Order:

1. **If `Mongo_ConnectionString` is set** → Uses MongoDB
2. **Else if `Cosmos_ConnectionString` is set** → Uses Cosmos DB
3. **Else** → Uses CosmosHelper in mock mode (for local development)

### Code Flow:

```csharp
// Program.cs automatically selects:
var cfg = new Config();
if (!string.IsNullOrWhiteSpace(cfg.MongoConnectionString))
{
    services.AddSingleton<IEmbeddingStore, MongoHelper>(); // MongoDB
}
else if (!string.IsNullOrWhiteSpace(cfg.CosmosConnectionString))
{
    services.AddSingleton<IEmbeddingStore, CosmosHelper>(); // Cosmos DB
}
```

**Functions use interface:**
```csharp
// ProcessSharePointFile.cs and QueryAnswer.cs
private readonly IEmbeddingStore _embeddingStore; // Works with both!
```

---

## 📋 Switching Steps

### From MongoDB (Testing) → Cosmos DB (Production)

**Step 1:** Remove MongoDB configuration
```json
// Remove or comment out:
// "Mongo_ConnectionString": "...",
```

**Step 2:** Add Cosmos DB configuration
```json
"Cosmos_ConnectionString": "AccountEndpoint=https://...",
"Cosmos_Database": "SMEPilotDB",
"Cosmos_Container": "Embeddings"
```

**Step 3:** Restart Function App
- Application automatically switches to Cosmos DB
- No code changes needed!

---

### From Cosmos DB → MongoDB

**Step 1:** Add MongoDB configuration
```json
"Mongo_ConnectionString": "mongodb://...",
"Mongo_Database": "SMEPilotDB",
"Mongo_Container": "Embeddings"
```

**Step 2:** Remove Cosmos DB configuration (or keep for fallback)
```json
// Comment out:
// "Cosmos_ConnectionString": "...",
```

**Step 3:** Restart Function App
- Application automatically switches to MongoDB

---

## 🎯 Current Setup (Testing)

**You're using MongoDB for testing:**

```json
{
  "Values": {
    "Mongo_ConnectionString": "mongodb://username:password@your-vm-ip:27017/SMEPilotDB?authSource=admin",
    "Mongo_Database": "SMEPilotDB",
    "Mongo_Container": "Embeddings"
  }
}
```

**Console Output:**
```
🔧 [CONFIG] Using MongoDB for embedding storage (testing mode)
🔍 [MONGO] Initializing MongoDB connection...
✅ [MONGO] MongoDB client initialized successfully
```

---

## 🚀 Future Production Setup

**When ready for production, just update configuration:**

**In Azure Function App → Configuration:**
```
Remove: Mongo_ConnectionString
Add: Cosmos_ConnectionString = AccountEndpoint=https://...
```

**That's it!** Application automatically switches to Cosmos DB.

---

## ✅ Benefits

1. **Easy Switching:** Just change configuration, no code changes
2. **Cost Savings:** Use MongoDB for testing (free)
3. **Production Ready:** Switch to Cosmos DB when needed
4. **No Code Changes:** Functions use interface, don't care about implementation
5. **Both Available:** Can test both side-by-side if needed

---

## 📝 Notes

- **Data Migration:** When switching, data doesn't automatically migrate
- **Same Data Model:** Both use same `EmbeddingDocument` model
- **Performance:** Both have same performance characteristics (fetch all, calculate in-memory)
- **Future:** Can optimize both with vector search when needed

---

## 🔍 Verification

**Check which database is being used:**

Look at Function App startup logs:
```
🔧 [CONFIG] Using MongoDB for embedding storage (testing mode)
```
OR
```
🔧 [CONFIG] Using Cosmos DB for embedding storage (production mode)
```

**Test operations:**
- Upload document → Check logs for `[MONGO]` or `[COSMOS]` messages
- Query answer → Should work with either database

---

## ✅ Summary

- ✅ **MongoDB for testing** (current setup)
- ✅ **Cosmos DB for production** (future)
- ✅ **Easy switching** via configuration
- ✅ **No code changes** needed
- ✅ **Both implementations** ready to use

