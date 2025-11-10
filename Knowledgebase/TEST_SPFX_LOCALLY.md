# 🧪 Test SPFx Locally - Complete Guide

## ✅ Yes! You Can Test Everything Locally Before Deployment

**Benefits:**
- ✅ Fast iteration (no deployment needed)
- ✅ Easy debugging
- ✅ Test with ngrok Function App URL
- ✅ Verify web parts work correctly
- ✅ Catch issues before deployment

---

## 🎯 Step 1: Start SPFx Local Development Server

### **1.1: Start Local Server**

```powershell
cd D:\CodeBase\DocuPilot\SMEPilot.SPFx
npx gulp serve
```

**Expected Output:**
```
[17:20:00] Starting 'serve'...
[17:20:00] Starting subtask 'spfx-serve'...
[17:20:00] [spfx-serve] To load your scripts, use this query string: ?debug=true&noredir=true&debugManifestsFile=https://localhost:4321/temp/manifests.js
```

**Browser Opens:**
- URL: `https://localhost:4321/temp/workbench.html`
- SharePoint Workbench loads
- You can add web parts

---

## 🎯 Step 2: Test DocumentUploader Web Part

### **2.1: Add Web Part to Workbench**

1. **In Workbench:**
   - Click **+** (Add web part)
   - Find **"SMEPilot Document Upload"**
   - Click to add

2. **Web Part Appears:**
   - Should show upload interface
   - May show error if Function App URL not configured (this is OK)

### **2.2: Configure Function App URL**

**Option A: Via Web Part Properties (If Implemented)**
1. Click **Edit** (pencil icon) on web part
2. Set **Function App URL**: `https://<your-ngrok-id>.ngrok-free.app`
3. Set **Scratch Docs Library**: `ScratchDocs`
4. Click **Apply**

**Option B: Update Code Default (Temporary for Testing)**

Edit: `SMEPilot.SPFx/src/webparts/documentUploader/components/DocumentUploader.tsx`

Find the state initialization and update:
```typescript
// Around line 30-40, find:
this.state = {
  functionAppUrl: this.props.functionAppUrl || "",
  // ...
};

// Change to:
this.state = {
  functionAppUrl: this.props.functionAppUrl || "https://<your-ngrok-id>.ngrok-free.app",
  // ...
};
```

**Then rebuild:**
```powershell
npm run build
npx gulp serve
```

---

## 🎯 Step 3: Test Document Upload

### **3.1: Prepare Test Document**

- Create a simple `.docx` file with:
  - Some text content
  - A few images (optional)
  - Save as `test-document.docx`

### **3.2: Upload via Web Part**

1. **In Workbench:**
   - Click **Upload Document** button
   - Select `test-document.docx`
   - Click **Upload**

2. **Expected Behavior:**
   - Shows upload progress
   - Calls Function App API
   - Shows success/error message

### **3.3: Verify Processing**

**Check Function App Logs (Visual Studio):**
- Should see: "Processing file..."
- Should see: "Enrichment complete"
- Check for any errors

**Check SharePoint (if using real SharePoint):**
- Go to `ProcessedDocs` folder
- Find `test-document_enriched.docx`
- Verify enrichment worked

---

## 🎯 Step 4: Test AdminPanel Web Part

### **4.1: Add AdminPanel Web Part**

1. **In Workbench:**
   - Click **+** → Find **"SMEPilot Admin"**
   - Add to page

2. **Expected:**
   - Shows admin interface
   - Lists enrichment history (if implemented)
   - Shows logs/status

### **4.2: Verify Data Display**

- Check if it connects to Function App
- Verify it shows enrichment history
- Test any admin functions

---

## 🎯 Step 5: Test API Integration

### **5.1: Test Function App Connection**

**Check Browser Console (F12):**
- Look for API calls
- Check for CORS errors
- Verify requests are going to ngrok URL

**Expected API Calls:**
```
POST https://<ngrok-id>.ngrok-free.app/api/ProcessSharePointFile
POST https://<ngrok-id>.ngrok-free.app/api/QueryAnswer
```

### **5.2: Test QueryAnswer (If Implemented in Web Part)**

