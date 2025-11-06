# Test Azure Functions - Quick Commands

## Functions are Running! ✅

Your functions are now ready at:
- **ProcessSharePointFile**: http://localhost:7071/api/ProcessSharePointFile
- **QueryAnswer**: http://localhost:7071/api/QueryAnswer

---

## Quick Test (Copy & Paste)

### Open a NEW PowerShell window
(Keep Visual Studio running)

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

Invoke-RestMethod -Uri "http://localhost:7071/api/ProcessSharePointFile" -Method Post -Body $body -ContentType "application/json"
```

### Test QueryAnswer:
```powershell
$queryBody = @{
    tenantId = "default"
    question = "What is this document about?"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:7071/api/QueryAnswer" -Method Post -Body $queryBody -ContentType "application/json"
```

---

## What to Expect

### ProcessSharePointFile Response:
- Status: 200 OK
- May return mock response (since credentials are empty)
- Check Visual Studio Output window for logs:
  - "Processing file: test.docx"
  - "Extracting text and images..."
  - "Calling OpenAI for sectioning..."
  - "Building enriched document..."

### QueryAnswer Response:
- Status: 200 OK
- JSON with answer (may be mock if no embeddings exist yet)

---

## Check Visual Studio Output Window

After calling endpoints, look in Visual Studio for:
- Function execution logs
- Processing steps
- Any errors or warnings

---

## Success Indicators

✅ **HTTP calls return 200 OK**  
✅ **Visual Studio shows function logs**  
✅ **No errors in Output window**

---

## Next Steps (After Testing)

1. **If endpoints work:** 
   - Configure real Azure credentials (OpenAI, Graph, CosmosDB)
   - Test with real SharePoint document

2. **If you see errors:**
   - Share the error message
   - We'll debug together

---

**Ready to test? Copy the commands above!** 🚀

