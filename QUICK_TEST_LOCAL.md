# ⚡ Quick Local Testing Guide

## 🎯 Test SPFx Locally in 5 Minutes!

**Perfect for testing before deployment!**

---

## ✅ Step 1: Update ngrok URL (One Time)

**Edit:** `SMEPilot.SPFx/src/webparts/documentUploader/DocumentUploaderWebPart.ts`

**Line 24-25:** Update the default URL:
```typescript
this._functionAppUrl = this.properties.functionAppUrl || 
  'https://<your-actual-ngrok-id>.ngrok-free.app'; // Replace with your ngrok URL
```

**Example:**
```typescript
this._functionAppUrl = this.properties.functionAppUrl || 
  'https://562fbad9f946.ngrok-free.app'; // Your actual ngrok URL
```

**Then rebuild:**
```powershell
cd D:\CodeBase\DocuPilot\SMEPilot.SPFx
npm run build
```

---

## ✅ Step 2: Start Local Server

**Open PowerShell:**
```powershell
cd D:\CodeBase\DocuPilot\SMEPilot.SPFx
npx gulp serve
```

**Expected:**
- Browser opens to: `https://localhost:4321/temp/workbench.html`
- SharePoint Workbench loads
- You see web part picker

**If certificate error:**
```powershell
npx gulp trust-dev-cert
npx gulp serve
```

---

## ✅ Step 3: Add Web Part

1. **In Workbench:**
   - Click **+** (Add web part)
   - Find **"SMEPilot Document Upload"**
   - Click to add

2. **Web Part Appears:**
   - Should show upload interface
   - May show default ngrok URL (if you updated code)

---

## ✅ Step 4: Configure Function App URL (If Needed)

**In Workbench:**
1. Click **Edit** (pencil icon) on web part
2. **Property Panel** opens on right
3. Set **Function App URL**: Your ngrok URL
   - Example: `https://562fbad9f946.ngrok-free.app`
4. Set **Scratch Docs Library**: `ScratchDocs`
5. Click **Apply**

**Note:** If you updated the default URL in code, this step may not be needed!

---

## ✅ Step 5: Test Upload

1. **Prepare Test File:**
   - Create a simple `.docx` file
   - Name it: `test-local.docx`

2. **Upload:**
   - Click **Upload Document** button
   - Select `test-local.docx`
   - Click **Upload**

3. **Watch:**
   - **Browser Console (F12):** Check for API calls
   - **Visual Studio:** Check Function App logs
   - **Web Part:** Shows progress/success

---

## ✅ Step 6: Verify It Works

**Check Function App Logs (Visual Studio Output):**
```
✅ Processing file: test-local.docx
✅ Enrichment complete
✅ File uploaded to ProcessedDocs
```

**Check Browser Console (F12 → Network tab):**
```
✅ POST https://<ngrok-id>.ngrok-free.app/api/ProcessSharePointFile
✅ Status: 200 OK
```

**Check SharePoint (if using real SharePoint):**
- Go to `ProcessedDocs` folder
- Find `test-local_enriched.docx`

---

## 🐛 Quick Troubleshooting

### **Web part doesn't appear:**
```powershell
# Rebuild
npm run build
# Refresh browser
```

### **Can't connect to Function App:**
- ✅ Check ngrok is running
- ✅ Verify Function App is running (Visual Studio)
- ✅ Check ngrok URL is correct
- ✅ Test URL in browser: `https://<ngrok-id>.ngrok-free.app/api/ProcessSharePointFile`

### **CORS errors:**
- Function App should allow all origins for testing
- Or use ngrok (bypasses CORS)

### **Certificate error:**
```powershell
npx gulp trust-dev-cert
```

---

## ✅ What You're Testing

**Functionality:**
- ✅ Web part loads correctly
- ✅ Configuration works
- ✅ Upload button works
- ✅ API calls go to correct URL
- ✅ Error handling works

**Integration:**
- ✅ Connects to Function App (ngrok)
- ✅ Upload triggers processing
- ✅ Enrichment completes
- ✅ Success/error messages display

---

## 🎯 After Local Testing Passes

**Once everything works locally:**
1. ✅ Deploy SPFx package to SharePoint
2. ✅ Test in real SharePoint environment
3. ✅ Verify with real SharePoint libraries

---

## 📋 Quick Commands Reference

**Start Local Server:**
```powershell
cd D:\CodeBase\DocuPilot\SMEPilot.SPFx
npx gulp serve
```

**Rebuild After Code Changes:**
```powershell
npm run build
# Then refresh browser
```

**Trust Certificate (First Time):**
```powershell
npx gulp trust-dev-cert
```

**Check ngrok URL:**
- Open: http://localhost:4040 (ngrok dashboard)
- Copy the HTTPS URL

---

**Ready?** Start with `npx gulp serve`! 🚀

