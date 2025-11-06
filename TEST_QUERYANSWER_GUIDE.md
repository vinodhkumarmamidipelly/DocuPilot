# How to Test QueryAnswer Endpoint

## ✅ Prerequisites

1. **Function App is running** (F5 in Visual Studio)
2. **CosmosDB has data** (You've already verified this! ✅)
3. **Embeddings are stored** (Visible in Data Explorer ✅)

---

## 🚀 Quick Test (PowerShell)

### **Option 1: Use the Test Script**

1. **Open PowerShell** in project directory:
   ```powershell
   cd D:\CodeBase\DocuPilot
   ```

2. **Run the test script**:
   ```powershell
   .\TEST_QUERYANSWER.ps1
   ```

This will run 3 test queries automatically.

---

### **Option 2: Manual Test (Single Query)**

**Open PowerShell** and run:

```powershell
$queryBody = @{
    tenantId = "default"
    question = "What are the types of alerts?"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:7071/api/QueryAnswer" -Method Post -Body $queryBody -ContentType "application/json"
```

**Expected Output:**
```json
{
  "answer": "Based on the documents, there are different types of alerts...",
  "sources": [
    {
      "fileUrl": "https://onblick.sharepoint.com/...",
      "heading": "Types of Alerts",
      "score": 0.85
    },
    {
      "fileUrl": "https://onblick.sharepoint.com/...",
      "heading": "Types of Alerts by Module",
      "score": 0.82
    }
  ]
}
```

---

## 🧪 Test Questions to Try

### **1. Basic Question**
```powershell
$query = @{ tenantId = "default"; question = "What are the types of alerts?" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:7071/api/QueryAnswer" -Method Post -Body $query -ContentType "application/json"
```

### **2. Specific Question**
```powershell
$query = @{ tenantId = "default"; question = "How are alerts triggered?" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:7071/api/QueryAnswer" -Method Post -Body $query -ContentType "application/json"
```

### **3. Technical Question**
```powershell
$query = @{ tenantId = "default"; question = "What is SignalR used for?" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:7071/api/QueryAnswer" -Method Post -Body $query -ContentType "application/json"
```

### **4. API Question**
```powershell
$query = @{ tenantId = "default"; question = "What are the API endpoints for alert management?" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:7071/api/QueryAnswer" -Method Post -Body $query -ContentType "application/json"
```

---

## ✅ What to Look For

### **Success Indicators:**

1. **Answer is returned** (not "No relevant documents found")
2. **Sources are listed** with:
   - `heading`: Section title
   - `fileUrl`: Link to SharePoint document
   - `score`: Similarity score (0.0 to 1.0, higher = more relevant)
3. **Answer is relevant** to your question

### **Example Good Response:**
```json
{
  "answer": "The system has two main types of alerts: immediate alerts triggered at the moment an action occurs, and scheduled alerts sent at regular intervals. Immediate alerts are triggered when actions happen, such as when an employee submits a timesheet. Scheduled alerts are sent at specific intervals until required actions are completed.",
  "sources": [
    {
      "fileUrl": "https://onblick.sharepoint.com/...",
      "heading": "Types of Alerts: Immediate vs. Scheduled",
      "score": 0.92
    },
    {
      "fileUrl": "https://onblick.sharepoint.com/...",
      "heading": "Alert Triggering Mechanism",
      "score": 0.85
    }
  ]
}
```

---

## 🔍 Troubleshooting

### **If you get "No relevant documents found":**

1. **Check tenantId**: Make sure it matches what's in CosmosDB
   - Look at your documents in Data Explorer
   - Check the `TenantId` field
   - Use that value (might be "default" or your actual tenant ID)

2. **Check Function App logs**:
   - Look for `[COSMOS]` messages
   - Should see: `Would fetch embeddings for tenant [id]`
   - If you see "Mock", CosmosDB isn't connected

3. **Verify embeddings exist**:
   - Open Data Explorer
   - Check `Embeddings` container
   - Should see documents with `Embedding` arrays

### **If you get 500 Internal Server Error:**

1. **Check Function App is running** (F5)
2. **Check logs** for error messages
3. **Verify Azure OpenAI is configured** (should see `✅ Azure OpenAI initialized`)

### **If answer is not relevant:**

- This is normal - semantic search quality depends on:
  - How well the question matches document content
  - Quality of embeddings
  - Number of documents in database
- Try rephrasing your question
- Upload more documents for better coverage

---

## 📊 Understanding the Results

### **Score (Similarity Score):**
- **0.8 - 1.0**: Very relevant
- **0.6 - 0.8**: Relevant
- **0.4 - 0.6**: Somewhat relevant
- **< 0.4**: Not very relevant

### **Sources:**
- Lists up to 3 most relevant sections
- Each source links back to the original SharePoint document
- Click the URL to see the full enriched document

---

## 🎯 Next Steps After Testing

Once QueryAnswer works:

1. ✅ **Upload more documents** → More embeddings → Better search results
2. ✅ **Test with different questions** → Verify semantic search quality
3. ✅ **Move to SPFx frontend** → Build UI for users
4. ✅ **O365 Copilot integration** → Make documents available to Copilot

---

## 🚀 Quick Start

**Just want to test quickly? Run this:**

```powershell
cd D:\CodeBase\DocuPilot
.\TEST_QUERYANSWER.ps1
```

This will test 3 different questions automatically and show you the results!