If AdminPanel has query functionality:
1. Enter a question
2. Click **Search** or **Query**
3. Verify it calls QueryAnswer API
4. Check response displays correctly

---

## 🎯 Step 6: Test Error Handling

### **6.1: Test Invalid Function App URL**

1. Set Function App URL to invalid value
2. Try to upload document
3. Verify error message displays

### **6.2: Test Network Errors**

1. Stop Function App (in Visual Studio)
2. Try to upload document
3. Verify error handling works

### **6.3: Test Invalid File Types**

1. Try uploading non-DOCX file
2. Verify validation works
3. Check error message

---

## ✅ Local Testing Checklist

**Before Deployment, Verify:**

- [ ] SPFx local server starts (`gulp serve`)
- [ ] Workbench loads in browser
- [ ] DocumentUploader web part appears
- [ ] AdminPanel web part appears
- [ ] Function App URL can be configured
- [ ] Document upload works
- [ ] API calls go to correct URL (ngrok)
- [ ] Error handling works
- [ ] UI looks correct
- [ ] No console errors
- [ ] No CORS issues

---

## 🐛 Troubleshooting Local Testing

### **Issue: Workbench doesn't load**

**Solution:**
```powershell
# Trust the dev certificate
npx gulp trust-dev-cert

# Then try again
npx gulp serve
```

### **Issue: Web parts don't appear**

**Solution:**
- Check browser console for errors
- Verify build completed successfully
- Try refreshing workbench page
- Check `lib/` folder has compiled files

### **Issue: Can't connect to Function App**

**Solution:**
- Verify ngrok is running
- Check ngrok URL is correct
- Verify Function App is running locally
- Check CORS settings in Function App
- Test Function App URL directly in browser

### **Issue: CORS errors**

**Solution:**
- Function App needs to allow localhost origins
- Check `ProcessSharePointFile.cs` - should have CORS headers
- Or test with ngrok (bypasses CORS)

### **Issue: API calls fail**

**Solution:**
- Check Function App logs
- Verify API endpoint URLs are correct
- Check request format matches API expectations
- Verify authentication (if required)

---

## 🎯 Quick Test Commands

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

**Check Build Output:**
```powershell
# Verify files exist
dir lib\webparts\documentUploader
dir lib\webparts\adminPanel
```

**Test Function App Directly:**
```powershell
# Test ProcessSharePointFile
Invoke-RestMethod -Uri "https://<ngrok-id>.ngrok-free.app/api/ProcessSharePointFile" -Method Get

# Test QueryAnswer
$body = @{ tenantId = "default"; question = "test" } | ConvertTo-Json
Invoke-RestMethod -Uri "https://<ngrok-id>.ngrok-free.app/api/QueryAnswer" -Method Post -Body $body -ContentType "application/json"
```

---

## 📋 What to Test

### **Functionality Tests:**
1. ✅ Web part loads
2. ✅ Configuration works
3. ✅ Document upload works
4. ✅ API integration works
5. ✅ Error handling works
6. ✅ UI displays correctly

### **Integration Tests:**
1. ✅ Connects to Function App (ngrok)
2. ✅ Uploads trigger processing
3. ✅ Enrichment completes
4. ✅ QueryAnswer works (if implemented)

### **UI/UX Tests:**
1. ✅ Buttons work
2. ✅ Forms submit correctly
3. ✅ Loading states show
4. ✅ Error messages display
5. ✅ Success messages display

---

## 🚀 After Local Testing Passes

**Once everything works locally:**
1. ✅ Deploy SPFx package to SharePoint
2. ✅ Test in real SharePoint environment
3. ✅ Verify with real SharePoint libraries
4. ✅ Test with multiple users (if possible)

---

## 💡 Pro Tips

1. **Keep ngrok running** - Don't close it during testing
2. **Keep Function App running** - Visual Studio debug mode
3. **Use browser DevTools** - Check Network tab for API calls
4. **Check Console** - Look for JavaScript errors
5. **Test on different browsers** - Chrome, Edge, etc.

---

**Ready to test?** Start with `npx gulp serve`! 🚀

