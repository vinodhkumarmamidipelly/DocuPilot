# Power Automate Quick Setup for SMEPilot

## You're Here: Trigger Created ✅

Now add the HTTP action to call SMEPilot.

---

## Quick Steps

### 1. Add HTTP Action
- Click **"+"** below your trigger
- Search **"HTTP"** → Select **"HTTP"**

### 2. Configure HTTP
- **Method**: `POST`
- **URI**: `https://your-function.azurewebsites.net/api/ProcessSharePointFile`
  - Replace with your actual Function App URL
- **Headers**: Add header
  - Key: `Content-Type`
  - Value: `application/json`

### 3. Body (JSON)
Copy this into the Body field:

```json
{
  "siteId": "@{triggerOutputs()?['body/SiteId']}",
  "driveId": "@{triggerOutputs()?['body/DriveId']}",
  "itemId": "@{triggerOutputs()?['body/Id']}",
  "fileName": "@{triggerOutputs()?['body/Name']}",
  "uploaderEmail": "@{triggerOutputs()?['body/CreatedBy/User/Email']}",
  "tenantId": "YOUR-TENANT-ID-HERE"
}
```

**Important**: 
- Replace `YOUR-TENANT-ID-HERE` with your actual Azure AD tenant ID
- Field names might vary - check what's available from your trigger

---

## How to Find Field Names

1. **Click on your "When a file is created" trigger**
2. **Look at "Outputs"** or click "{}" to see dynamic content
3. **Check available fields:**
   - File ID: Usually `Id` or `ItemId`
   - File Name: Usually `Name` or `DisplayName`
   - Drive ID: Usually `DriveId` or `ParentReference/DriveId`
   - Site ID: May need additional action to get

---

## If Field Names Don't Match

### Option 1: Check Trigger Outputs
- Click trigger → See available fields
- Adjust field names in JSON accordingly

### Option 2: Add "Get file properties" Action
- Add action before HTTP
- Get more detailed file information
- Use those fields instead

---

## Get Your Tenant ID

**To find your tenant ID:**
1. Go to Azure Portal
2. Azure Active Directory → Overview
3. Copy "Tenant ID" value

**Or from SharePoint URL:**
- Your tenant: `onblick`
- Format: Usually UUID like `12345678-1234-1234-1234-123456789012`

---

## Test

1. **Save** the flow
2. **Test** → "Manually"
3. **Upload a file** to SharePoint
4. **Check** Function App logs to see if it receives the request

---

## Troubleshooting

### "Invalid expression"
- Check field names match trigger outputs
- Try simpler field names first

### "HTTP 400"
- Verify JSON is valid
- Check all required fields present

### "Cannot find field"
- Add "Get file properties" action
- Or use SharePoint REST API to get metadata

---

**That's it! Once configured, files uploaded to SharePoint will automatically trigger SMEPilot!** ✅

