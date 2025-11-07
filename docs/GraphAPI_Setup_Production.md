# Graph API Subscriptions Setup - Production Guide

## Overview

SMEPilot uses **Microsoft Graph API Subscriptions** for automatic file upload triggers. This is the **production-ready** approach - no Power Automate licenses needed.

---

## Prerequisites

✅ **Azure Function App deployed** (or running locally with public URL)  
✅ **Graph API credentials configured** (Tenant ID, Client ID, Client Secret)  
✅ **SharePoint site created** (DocEnricher-PoC)  
✅ **Public HTTPS URL** for Function App (required for Graph webhooks)

---

## Step 1: Get SharePoint Site and Drive IDs

### Method 1: Graph Explorer (Easiest)

1. **Go to**: https://developer.microsoft.com/graph/graph-explorer
2. **Sign in** with your work account
3. **Query 1: Get Site ID**
   ```
   GET https://graph.microsoft.com/v1.0/sites/onblick.sharepoint.com:/sites/DocEnricher-PoC
   ```
   
   **Response**:
   ```json
   {
     "id": "onblick.sharepoint.com,12345678-1234-1234-1234-123456789012,abc123...",
     "displayName": "DocEnricher-PoC",
     ...
   }
   ```
   - **Site ID**: Copy the entire `id` value
   - **Tenant ID**: Extract middle part (between commas)

4. **Query 2: Get Drive ID**
   ```
   GET https://graph.microsoft.com/v1.0/sites/{site-id}/drives
   ```
   Replace `{site-id}` with the Site ID from step 3.
   
   **Response**:
   ```json
   {
     "value": [{
       "id": "b!xyz123...",
       "name": "Documents",
       ...
     }]
   }
   ```
   - **Drive ID**: Copy the `id` for "Documents" drive

### Method 2: PowerShell Script

Use `GetSharePointIDs.ps1`:

```powershell
.\GetSharePointIDs.ps1 -SiteUrl "https://onblick.sharepoint.com/sites/DocEnricher-PoC"
```

This will show:
- Site ID
- Drive IDs
- Tenant ID

---

## Step 2: Get Public URL for Function App

### Option A: Deployed to Azure

Your Function App URL is already public:
```
https://a5fb7edc07fe.ngrok-free.app/api/ProcessSharePointFile
```

### Option B: Local Development (ngrok)

If testing locally:

1. **Install ngrok**: https://ngrok.com/download
2. **Run ngrok**:
   ```bash
   ngrok http 7071
   ```
3. **Copy HTTPS URL**: `https://abc123.ngrok.io`
4. **Your notification URL**: `https://abc123.ngrok.io/api/ProcessSharePointFile`

⚠️ **Note**: ngrok free tier changes URL on restart. Use paid tier for stable URLs, or deploy to Azure.

---

## Step 3: Create Graph Subscription

### Option A: Using SetupSubscription Function

**Make sure Function App is running**, then:

```powershell
.\SetupSubscription.ps1 `
  -SiteId "onblick.sharepoint.com,12345678-1234-1234-1234-123456789012,abc123..." `
  -DriveId "b!xyz123..." `
  -NotificationUrl "https://a5fb7edc07fe.ngrok-free.app/api/ProcessSharePointFile"
```

**OR directly call HTTP:**

```powershell
$siteId = "onblick.sharepoint.com,12345678-1234-1234-1234-123456789012,abc123..."
$driveId = "b!xyz123..."
$notificationUrl = "https://a5fb7edc07fe.ngrok-free.app/api/ProcessSharePointFile"

$url = "http://localhost:7071/api/SetupSubscription?siteId=$siteId&driveId=$driveId&notificationUrl=$notificationUrl"

Invoke-RestMethod -Uri $url -Method Get
```

### Option B: Using Graph API Directly

**Using PowerShell with Microsoft Graph module:**

```powershell
# Connect to Graph
Connect-MgGraph -Scopes "Subscription.ReadWrite.All"

# Create subscription
$resource = "/sites/{siteId}/drives/{driveId}/root/children"
$notificationUrl = "https://a5fb7edc07fe.ngrok-free.app/api/ProcessSharePointFile"
$expiration = (Get-Date).AddDays(3)

