# SPFx Upgrade to 1.21.1 - Step by Step Guide

## ✅ Prerequisites Check

**Step 1: Verify Node 22 is Active**

Open **CMD** and run:
```cmd
node --version
```

**Expected:** `v22.x.x`

**If not v22.x.x**, activate Node 22:
```cmd
nvm use 22
```

**If nvm fails**, manually set PATH:
```cmd
set PATH=C:\Users\Vinod Kumar\AppData\Local\nvm\v22.21.1;%PATH%
set PATH=C:\Users\Vinod Kumar\AppData\Local\nvm\v22.21.1\node_modules\npm\bin;%PATH%
node --version
```

---

## 🔄 Upgrade Steps

### **Step 2: Navigate to Project**

```cmd
cd D:\CodeBase\DocuPilot\SMEPilot.SPFx
```

---

### **Step 3: Clean Previous Build**

```cmd
npx gulp clean
```

**Expected:** Clean completed successfully

---

### **Step 4: Remove Old Packages**

```cmd
rmdir /s /q node_modules
del package-lock.json
```

**Expected:** Old files removed

---

### **Step 5: Install New Packages**

```cmd
npm install --legacy-peer-deps
```

**This will take 3-5 minutes. Wait for completion.**

**Expected:** 
- ✅ Packages installed
- ⚠️ Some warnings are OK (peer dependencies)

---

### **Step 6: Verify Installation**

```cmd
npm list @microsoft/sp-core-library
```

**Expected:** `@microsoft/sp-core-library@1.21.1`

---

### **Step 7: Test Build (Debug)**

```cmd
npm run build
```

**Expected:**
- ✅ TypeScript compiles
- ✅ Webpack bundles
- ✅ Build completes successfully
- ✅ **NO webpack errors!**

**Time:** ~1-2 minutes

---

### **Step 8: Test Production Build**

```cmd
npx gulp bundle --ship
```

**Expected:**
- ✅ Production build completes
- ✅ Release folder populated with files!

**Time:** ~30 seconds

---

### **Step 9: Verify Release Folder**

```cmd
dir release\manifests
```

**Expected:** Should see `.json` manifest files

---

### **Step 10: Package Solution**

```cmd
npx gulp package-solution --ship
```

**Expected:**
- ✅ Package created successfully
- ✅ File: `solution\sme-pilot.sppkg`

---

## ✅ Success Checklist

After completing all steps, verify:

- [ ] Node version: `v22.x.x`
- [ ] SPFx version: `1.21.1` (check `npm list @microsoft/sp-core-library`)
- [ ] Build completes without errors
- [ ] Release folder has files
- [ ] `.sppkg` file created in `solution/` folder

---

## 🐛 Troubleshooting

### **Issue: npm install fails**

**Solution:**
```cmd
npm install --legacy-peer-deps --force
```

---

### **Issue: Build fails with TypeScript errors**

**Solution:**
```cmd
npx gulp clean
npm install --legacy-peer-deps
npm run build
```

---

### **Issue: Webpack errors still appear**

**Solution:** Check `gulpfile.js` - webpack suppression should still be there, but SPFx 1.21+ should fix it automatically.

---

### **Issue: Node version still shows v18**

**Solution:**
```cmd
REM Close CMD and open NEW CMD window
REM Then activate Node 22:
nvm use 22
REM Or manually:
set PATH=C:\Users\Vinod Kumar\AppData\Local\nvm\v22.21.1;%PATH%
node --version
```

---

## 📋 Quick Copy-Paste (All Steps)

```cmd
REM Step 1: Verify Node 22
node --version

REM Step 2: Navigate
cd D:\CodeBase\DocuPilot\SMEPilot.SPFx

REM Step 3: Clean
npx gulp clean

REM Step 4: Remove old packages
rmdir /s /q node_modules
del package-lock.json

REM Step 5: Install
npm install --legacy-peer-deps

REM Step 6: Verify
npm list @microsoft/sp-core-library

REM Step 7: Build
npm run build

REM Step 8: Production build
npx gulp bundle --ship

REM Step 9: Check release folder
dir release\manifests

REM Step 10: Package
npx gulp package-solution --ship
```

---

## 🎯 Expected Results

After completion:

✅ **SPFx 1.21.1** installed  
✅ **React 18.2.0** installed  
✅ **Build works** without webpack errors  
✅ **Release folder** populated  
✅ **`.sppkg` file** ready for deployment  

---

**Ready to start?** Begin with Step 1! 🚀

