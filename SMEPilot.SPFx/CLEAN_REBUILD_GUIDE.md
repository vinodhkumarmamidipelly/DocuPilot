# Clean Rebuild Guide - Starting Fresh

## ✅ Files Already Deleted
- `node_modules/` ✓
- `lib/` ✓
- `temp/` ✓
- `dist/` ✓
- `release/` ✓
- `gulpfile.js` (recreated clean) ✓

## 📋 Clean Rebuild Steps

### Step 1: Verify Node Version
```powershell
node --version
```
**Expected:** `v22.x.x` (SPFx 1.21+ requires Node 22)

If not Node 22, switch:
```powershell
nvm use 22
# OR manually set PATH if nvm doesn't work
```

### Step 2: Install Dependencies
```powershell
cd D:\CodeBase\DocuPilot\SMEPilot.SPFx
npm install
```

### Step 3: Trust Development Certificate
```powershell
npx gulp trust-dev-cert
```

### Step 4: Build
```powershell
npx gulp build
```

### Step 5: Bundle
```powershell
npx gulp bundle
```

### Step 6: Start Development Server
```powershell
npx gulp serve
```

### Step 7: Open Workbench
1. Copy the **EXACT** `debugManifestsFile` URL from the console output
2. Open: `https://onblick.sharepoint.com/sites/DocEnricher-PoC/_layouts/15/workbench.aspx?debug=true&noredir=true&debugManifestsFile=<URL_FROM_CONSOLE>`

## 🔍 Current Configuration

### Entry Points (config.json)
- DocumentUploader: `./lib/webparts/documentUploader/index.js`
- AdminPanel: `./lib/webparts/adminPanel/index.js`

### Manifest Entry Module IDs
- DocumentUploader: `"./index"`
- AdminPanel: `"./index"`

### Index.ts Files
Both properly export the web part class as default:
```typescript
export * from './WebPart';
import WebPart from './WebPart';
export default WebPart;
```

## ✅ What Should Work Now

1. **Entry points match manifests** - Both point to `index.js`
2. **Index files export correctly** - Default export is the web part class
3. **Clean gulpfile** - Minimal, no complex hooks
4. **SPFx 1.21.1** - Latest version with fixes

## 🐛 If Issues Persist

1. **Check console logs** - Look for module loading errors
2. **Verify bundle loads** - Check Network tab for `*.js` files (status 200)
3. **Check manifest** - Verify `entryModuleId` matches entrypoint in config.json
4. **Clear browser cache** - Hard refresh (Ctrl+Shift+R)

## 📝 Notes

- No ngrok needed - SPFx serve uses HTTPS on localhost:4321
- Hosted workbench is correct for SPFx 1.21+
- All previous workarounds removed - clean setup

