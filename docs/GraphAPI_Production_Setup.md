# Graph API Subscriptions - Complete Production Setup Guide

## Overview

SMEPilot uses **Microsoft Graph API Subscriptions** for automatic file upload triggers. This is the **production-ready** approach - no Power Automate licenses needed, scalable, professional.

---

## Prerequisites Checklist

- [ ] Azure Function App deployed (or running locally with public URL)
- [ ] Graph API credentials configured (Tenant ID, Client ID, Client Secret)
- [ ] SharePoint site created (DocEnricher-PoC)
- [ ] Public HTTPS URL for Function App (required for Graph webhooks)

---

## Step 1: Get SharePoint Site and Drive IDs

### Option A: Use PowerShell Script (Easiest)

1. **Open PowerShell**
2. **Navigate to**: `D:\CodeBase\DocuPilot`
3. **Run**:
   ```powershell
   .\GetSharePointIDs.ps1 -SiteUrl "https://onblick.sharepoint.com/sites/DocEnricher-PoC"
   ```

**Output will show:**
- ✅ Site ID
- ✅ Drive ID (for Documents library)
- ✅ Tenant ID
- ✅ Ready-to-use SetupSubscription command

### Option B: Graph Explorer (Manual)

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
   Replace `{site-id}` with Site ID from step 3.
   Find "Documents" drive → Copy `id` → **Drive ID**

---

## Step 2: Get Public URL for Function App

### Option A: Deployed to Azure ✅

Your Function App URL:
```
https://a5fb7edc07fe.ngrok-free.app/api/ProcessSharePointFile
```

### Option B: Local Development (ngrok)

1. **Install ngrok**: https://ngrok.com/download
2. **Run ngrok**:
   ```bash
   ngrok http 7071
   ```
3. **Copy HTTPS URL**: `https://abc123.ngrok.io`
4. **Your notification URL**: `https://abc123.ngrok.io/api/ProcessSharePointFile`

⚠️ **Note**: For production, deploy to Azure. ngrok is only for testing.

---

## Step 3: Create Graph Subscription

### Method 1: Using SetupSubscription Function (Recommended)

**Make sure Function App is running**, then:

```powershell
.\SetupSubscription.ps1 `
  -SiteId "onblick.sharepoint.com,12345678-1234-1234-1234-123456789012,abc123..." `
  -DriveId "b!xyz123..." `
  -NotificationUrl "https://a5fb7edc07fe.ngrok-free.app/api/ProcessSharePointFile"
```

**Expected Response:**
```json
{
  "success": true,
  "subscriptionId": "abc123...",
  "expiration": "2025-11-06T...",
  "message": "Subscription created successfully. It will expire in 3 days and needs renewal."
}
```

### Method 2: Direct HTTP Call

```powershell
$siteId = "onblick.sharepoint.com,12345678-1234-1234-1234-123456789012,abc123..."
$driveId = "b!xyz123..."
$notificationUrl = "https://a5fb7edc07fe.ngrok-free.app/api/ProcessSharePointFile"

$encodedUrl = [System.Web.HttpUtility]::UrlEncode($notificationUrl)
$url = "http://localhost:7071/api/SetupSubscription?siteId=$siteId&driveId=$driveId&notificationUrl=$encodedUrl"

