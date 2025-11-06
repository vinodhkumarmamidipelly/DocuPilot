# How to Get Tenant ID Without Azure Portal Access

## The Problem

Regular personnel accounts typically **don't have access** to Azure Portal (requires admin permissions).

## Solutions (No Azure Portal Needed)

---

## Method 1: From SharePoint URL (Easiest)

### Step 1: Look at Your SharePoint URL

Your SharePoint URL: `https://onblick.sharepoint.com/...`

The **tenant domain** is: `onblick.sharepoint.com`

### Step 2: Get Tenant ID from Domain

**Option A: PowerShell (with Microsoft Graph module)**

```powershell
# Connect to Graph (requires Sign-In permission only)
Connect-MgGraph -Scopes "User.Read"

# Get tenant info
$context = Get-MgContext
$tenantId = $context.TenantId

Write-Host "Tenant ID: $tenantId"
```

**Option B: From SharePoint Site Properties**

1. Go to SharePoint site
2. Settings → Site information
3. Tenant ID might be visible in site properties

**Option C: Ask Your IT Admin**

- IT/Admin has access to Azure Portal
- They can get Tenant ID from: Azure AD → Overview
- Or share it from existing configuration

---

## Method 2: From Microsoft Graph Explorer (No Admin Needed)

### Step 1: Go to Graph Explorer
https://developer.microsoft.com/graph/graph-explorer

### Step 2: Sign In
- Sign in with your work account
- Grant permissions when prompted

### Step 3: Run Query
```
GET https://graph.microsoft.com/v1.0/organization
```

### Step 4: Get Tenant ID
Response will contain:
```json
{
  "value": [{
    "id": "12345678-1234-1234-1234-123456789012",
    "displayName": "OnBlick Inc",
    ...
  }]
}
```

The `id` field is your **Tenant ID**.

---

## Method 3: From SharePoint REST API

### Step 1: Go to SharePoint Site
Navigate to: `https://onblick.sharepoint.com/sites/YOUR-SITE`

### Step 2: Open Browser Console (F12)

### Step 3: Run JavaScript
```javascript
fetch("https://onblick.sharepoint.com/_api/web/currentuser")
  .then(r => r.json())
  .then(data => {
    console.log("User Info:", data);
    // Tenant ID might be in the response
  });
```

**Note**: This might not directly show Tenant ID, but can help.

---

## Method 4: Ask Admin/IT Department

**Recommended Approach:**

Ask your IT administrator or Azure AD admin for:
- **Tenant ID** (UUID format)
- Or confirm the tenant domain mapping

**What to say:**
> "I need the Azure AD Tenant ID for [Your Organization Name] to configure SMEPilot integration. Can you provide it or guide me to where I can find it?"

---

## Method 5: From Existing Azure Configurations

If you or your organization already has:
- Azure Function Apps deployed
- Other Azure services configured
- Configuration files or scripts

Check existing configs - Tenant ID might already be documented.

---

## Method 6: Use Tenant Domain Instead (If Applicable)

Some configurations accept tenant domain instead of Tenant ID:
- Domain: `onblick.onmicrosoft.com` or `onblick.sharepoint.com`
- This might work for some integrations

**Note**: SMEPilot specifically needs Tenant ID (UUID), not domain name.

---

## Quick PowerShell Script (No Admin Rights Needed)

Create `GetTenantID.ps1`:

```powershell
# Install Microsoft Graph module if needed
# Install-Module Microsoft.Graph -Scope CurrentUser

Write-Host "Connecting to Microsoft Graph..." -ForegroundColor Yellow
Write-Host "You'll be prompted to sign in with your work account." -ForegroundColor Gray
Write-Host ""

Connect-MgGraph -Scopes "User.Read"

$context = Get-MgContext

if ($context) {
    Write-Host ""
    Write-Host "✅ Tenant ID Found:" -ForegroundColor Green
    Write-Host "   Tenant ID: $($context.TenantId)" -ForegroundColor Cyan
    Write-Host "   Domain: $($context.TenantDomain)" -ForegroundColor Cyan
    Write-Host ""
    
    # Copy to clipboard (optional)
    $context.TenantId | Set-Clipboard
    Write-Host "Tenant ID copied to clipboard!" -ForegroundColor Green
}
else {
    Write-Host "❌ Could not get tenant information" -ForegroundColor Red
}

Write-Host ""
Write-Host "Disconnecting..." -ForegroundColor Gray
Disconnect-MgGraph
```

**Run it:**
```powershell
.\GetTenantID.ps1
```

**Result:** Tenant ID displayed and copied to clipboard

---

## What SMEPilot Needs

SMEPilot needs Tenant ID in this format:
```
12345678-1234-1234-1234-123456789012
```

**UUID/GUID format** - Not just the domain name.

---

## Recommended Approach

### If You Have PowerShell Access:
✅ Use **Method 2 (Graph Explorer)** or **PowerShell script** above

### If You Don't Have PowerShell:
✅ Use **Method 2 (Graph Explorer)** - Browser-based, no installation

### If You Need Help:
✅ **Ask IT Admin** - They can get it in 30 seconds from Azure Portal

---

## For Testing (Temporary Solution)

If you just need to test Power Automate flow:

1. Use a **placeholder** like: `"default"` or `"test-tenant"`
2. SMEPilot will work in mock mode
3. Get real Tenant ID later for production

**Example for testing:**
```json
{
  "tenantId": "default"
}
```

This allows testing, but won't work for real multi-tenant scenarios.

---

## Summary

| Method | Requires | Difficulty |
|--------|----------|-----------|
| **Graph Explorer** | Browser only | ⭐ Easy |
| **PowerShell Script** | PowerShell + Graph module | ⭐ Easy |
| **Ask IT Admin** | None (for you) | ⭐⭐ Medium |
| **SharePoint REST** | Browser console | ⭐⭐ Medium |
| **Azure Portal** | Admin access | ⭐ Easy (if you have access) |

**Recommended:** **Graph Explorer** - Easiest, no special permissions needed!

---

**Ready to try? Start with Graph Explorer method!** 🚀

