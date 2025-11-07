# Power Automate Setup for SMEPilot - Step by Step

## Current Status

✅ **Step 1 Complete**: "When a file is created" trigger configured

## Next Steps

---

## Step 2: Add HTTP Action

### 2.1 Click the "+" icon (below your trigger)

### 2.2 Search for "HTTP"

Select **"HTTP"** action (not "Call an HTTP endpoint" or "Invoke an HTTP Web Request" - just **"HTTP"**)

---

## Step 3: Configure HTTP Action

### 3.1 Method
- Select: **POST**

### 3.2 URI
- Enter your Function App URL:
```
https://a5fb7edc07fe.ngrok-free.app/api/ProcessSharePointFile
```

**OR if testing locally:**
```
http://your-ngrok-url.ngrok.io/api/ProcessSharePointFile
```

**Example:**
```
https://sme-pilot-func.azurewebsites.net/api/ProcessSharePointFile
```

### 3.3 Headers
Add header:
- **Key**: `Content-Type`
- **Value**: `application/json`

### 3.4 Body
**This is where you map SharePoint data to SMEPilot request**

---

## Step 4: Configure Request Body

The SMEPilot `ProcessSharePointFile` expects this JSON:

```json
{
  "siteId": "site-id-here",
  "driveId": "drive-id-here",
  "itemId": "item-id-here",
  "fileName": "filename.docx",
  "uploaderEmail": "user@domain.com",
  "tenantId": "tenant-id-here"
}
```

### How to Map from SharePoint Trigger:

In Power Automate, use **dynamic content** from your trigger:

```json
{
  "siteId": "<Site ID from trigger>",
  "driveId": "<Drive ID from trigger>",
  "itemId": "<File ID from trigger>",
  "fileName": "<File Name from trigger>",
  "uploaderEmail": "<Created By Email from trigger>",
  "tenantId": "<Your Tenant ID>"
}
```

---

## Step 5: Map SharePoint Fields to JSON

### Option A: Using Dynamic Content (Recommended)

In the **Body** field of HTTP action:

1. **Click inside the Body field**
2. **Click "{}" (Expression tab)** or use dynamic content picker
3. **Build the JSON using dynamic content**

Here's the mapping:

```json
{
  "siteId": "@{triggerOutputs()?['body/SiteId']}",
  "driveId": "@{triggerOutputs()?['body/DriveId']}",
  "itemId": "@{triggerOutputs()?['body/Id']}",
  "fileName": "@{triggerOutputs()?['body/Name']}",
  "uploaderEmail": "@{triggerOutputs()?['body/CreatedBy/Email']}",
  "tenantId": "YOUR-TENANT-ID-HERE"
}
```

**Note**: The exact field names might vary. Check what fields are available from your trigger.

### Option B: Manual Mapping (If Dynamic Content Doesn't Work)

1. **Get values from trigger**:
   - Click on your "When a file is created" trigger
   - See what fields are available
   - Note the field names

2. **Common field names**:
   - File ID: Usually `Id` or `ItemId`
   - File Name: Usually `Name` or `DisplayName`
   - Site ID: May need to get from site properties
   - Drive ID: Usually `DriveId` or `ParentReference/DriveId`

---

## Step 6: Get Missing Values

If some fields aren't available directly from trigger:

### Get Site ID:
Add an action **"Get file properties"** or **"Get file metadata"** before HTTP action.

### Alternative: Use SharePoint REST API

Add action: **"HTTP"** → **GET**
- URI: `https://onblick.sharepoint.com/sites/YOUR-SITE/_api/web`
- Parse JSON response to get Site ID

---

## Complete Flow Structure

```
Trigger: When a file is created (SharePoint)
    ↓
Action 1: Get file metadata (optional - if needed)
    ↓
Action 2: HTTP (POST to SMEPilot)
    - Method: POST
    - URI: https://your-function.azurewebsites.net/api/ProcessSharePointFile
    - Headers: Content-Type: application/json
    - Body: JSON with mapped fields
    ↓
Done!
```

---

## Example Complete Body (Replace with Your Values)

```json
{
  "siteId": "@{triggerOutputs()?['body/SiteId']}",
  "driveId": "@{triggerOutputs()?['body/DriveId']}",
  "itemId": "@{triggerOutputs()?['body/Id']}",
  "fileName": "@{triggerOutputs()?['body/Name']}",
  "uploaderEmail": "@{triggerOutputs()?['body/CreatedBy/User/Email']}",
  "tenantId": "12345678-1234-1234-1234-123456789012"
}
```

**Important**: Replace `tenantId` with your actual tenant ID.

---

## Step 7: Save and Test

### 7.1 Save
- Click **"Save"** button (top right)

### 7.2 Test
- Click **"Test"** button
- Select **"Manually"**
- Click **"Run flow"**
- Upload a test document to SharePoint
- Check if SMEPilot receives the request

---

## Troubleshooting

### Issue: "Field not found" or "Invalid expression"
**Solution**: 
- Check what fields are actually available from trigger
- Click on trigger → See "Outputs" or "Dynamic content"
- Adjust field names in JSON

### Issue: "HTTP 400 Bad Request"
**Solution**:
- Check JSON format (should be valid JSON)
- Verify all required fields are present
- Check field names match SMEPilot expected format

### Issue: "HTTP 500 Internal Server Error"
**Solution**:
- Check Function App is running
- Verify Function App URL is correct
- Check Function App logs for errors

### Issue: "Cannot get Site ID or Drive ID"
**Solution**:
- Add "Get file properties" action before HTTP
- Or use SharePoint REST API to get metadata
- Site ID format: `abc123.sharepoint.com,def456,ghi789`

---

## Quick Reference

### Required Fields for SMEPilot:
- `siteId` - SharePoint site ID
- `driveId` - Document library drive ID  
- `itemId` - File/item ID
- `fileName` - Name of the file
- `uploaderEmail` - Email of person who uploaded
- `tenantId` - Your Azure AD tenant ID

### Power Automate Tips:
- Use `@{triggerOutputs()?['body/FieldName']}` for dynamic content
- Use `@{outputs('ActionName')?['body/FieldName']}` for previous action output
- Test with a sample file first
- Check flow run history for errors

---

## Next Steps After Setup

1. ✅ Test with sample document
2. ✅ Verify SMEPilot receives request (check Function App logs)
3. ✅ Verify enriched document appears in ProcessedDocs folder
4. ✅ Set up error handling (optional)
5. ✅ Monitor flow runs

---

**Ready to complete the HTTP action? Follow Step 2-4 above!** 🚀

