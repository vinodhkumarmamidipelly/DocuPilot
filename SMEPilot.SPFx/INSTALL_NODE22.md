# Install Node 22 - Step by Step Guide

## 🎯 Goal
Install Node 22 to enable SPFx 1.21+ upgrade and fix webpack issue properly.

## ⚠️ Known Issue
nvm-windows has activation issues with spaces in username ("Vinod Kumar"). We'll work around this.

## 📋 Installation Options

### **Option 1: Manual Installation (RECOMMENDED)**

**Easiest and most reliable:**

1. **Download Node 22:**
   - Go to: https://nodejs.org/
   - Download **Node.js 22.x LTS** (Windows Installer .msi)
   - Choose **64-bit** version

2. **Install:**
   - Run the installer
   - Use default settings
   - ✅ Check "Add to PATH" option
   - Complete installation

3. **Verify:**
   ```powershell
   # Open NEW PowerShell window
   node --version  # Should show v22.x.x
   npm --version   # Should show 10.x.x
   ```

4. **Then upgrade SPFx:**
   ```powershell
   cd D:\CodeBase\DocuPilot\SMEPilot.SPFx
   # Update package.json to SPFx 1.21.1
   npm install
   npm run build
   ```

---

### **Option 2: Fix nvm-windows (Advanced)**

**If you prefer using nvm:**

1. **Node 22 is already installed** (we installed it earlier)
   - Location: `C:\Users\Vinod Kumar\AppData\Local\nvm\v22.21.1`

2. **Manual activation** (workaround for spaces in username):
   ```powershell
   $nvmPath = "C:\Users\Vinod Kumar\AppData\Local\nvm"
   $node22Path = "$nvmPath\v22.21.1"
   $env:Path = "$node22Path;$node22Path\node_modules\npm\bin;$env:Path"
   node --version  # Verify: v22.21.1
   ```

3. **Make it permanent** (add to PowerShell profile):
   ```powershell
   # Edit profile
   notepad $PROFILE
   
   # Add this line:
   $env:Path = "C:\Users\Vinod Kumar\AppData\Local\nvm\v22.21.1;C:\Users\Vinod Kumar\AppData\Local\nvm\v22.21.1\node_modules\npm\bin;$env:Path"
   ```

---

## ✅ Recommended: Option 1 (Manual Install)

**Why?**
- ✅ No nvm activation issues
- ✅ Works immediately
- ✅ Easier to manage
- ✅ Better for production

**After manual install, we'll:**
1. Upgrade SPFx to 1.21.1
2. Update React to 18.2.0
3. Fix webpack properly
4. Get release folder populated!

---

## 🚀 Next Steps After Node 22 Installation

Once Node 22 is installed and verified:

1. **I'll update package.json** to SPFx 1.21.1
2. **Clean and reinstall** packages
3. **Test build** - should work perfectly!
4. **Release folder** should be populated!

---

**Ready to proceed?** Install Node 22 manually, then let me know and I'll complete the SPFx upgrade! 🎯

