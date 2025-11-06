# 🚀 STEP 0: Run in Visual Studio (Simple)

## Quick Start (3 Steps)

---

## Step 1: Open Project (30 seconds)

1. **Open Visual Studio**
2. **File → Open → Project/Solution**
3. **Navigate to:** `D:\CodeBase\DocuPilot\SMEPilot.FunctionApp\SMEPilot.FunctionApp.csproj`
4. **Click Open**

---

## Step 2: Run Function App (30 seconds)

1. **Press F5** (or click the green "Start" button)
2. **Wait for build** - Should see "Build succeeded"
3. **Look for Output window** - Should show:
   ```
   Functions:
       ProcessSharePointFile: [POST] http://localhost:7071/api/ProcessSharePointFile
       QueryAnswer: [POST] http://localhost:7071/api/QueryAnswer
   ```

✅ **Success = You see the function URLs in the Output window**

---

## Step 3: Test the Endpoints (2 minutes)

### Open a NEW PowerShell window (keep Visual Studio running)

### Test ProcessSharePointFile:
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

### Test QueryAnswer:
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

---

## What You Should See

✅ **Visual Studio Output shows function URLs**  
✅ **HTTP calls return responses (200 OK)**  
✅ **Visual Studio Output/Console shows processing logs**

---

## If You Get Errors

### "Build failed"
- Check Error List window in Visual Studio
- Share the error message

### "Cannot start function host"
- Check Output window
- Verify `local.settings.json` exists

### "Connection refused" or "Cannot connect"
- Make sure Visual Studio is still running (don't stop debugging)
- Check if port 7071 is available

---

## Bonus: Debug with Breakpoints

1. **Open `ProcessSharePointFile.cs`**
2. **Click in left margin on line 35** (sets breakpoint)
3. **Press F5** to run
4. **Call the endpoint from PowerShell**
5. **Visual Studio will pause at breakpoint**
6. **Press F10** to step through code
7. **Hover over variables to see values**

**This is how you debug!**

---

## Next Steps

Once Step 0 works:
1. ✅ Backend is verified
2. ✅ Configure real Azure credentials
3. ✅ Test with real SharePoint document
4. ✅ Fix SPFx packaging (for selling)

---

**Total Time: ~3 minutes**

Ready? Open Visual Studio and press F5! 🚀

