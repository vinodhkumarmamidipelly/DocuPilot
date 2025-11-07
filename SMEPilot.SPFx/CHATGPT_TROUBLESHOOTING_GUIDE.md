# ChatGPT's SPFx Workbench Troubleshooting Guide

## ✅ Applied Fixes

### 1. Defensive Error Handling in DocumentUploader.tsx
- ✅ Added null checks for `webUrl`
- ✅ Better error logging with HTTP status codes
- ✅ Defensive `componentDidMount()` with try/catch
- ✅ Proper state updates on errors

### 2. Manifests.js Path
- ✅ Current setup: Copies `temp/build/manifests.js` → `temp/manifests.js`
- ✅ SPFx serve generates the correct URL automatically
- ✅ Use the **EXACT URL** from `gulp serve` output (don't hardcode)

## 📋 Proper Workflow (ChatGPT's Recommendations)

### Step 1: Setup
```powershell
cd D:\CodeBase\DocuPilot\SMEPilot.SPFx
node -v  # Should be v22.x
npm install
npx gulp trust-dev-cert
```

### Step 2: Start Serve
```powershell
npx gulp serve
```

### Step 3: Copy EXACT URL from Output
When `gulp serve` starts, it will print:
```
Server started https://localhost:4321
To load your scripts, use this query string: 
?debug=true&noredir=true&debugManifestsFile=https://localhost:4321/temp/build/manifests.js
```

**⚠️ IMPORTANT:** Copy the ENTIRE `debugManifestsFile` URL from the output!

### Step 4: Open Hosted Workbench
Use the EXACT URL from Step 3:
```
https://onblick.sharepoint.com/sites/DocEnricher-PoC/_layouts/15/workbench.aspx?debug=true&noredir=true&debugManifestsFile=https://localhost:4321/temp/build/manifests.js
```

## 🔍 Verification Steps

### 1. Check manifests.js is Served
Open in browser:
```
https://localhost:4321/temp/build/manifests.js
```

**Expected:**
- ✅ JS content loads (status 200)
- ✅ No certificate errors (trust dev cert first)

**If ERR_CERT_AUTHORITY_INVALID:**
- Run: `npx gulp trust-dev-cert`
- Accept certificate in browser manually

**If 404:**
- Ensure `gulp serve` is running
- Check firewall/proxy isn't blocking localhost:4321

### 2. Clear Service Workers & Cache
In Chrome DevTools:
1. **Application** → **Service Workers** → Unregister any `spserviceworker*`
2. **Application** → **Clear storage** → **Clear site data**
3. Hard reload: `Ctrl+Shift+R` or use Incognito

### 3. Check Network Tab
- Filter for `manifests.js`
- Status should be **200**
- Check for any blocked `localhost:4321` requests

## 🐛 Common Issues & Fixes

### Issue: "Something went wrong" in workbench
**Solution:**
1. Use EXACT URL from `gulp serve` output
2. Clear service workers (Step 2 above)
3. Check console for first red error
4. Verify `manifests.js` returns 200

### Issue: ERR_SSL_PROTOCOL_ERROR
**Solution:**
- Ensure `serve.json` has `"https": true`
- Trust dev cert: `npx gulp trust-dev-cert`
- Accept certificate in browser

### Issue: ERR_CERT_AUTHORITY_INVALID
**Solution:**
- Run: `npx gulp trust-dev-cert`
- Manually open `https://localhost:4321` in browser
- Click "Advanced" → "Proceed to localhost"

### Issue: 404 for manifests.js
**Solution:**
- Ensure `gulp serve` is running
- Check path: should be `/temp/build/manifests.js`
- Rebuild: `npx gulp clean && npx gulp build`

### Issue: Web part crashes with "Cannot read properties of undefined"
**Solution:**
- ✅ Already fixed with defensive error handling
- Check console for specific error
- Verify props are passed correctly

## 🎯 Expected Outcomes

After following all steps:
- ✅ `https://localhost:4321/temp/build/manifests.js` loads (200 OK)
- ✅ Hosted workbench loads without "Something went wrong"
- ✅ DocumentUploader web part renders
- ✅ Console shows: "DocumentUploader mounted"
- ✅ No red errors in console

## 📝 Notes

1. **Don't hardcode URLs** - Always use what `gulp serve` outputs
2. **Clear cache regularly** - Service workers can cause stale issues
3. **Use Incognito for testing** - Avoids extension/cache interference
4. **Check console first** - First red error usually reveals the issue

## 🔄 If Still Failing

Provide these to troubleshoot:
1. First red console error (full stack trace)
2. Network tab: `manifests.js` status + response body snippet
3. Complete `gulp serve` output
4. Exact workbench URL you used
5. `DocumentUploader.tsx` file contents (if modified)

