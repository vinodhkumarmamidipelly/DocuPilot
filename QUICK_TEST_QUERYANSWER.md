# Quick Test: QueryAnswer with MongoDB

## 🎯 Goal
Test if QueryAnswer function can retrieve embeddings from MongoDB and return answers.

---

## ✅ Prerequisites

1. **Function App Running** - Should be running on `http://localhost:7071`
2. **MongoDB Connected** - Check startup logs for `✅ [MONGO] MongoDB client initialized successfully`
3. **Embeddings Stored** - At least one document should be processed (embeddings stored in MongoDB)

---

## 🚀 Quick Test Methods

### **Method 1: PowerShell Script (Easiest)**

**Run the test script:**
```powershell
.\TEST_QUERYANSWER_MONGODB.ps1
```

**Or with custom URL (if using ngrok):**
```powershell
.\TEST_QUERYANSWER_MONGODB.ps1 -FunctionAppUrl "https://a5fb7edc07fe.ngrok-free.app"
```

---

### **Method 2: PowerShell One-Liner**

**Test with a simple question:**
```powershell
$body = @{
    tenantId = "default"
    question = "What are the types of alerts?"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:7071/api/QueryAnswer" -Method Post -Body $body -ContentType "application/json"
```

---

### **Method 3: Using curl (if available)**

```bash
curl -X POST http://localhost:7071/api/QueryAnswer \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"default","question":"What are the types of alerts?"}'
```

---

### **Method 4: Using Postman or Browser Extension**

**URL:** `http://localhost:7071/api/QueryAnswer`  
**Method:** `POST`  
**Headers:** `Content-Type: application/json`  
**Body:**
```json
{
  "tenantId": "default",
  "question": "What are the types of alerts?"
}
```

---

## 📋 Expected Response

**Success Response:**
```json
{
  "answer": "There are two types of alerts: Immediate alerts and Scheduled alerts...",
  "sources": [
    {
      "fileUrl": "https://.../Alerts_enriched.docx",
      "heading": "Types of Alerts",
      "score": 0.856
    },
    {
      "fileUrl": "https://.../Alerts_enriched.docx",
      "heading": "Alert Triggering Mechanism",
      "score": 0.742
    }
  ]
}
```

---

## 🔍 What to Check

### **1. Check MongoDB Has Data**

**In MongoDB (via Robo 3T or mongosh):**
```javascript
use SMEPilotDb
db.Embeddings.find().count()
```

**Should show:** Number of embeddings stored (should be > 0)

**Check a sample document:**
```javascript
db.Embeddings.findOne()
```

**Should show:** Document with `TenantId`, `Heading`, `Embedding` array, etc.

---

### **2. Check Function App Logs**

**When you call QueryAnswer, check logs for:**
```
🔍 [MONGO] Retrieved X embeddings for tenant default
✅ [MONGO] Retrieved X embeddings for tenant default
```

**If you see:**
```
Mock: Would fetch embeddings for tenant default
```
**This means MongoDB connection failed - check startup logs.**

---

### **3. Verify Tenant ID**

**Important:** The `tenantId` in your query must match the `TenantId` used when processing documents.

**From your processed documents, check what TenantId was used:**
- In MongoDB: `db.Embeddings.distinct("TenantId")`
- In Function App logs: Look for `TenantId: ...` when processing

**Common values:**
- `"default"` - If no tenant was specified
- `"8e8a9805-15bc-44e4-9fd5-8bf2cd01fe09"` - Your Graph tenant ID

---

## 🐛 Troubleshooting

### **Issue: "No relevant documents found"**

**Possible causes:**
1. **No embeddings in MongoDB** - Process a document first
2. **Wrong TenantId** - Check what TenantId was used when processing
3. **MongoDB query failing** - Check Function App logs

**Solution:**
```powershell
# Check MongoDB has data
# In Robo 3T or mongosh:
use SMEPilotDb
db.Embeddings.find().pretty()

# Check TenantId used
db.Embeddings.distinct("TenantId")
```

---

### **Issue: "Authorization required or tenantId must be provided"**

**Solution:** Make sure you're sending `tenantId` in the request body:
```json
{
  "tenantId": "default",
  "question": "your question"
}
```

---

### **Issue: MongoDB connection failed**

**Check startup logs for:**
```
❌ [MONGO] Failed to initialize MongoDB client
```

**Solution:**
- Verify MongoDB connection string in `local.settings.json`
- Check MongoDB server is accessible
- Verify credentials are correct

---

## ✅ Success Indicators

1. ✅ **Function App logs show:** `✅ [MONGO] Retrieved X embeddings for tenant default`
2. ✅ **Response contains:** `answer` and `sources` array
3. ✅ **Sources have scores:** Similarity scores between 0 and 1
4. ✅ **Answer is relevant:** Answer relates to the question asked

---

## 🎯 Test Questions to Try

1. **"What are the types of alerts?"**
2. **"How are alerts triggered?"**
3. **"What is SignalR used for?"**
4. **"How is data stored in Elastic DB?"**
5. **"What are the API endpoints for alerts?"**

---

## 📝 Notes

- **First query might be slow** - MongoDB needs to fetch all embeddings
- **Scores indicate relevance** - Higher scores (closer to 1.0) = more relevant
- **TenantId must match** - Use the same TenantId that was used when processing documents

---

**Ready to test? Run:**
```powershell
.\TEST_QUERYANSWER_MONGODB.ps1
```

