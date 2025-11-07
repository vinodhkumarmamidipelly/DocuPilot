# Complete Setup Guide: From Scratch - SharePoint Site to SMEPilot Integration

## Step 0: You're Not Doing Anything Wrong ✅

**Important**: SharePoint webhooks are **NOT configured in SharePoint UI**. They're configured **outside** SharePoint using:
- Power Automate (easiest)
- Microsoft Graph API (programmatic)
- SharePoint REST API (advanced)

You're in the right place - you just need to configure the trigger **outside** SharePoint.

---

## What You Have

✅ SharePoint site created: **DocEnricher-PoC**  
✅ Document library: **Shared Documents**  
✅ Site URL: `onblick.sharepoint.com/sites/DocEnricher-PoC`

**Perfect!** Now let's connect it to SMEPilot.

---

## Step 1: Get Your Site Information

### Option A: From SharePoint URL (Easiest)

Your site URL: `onblick.sharepoint.com/sites/DocEnricher-PoC`

**What we need:**
- **Site Path**: `onblick.sharepoint.com:/sites/DocEnricher-PoC`
- **Site ID**: We'll get this via Graph API
- **Drive ID**: Document library ID (we'll get this too)

### Option B: Use Graph Explorer

1. Go to: https://developer.microsoft.com/graph/graph-explorer
2. Sign in
3. Query your site:

**Query 1: Get Site ID**
```
GET https://graph.microsoft.com/v1.0/sites/onblick.sharepoint.com:/sites/DocEnricher-PoC
```

**Response will have:**
```json
{
  "id": "onblick.sharepoint.com,12345678-1234-1234-1234-123456789012,abc123...",
  "displayName": "DocEnricher-PoC",
  ...
}
```
- **Site ID**: The entire `id` value
- **Tenant ID**: Middle part (between commas)

**Query 2: Get Drive ID (Document Library)**
```
GET https://graph.microsoft.com/v1.0/sites/{site-id}/drives
```

**Response will have:**
```json
{
  "value": [{
    "id": "b!xyz123...",
    "name": "Documents",
    ...
  }]
}
```
- **Drive ID**: The `id` value for "Documents"

---

## Step 2: Choose Your Integration Method

### Method 1: Power Automate (Recommended - Easiest) ⭐

**Why**: No coding, visual interface, works immediately

**Steps:**
1. Go to https://make.powerautomate.com
2. Create Automated cloud flow
3. Trigger: "When a file is created" (SharePoint)
4. Action: HTTP POST to SMEPilot

**Continue to Step 3 below**

---

### Method 2: Graph API Subscription (Advanced)

**Why**: More control, programmatic setup

**Steps:**
1. Use SetupSubscription function we created
2. Call with Site ID and Drive ID
3. Creates webhook subscription

**Skip to Step 4 if using this method**

---

## Step 3: Set Up Power Automate Flow

### 3.1 Create Flow

1. **Go to**: https://make.powerautomate.com
2. **Sign in** with your work account
3. **Create** → **Automated cloud flow**
4. **Name**: `SMEPilot Document Enrichment`
5. **Click**: Create

### 3.2 Add Trigger

1. **Search**: "SharePoint"
2. **Select**: **"When a file is created"**
3. **Configure**:
   - **Site Address**: `onblick.sharepoint.com`
   - **Library Name**: `Documents` (or "Shared Documents" - check exact name)
   - **Folder**: Leave empty (or specify folder if needed)

**Click**: Done

### 3.3 Add HTTP Action

1. **Click**: **"+"** → **"Add an action"**
2. **Search**: **"HTTP"**
3. **Select**: **"HTTP"** (not "Call an HTTP endpoint")

### 3.4 Configure HTTP Action

**Method**: `POST`

**URI**: Your Function App URL
```
https://a5fb7edc07fe.ngrok-free.app/api/ProcessSharePointFile
```

**OR if testing locally with ngrok**:
```
https://your-ngrok-url.ngrok.io/api/ProcessSharePointFile
```

**Headers**:
- **Key**: `Content-Type`
- **Value**: `application/json`

**Body**: Click **"{}"** (Expression mode) and paste:

```json
{
  "siteId": "@{triggerOutputs()?['body/SiteId']}",
  "driveId": "@{triggerOutputs()?['body/DriveId']}",
  "itemId": "@{triggerOutputs()?['body/Id']}",
  "fileName": "@{triggerOutputs()?['body/Name']}",
  "uploaderEmail": "@{triggerOutputs()?['body/CreatedBy/User/Email']}",
  "tenantId": "YOUR-TENANT-ID"
}
```

**Important**: Replace `YOUR-TENANT-ID` with actual Tenant ID (from Graph Explorer or PowerShell script)

### 3.5 Get Tenant ID (If Needed)

**Option A: Graph Explorer**
- Query: `https://graph.microsoft.com/v1.0/sites/onblick.sharepoint.com`
- Extract UUID from Site ID (middle part)

**Option B: PowerShell**
- Run: `.\GetTenantID.ps1`
- Copy Tenant ID from output

**Option C: For Testing**
- Use: `"default"` temporarily
- Works for testing, get real ID later

### 3.6 Save and Test

1. **Click**: **Save** (top right)
2. **Click**: **Test** → **Manually**
3. **Upload a test file** to SharePoint Documents library
4. **Check**: Flow should trigger automatically
5. **Verify**: SMEPilot Function App receives request

---

## Step 4: Verify Setup

### Check Flow Runs

1. **Go to**: Power Automate → My flows
2. **Click**: Your flow
3. **Check**: Run history
4. **Verify**: Flow triggered when file uploaded

### Check SMEPilot Function App

1. **Check**: Visual Studio Output window (if running locally)
2. **OR**: Azure Portal → Function App → Logs (if deployed)
3. **Verify**: Request received and processed

### Check SharePoint

1. **Go to**: SharePoint → Documents library
2. **Check**: ProcessedDocs folder (created automatically)
3. **Verify**: Enriched document appears

---

## Troubleshooting

### Flow Not Triggering

**Check:**
- ✅ Site address matches exactly
- ✅ Library name matches exactly ("Documents" vs "Shared Documents")
- ✅ Flow is turned ON (not disabled)
- ✅ File uploaded to correct library

### HTTP Action Fails

**Check:**
- ✅ Function App URL is correct
- ✅ Function App is running
- ✅ JSON body is valid
- ✅ Field names match trigger outputs

### Field Names Don't Match

**Solution:**
1. Click on trigger → See outputs
2. Check available fields
3. Adjust field names in JSON body
4. Use dynamic content picker if needed

---

## Complete Flow Structure

```
Trigger: When a file is created (SharePoint)
    ↓
    Site: onblick.sharepoint.com
    Library: Documents
    ↓
Action: HTTP (POST)
    ↓
    URI: https://your-function.azurewebsites.net/api/ProcessSharePointFile
    Body: JSON with file details
    ↓
SMEPilot processes document
    ↓
Enriched document saved to ProcessedDocs folder
```

---

## Next Steps After Setup

1. ✅ Test with sample document
2. ✅ Verify enriched document created
3. ✅ Check ProcessedDocs folder
4. ✅ Configure Azure services (for real processing)
5. ✅ Set up Copilot integration

---

## Summary

**You're doing everything right!** ✅

- ✅ Site created
- ✅ Document library ready
- ✅ Now just need to configure Power Automate trigger
- ✅ No webhook configuration needed in SharePoint UI

**The webhook is configured in Power Automate, not SharePoint!**

---

**Ready? Start with Step 3: Power Automate setup!** 🚀

