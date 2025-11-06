# Quick Fix: Run GetSharePointIDs.ps1

## ❌ Problem
You're in wrong directory: `C:\WINDOWS\system32`
Script is in: `D:\CodeBase\DocuPilot`

## ✅ Solution

### Step 1: Open PowerShell in Project Folder

**Option A: Visual Studio Terminal**
1. Visual Studio → View → Terminal
2. Should already be in `D:\CodeBase\DocuPilot`

**Option B: Windows PowerShell**
1. Open PowerShell
2. Run this command:
```powershell
cd D:\CodeBase\DocuPilot
```

### Step 2: Install Microsoft.Graph Module (First Time Only)

```powershell
Install-Module Microsoft.Graph -Scope CurrentUser
```

**When prompted:**
- Type `Y` and press Enter
- Wait 1-2 minutes (downloading)

### Step 3: Run the Script

```powershell
.\GetSharePointIDs.ps1 -SiteUrl "https://onblick.sharepoint.com/sites/DocEnricher-PoC"
```

**What happens:**
1. Browser opens → Sign in with `vinodkumar@rigaps.com`
2. Grant permissions → Click "Accept"
3. Script fetches Site ID and Drive ID
4. Results displayed

---

## 🚀 All-in-One Command

**Copy and paste this entire block:**

```powershell
cd D:\CodeBase\DocuPilot
Install-Module Microsoft.Graph -Scope CurrentUser
.\GetSharePointIDs.ps1 -SiteUrl "https://onblick.sharepoint.com/sites/DocEnricher-PoC"
```

**Note:** First command changes directory, second installs module (if needed), third runs script.

---

## 📋 Expected Output

```
✅ Site ID: onblick.sharepoint.com,12345678-1234-1234-1234-123456789012,abc123...
✅ Drive ID: b!xyz123...
✅ Tenant ID: 12345678-1234-1234-1234-123456789012
```

---

**Ready? Copy the "All-in-One Command" above and run it!** 🎯

