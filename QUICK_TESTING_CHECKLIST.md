# Quick Testing Checklist

## 🚀 Quick Start Testing

### Step 1: Verify Function App is Running
1. Go to Azure Portal → Function App
2. Click **Log stream**
3. Look for: `🚀 SMEPilot Function App starting...`
4. ✅ **PASS** if no errors

**If FAIL:**
- Check `Logs/sme-pilot-*.log` file
- Check Application Insights → **Failures**
- Verify environment variables

---

### Step 2: Verify SPFx Web Part Loads
1. Open SharePoint page with SPFx web part
2. Press **F12** (browser console)
3. Check for errors
4. ✅ **PASS** if web part renders

**If FAIL:**
- Check browser console for error messages
- Verify Function App URL is correct

---

### Step 3: Test Configuration Save
1. Fill configuration form
2. Click "Save Configuration"
3. Check browser console (F12)
4. ✅ **PASS** if you see: `Configuration saved successfully`

**If FAIL:**
- Check browser console for CORS errors
- Check Network tab for failed requests
- Verify Function App is accessible

---

### Step 4: Test Document Processing
1. Upload a .docx file to Source Folder
2. Wait 30-60 seconds
3. Check Function App → **Log stream**
4. Look for: `✅ [ENRICHMENT] Document enriched successfully`
5. ✅ **PASS** if enriched document appears in Destination Folder

**If FAIL:**
- Check Function App logs for error details
- Check Application Insights → **Failures**
- Verify template file exists

---

## 🔍 Where to Find Logs

### Function App Logs
- **Real-time:** Azure Portal → Function App → **Log stream**
- **File logs:** `Logs/sme-pilot-{date}.log` (via Kudu or download)
- **Telemetry:** Application Insights → **Logs**

### SPFx Logs
- **Browser Console:** F12 → **Console** tab
- **Network:** F12 → **Network** tab (for API calls)

---

## 🐛 Common Issues & Quick Fixes

### Issue: CORS Error
**Symptom:** Browser console shows "CORS error"
**Fix:** Configure CORS in Azure Functions portal

### Issue: Template File Not Found
**Symptom:** Logs show "Template file not found"
**Fix:** Ensure `Templates/**/*` is in deployment package

### Issue: Function App Not Starting
**Symptom:** Function App shows "Error" status
**Fix:** Check environment variables (Graph API credentials)

---

**Quick Test Time:** ~10 minutes  
**Full Test Time:** ~1 hour

