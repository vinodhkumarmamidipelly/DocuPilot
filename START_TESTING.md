# 🧪 Start Testing - Step by Step

## ✅ Current Status
- ✅ **Build:** Successful (0 errors)
- ✅ **Configuration:** Graph API credentials configured
- ❌ **Azure Functions Core Tools:** Not installed (need to install)

---

## 📦 Step 1: Install Azure Functions Core Tools

### **Option A: Using npm (Recommended)**
```powershell
npm install -g azure-functions-core-tools@4 --unsafe-perm true
```

### **Option B: Using Chocolatey**
```powershell
choco install azure-functions-core-tools-4
```

### **Option C: Manual Download**
Download from: https://github.com/Azure/azure-functions-core-tools/releases

**Verify Installation:**
```powershell
func --version
```
Should show version 4.x.x

---

## 🚀 Step 2: Start Function App

Once Azure Functions Core Tools is installed:

```powershell
cd D:\CodeBase\DocuPilot\SMEPilot.FunctionApp
func start
```

**Expected Output:**
```
Azure Functions Core Tools
Core Tools Version: 4.x.x
Function Runtime Version: 4.x.x

Functions:
    ProcessSharePointFile: [GET,POST,OPTIONS] http://localhost:7071/api/ProcessSharePointFile
    SetupSubscription: [GET,POST] http://localhost:7071/api/SetupSubscription

Host started
```

**⚠️ Keep this terminal running!**

---

## 🧪 Step 3: Test Health Check

Open a **NEW** PowerShell terminal and run:

```powershell
# Test SetupSubscription endpoint
$response = Invoke-WebRequest -Uri "http://localhost:7071/api/SetupSubscription" -Method GET
Write-Host "Status: $($response.StatusCode)" -ForegroundColor Green
```

**Expected:** Status 200 OK

---

## 📄 Step 4: Test Document Processing

### **A. Prepare Test Document**

1. **Upload a test document** to your SharePoint site
   - Any Word document (.docx) works
   - Upload to any document library

2. **Get SharePoint IDs** using one of these methods:

   **Method 1: Use PowerShell Script**
   ```powershell
   cd D:\CodeBase\DocuPilot
   .\GetSharePointIDs.ps1 -SiteUrl "https://your-tenant.sharepoint.com/sites/your-site"
   ```
   
   **Method 2: Use Graph Explorer**
   - Go to: https://developer.microsoft.com/graph/graph-explorer
   - Sign in
   - Query: `GET /sites/{site-id}/drives/{drive-id}/items/{item-id}`
   - Copy the IDs from the response

   **Method 3: From SharePoint URL**
   - Open the file in SharePoint
   - Copy the URL
   - Extract IDs from URL structure

### **B. Process the File**

Once you have the IDs, create a test script:

```powershell
# Test Document Processing
$body = @{
    siteId = "YOUR-SITE-ID-HERE"
    driveId = "YOUR-DRIVE-ID-HERE"
    itemId = "YOUR-ITEM-ID-HERE"
    fileName = "your-test-file.docx"
    uploaderEmail = "your-email@domain.com"
    tenantId = "default"
} | ConvertTo-Json

Write-Host "Processing document..." -ForegroundColor Yellow
$response = Invoke-WebRequest -Uri "http://localhost:7071/api/ProcessSharePointFile" `
    -Method POST `
    -ContentType "application/json" `
    -Body $body

Write-Host "Status: $($response.StatusCode)" -ForegroundColor Green
$result = $response.Content | ConvertFrom-Json
Write-Host "Enriched URL: $($result.enrichedUrl)" -ForegroundColor Cyan
```

**Replace the placeholders:**
- `YOUR-SITE-ID-HERE` - Your SharePoint site ID
- `YOUR-DRIVE-ID-HERE` - Your document library drive ID  
- `YOUR-ITEM-ID-HERE` - The file/item ID
- `your-test-file.docx` - Actual file name
- `your-email@domain.com` - Your email

---

## ✅ Step 5: Verify Results

### **Check Function App Logs:**
Look at the terminal where `func start` is running. You should see:
```
✅ [EXTRACTION] Extracted X characters and Y images
✅ [ENRICHMENT] Template formatting completed
✅ [UPLOAD] File uploaded to SharePoint
✅ Success: Document processed successfully
```

### **Check SharePoint:**
1. Go to your SharePoint site
2. Navigate to **"ProcessedDocs"** folder (or your configured folder path)
3. Find the formatted document (usually named `[OriginalName]_formatted.docx`)
4. Open it and verify:
   - ✅ Template formatting applied
   - ✅ Sections properly structured
   - ✅ Table of contents (if applicable)
   - ✅ Images included (if any)

---

## 🧪 Step 6: Test Different Formats

Once basic testing works, try different file types:

### **Test PDF:**
```powershell
# Same process, just change fileName to .pdf
$body = @{
    # ... same IDs ...
    fileName = "test-document.pdf"
    # ...
}
```

### **Test PowerPoint:**
```powershell
$body = @{
    # ... same IDs ...
    fileName = "presentation.pptx"
    # ...
}
```

### **Test Excel:**
```powershell
$body = @{
    # ... same IDs ...
    fileName = "spreadsheet.xlsx"
    # ...
}
```

### **Test Images (requires OCR):**
```powershell
$body = @{
    # ... same IDs ...
    fileName = "image.png"
    # ...
}
```
**Note:** Images require Azure Computer Vision configured in `local.settings.json`

---

## 🐛 Troubleshooting

### **Issue: "func: command not found"**
**Solution:** 
- Install Azure Functions Core Tools (see Step 1)
- Restart PowerShell after installation
- Verify: `func --version`

### **Issue: "Cannot connect to Graph API"**
**Check:**
- Graph API credentials in `local.settings.json`
- App registration has `Sites.ReadWrite.All` permission
- Client secret is correct and not expired

### **Issue: "File not found"**
**Check:**
- File IDs are correct
- File exists in SharePoint
- You have access to the file
- Site/Drive/Item IDs match the file

### **Issue: "Processing failed"**
**Check Function App logs** for specific error:
- Authentication errors → Check Graph API credentials
- File lock errors → Wait and retry
- Template errors → Check document structure

---

## ✅ Success Checklist

- [ ] Azure Functions Core Tools installed
- [ ] Function App starts successfully
- [ ] Health check returns 200 OK
- [ ] Document processing completes
- [ ] Formatted document appears in SharePoint
- [ ] Template formatting looks correct
- [ ] Multiple file formats tested

---

## 📝 Next Steps

Once local testing is successful:
1. ✅ Test with multiple documents
2. ✅ Test webhook subscriptions (automatic triggers)
3. ✅ Deploy to Azure Function App
4. ✅ Create SPFx package
5. ✅ Deploy to SharePoint

---

## 🆘 Need Help?

- **Detailed Guide:** See `Knowledgebase/LOCAL_TESTING_GUIDE.md`
- **Quick Reference:** See `Knowledgebase/TEST_QUICK_START.md`
- **Configuration:** Check `local.settings.json`
- **Logs:** Check Function App terminal output

---

**Ready to start? Install Azure Functions Core Tools first, then run `func start`!**

