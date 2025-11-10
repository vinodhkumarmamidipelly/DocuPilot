# Local Testing Guide - Step by Step

## 🎯 Goal
Test the Function App locally to verify document processing works end-to-end.

---

## ✅ Prerequisites Check

### **1. Verify Build**
```bash
cd SMEPilot.FunctionApp
dotnet build
```
**Expected:** Build succeeded, 0 errors

### **2. Check Configuration**
Verify `local.settings.json` has:
- ✅ `Graph_TenantId`
- ✅ `Graph_ClientId`
- ✅ `Graph_ClientSecret`
- ✅ `EnrichedFolderRelativePath`

---

## 🚀 Step 1: Start Function App

### **Start the Function App:**
```bash
cd SMEPilot.FunctionApp
func start
```

**Expected Output:**
```
Functions:
    ProcessSharePointFile: [GET,POST,OPTIONS] http://localhost:7071/api/ProcessSharePointFile
    SetupSubscription: [GET,POST] http://localhost:7071/api/SetupSubscription
```

**Keep this terminal running!**

---

## 🧪 Step 2: Test Endpoints

### **Test 1: Health Check (SetupSubscription)**
Open a new terminal/PowerShell and run:
```powershell
Invoke-WebRequest -Uri "http://localhost:7071/api/SetupSubscription" -Method GET
```

**Expected:** Status 200 OK

### **Test 2: ProcessSharePointFile (Manual Test)**

**Option A: Using PowerShell**
```powershell
$body = @{
    siteId = "your-site-id"
    driveId = "your-drive-id"
    itemId = "your-item-id"
    fileName = "test.docx"
    uploaderEmail = "test@example.com"
    tenantId = "default"
} | ConvertTo-Json

Invoke-WebRequest -Uri "http://localhost:7071/api/ProcessSharePointFile" `
    -Method POST `
    -ContentType "application/json" `
    -Body $body
```

**Option B: Using curl**
```bash
curl -X POST http://localhost:7071/api/ProcessSharePointFile \
  -H "Content-Type: application/json" \
  -d '{
    "siteId": "your-site-id",
    "driveId": "your-drive-id",
    "itemId": "your-item-id",
    "fileName": "test.docx",
    "uploaderEmail": "test@example.com",
    "tenantId": "default"
  }'
```

---

## 📄 Step 3: Test with Real SharePoint File

### **Get SharePoint File Details:**

1. **Upload a test document to SharePoint** (any document library)

2. **Get File IDs using PowerShell:**
   ```powershell
   .\GetSharePointIDs.ps1
   ```
   Or use Graph Explorer to get:
   - Site ID
   - Drive ID
   - Item ID (File ID)

3. **Call ProcessSharePointFile:**
   ```powershell
   $body = @{
       siteId = "<site-id-from-step-2>"
       driveId = "<drive-id-from-step-2>"
       itemId = "<item-id-from-step-2>"
       fileName = "<actual-file-name>"
       uploaderEmail = "your-email@domain.com"
       tenantId = "default"
   } | ConvertTo-Json

   Invoke-WebRequest -Uri "http://localhost:7071/api/ProcessSharePointFile" `
       -Method POST `
       -ContentType "application/json" `
       -Body $body
   ```

**Expected Result:**
- ✅ Status 200 OK
- ✅ Response contains `enrichedUrl`
- ✅ Formatted document appears in ProcessedDocs folder

---

## 🔍 Step 4: Verify Results

### **Check Function App Logs:**
Look at the terminal where `func start` is running. You should see:
- ✅ File extraction logs
- ✅ Template formatting logs
- ✅ Upload to SharePoint logs
- ✅ Success message

### **Check SharePoint:**
1. Go to SharePoint site
2. Navigate to "ProcessedDocs" folder (or configured folder)
3. Verify formatted document is there
4. Open document and verify template formatting

---

## 🧪 Step 5: Test Multi-Format Support

### **Test Different File Types:**

1. **DOCX:**
   - Upload a Word document
   - Process it
   - Verify formatting

2. **PDF:**
   - Upload a PDF file
   - Process it
   - Verify text extraction and formatting

3. **PPTX:**
   - Upload a PowerPoint presentation
   - Process it
   - Verify slide content extraction

4. **XLSX:**
   - Upload an Excel spreadsheet
   - Process it
   - Verify cell content extraction

5. **Images (with OCR):**
   - Upload an image (PNG, JPG)
   - Process it (requires Azure Vision configured)
   - Verify OCR text extraction

---

## 🐛 Troubleshooting

### **Issue: Function App won't start**
- **Check:** .NET 8.0 SDK installed
- **Check:** Azure Functions Core Tools installed
- **Fix:** Run `dotnet restore` then `func start`

### **Issue: Authentication errors**
- **Check:** Graph API credentials in `local.settings.json`
- **Check:** App registration permissions in Azure AD
- **Fix:** Verify `Graph_ClientSecret` is correct

### **Issue: File not processing**
- **Check:** Function App logs for errors
- **Check:** SharePoint file permissions
- **Check:** File IDs are correct

### **Issue: Template not applying**
- **Check:** `TemplateBuilder.cs` is working
- **Check:** Document structure is valid
- **Check:** Logs for template building errors

---

## ✅ Success Criteria

You've successfully tested when:
- ✅ Function App starts without errors
- ✅ Endpoints respond correctly
- ✅ Documents are processed successfully
- ✅ Formatted documents appear in SharePoint
- ✅ Template formatting is applied correctly
- ✅ Multi-format support works

---

## 📝 Next Steps After Testing

Once local testing is successful:
1. ✅ Deploy to Azure Function App
2. ✅ Create SPFx package
3. ✅ Deploy to SharePoint
4. ✅ Production rollout

---

**Ready to start testing? Let's begin!**