Invoke-RestMethod -Uri $url -Method Get
```

---

## Step 4: Verify Subscription Created

### Check Response

**SetupSubscription should return:**
- ✅ `success: true`
- ✅ `subscriptionId` (save this for renewal)
- ✅ `expiration` date (3 days from now)

### Verify in Graph Explorer

**Query:**
```
GET https://graph.microsoft.com/v1.0/subscriptions
```

**Should show your subscription** in the list.

---

## Step 5: Test Automatic Trigger

1. **Upload a test document** to SharePoint Documents library
2. **Check Function App logs** - Should see:
   ```
   Received Graph notification with 1 items
   Processing Graph notification: File test.docx (ID: ...)
   Successfully processed test.docx
   ```
3. **Verify enriched document** appears in ProcessedDocs folder

---

## Step 6: Handle Subscription Renewal

⚠️ **Critical**: Graph subscriptions expire in **3 days**. Set up renewal.

### Option A: Manual Renewal

**Before expiration** (every 3 days), run SetupSubscription again:
```powershell
.\SetupSubscription.ps1 -SiteId ... -DriveId ... -NotificationUrl ...
```

### Option B: Automatic Renewal (Production)

Create a timer function that runs daily:

**File**: `SMEPilot.FunctionApp/Functions/RenewSubscriptions.cs`

```csharp
[Function("RenewSubscriptions")]
public async Task Run([TimerTrigger("0 0 0 * * *")] TimerInfo timer) // Daily at midnight
{
    // Get all active subscriptions
    // Renew each one before expiration
    // Call SetupSubscription for each
}
```

**This will be implemented in next phase.**

---

## What Changed: Graph Notification Handling

### Updated ProcessSharePointFile

✅ **Now handles Graph notifications**:
- Parses Graph notification format
- Extracts file details automatically
- Processes multiple files in one notification
- Returns 202 Accepted (async processing)

✅ **Still supports manual payload**:
- For testing via HTTP POST
- For Power Automate (if needed)

---

## Complete Setup Checklist

- [ ] Get Site ID (`GetSharePointIDs.ps1` or Graph Explorer)
- [ ] Get Drive ID (from same script/query)
- [ ] Deploy Function App OR set up ngrok
- [ ] Configure Graph API credentials in `local.settings.json`
- [ ] Run SetupSubscription script
- [ ] Verify subscription created
- [ ] Test with file upload
- [ ] Set up renewal mechanism

---

## Troubleshooting

### "Subscription validation failed"
- ✅ Ensure notification URL is publicly accessible (HTTPS)
- ✅ Check Function App is running
- ✅ Verify URL is correct (no typos)

### "Invalid resource"
- ✅ Check Site ID format is correct
- ✅ Verify Drive ID matches document library
- ✅ Ensure resource path: `/sites/{siteId}/drives/{driveId}/root/children`

### "No notifications received"
- ✅ Verify subscription is active (`GET /subscriptions`)
- ✅ Check subscription hasn't expired
- ✅ Ensure file uploaded to correct library
- ✅ Check Function App logs for errors

### "Graph API authentication failed"
- ✅ Verify credentials in `local.settings.json`
- ✅ Check App Registration permissions granted
- ✅ Ensure admin consent granted

---

## Production Deployment Steps

### 1. Deploy Function App to Azure

```bash
# Build for production
dotnet publish -c Release

# Deploy to Azure
func azure functionapp publish your-function-app-name
```

### 2. Configure App Settings

In Azure Portal → Function App → Configuration:
- Add all settings from `local.settings.json`
- Keep secrets secure (use Key Vault for production)

### 3. Create Subscription

Use production Function App URL:
```powershell
.\SetupSubscription.ps1 `
  -SiteId "..." `
  -DriveId "..." `
  -NotificationUrl "https://a5fb7edc07fe.ngrok-free.app/api/ProcessSharePointFile"
```

### 4. Set Up Monitoring

- Enable Application Insights
- Set up alerts for failures
- Monitor subscription status

---

## Next Steps After Setup

1. ✅ Test with real documents
2. ✅ Verify enriched documents created
3. ✅ Set up subscription renewal
4. ✅ Configure Microsoft Search Connector (for Copilot)
5. ✅ Monitor and optimize

---

## Summary

**Graph API Subscriptions = Production-Ready Solution**

✅ No license dependencies  
✅ Scalable and reliable  
✅ Professional approach  
✅ Automated setup possible  

**You're all set! Follow Step 1-3 to create your subscription.** 🚀

