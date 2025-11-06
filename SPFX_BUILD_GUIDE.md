# SPFx UI Project - Build Guide

## 🎯 Goal

Build and package the SPFx SharePoint App while waiting for SharePoint indexing.

---

## ✅ Prerequisites Check

### **Step 1: Verify Node.js Version**

**Required:** Node.js v18.x (SPFx 1.18.2 requirement)

**Check version:**
```powershell
cd SMEPilot.SPFx
node --version
```

**Should show:** `v18.x.x`

**If wrong version:**
- Use `nvm` to switch: `nvm use 18`
- Or install Node 18 from nodejs.org

---

## 🚀 Build Steps

### **Step 1: Navigate to SPFx Folder**

```powershell
cd D:\CodeBase\DocuPilot\SMEPilot.SPFx
```

---

### **Step 2: Install Dependencies (If Needed)**

**Check if node_modules exists:**
- If `node_modules` folder exists → Skip this step
- If not → Run: `npm install`

**If you see errors, try:**
```powershell
npm install --legacy-peer-deps
```

---

### **Step 3: Clean Previous Builds (Optional)**

```powershell
npm run clean
```

**Or:**
```powershell
gulp clean
```

---

### **Step 4: Build SPFx Solution**

**Development build:**
```powershell
npm run build
```

**Or:**
```powershell
gulp build
```

**This will:**
- Compile TypeScript to JavaScript
- Bundle web parts
- Create output in `lib/` folder

---

### **Step 5: Package for Production**

**After successful build:**

```powershell
gulp bundle --ship
```

**Then:**

```powershell
gulp package-solution --ship
```

**This creates:**
- `sharepoint/solution/smepilot.sppkg` - App package file

---

## 🔍 Troubleshooting Common Issues

### **Issue 1: Node Version Mismatch**

**Error:** "Your dev environment is running NodeJS version..."

**Fix:**
```powershell
nvm use 18
# Or install Node 18
```

---

### **Issue 2: npm install Errors**

**Error:** Peer dependency conflicts

**Fix:**
```powershell
npm install --legacy-peer-deps
```

---

### **Issue 3: TypeScript Errors**

**Error:** Type errors in code

**Fix:**
- Check `tsconfig.json` is correct
- Fix TypeScript errors shown
- Common: Missing imports, type mismatches

---

### **Issue 4: Webpack/Build Errors**

**Error:** Webpack compilation errors

**Fix:**
- Check `config.json` format
- Verify all web parts are listed in bundles
- Check for missing dependencies

---

### **Issue 5: Missing Dependencies**

**Error:** "Cannot find module..."

**Fix:**
```powershell
npm install <package-name>
```

---

## 📋 Build Checklist

- [ ] Node.js v18.x installed
- [ ] Navigate to SMEPilot.SPFx folder
- [ ] Run `npm install` (if needed)
- [ ] Run `npm run build` or `gulp build`
- [ ] Fix any build errors
- [ ] Run `gulp bundle --ship`
- [ ] Run `gulp package-solution --ship`
- [ ] Verify `.sppkg` file created

---

## 🎯 Expected Output

**After successful build:**

1. **lib/ folder** - Compiled JavaScript files
2. **release/ folder** - Bundled files
3. **sharepoint/solution/smepilot.sppkg** - App package (for deployment)

---

## 🚀 Next Steps After Build

1. **Deploy to App Catalog:**
   - Upload `smepilot.sppkg` to SharePoint App Catalog
   - Deploy to your site

2. **Add Web Parts to Page:**
   - Edit a SharePoint page
   - Add "DocumentUploader" web part
   - Add "AdminPanel" web part

3. **Test Functionality:**
   - Upload a document
   - Verify it triggers enrichment
   - Check admin panel for status

---

## 💡 Quick Start Commands

```powershell
# Navigate to SPFx folder
cd D:\CodeBase\DocuPilot\SMEPilot.SPFx

# Check Node version
node --version

# Install dependencies (if needed)
npm install

# Build solution
npm run build

# Package for production
gulp bundle --ship
gulp package-solution --ship
```

---

**Let's start building! Follow the steps above.** 🚀

