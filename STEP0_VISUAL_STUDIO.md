# 🚀 STEP 0: Test Backend in Visual Studio

## Quick Start - Run & Debug Azure Functions in VS

**Much easier than command line!** Visual Studio has built-in Azure Functions support.

---

## Step 0.1: Open Project in Visual Studio (1 minute)

### 1. Open Visual Studio
- Launch Visual Studio (2022 recommended)

### 2. Open the Function App Project
- File → Open → Project/Solution
- Navigate to: `D:\CodeBase\DocuPilot\SMEPilot.FunctionApp\SMEPilot.FunctionApp.csproj`
- Click Open

**OR** if you have Solution file:
- Open: `D:\CodeBase\DocuPilot\SMEPilot.sln` (if it exists)

---

## Step 0.2: Check Prerequisites in Visual Studio

### 1. Verify Azure Functions Tools
- Tools → Get Tools and Features
- Check if "Azure development" workload is installed
- If not, install it (takes 5-10 minutes)

### 2. Verify .NET 8 SDK
- Visual Studio should detect .NET automatically
- If not, install from: https://dotnet.microsoft.com/download

---

## Step 0.3: Set Startup Project (30 seconds)

### 1. Right-click on `SMEPilot.FunctionApp` project
- Select "Set as Startup Project"

### 2. Check Project Properties
- Right-click project → Properties
- Verify:
  - **Target Framework:** .NET 8.0
  - **Output Type:** Exe

---

## Step 0.4: Configure Local Settings (Optional - 1 minute)

### 1. Open `local.settings.json`
- In Solution Explorer, expand `SMEPilot.FunctionApp`
- Double-click `local.settings.json`

### 2. For Testing (Mock Mode), keep credentials empty:
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

## Step 0.5: Run/Debug Function App (30 seconds)

### 1. Press F5 (or click "Start" button)
- Visual Studio will:
  - Build the project
  - Start the Function App
  - Open a browser/console showing function endpoints

### 2. Look for Output
- You should see in Output window:
```
Functions:
    ProcessSharePointFile: [POST] http://localhost:7071/api/ProcessSharePointFile
    QueryAnswer: [POST] http://localhost:7071/api/QueryAnswer
```

### 3. Keep Visual Studio Running
- Don't stop debugging
- This keeps the functions running

---

## Step 0.6: Test ProcessSharePointFile (1 minute)

### 1. Open a NEW PowerShell window
(Don't close Visual Studio)

### 2. Test the endpoint
```powershell
$body = @{
    siteId = "local-test"
    driveId = "local-test"
    itemId = "local-test"
    fileName = "test.docx"
    uploaderEmail = "test@example.com"
    tenantId = "default"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:7071/api/ProcessSharePointFile" `
    -Method Post `
    -Body $body `
    -ContentType "application/json"
```

**Note:** If you get authentication error, we need to change `AuthorizationLevel.Function` to `AuthorizationLevel.Anonymous` temporarily for local testing.

### 3. Check Visual Studio Output Window
- Look for logs showing:
  - "Processing file..."
  - "Extracting text..."
  - "Calling OpenAI..."
  - "Building enriched document..."

### 4. Check Debug Console
- In Visual Studio, check the Console/Output window
- You'll see function execution logs

---

## Step 0.7: Test QueryAnswer (1 minute)

### 1. Test query endpoint
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
- Status 200
- JSON response with answer

---

## Step 0.8: Debug & Set Breakpoints (Bonus!)

### 1. Set Breakpoints
- Open `ProcessSharePointFile.cs`
- Click in the left margin to set breakpoint on line 35 (start of `Run` method)
- Set another on line 55 (where processing starts)

### 2. Run with F5
- When you call the endpoint, Visual Studio will pause at breakpoints
- You can:
  - Inspect variables
  - Step through code (F10)
  - See call stack
  - Check values

### 3. This is Powerful!
- You can see exactly what's happening
- Find bugs easily
- Test logic step-by-step

---

## What Success Looks Like

✅ **Visual Studio builds without errors**  
✅ **Function App starts (F5)**  
✅ **Endpoints shown in Output window**  
✅ **ProcessSharePointFile responds**  
✅ **QueryAnswer responds**  
✅ **Logs appear in Visual Studio Output**

---

## Next Steps (After Step 0)

1. **If backend works:** 
   - Configure real Azure/SharePoint credentials
   - Test with real SharePoint document
   - Set up Graph webhook for automatic triggers

2. **If backend has errors:**
   - Use Visual Studio debugging (breakpoints!)
   - Share error details
   - We'll fix step by step

3. **Then:** Fix SPFx packaging (for selling)

---

## Troubleshooting in Visual Studio

### Error: "Azure development workload not installed"
- Tools → Get Tools and Features
- Install "Azure development" workload
- Restart Visual Studio

### Error: "Cannot start function host"
- Check Output window for errors
- Verify `local.settings.json` exists
- Try Clean Solution (Build → Clean Solution)

### Error: "AuthorizationLevel.Function" authentication
- Temporarily change to `AuthorizationLevel.Anonymous` in both functions
- For `ProcessSharePointFile.cs` line 33
- For `QueryAnswer.cs` line 28
- Rebuild and run

### Breakpoints not hitting?
- Make sure you're running in Debug mode (F5, not Ctrl+F5)
- Check "Debug" configuration (not "Release")
- Verify build succeeded

---

## Visual Studio Advantages

✅ **No command line needed**  
✅ **Built-in debugging with breakpoints**  
✅ **See variables, call stack, exceptions**  
✅ **IntelliSense and code completion**  
✅ **Integrated testing**  

---

## Summary

**Step 0 in Visual Studio:**

1. ✅ Open project → `SMEPilot.FunctionApp.csproj`
2. ✅ Press F5 → Starts Function App
3. ✅ Test endpoints → PowerShell HTTP calls
4. ✅ Check logs → Visual Studio Output window
5. ✅ Debug → Set breakpoints, inspect variables

**Total Time: ~5 minutes**

---

## Questions?

If you hit any errors:
1. Check Visual Studio Output window
2. Share the error message
3. Screenshot (optional)

We'll fix it together! 🚀

