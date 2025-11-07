# Debug Web Parts - Step by Step

## Current Issue
Web parts showing "Something went wrong" error in SharePoint workbench.

## What We've Fixed
1. ✅ React 18 compatibility (using `createRoot`)
2. ✅ Improved error handling (shows actual error messages)
3. ✅ Build completed successfully

## Next Steps to Debug

### Step 1: Start Serve Task
```powershell
cd D:\CodeBase\DocuPilot\SMEPilot.SPFx
npx gulp serve
```

Wait for it to say "Finished subtask 'serve'"

### Step 2: Open Browser Console
1. Open workbench: `https://onblick.sharepoint.com/sites/DocEnricher-PoC/_layouts/15/workbench.aspx`
2. Press **F12** to open Developer Tools
3. Click **Console** tab
4. Look for **RED errors**

### Step 3: Check Error Details
The improved error handling should now show:
- Actual error message (not just "[object Object]")
- Error stack trace
- Which component is failing

### Step 4: Share Error Details
Please share:
1. **Browser console errors** (copy/paste the red error messages)
2. **Any errors in terminal** where `gulp serve` is running
3. **Screenshot** of the error message if it shows details now

## Common Issues to Check

### Issue 1: Module Not Found
**Error:** `Cannot find module '...'`
**Fix:** Check if all dependencies are installed: `npm install`

### Issue 2: Import Error
**Error:** `Failed to resolve module '...'`
**Fix:** Check import paths in component files

### Issue 3: React Version Mismatch
**Error:** `Invalid hook call` or `React version mismatch`
**Fix:** Ensure React 18.2.0 is installed correctly

### Issue 4: SPHttpClient Error
**Error:** `spHttpClient is undefined`
**Fix:** Check if `this.context.spHttpClient` is available

## What to Look For

When you check the browser console, look for errors like:
- `Error: Cannot read property '...' of undefined`
- `Error: Module not found: ...`
- `Error: Invalid hook call`
- `TypeError: ...`

These will tell us exactly what's wrong!

