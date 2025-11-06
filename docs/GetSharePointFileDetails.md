# How to Get SharePoint File Details for Testing

## Method 1: From SharePoint URL

### Step 1: Get SharePoint Site URL

When you open a file in SharePoint, the URL looks like:
```
https://yourtenant.sharepoint.com/sites/SiteName/Shared%20Documents/file.docx
```

### Step 2: Use Graph Explorer to Get IDs

1. Go to [Microsoft Graph Explorer](https://developer.microsoft.com/graph/graph-explorer)
2. Sign in with your account
3. Run these queries:

**Get Site ID:**
```
GET https://graph.microsoft.com/v1.0/sites/yourtenant.sharepoint.com:/sites/SiteName
```
Response contains `id` → This is your **siteId**

**Get Drive ID:**
```
GET https://graph.microsoft.com/v1.0/sites/{siteId}/drives
```
Response contains `id` for the document library → This is your **driveId**

**Get Item ID:**
```
GET https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/root:/Shared Documents/file.docx
```
Response contains `id` → This is your **itemId**

---

## Method 2: Using PowerShell (Graph Module)

### Install Graph Module
```powershell
Install-Module Microsoft.Graph -Scope CurrentUser
```

### Connect and Get IDs
```powershell
# Connect
Connect-MgGraph -Scopes "Sites.ReadWrite.All"

# Get Site
$site = Get-MgSite -SiteId "yourtenant.sharepoint.com:/sites/SiteName"
$siteId = $site.Id

# Get Drive
$drives = Get-MgSiteDrive -SiteId $siteId
$driveId = $drives[0].Id  # Usually first drive is Documents library

# Get File
$file = Get-MgDriveItem -DriveId $driveId -ItemId "root:/Shared Documents/your-file.docx"
$itemId = $file.Id

Write-Host "Site ID: $siteId"
Write-Host "Drive ID: $driveId"
Write-Host "Item ID: $itemId"
```

---

## Method 3: From SharePoint UI

1. **Open SharePoint file**
2. **Click "..." (three dots)** → **Details**
3. **Check URL** - sometimes contains IDs
4. **Or use browser DevTools:**
   - Press F12
   - Network tab
   - Look for API calls with IDs in responses

---

## Quick Test Values

For testing with a **real SharePoint document**, you need:

```json
{
  "siteId": "abc123.site,def456,ghi789",
  "driveId": "b!xyz...",
  "itemId": "01ABC...",
  "fileName": "your-document.docx",
  "uploaderEmail": "your-email@domain.com",
  "tenantId": "12345678-1234-1234-1234-123456789012"
}
```

---

## Test Command

Once you have the IDs:

```powershell
$body = @{
    siteId = "YOUR_SITE_ID"
    driveId = "YOUR_DRIVE_ID"
    itemId = "YOUR_ITEM_ID"
    fileName = "your-document.docx"
    uploaderEmail = "your-email@domain.com"
    tenantId = "YOUR_TENANT_ID"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:7071/api/ProcessSharePointFile" `
    -Method Post `
    -Body $body `
    -ContentType "application/json"
```

---

**Note:** Once configured, you can also set up Graph webhooks to automatically trigger when files are uploaded - no need to manually call the endpoint!

