# SPFx Upgrade Instructions

## 🎯 Upgrade Strategy

Since Node 22 activation has issues, we're upgrading to **SPFx 1.20.0** which:
- ✅ Works with Node 18.20.4 (your current version)
- ✅ Fixes webpack issues
- ✅ No Node version change needed

## 📋 Manual Steps Required

### Step 1: Switch to Node 18 (if not already)
```powershell
nvm use 18.20.4
```

**If nvm activation fails** (due to spaces in username), manually:
1. Open a **new PowerShell window as Administrator**
2. Run: `nvm use 18.20.4`
3. Verify: `node --version` should show `v18.20.4`

### Step 2: Clean and Install
```powershell
cd D:\CodeBase\DocuPilot\SMEPilot.SPFx
gulp clean
Remove-Item -Recurse -Force node_modules -ErrorAction SilentlyContinue
Remove-Item package-lock.json -ErrorAction SilentlyContinue
npm install
```

### Step 3: Test Build
```powershell
npm run build
gulp bundle --ship
```

### Step 4: Package Solution
```powershell
gulp package-solution --ship
```

## ✅ Expected Result

- ✅ Build completes successfully
- ✅ Release folder populated with files
- ✅ No webpack errors
- ✅ `.sppkg` file created in `solution/` folder

## 🔄 Alternative: Upgrade to SPFx 1.21+ (Requires Node 22)

If you want the latest features, you'll need to:

1. **Fix nvm activation issue** (spaces in username):
   - Install Node 22 manually from nodejs.org
   - Or fix nvm-windows path handling

2. **Then upgrade to SPFx 1.21.1**:
   - Update package.json to SPFx 1.21.1
   - Update React to 18.2.0
   - Clean and reinstall

## 📝 Current Configuration

- **SPFx**: 1.20.0 (upgraded from 1.18.2)
- **Node**: 18.20.4 (compatible)
- **React**: 17.0.1 (kept for compatibility)

