# Step 0: Test Backend Enrichment Pipeline (No SPFx Needed)

## Goal
Verify that the core document enrichment functionality works end-to-end.

---

## Prerequisites Check

### 1. Verify .NET 8 SDK is installed
```powershell
dotnet --version
```
**Expected:** Should show `8.x.x` or higher

### 2. Verify Azure Functions Core Tools installed
```powershell
func --version
```
**Expected:** Should show version number (e.g., `4.x.x`)

If not installed:
```powershell
# Install Azure Functions Core Tools
npm install -g azure-functions-core-tools@4 --unsafe-perm true
```

---

## Step 0.1: Prepare Test Environment

### 1. Navigate to Function App
```powershell
cd D:\CodeBase\DocuPilot\SMEPilot.FunctionApp
```

### 2. Check if Function App compiles
```powershell
dotnet build
```
**Expected:** Should say "Build succeeded"

---

## Step 0.2: Configure Local Settings (For Testing)

### 1. Edit `local.settings.json`
```powershell
notepad local.settings.json
```

### 2. For Testing (Mock Mode), you can leave credentials empty:
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet",
    "Graph_TenantId": "",
    "Graph_ClientId": "",
    "Graph_ClientSecret": "",
    "AzureOpenAI_Endpoint": "",
    "AzureOpenAI_Key": "",
    "AzureOpenAI_Deployment_GPT": "",
    "AzureOpenAI_Embedding_Deployment": "",
    "Cosmos_ConnectionString": "",
    "Cosmos_Database": "SMEPilotDB",
    "Cosmos_Container": "Embeddings",
    "EnrichedFolderRelativePath": "/Shared Documents/ProcessedDocs"
  }
}
```

**Note:** The code has mock responses when credentials are empty, so you can test the flow.

---

## Step 0.3: Create Test Document

### 1. Create a simple test Word document
- Open Word
- Create a document with:
  - Title: "Test Document"
  - Some text: "This is a test document about product features."
  - Add 1-2 images (screenshots) if possible
- Save as `test-document.docx`

### 2. Copy to samples folder (if exists)
```powershell
# Create samples folder if it doesn't exist
New-Item -ItemType Directory -Force -Path ..\samples\input
Copy-Item "path\to\test-document.docx" ..\samples\input\
```

---

## Step 0.4: Start Azure Function Locally

### 1. Start Function App
```powershell
func start
```

**Expected Output:**
```
Functions:
    ProcessSharePointFile: [POST] http://localhost:7071/api/ProcessSharePointFile
    QueryAnswer: [POST] http://localhost:7071/api/QueryAnswer
```

**Note:** Keep this terminal window open.

---

## Step 0.5: Test ProcessSharePointFile Endpoint

### 1. Open a NEW PowerShell window
(Keep the `func start` window running)

### 2. Test with mock/local file
```powershell
# Navigate to project root
cd D:\CodeBase\DocuPilot

# Create test request payload
$body = @{
    siteId = "local"
    driveId = "local"
    itemId = "local"
    fileName = "test-document.docx"
    uploaderEmail = "test@example.com"
    tenantId = "default"
} | ConvertTo-Json

# Call the function
Invoke-RestMethod -Uri "http://localhost:7071/api/ProcessSharePointFile?code=YOUR_FUNCTION_KEY" `
    -Method Post `
    -Body $body `
    -ContentType "application/json"
```

**Note:** For local testing, you may not need the `code` parameter. Try without it first:
```powershell
Invoke-RestMethod -Uri "http://localhost:7071/api/ProcessSharePointFile" `
    -Method Post `
    -Body $body `
    -ContentType "application/json"
```

### 3. Expected Response
- Should return status 200 or 202 (Accepted)
- May return mock response if credentials not configured
- Check the `func start` window for logs

---

## Step 0.6: Verify Results

### 1. Check Function Logs
- Look in the `func start` window
- Should see:
  - "Processing file: test-document.docx"
  - "Extracting text and images..."
  - "Calling OpenAI for sectioning..."
  - "Building enriched document..."
  - "Uploading to ProcessedDocs..."

### 2. Check Output Location
- If mock mode: Check `samples/output/` folder
- If real mode: Check SharePoint ProcessedDocs folder

---

## Step 0.7: Test QueryAnswer Endpoint

### 1. Test Query Endpoint
```powershell
$queryBody = @{
    tenantId = "default"
    question = "What is this document about?"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:7071/api/QueryAnswer" `
    -Method Post `
    -Body $queryBody `
    -ContentType "application/json"
```

### 2. Expected Response
- Should return answer with sources
- May return mock response if no embeddings in CosmosDB yet

---

## What Success Looks Like

✅ **Function App starts without errors**  
✅ **ProcessSharePointFile endpoint responds**  
✅ **QueryAnswer endpoint responds**  
✅ **Logs show processing steps**  

---

## Next Steps (After Step 0)

1. **If backend works:** Configure real Azure/SharePoint credentials
2. **If backend works:** Set up Graph webhook for automatic triggers
3. **If backend works:** Test with real SharePoint document
4. **Then:** Fix SPFx packaging (if needed for selling)

---

## Troubleshooting

### Error: "Function host is not running"
- Make sure `func start` is running
- Check if port 7071 is available

### Error: "Build failed"
- Run `dotnet clean` then `dotnet build`
- Check for missing NuGet packages

### Error: "Cannot connect to localhost:7071"
- Verify function is running
- Check firewall settings

---

## Questions?

If you encounter any errors, share the error message and we'll fix it step by step.

