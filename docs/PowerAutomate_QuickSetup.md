# Quick Setup: Power Automate for SMEPilot

## Your SharePoint Site
- **Site**: DocEnricher-PoC
- **URL**: `onblick.sharepoint.com/sites/DocEnricher-PoC`
- **Library**: Documents

## Step-by-Step: Power Automate Setup

### Step 1: Go to Power Automate
https://make.powerautomate.com

### Step 2: Create Flow
- **Create** → **Automated cloud flow**
- **Name**: `SMEPilot Trigger`
- **Create**

### Step 3: Add Trigger
- **Search**: "SharePoint"
- **Select**: "When a file is created"
- **Configure**:
  - Site: `onblick.sharepoint.com`
  - Library: `Documents` (or "Shared Documents")
- **Done**

### Step 4: Add HTTP Action
- **Click**: "+" → **Add an action**
- **Search**: "HTTP"
- **Select**: "HTTP"

### Step 5: Configure HTTP
- **Method**: `POST`
- **URI**: `https://your-function.azurewebsites.net/api/ProcessSharePointFile`
- **Headers**: Add `Content-Type: application/json`
- **Body**: 

```json
{
  "siteId": "@{triggerOutputs()?['body/SiteId']}",
  "driveId": "@{triggerOutputs()?['body/DriveId']}",
  "itemId": "@{triggerOutputs()?['body/Id']}",
  "fileName": "@{triggerOutputs()?['body/Name']}",
  "uploaderEmail": "@{triggerOutputs()?['body/CreatedBy/User/Email']}",
  "tenantId": "default"
}
```

**Note**: Replace `tenantId` with real Tenant ID when you have it. Use `"default"` for testing.

### Step 6: Save & Test
- **Save** flow
- **Test** → Manually
- **Upload file** to SharePoint Documents
- **Check** flow runs

---

## Important Notes

✅ **No webhook setup in SharePoint UI** - It's configured in Power Automate  
✅ **Power Automate IS the webhook** - It triggers when files are created  
✅ **You're doing it right** - SharePoint site is ready, just connect Power Automate  

---

## What Happens Next

1. User uploads file → SharePoint Documents
2. Power Automate detects → Triggers flow
3. Flow calls SMEPilot → HTTP POST
4. SMEPilot enriches → Creates enriched document
5. Document saved → ProcessedDocs folder

---

**That's it! No SharePoint webhook configuration needed - Power Automate handles it!** ✅

