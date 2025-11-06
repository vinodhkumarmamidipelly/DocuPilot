# SPFx Upgrade Guide - Fix Webpack Issue

## 🎯 Goal
Upgrade SPFx from 1.18.2 to 1.21+ to fix webpack stats error.

## 📋 Requirements
- **Node.js 22+** (currently on 18.20.4)
- **SPFx 1.21+** (currently on 1.18.2)

## 🔄 Upgrade Steps

### Step 1: Upgrade Node.js to 22+
```powershell
nvm install 22
nvm use 22
node --version  # Verify: v22.x.x
```

### Step 2: Update package.json
Update all `@microsoft/sp-*` packages to latest version (1.21+)

### Step 3: Clean and Reinstall
```powershell
cd D:\CodeBase\DocuPilot\SMEPilot.SPFx
gulp clean
Remove-Item -Recurse -Force node_modules
npm install
```

### Step 4: Update React (if needed)
SPFx 1.21+ may require React 18

### Step 5: Test Build
```powershell
npm run build
gulp bundle --ship
```

## ⚠️ Breaking Changes
- React 18 (if upgrading)
- TypeScript config changes
- API changes in SPFx

## ✅ Expected Result
- Build completes successfully
- Release folder populated with files
- No webpack errors

