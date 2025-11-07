# MongoDB Migration Guide - Using MongoDB VM Instead of Cosmos DB

## 🎯 Overview

This guide explains how to switch from Azure Cosmos DB to MongoDB on your VM to save costs.

---

## ✅ What Works

- **Document Storage**: MongoDB stores JSON documents perfectly
- **Basic Operations**: Upsert and query by TenantId work identically
- **Cost Savings**: No Cosmos DB charges
- **Same Interface**: `MongoHelper` has same methods as `CosmosHelper`

---

## ⚠️ Important Considerations

### 1. **Performance Issue (Same as Cosmos DB)**

**Current Implementation:**
- Fetches ALL embeddings for tenant
- Calculates similarity in-memory
- **This is slow with large datasets** (both Cosmos DB and MongoDB have same issue)

**Performance:**
- 100 embeddings: ~200ms ✅
- 1,000 embeddings: ~2-3 seconds ⚠️
- 10,000 embeddings: ~20-30 seconds ❌

**Solution:** Implement vector search index (future optimization)

### 2. **Scalability Limits**

**VM Constraints:**
- Fixed CPU/RAM/disk resources
- No auto-scaling
- Manual upgrades needed as data grows

**Recommendation:** Monitor usage and plan VM upgrades

### 3. **High Availability**

**Single VM = Single Point of Failure:**
- If VM goes down, search stops
- No automatic failover
- Need backup/recovery plan

**Recommendation:** 
- Set up automated backups
- Consider MongoDB replica set (future)

### 4. **Network Connectivity**

**Azure Functions → MongoDB VM:**
- Requires public IP or VPN
- Firewall rules needed
- Security considerations

**Connection String Format:**
```
mongodb://username:password@your-vm-ip:27017/SMEPilotDB?authSource=admin
```

---

## 🚀 Migration Steps

### Step 1: Update Project File

Already done - `MongoDB.Driver` package added to `.csproj`

### Step 2: Update Configuration

**In `local.settings.json` (for local testing):**

```json
{
  "Values": {
    // ... existing settings ...
    
    // MongoDB Configuration (instead of Cosmos DB)
    "Mongo_ConnectionString": "mongodb://username:password@your-vm-ip:27017/SMEPilotDB?authSource=admin",
    "Mongo_Database": "SMEPilotDB",
    "Mongo_Container": "Embeddings",
    
    // Keep Cosmos DB settings commented out or remove
    // "Cosmos_ConnectionString": "...",
  }
}
```

**In Azure Function App Configuration (for production):**

1. Go to Azure Portal → Function App → Configuration
2. Add Application Settings:
   - `Mongo_ConnectionString`: `mongodb://username:password@your-vm-ip:27017/SMEPilotDB?authSource=admin`
   - `Mongo_Database`: `SMEPilotDB`
   - `Mongo_Container`: `Embeddings`

### Step 3: Update Program.cs

**Option A: Use MongoDB Only (Recommended)**

Replace `CosmosHelper` with `MongoHelper`:

```csharp
// In Program.cs
services.AddSingleton<MongoHelper>(); // Instead of CosmosHelper
```

**Option B: Support Both (Flexible)**

Update `Program.cs` to choose based on configuration:

```csharp
var cfg = new Config();

// Use MongoDB if connection string is provided, otherwise Cosmos DB
if (!string.IsNullOrWhiteSpace(cfg.MongoConnectionString))
{
    services.AddSingleton<MongoHelper>();
    // Update functions to use MongoHelper
}
else
{
    services.AddSingleton<CosmosHelper>();
}
```

### Step 4: Update Function Classes

**Update `ProcessSharePointFile.cs`:**

Change:
```csharp
private readonly CosmosHelper _cosmos;
```

To:
```csharp
private readonly MongoHelper _mongo; // or keep _cosmos and use interface
```

**Update `QueryAnswer.cs`:**

Same change - replace `CosmosHelper` with `MongoHelper`

### Step 5: Test Connection