New-MgSubscription `
  -Resource $resource `
  -ChangeType "created" `
  -NotificationUrl $notificationUrl `
  -ExpirationDateTime $expiration `
  -ClientState "SMEPilot"
```

---

## Step 4: Verify Subscription Created

### Check Response

**SetupSubscription should return:**
```json
{
  "success": true,
  "subscriptionId": "abc123...",
  "resource": "/sites/.../drives/.../root/children",
  "expiration": "2025-11-06T...",
  "notificationUrl": "https://...",
  "changeType": "created,updated"
}
```

### Verify in Graph Explorer

**Query:**
```
GET https://graph.microsoft.com/v1.0/subscriptions
```

**Should show your subscription** in the list.

---

## Step 5: Test Automatic Trigger

1. **Upload a test document** to SharePoint Documents library
2. **Check Function App logs** - Should see notification received
3. **Verify enriched document** appears in ProcessedDocs folder

---

## Step 6: Handle Subscription Renewal

⚠️ **Important**: Graph subscriptions expire in **3 days**. You need to renew them.

### Option A: Automatic Renewal Function

Create a timer function that runs daily:

```csharp
[Function("RenewSubscriptions")]
[TimerTrigger("0 0 0 * * *")] // Daily at midnight
public async Task RenewSubscriptions([TimerTrigger] TimerInfo timer)
{
    // Get all active subscriptions
    // Renew each one before expiration
    // Call SetupSubscription for each
}
```

### Option B: Manual Renewal

Run `SetupSubscription.ps1` again before expiration (every 3 days).

### Option C: Use Maximum Duration

Graph subscriptions can be renewed up to 3 days before expiry. Set up a schedule to renew automatically.

---

## Troubleshooting

### "Subscription validation failed"
- ✅ Ensure notification URL is publicly accessible (HTTPS)
- ✅ Check Function App is running
- ✅ Verify validation token is echoed correctly (code already handles this)

### "Invalid resource"
- ✅ Check Site ID format is correct
- ✅ Verify Drive ID matches the document library
- ✅ Ensure resource path format: `/sites/{siteId}/drives/{driveId}/root/children`

### "Subscriptions not received"
- ✅ Verify subscription is active (check Graph API)
- ✅ Ensure notification URL is correct
- ✅ Check Function App logs for errors
- ✅ Verify file is uploaded to correct library

### "Function App not receiving notifications"
- ✅ Check Function App is publicly accessible
- ✅ Verify HTTPS is enabled
- ✅ Check firewall/network allows inbound connections
- ✅ Review Function App logs

---

## Complete Setup Checklist

- [ ] Get Site ID from Graph Explorer
- [ ] Get Drive ID from Graph Explorer  
- [ ] Deploy Function App (or set up ngrok for local)
- [ ] Get public HTTPS URL for Function App
- [ ] Configure Graph API credentials (if not done)
- [ ] Run SetupSubscription script
- [ ] Verify subscription created
- [ ] Test with file upload
- [ ] Set up subscription renewal mechanism

---

## Production Deployment

### For Production:

1. **Deploy Function App to Azure**
   - Get stable HTTPS URL
   - Configure App Settings (Graph credentials)

2. **Create Subscription**
   - Use SetupSubscription function
   - Store subscription ID for renewal

3. **Set Up Auto-Renewal**
   - Create timer function for renewal
   - Or schedule PowerShell script

4. **Monitor**
   - Check Function App logs
   - Monitor subscription status
   - Set up alerts for failures

---

## What Happens After Setup

```
1. User uploads document → SharePoint Documents
   ↓
2. Microsoft Graph detects → Sends notification
   ↓
3. Function App receives → ProcessSharePointFile triggered
   ↓
4. Graph notification parsed → File details extracted
   ↓
5. Document processed → Enriched document created
   ↓
6. Saved to ProcessedDocs folder
   ↓
7. Embeddings stored → Ready for Copilot queries
```

---

## Next Steps

1. ✅ Get Site ID and Drive ID
2. ✅ Create subscription
3. ✅ Test with file upload
4. ✅ Set up renewal mechanism
5. ✅ Deploy to production

---

**Ready? Start with Step 1: Get Site ID and Drive ID!** 🚀

