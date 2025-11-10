# 📦 Deploy SPFx Package to SharePoint - Step by Step

## 🎯 Goal
Deploy `sme-pilot.sppkg` to SharePoint App Catalog so it can be used in SharePoint sites.

---

## 📋 Prerequisites

✅ **Package Ready:**
- File: `D:\CodeBase\DocuPilot\SMEPilot.SPFx\sharepoint\solution\sme-pilot.sppkg`
- Size: 617 KB
- Status: ✅ Built and ready

✅ **Required Access:**
- SharePoint Admin permissions (to access App Catalog)
- Or ask your SharePoint admin to deploy

---

## 🚀 Step-by-Step Deployment

### **Step 1: Access SharePoint App Catalog**

**Option A: Via Admin Center (Recommended)**
1. Go to: https://admin.microsoft.com/Adminportal/Home#/SharePoint
2. Click **More features** → **Apps** → **App Catalog**
3. Click **Open** (opens App Catalog site)

**Option B: Direct URL**
```
https://<your-tenant>.sharepoint.com/sites/appcatalog
```

**If App Catalog doesn't exist:**
- Go to SharePoint Admin Center
- Click **More features** → **Apps**
- Click **App Catalog** → **Create**
- Follow wizard to create App Catalog site

---

### **Step 2: Upload SPFx Package**

1. **In App Catalog site:**
   - Click **Distribute apps for SharePoint** (left navigation)
   - Or go to: `https://<tenant>.sharepoint.com/sites/appcatalog/AppCatalog/Forms/AllItems.aspx`

2. **Upload Package:**
   - Click **New** → **App for SharePoint**
   - Or click **Upload** button
   - Browse to: `D:\CodeBase\DocuPilot\SMEPilot.SPFx\sharepoint\solution\sme-pilot.sppkg`
   - Click **OK**

3. **Review Package:**
   - Check package details
   - Verify version: 1.0.0.0
   - Click **Deploy** (if not auto-deployed)

---

### **Step 3: Approve API Permissions**

**Required Permissions:**
- `Sites.ReadWrite.All` (Microsoft Graph)
- `Files.ReadWrite` (Microsoft Graph)

**Steps:**
1. Go to: https://admin.microsoft.com/Adminportal/Home#/SharePoint
2. Click **More features** → **Apps** → **API Management**
3. Go to **Pending requests** tab
4. Find **SMEPilot** requests
5. Click **Approve** for each permission

**Or via SharePoint Admin Center:**
1. Go to **Advanced** → **API Access**
2. Find pending requests
3. Approve permissions

---

### **Step 4: Add App to SharePoint Site**

1. **Go to your SharePoint site:**
   ```
   https://onblick.sharepoint.com/sites/DocEnricher-PoC
   ```

2. **Add App:**
   - Click **Settings** (gear icon, top right)
   - Click **Add an app**
   - Find **SMEPilot** or **sme-pilot-client-side-solution**
   - Click **Add**

3. **Trust the App:**
   - Click **Trust It** when prompted
   - Wait for installation to complete

---

### **Step 5: Add Web Parts to Pages**

#### **5.1: Add DocumentUploader Web Part**

1. **Create or Edit a Page:**
   - Go to **Pages** → Create new or edit existing
   - Click **Edit** (if editing)

2. **Add Web Part:**
   - Click **+** (Insert web part)
   - Search for: **"SMEPilot Document Upload"**
   - Click to add

3. **Configure Web Part:**
   - Click **Edit** on web part (pencil icon)
   - Set **Function App URL**: `https://<your-ngrok-id>.ngrok-free.app`
   - Set **Scratch Docs Library**: `ScratchDocs` (or your library name)
   - Click **Apply**

#### **5.2: Add AdminPanel Web Part (Optional)**

1. **Add to Admin Page:**
   - Create or edit admin page
   - Add **"SMEPilot Admin"** web part
   - Configure Function App URL

---

### **Step 6: Test in SharePoint**

1. **Upload Document:**
   - Use DocumentUploader web part
   - Click **Upload Document**
   - Select a `.docx` file
   - Click **Upload**

2. **Verify Processing:**
   - Check Function App logs (Visual Studio)
   - Check `ProcessedDocs` folder for enriched document
   - Verify enrichment worked

---

## ✅ Verification Checklist

- [ ] Package uploaded to App Catalog
- [ ] Package deployed successfully
- [ ] API permissions approved
- [ ] App added to SharePoint site
- [ ] Web part appears in web part picker
- [ ] Web part added to page
- [ ] Function App URL configured
- [ ] Document upload tested
- [ ] Enrichment verified

---

## 🐛 Troubleshooting

### **Issue: Can't access App Catalog**

**Solution:**
- Verify you have SharePoint Admin permissions
- Ask admin to grant access
- Or ask admin to deploy package for you

### **Issue: Package upload fails**

**Solution:**
- Verify package file exists and is not corrupted
- Check file size (should be ~617 KB)
- Try uploading again
- Check browser console for errors

### **Issue: API permissions not showing**

**Solution:**
- Wait a few minutes after deployment
- Refresh API Management page
- Check if permissions are in "Pending requests"
- Verify package was deployed successfully

### **Issue: Web part not appearing**

**Solution:**
- Verify app is added to site (Site Contents)
- Check if app is trusted
- Refresh page
- Clear browser cache
- Try adding web part again

### **Issue: Web part can't connect to Function App**

**Solution:**
- Verify Function App URL is correct (ngrok URL)
- Check ngrok is still running
- Verify Function App is running locally
- Check browser console for CORS errors
- Test Function App URL directly in browser

---

## 📝 Quick Reference

**Package Location:**
```
D:\CodeBase\DocuPilot\SMEPilot.SPFx\sharepoint\solution\sme-pilot.sppkg
```

**App Catalog URL:**
```
https://<your-tenant>.sharepoint.com/sites/appcatalog
```

**API Management:**
```
https://admin.microsoft.com/Adminportal/Home#/SharePoint → API Management
```

**Your SharePoint Site:**
```
https://onblick.sharepoint.com/sites/DocEnricher-PoC
```

---

## 🎯 Next Steps After Deployment

1. **Test Document Upload** via web part
2. **Verify Enrichment** happens automatically
3. **Test QueryAnswer** API
4. **Verify Copilot** integration (if configured)

---

**Ready to deploy?** Start with **Step 1: Access App Catalog**! 🚀

