# Get Tenant ID - Alternative Methods (400 Error Fix)

## Problem: `/organization` Endpoint Requires Admin Permissions

If you get "Bad Request - 400" when querying `/organization`, it means your account doesn't have the required permissions.

## Alternative Methods (No Admin Permissions Needed)

---

## Method 1: Get Tenant ID from Your Profile (Easiest)

### In Graph Explorer:

1. **Change the query** from:
   ```
   https://graph.microsoft.com/v1.0/organization
   ```
   
   **To:**
   ```
   https://graph.microsoft.com/v1.0/me
   ```

2. **Click "Run query"**

3. **Look at the response** - You'll see something like:
   ```json
   {
     "@odata.context": "https://graph.microsoft.com/v1.0/$metadata#users/$entity",
     "id": "abc123...",
     "userPrincipalName": "vinodkumar@rigaps.com",
     ...
   }
   ```

4. **The tenant ID is in your UPN domain:**
   - Your UPN: `vinodkumar@rigaps.com`
   - Domain: `rigaps.com` or `rigaps.onmicrosoft.com`
   - But we need the UUID format...

### Better: Get from Site Information

---

## Method 2: Get from SharePoint Site (Recommended)

### In Graph Explorer:

**Query:**
```
https://graph.microsoft.com/v1.0/sites/onblick.sharepoint.com
```

**Response will contain:**
```json
{
  "id": "abc123.sharepoint.com,def456-SITE-ID,ghi789",
  ...
}
```

The Site ID format is: `domain.sharepoint.com,TENANT-ID,SITE-ID`

**Extract the middle part** (between commas) - that's part of your tenant structure, but not the exact Tenant ID.

---

## Method 3: Use PowerShell Script (Best Alternative)

I've updated the script to use methods that don't require admin permissions.

**Run this:**

```powershell
# Connect to Graph
Connect-MgGraph -Scopes "User.Read"

# Get your context
$context = Get-MgContext

# Tenant ID is in the context
Write-Host "Tenant ID: $($context.TenantId)"

# Or get from your profile
$me = Get-MgUser -UserId "vinodkumar@rigaps.com"
# Tenant ID might be extractable from domain

Disconnect-MgGraph
```

---

## Method 4: Extract from SharePoint URL/Site ID

### If you have a SharePoint site:

The Site ID format is: `domain.sharepoint.com,TENANT-UUID,SITE-GUID`

**Example:**
```
Site ID: "onblick.sharepoint.com,12345678-1234-1234-1234-123456789012,abc123..."
                                              ↑
                                        This is your Tenant ID!
```

### How to get Site ID in Graph Explorer:

**Query:**
```
https://graph.microsoft.com/v1.0/sites/onblick.sharepoint.com
```

**Response:**
```json
{
  "id": "onblick.sharepoint.com,12345678-1234-1234-1234-123456789012,abc123...",
  ...
}
```

**Extract the UUID** (second part, between commas) - That's your Tenant ID!

---

## Method 5: Ask Admin or Check Existing Config

If you have:
- Existing Azure Function Apps
- Other Microsoft 365 integrations
- Configuration files

The Tenant ID might already be documented there.

**Or simply ask your IT admin** - They can get it quickly from:
- Azure Portal → Azure AD → Overview → Tenant ID
- Or PowerShell: `Get-AzureADTenantDetail`

---

## Quick Fix: Use Domain for Testing

**For Power Automate testing**, you can temporarily use:

```json
{
  "tenantId": "rigaps.onmicrosoft.com"
}
```

Or:
```json
{
  "tenantId": "rigaps"
}
```

**Note**: SMEPilot expects UUID format, but for testing this might work in some cases.

---

## Method 6: Check Browser Cookies/Tokens

1. **In Graph Explorer**, open Browser DevTools (F12)
2. **Go to Application** tab → Cookies
3. **Look for tokens** - Tenant ID might be visible in token claims
4. **Or check Network** tab → Look at request headers

---

## Recommended Solution

### Option A: Ask IT Admin (Fastest)
- Send message: "I need the Azure AD Tenant ID for SMEPilot configuration"
- They can get it in 30 seconds

### Option B: Use PowerShell Script
```powershell
.\GetTenantID.ps1
```
This uses `User.Read` permission which regular users have.

### Option C: Extract from SharePoint Site ID
- Get Site ID from SharePoint
- Extract UUID from middle of Site ID string

---

## Updated PowerShell Script

I'll update `GetTenantID.ps1` to use alternative methods that work without admin permissions.

---

**Try Method 4 (Extract from SharePoint Site ID) - It's the most reliable without admin access!**

