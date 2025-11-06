# 🚀 STEP 0: Test Backend (Start Here)

## Quick Start - Test Core Enrichment Pipeline

**Goal:** Verify the backend enrichment works before worrying about SPFx.

---

## Prerequisites (30 seconds)

```powershell
# 1. Check .NET 8
dotnet --version
# Should show 8.x.x

# 2. Check Azure Functions Tools
func --version
# Should show version number

# If not installed:
# npm install -g azure-functions-core-tools@4 --unsafe-perm true
```

---

## Step 1: Start Function App (2 minutes)

### 1. Open PowerShell, navigate to Function App
```powershell
cd D:\CodeBase\DocuPilot\SMEPilot.FunctionApp
```

### 2. Build the project
```powershell
dotnet build
```
✅ **Success = "Build succeeded"**

### 3. Start Function App
```powershell
func start
```

**Expected Output:**
```
Functions:
    ProcessSharePointFile: [POST] http://localhost:7071/api/ProcessSharePointFile
    QueryAnswer: [POST] http://localhost:7071/api/QueryAnswer
```

**⚠️ Keep this window open!** Functions are now running.

---

## Step 2: Test ProcessSharePointFile Endpoint (1 minute)

### 1. Open a NEW PowerShell window
(Don't close the `func start` window)

### 2. Test the endpoint
```powershell
# Create test payload
$body = @{
    siteId = "local-test"
    driveId = "local-test"
    itemId = "local-test"
    fileName = "test.docx"
    uploaderEmail = "test@example.com"
    tenantId = "default"
} | ConvertTo-Json

# Call the function (use Anonymous for local testing)
Invoke-RestMethod -Uri "http://localhost:7071/api/ProcessSharePointFile" `
    -Method Post `
    -Body $body `
    -ContentType "application/json"
```

**If you get authentication error:** The function uses `AuthorizationLevel.Function`. For local testing, temporarily change it:

**Option A:** Modify `ProcessSharePointFile.cs` line 33:
```csharp
// Change from:
[HttpTrigger(AuthorizationLevel.Function, "post")]

// To:
[HttpTrigger(AuthorizationLevel.Anonymous, "post")]
```

Then restart `func start`.

**Option B:** Get the function key from the `func start` output and add `?code=YOUR_KEY` to the URL.

### 3. Check the `func start` window
You should see logs like:
- "Processing file..."
- "Extracting text..."
- "Calling OpenAI..."
- "Building enriched document..."

✅ **Success = Function responds and logs appear**

---

## Step 3: Test QueryAnswer Endpoint (1 minute)

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

**If authentication error:** Same fix as above - change `QueryAnswer.cs` line 28 to `AuthorizationLevel.Anonymous`.

### 2. Expected Response
- Status 200
- JSON response with answer (may be mock if no CosmosDB configured)

✅ **Success = Endpoint responds**

---

## What You've Accomplished

✅ **Backend is running**  
✅ **Enrichment endpoint works**  
✅ **Query endpoint works**  
✅ **Core functionality verified**  

---

## Next Steps

### If Backend Works ✅
1. **Configure real credentials** (Azure OpenAI, Graph API, CosmosDB)
2. **Test with real SharePoint document**
3. **Set up Graph webhook for automatic triggers**
4. **Fix SPFx packaging** (for selling)

### If Backend Has Errors ❌
- Share the error message
- We'll fix it step by step

---

## Common Issues

### "Cannot connect to localhost:7071"
- ✅ Make sure `func start` is running
- ✅ Check if port 7071 is available

### "AuthorizationLevel.Function" error
- ✅ Change to `AuthorizationLevel.Anonymous` for local testing
- ✅ Or get function key from `func start` output

### "Build failed"
- ✅ Run `dotnet clean`
- ✅ Run `dotnet restore`
- ✅ Run `dotnet build` again

---

## Summary

**Step 0 = Test backend without SPFx**

1. ✅ Start Function App → `func start`
2. ✅ Test ProcessSharePointFile → HTTP POST
3. ✅ Test QueryAnswer → HTTP POST
4. ✅ Verify logs show processing

**Total Time: ~5 minutes**

---

## Questions?

If you hit any errors, share:
1. The command you ran
2. The error message
3. Screenshot (optional)

We'll fix it together! 🚀

