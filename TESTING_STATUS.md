# 🧪 Testing Status

## ✅ Setup Complete

- ✅ **Build:** Successful
- ✅ **Azure Functions Core Tools:** Installed (v4.4.0)
- ✅ **Function App:** Starting...

---

## 🚀 Current Status

The Function App is starting in the background. It should be ready in 10-15 seconds.

**Endpoints will be available at:**
- `http://localhost:7071/api/ProcessSharePointFile`
- `http://localhost:7071/api/SetupSubscription`

---

## 🧪 Quick Test

### **Option 1: Use Test Script**
```powershell
.\TEST_FUNCTION_APP.ps1
```

### **Option 2: Manual Test**
```powershell
# Health check
Invoke-WebRequest -Uri "http://localhost:7071/api/SetupSubscription" -Method GET
```

**Expected:** Status 200 OK

---

## 📄 Test Document Processing

Once the Function App is running:

1. **Get SharePoint File IDs:**
   ```powershell
   .\GetSharePointIDs.ps1 -SiteUrl "https://your-tenant.sharepoint.com/sites/your-site"
   ```

2. **Process the File:**
   ```powershell
   $body = @{
       siteId = "YOUR-SITE-ID"
       driveId = "YOUR-DRIVE-ID"
       itemId = "YOUR-ITEM-ID"
       fileName = "test.docx"
       uploaderEmail = "your-email@domain.com"
       tenantId = "default"
   } | ConvertTo-Json

   Invoke-WebRequest -Uri "http://localhost:7071/api/ProcessSharePointFile" `
       -Method POST `
       -ContentType "application/json" `
       -Body $body
   ```

3. **Check Results:**
   - Look at Function App terminal for logs
   - Check SharePoint "ProcessedDocs" folder for formatted document

---

## 📚 Detailed Guides

- **Full Testing Guide:** `START_TESTING.md`
- **Quick Start:** `Knowledgebase/TEST_QUICK_START.md`
- **Local Testing:** `Knowledgebase/LOCAL_TESTING_GUIDE.md`

---

## 🐛 Troubleshooting

**Function App not starting?**
- Check if port 7071 is available
- Check Function App terminal for errors
- Verify `local.settings.json` is correct

**Can't connect to endpoints?**
- Wait a bit longer (Function App takes 10-15 seconds to start)
- Check if Function App is running in background
- Verify URL: `http://localhost:7071`

---

**Ready to test! The Function App should be running now.**