**Test MongoDB connection:**

```powershell
# Test from Azure Function App
# Or test locally with MongoDB connection string
```

---

## 📊 Performance Comparison

| Metric | Cosmos DB | MongoDB VM |
|--------|-----------|------------|
| **Current Implementation** | Fetches all, calculates in-memory | Same (fetches all, calculates in-memory) |
| **Cost** | Pay per RU | VM hosting cost only |
| **Scalability** | Auto-scales | Manual scaling |
| **High Availability** | Built-in | Single VM (no HA) |
| **Vector Search** | Native (not used yet) | Need custom implementation |
| **Maintenance** | Managed | You manage |

---

## 🔧 MongoDB VM Setup Requirements

### 1. **Network Access**

**Option A: Public IP (Easier)**
- MongoDB VM needs public IP
- Configure firewall to allow Azure Functions IP ranges
- Use authentication (username/password)

**Option B: VPN/Private Endpoint (More Secure)**
- Set up VPN between Azure and VM
- MongoDB only accessible via VPN
- More secure but complex setup

### 2. **MongoDB Configuration**

**Enable Authentication:**
```bash
# In MongoDB config
security:
  authorization: enabled
```

**Create User:**
```javascript
use admin
db.createUser({
  user: "smepilot",
  pwd: "your-secure-password",
  roles: [{ role: "readWrite", db: "SMEPilotDB" }]
})
```

**Bind to Network:**
```yaml
# In mongod.conf
net:
  bindIp: 0.0.0.0  # Or specific IP
  port: 27017
```

### 3. **Firewall Rules**

**Allow Azure Functions:**
- Azure Functions IP ranges (check Azure docs)
- Or allow specific Function App outbound IPs

**Test Connection:**
```bash
# From Azure Function App or test machine
mongosh "mongodb://username:password@your-vm-ip:27017/SMEPilotDB?authSource=admin"
```

---

## 🚨 Future Issues & Solutions

### Issue 1: Performance Degradation

**Problem:** As embeddings grow, in-memory calculation becomes slow

**Solution:**
- Implement vector search index (MongoDB 6.0.11+ supports vector search)
- Or use external vector database (Pinecone, Weaviate, Qdrant)

### Issue 2: VM Resource Limits

**Problem:** VM runs out of CPU/RAM as data grows

**Solution:**
- Monitor VM resources
- Upgrade VM size when needed
- Consider MongoDB replica set for read scaling

### Issue 3: Single Point of Failure

**Problem:** VM downtime = service unavailable

**Solution:**
- Set up MongoDB replica set (primary + secondaries)
- Automated backups
- Disaster recovery plan

### Issue 4: Network Latency

**Problem:** Azure Functions → VM might have latency

**Solution:**
- Use Azure VM in same region as Function App
- Or use VPN for better performance

---

## ✅ Migration Checklist

- [ ] MongoDB VM accessible from Azure Functions
- [ ] MongoDB authentication configured
- [ ] Firewall rules configured
- [ ] Connection string tested
- [ ] `MongoHelper.cs` added to project
- [ ] `Program.cs` updated to use `MongoHelper`
- [ ] Function classes updated
- [ ] `local.settings.json` updated
- [ ] Azure Function App configuration updated
- [ ] Test upsert operation
- [ ] Test query operation
- [ ] Monitor performance

---

## 🎯 Recommendation

**For Now (Cost Savings):**
✅ Use MongoDB VM - works fine for small/medium scale

**For Future (Production Scale):**
- Monitor performance
- Implement vector search when needed
- Consider MongoDB Atlas (managed) if VM becomes limiting
- Or optimize current approach with vector indexes

---

## 📝 Notes

- `MongoHelper` maintains same interface as `CosmosHelper`
- No changes needed to data model (`EmbeddingDocument`)
- Same performance characteristics (both fetch all, calculate in-memory)
- Cost savings: No Cosmos DB charges
- Trade-off: You manage MongoDB (updates, backups, monitoring)

