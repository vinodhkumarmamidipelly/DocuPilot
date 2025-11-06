# 🧪 SMEPilot - Local Testing Guide

## ✅ Yes! You Can Test Locally First!

**Current Setup:**
- ✅ Function App running locally (Visual Studio)
- ✅ ngrok exposing Function App to internet
- ✅ SPFx package built and ready

**This is perfect for testing before Azure deployment!**

---

## 🎯 Step 1: Test with ngrok (Current Setup)

### **1.1: Verify ngrok is Running**

Your Function App should be accessible via ngrok URL:
```
https://<your-ngrok-id>.ngrok-free.app
```

**Test endpoints:**
```powershell
# Test ProcessSharePointFile
Invoke-RestMethod -Uri "https://<your-ngrok-id>.ngrok-free.app/api/ProcessSharePointFile" -Method Get

# Test QueryAnswer
$body = @{
    tenantId = "default"
    question = "What are the types of alerts?"
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://<your-ngrok-id>.ngrok-free.app/api/QueryAnswer" `
  -Method Post -Body $body -ContentType "application/json"
```

### **1.2: Update Graph Subscription (if needed)**

If your Graph subscription is pointing to ngrok, it should already work!

**Check current subscription:**
```powershell
# Your subscription should have notification URL like:
# https://<your-ngrok-id>.ngrok-free.app/api/ProcessSharePointFile
```

**If you need to update:**
```powershell
.\SetupSubscription.ps1 `
  -SiteId "<your-site-id>" `
  -DriveId "<your-drive-id>" `
  -NotificationUrl "https://<your-ngrok-id>.ngrok-free.app/api/ProcessSharePointFile"
```

---

## 🎯 Step 2: Test SPFx Locally (SharePoint Workbench)

### **2.1: Start SPFx Local Server**

```powershell
cd D:\CodeBase\DocuPilot\SMEPilot.SPFx
npx gulp serve
```

**Expected:**
- Opens browser to: `https://localhost:4321/temp/workbench.html`
- SharePoint Workbench loads
- You can add web parts

### **2.2: Configure Web Part**

1. **Add DocumentUploader web part:**
   - Click **+** → Find **"SMEPilot Document Upload"**
   - Add to page

2. **Configure Function App URL:**
   - Click **Edit** on web part
   - Set **Function App URL**: `https://<your-ngrok-id>.ngrok-free.app`
   - Set **Scratch Docs Library**: `ScratchDocs` (or your library name)
   - Click **Apply**

3. **Test Upload:**
   - Click **Upload Document**
   - Select a `.docx` file
   - Click **Upload**
   - Check Function App logs in Visual Studio

---

## 🎯 Step 3: Test End-to-End Locally

### **3.1: Upload Document via SharePoint**

**Option A: Direct SharePoint Upload (Current Method)**
- Upload document to SharePoint library
- Graph subscription triggers Function App
- Check logs in Visual Studio
- Verify enriched document in ProcessedDocs folder

**Option B: Via SPFx Web Part (After Step 2)**
- Use DocumentUploader web part in Workbench
- Upload document
- Verify processing

### **3.2: Verify Enrichment**

1. **Check Function App Logs:**
   - Visual Studio Output window
   - Should show: "Processing file...", "Enrichment complete"

2. **Check SharePoint:**
   - Go to `ProcessedDocs` folder
   - Find `*_enriched.docx` file
   - Open and verify content

3. **Check CosmosDB:**
   - Open CosmosDB Emulator
   - Check `SMEPilotDB` → `Embeddings` container
   - Should see stored embeddings

### **3.3: Test QueryAnswer**

```powershell
$body = @{
    tenantId = "default"
    question = "What are the types of alerts?"
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://<your-ngrok-id>.ngrok-free.app/api/QueryAnswer" `
  -Method Post -Body $body -ContentType "application/json"
