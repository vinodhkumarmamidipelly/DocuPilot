# Quick Start: Run GetSharePointIDs.ps1

## 🎯 Easiest Way: Visual Studio Terminal

1. **In Visual Studio:**
   - View → Terminal (or press `Ctrl + \`)
   - Terminal opens at bottom

2. **Copy and paste this command:**

```powershell
.\GetSharePointIDs.ps1 -SiteUrl "https://onblick.sharepoint.com/sites/YOUR_SITE_NAME"
```

**Replace `YOUR_SITE_NAME`** with your actual SharePoint site name.

**Example:**
```powershell
.\GetSharePointIDs.ps1 -SiteUrl "https://onblick.sharepoint.com/sites/DocEnricher-PoC"
```

3. **Press Enter**

4. **What happens:**
   - Browser opens → Sign in with your work account (`vinodkumar@rigaps.com`)
   - Grant permissions → Click "Accept"
   - Script fetches IDs → Results displayed

---

## 🔧 Option 2: Windows PowerShell

1. **Open PowerShell:**
   - Press `Win + X` → Select "Windows PowerShell"
   - OR search "PowerShell" in Start menu

2. **Navigate to project:**
```powershell
cd D:\CodeBase\DocuPilot
```

3. **Run script:**
```powershell
.\GetSharePointIDs.ps1 -SiteUrl "https://onblick.sharepoint.com/sites/YOUR_SITE_NAME"
```

---

## ⚠️ First Time Setup (If Needed)

**If you get error: "Cannot run script"**

Run this FIRST:
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

**If you get error: "Module Microsoft.Graph not found"**

Run this FIRST:
```powershell
Install-Module Microsoft.Graph -Scope CurrentUser
```
(Press `Y` when prompted)

---

## 📋 What You'll Get

After running, you'll see:
- ✅ **Site ID** (long string starting with `onblick.sharepoint.com,`)
- ✅ **Drive ID** (starts with `b!`)
- ✅ **Tenant ID** (UUID format)
- ✅ Ready-to-use command for next step

---

## 🚀 Quick Command (Copy-Paste Ready)

**Replace `YOUR_SITE_NAME` and run:**

```powershell
cd D:\CodeBase\DocuPilot; .\GetSharePointIDs.ps1 -SiteUrl "https://onblick.sharepoint.com/sites/YOUR_SITE_NAME"
```

---

**Need help? Share the error message you see!**

