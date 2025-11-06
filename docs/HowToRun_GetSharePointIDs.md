# How to Run GetSharePointIDs.ps1 - Step by Step

## Step-by-Step Instructions

### Step 1: Open PowerShell

**Option A: Windows PowerShell**
- Press `Win + X`
- Select "Windows PowerShell" or "Terminal"
- Or search "PowerShell" in Start menu

**Option B: From Visual Studio**
- View → Terminal
- Or: Terminal → New Terminal

### Step 2: Navigate to Project Folder

In PowerShell, type:

```powershell
cd D:\CodeBase\DocuPilot
```

**Press Enter**

### Step 3: Check if Script Exists

Verify the script is there:

```powershell
ls GetSharePointIDs.ps1
```

**OR:**

```powershell
Test-Path GetSharePointIDs.ps1
```

Should return: `True`

### Step 4: Install Microsoft Graph Module (If Needed)

**First time only**, install the module:

```powershell
Install-Module Microsoft.Graph -Scope CurrentUser
```

**If prompted:**
- Type `Y` for Yes
- May take 1-2 minutes

### Step 5: Run the Script

**Run this command:**

```powershell
.\GetSharePointIDs.ps1 -SiteUrl "https://onblick.sharepoint.com/sites/DocEnricher-PoC"
```

**What happens:**
1. Script connects to Microsoft Graph
2. You'll be prompted to sign in
3. Sign in with your work account (`vinodkumar@rigaps.com`)
4. Grant permissions when prompted
5. Script fetches Site ID and Drive ID
6. Results displayed

### Step 6: Copy the Values

**Script will output:**
- ✅ Site ID
- ✅ Drive ID (for Documents library)
- ✅ Tenant ID
- ✅ Ready-to-use SetupSubscription command

**Copy these values** - you'll need them for Step 3.

---

## Alternative: Manual Method (If Script Doesn't Work)

### Use Graph Explorer Instead

1. **Go to**: https://developer.microsoft.com/graph/graph-explorer
2. **Sign in** with your work account
3. **Query 1: Get Site ID**
   ```
   GET https://graph.microsoft.com/v1.0/sites/onblick.sharepoint.com:/sites/DocEnricher-PoC
   ```
   Copy the `id` value → **Site ID**

4. **Query 2: Get Drive ID**
   ```
   GET https://graph.microsoft.com/v1.0/sites/{site-id}/drives
   ```
   Replace `{site-id}` with Site ID from step 3
   Find "Documents" drive → Copy `id` → **Drive ID**

---

## Quick Reference

**Full command to run:**

```powershell
cd D:\CodeBase\DocuPilot
.\GetSharePointIDs.ps1 -SiteUrl "https://onblick.sharepoint.com/sites/DocEnricher-PoC"
```

**Expected output:**
```
✅ Site ID: onblick.sharepoint.com,12345678-1234-1234-1234-123456789012,abc123...
✅ Drive ID: b!xyz123...
✅ Tenant ID: 12345678-1234-1234-1234-123456789012
```

---

## Troubleshooting

### "Cannot run script - execution policy"
**Solution:**
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### "Module Microsoft.Graph not found"
**Solution:**
```powershell
Install-Module Microsoft.Graph -Scope CurrentUser
```

### "Access denied" or "Permission error"
- Ensure you're signed in with work account
- Grant permissions when prompted
- Check you have access to the SharePoint site

---

**Ready? Open PowerShell and run the command!** 🚀