```

**Expected:** Returns answer with sources

---

## 🎯 Step 4: Deploy SPFx Package (When Ready)

### **4.1: Update Function App URL in Code (Optional)**

If you want SPFx to use ngrok URL by default:

**File:** `SMEPilot.SPFx/src/webparts/documentUploader/components/DocumentUploader.tsx`

Find and update:
```typescript
private defaultFunctionAppUrl: string = "https://<your-ngrok-id>.ngrok-free.app";
```

**Then rebuild:**
```powershell
cd D:\CodeBase\DocuPilot\SMEPilot.SPFx
npx gulp clean
npm run build
npx gulp bundle --ship
npx gulp package-solution --ship
```

### **4.2: Deploy to SharePoint App Catalog**

**Step 1: Access App Catalog**
1. Go to: https://admin.microsoft.com/Adminportal/Home#/SharePoint
2. Click **More features** → **Apps** → **App Catalog**
3. Or direct: `https://<your-tenant>.sharepoint.com/sites/appcatalog`

**Step 2: Upload Package**
1. Click **Distribute apps for SharePoint**
2. Click **New** → **App for SharePoint**
3. Upload: `D:\CodeBase\DocuPilot\SMEPilot.SPFx\sharepoint\solution\sme-pilot.sppkg`
4. Click **Deploy**

**Step 3: Approve API Permissions**
1. Go to **API Management** → **Pending requests**
2. Approve:
   - `Sites.ReadWrite.All` (Microsoft Graph)
   - `Files.ReadWrite` (Microsoft Graph)

**Step 4: Add to Site**
1. Go to your SharePoint site: `https://onblick.sharepoint.com/sites/DocEnricher-PoC`
2. Click **Settings** (gear) → **Add an app**
3. Find **SMEPilot** → Click **Add**
4. Click **Trust It**

**Step 5: Add Web Part to Page**
1. Create or edit a page
2. Click **+** → Search **"SMEPilot Document Upload"**
3. Add web part
4. Configure:
   - **Function App URL**: `https://<your-ngrok-id>.ngrok-free.app`
   - **Scratch Docs Library**: `ScratchDocs`

---

## ✅ Local Testing Checklist

- [ ] Function App running in Visual Studio
- [ ] ngrok exposing Function App (check URL)
- [ ] Graph subscription pointing to ngrok URL
- [ ] SPFx local server running (`gulp serve`)
- [ ] Web part loads in Workbench
- [ ] Function App URL configured in web part
- [ ] Document upload tested
- [ ] Enrichment verified
- [ ] QueryAnswer tested
- [ ] CosmosDB embeddings verified

---

## 🐛 Troubleshooting Local Testing

### **Issue: ngrok URL not accessible**

**Solution:**
```powershell
# Check ngrok is running
# Get ngrok URL from ngrok dashboard: http://localhost:4040
# Update Graph subscription with correct URL
```

### **Issue: SPFx can't connect to Function App**

**Solution:**
- Verify ngrok URL is correct
- Check CORS settings (Function App should allow all origins for testing)
- Check browser console for errors
- Verify Function App is running

### **Issue: Graph subscription not triggering**

**Solution:**
- Verify subscription is active (check expiration date)
- Verify notification URL matches ngrok URL exactly
- Check Function App logs for validation errors
- Test with manual file upload to SharePoint

---

## 🎯 Recommended Testing Flow

**Phase 1: Backend Testing (Current)**
1. ✅ Function App running locally
2. ✅ ngrok exposing to internet
3. ✅ Graph subscription configured
4. ✅ Test document upload → enrichment

**Phase 2: SPFx Local Testing**
1. Start SPFx local server (`gulp serve`)
2. Test web part in Workbench
3. Configure Function App URL (ngrok)
4. Test upload via web part

**Phase 3: SPFx Deployment (When Ready)**
1. Deploy package to App Catalog
2. Add to SharePoint site
3. Test in real SharePoint environment

**Phase 4: Azure Deployment (Production)**
1. Deploy Function App to Azure
2. Update Graph subscription
3. Update SPFx Function App URL
4. Redeploy SPFx package

---

## 💡 Key Points

✅ **ngrok works perfectly for testing!**  
✅ **Test locally first** - much faster iteration  
✅ **SPFx Workbench** - test web parts without deployment  
✅ **Deploy to SharePoint** - only when ready for real testing  

**Start with Step 1 and 2 - test locally first!** 🚀

