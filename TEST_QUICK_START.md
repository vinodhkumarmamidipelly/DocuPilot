# Quick Test Guide - Get Started in 5 Minutes

## ✅ Pre-Flight Check

**Status:** Build successful ✅

**Configuration:** 
- Graph API credentials: ✅ Configured
- Enriched folder path: ✅ Configured
- OCR: ⚠️ Optional (not required for basic testing)

---

## 🚀 Step 1: Start Function App

### **Option A: If Azure Functions Core Tools is installed**
```bash
cd SMEPilot.FunctionApp
func start
```

### **Option B: If Azure Functions Core Tools is NOT installed**
Install it first:
```powershell
# Install Azure Functions Core Tools
npm install -g azure-functions-core-tools@4 --unsafe-perm true
```

Or download from: https://github.com/Azure/azure-functions-core-tools

Then run:
```bash
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

## 🧪 Step 2: Quick Health Check

Open a **NEW** PowerShell terminal and run:

```powershell
# Test SetupSubscription endpoint
Invoke-WebRequest -Uri "http://localhost:7071/api/SetupSubscription" -Method GET
```

**Expected:** Status 200 OK

---

## 📄 Step 3: Test with SharePoint File

### **A. Get SharePoint File Details**

1. **Upload a test document** to your SharePoint site (any document library)

2. **Get File IDs** using one of these methods:

   **Method 1: Use PowerShell script**
   ```powershell
   cd D:\CodeBase\DocuPilot
   .\GetSharePointIDs.ps1 -SiteUrl "https://your-tenant.sharepoint.com/sites/your-site"
   ```

   **Method 2: Use Graph Explorer**
   - Go to: https://developer.microsoft.com/graph/graph-explorer
   - Sign in and query: `GET /sites/{site-id}/drives/{drive-id}/items/{item-id}`

   **Method 3: From SharePoint URL**
   - Open file in SharePoint
   - Copy URL and extract IDs from it

### **B. Process the File**

Once you have the IDs, run:

```powershell
$body = @{
    siteId = "YOUR-SITE-ID"
    driveId = "YOUR-DRIVE-ID"
    itemId = "YOUR-ITEM-ID"
    fileName = "test-document.docx"
    uploaderEmail = "your-email@domain.com"
    tenantId = "default"
} | ConvertTo-Json

Invoke-WebRequest -Uri "http://localhost:7071/api/ProcessSharePointFile" `
    -Method POST `
    -ContentType "application/json" `
    -Body $body
```

**Replace:**
- `YOUR-SITE-ID` - Your SharePoint site ID
- `YOUR-DRIVE-ID` - Your document library drive ID
- `YOUR-ITEM-ID` - The file/item ID
- `test-document.docx` - Actual file name
- `your-email@domain.com` - Your email

---

## ✅ Step 4: Verify Results

### **Check Function App Logs:**
Look at the terminal where `func start` is running. You should see:
- ✅ File download logs
- ✅ Extraction logs (text/images)
- ✅ Template formatting logs
- ✅ Upload to SharePoint logs
- ✅ Success message with enriched URL

### **Check SharePoint:**
1. Go to your SharePoint site
2. Navigate to "ProcessedDocs" folder (or your configured folder)
3. Find the formatted document
4. Open it and verify:
   - ✅ Template formatting applied
   - ✅ Sections properly structured
   - ✅ Images included (if any)
   - ✅ Table of contents (if applicable)

---

## 🐛 Common Issues

### **Issue: "func: command not found"**
**Solution:** Install Azure Functions Core Tools
```powershell
npm install -g azure-functions-core-tools@4 --unsafe-perm true
```

### **Issue: Authentication errors**
**Check:**
- Graph API credentials in `local.settings.json`
- App registration has correct permissions
- Client secret is correct

### **Issue: File not found**
**Check:**
- File IDs are correct
- File exists in SharePoint
- You have access to the file

### **Issue: Processing fails**
**Check Function App logs** for specific error messages

---

## 🎯 Success Indicators

You've successfully tested when:
- ✅ Function App starts without errors
- ✅ Health check returns 200 OK
- ✅ File processing completes successfully
- ✅ Formatted document appears in SharePoint
- ✅ Template formatting looks correct

---

## 📝 Next Steps

Once testing is successful:
1. Test different file formats (PDF, PPTX, XLSX, Images)
2. Test with multiple documents
3. Verify webhook subscriptions work
4. Deploy to Azure

---

**Ready? Let's start testing!**

