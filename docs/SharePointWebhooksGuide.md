# SharePoint Webhooks Guide for SMEPilot

## Important: Two Types of Webhooks

### 1. SharePoint REST API Webhooks (Legacy)
- Direct SharePoint webhooks
- Configured in SharePoint UI or REST API
- Limited to SharePoint lists/libraries

### 2. Microsoft Graph Subscriptions (Modern - What We Use) ✅
- Graph change notifications
- More powerful and modern
- Works across Microsoft 365
- **This is what SMEPilot uses**

---

## Option 1: Microsoft Graph Subscriptions (Recommended for SMEPilot)

SMEPilot uses **Microsoft Graph change notifications** - these are **NOT configured in SharePoint UI**. They're set up programmatically.

### How to Set Up (Already Created for You)

#### Step 1: Use the SetupSubscription Function

We've already created `SetupSubscription.cs` function. Just call it:

```powershell
# Make sure Function App is running
.\SetupSubscription.ps1 `
  -SiteId "YOUR_SITE_ID" `
  -DriveId "YOUR_DRIVE_ID" `
  -NotificationUrl "https://your-function.azurewebsites.net/api/ProcessSharePointFile"
```

#### Step 2: Get Site and Drive IDs

**Using Graph Explorer:**
1. Go to https://developer.microsoft.com/graph/graph-explorer
2. Sign in with your account
3. Run:

**Get Site ID:**
```
GET https://graph.microsoft.com/v1.0/sites/onblick.sharepoint.com:/sites/OnBlick-Inc-Team-Site
```
Copy the `id` value → This is your Site ID

**Get Drive ID (Document Library):**
```
GET https://graph.microsoft.com/v1.0/sites/{siteId}/drives
```
Find the "Documents" drive and copy its `id` → This is your Drive ID

#### Step 3: Create Subscription

Call the SetupSubscription endpoint:
```powershell
.\SetupSubscription.ps1 `
  -SiteId "abc123.site,def456,ghi789" `
  -DriveId "b!xyz..." `
  -NotificationUrl "https://your-function.azurewebsites.net/api/ProcessSharePointFile"
```

✅ **Done!** Now uploads to that library will automatically trigger enrichment.

---

## Option 2: SharePoint REST API Webhooks (Alternative)

If you want to use traditional SharePoint webhooks instead:

### Method A: Using SharePoint UI (Limited)

**SharePoint REST API webhooks can't be created from the UI easily.** They're typically created programmatically via REST API.

### Method B: Using REST API (Programmatic)

Create a webhook via SharePoint REST API:

```powershell
# Get access token (use Azure AD app registration)
$token = # Your access token

# Create webhook subscription
$siteUrl = "https://onblick.sharepoint.com/sites/OnBlick-Inc-Team-Site"
$listName = "Documents" # Your document library

$webhookUrl = "https://your-function.azurewebsites.net/api/ProcessSharePointFile"

$body = @{
    notificationUrl = $webhookUrl
    expirationDateTime = (Get-Date).AddDays(3).ToString("o")
    resource = "$siteUrl/_api/web/lists/getbytitle('$listName')/items"
} | ConvertTo-Json

$headers = @{
    Authorization = "Bearer $token"
    Accept = "application/json"
    Content-Type = "application/json"
}

Invoke-RestMethod -Uri "$siteUrl/_api/web/Subscriptions" -Method Post -Body $body -Headers $headers
```

**But this is more complex and not recommended for SMEPilot.**

---

## Option 3: Power Automate (Easiest for Testing)

If you want a quick UI-based solution for testing:

### Step 1: Create Power Automate Flow

1. Go to https://make.powerautomate.com
2. Create new **Automated cloud flow**
3. Name: "SMEPilot Document Enrichment"

### Step 2: Add Trigger

1. Search for **"SharePoint"**
2. Select **"When a file is created"** or **"When an item is created"**
3. Configure:
   - **Site Address**: `onblick.sharepoint.com`
   - **Library Name**: Your document library (e.g., "Documents" or "ScratchDocs")

### Step 3: Add Action

1. Add action: **"HTTP"**
2. Configure:
   - **Method**: POST
   - **URI**: `https://your-function.azurewebsites.net/api/ProcessSharePointFile`
   - **Headers**: 
     ```
     Content-Type: application/json
     ```
   - **Body**:
     ```json
     {
       "siteId": "@{triggerOutputs()?['body/@{odata.type}']}",
       "driveId": "@{triggerOutputs()?['body/DriveId']}",
       "itemId": "@{triggerOutputs()?['body/Id']}",
       "fileName": "@{triggerOutputs()?['body/Name']}",
       "uploaderEmail": "@{triggerOutputs()?['body/CreatedBy/Email']}",
       "tenantId": "YOUR_TENANT_ID"
     }
     ```
   - **Note**: Adjust the JSON based on what SharePoint provides in the trigger

### Step 4: Save and Test

1. Save the flow
2. Upload a test document to SharePoint
3. Flow should trigger automatically
4. Check Function App logs

✅ **This works immediately and doesn't require Graph API setup!**

---

## Recommended Approach for SMEPilot

### For Development/Testing:
✅ **Use Power Automate** (easiest, no coding)

### For Production:
✅ **Use Microsoft Graph Subscriptions** (SetupSubscription function we created)
- More reliable
- Programmatic control
- Better for automation

---

## Quick Start: Power Automate (5 minutes)

1. **Go to**: https://make.powerautomate.com
2. **Create** → Automated cloud flow
3. **Trigger**: "When a file is created" (SharePoint)
4. **Action**: "HTTP" → POST to your Function App URL
5. **Test** → Upload a document

That's it! ✅

---

## Quick Start: Graph Subscription (10 minutes)

1. **Get Site ID and Drive ID** (Graph Explorer)
2. **Run** `.\SetupSubscription.ps1` with your values
3. **Test** → Upload a document

Done! ✅

---

## Which Method Should You Use?

| Method | Difficulty | Best For |
|--------|-----------|----------|
| **Power Automate** | ⭐ Easy | Testing, Quick setup |
| **Graph Subscription** | ⭐⭐ Medium | Production, Automation |
| **SharePoint REST Webhooks** | ⭐⭐⭐ Hard | Legacy systems |

**Recommendation**: Start with **Power Automate** for testing, then move to **Graph Subscriptions** for production.

---

## Troubleshooting

### Graph Subscription Not Working
- ✅ Verify subscription was created (check response)
- ✅ Ensure Function App URL is publicly accessible (HTTPS)
- ✅ Check subscription hasn't expired (3 days max)
- ✅ Verify Site ID and Drive ID are correct

### Power Automate Not Triggering
- ✅ Check flow is turned on
- ✅ Verify library name matches exactly
- ✅ Check flow run history for errors
- ✅ Ensure HTTP action URL is correct

---

## Next Steps

1. **Choose your method** (Power Automate or Graph Subscription)
2. **Set it up** using the steps above
3. **Test** by uploading a document
4. **Verify** automatic enrichment works
5. **Monitor** Function App logs

---

**Ready? Start with Power Automate for quick testing!** 🚀

