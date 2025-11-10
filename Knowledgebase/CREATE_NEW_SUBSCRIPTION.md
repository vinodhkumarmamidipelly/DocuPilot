# Create New Subscription - Quick Guide

## 📋 Information You Need

Based on your previous subscription:

- **Site ID:** `onblick.sharepoint.com,4b82e6b5-e79c-4787-8aa8-8c4aad49e9e0,af7fd8eb-328c-45b1-b7a7-8b79d27dd516`
- **Drive ID:** `b!teaCS5znh0eKqIxKrUnp4OvYf6-MMrFFt6eLedJ91Ra5fXoii7EjRK7WFJxSDDf3`

## ⚠️ Important: Public HTTPS URL Required

Graph API requires a **public HTTPS URL** for webhooks. You have two options:

### Option 1: Use ngrok (For Local Testing)

1. **Install ngrok** (if not already): https://ngrok.com/download
2. **Run ngrok** in a separate terminal:
   ```powershell
   ngrok http 7071
   ```
3. **Copy the HTTPS URL** (e.g., `https://abc123.ngrok.io`)
4. **Your notification URL:** `https://abc123.ngrok.io/api/ProcessSharePointFile`

### Option 2: Deploy to Azure (Production)

Use your Azure Function App URL:
```
https://a5fb7edc07fe.ngrok-free.app/api/ProcessSharePointFile
```

## 🚀 Steps to Create New Subscription

### Step 1: Ensure Function App is Running

**In Visual Studio:**
- Press **F5** (or Debug → Start Debugging)
- Wait for console to show:
  ```
  Functions:
      ProcessSharePointFile: [GET,POST] http://localhost:7071/api/ProcessSharePointFile
      SetupSubscription: [GET,POST] http://localhost:7071/api/SetupSubscription
  ```

### Step 2: Run SetupSubscription Script

**In PowerShell (D:\CodeBase\DocuPilot):**

**If using ngrok:**
```powershell
.\SetupSubscription.ps1 `
  -SiteId "onblick.sharepoint.com,4b82e6b5-e79c-4787-8aa8-8c4aad49e9e0,af7fd8eb-328c-45b1-b7a7-8b79d27dd516" `
  -DriveId "b!teaCS5znh0eKqIxKrUnp4OvYf6-MMrFFt6eLedJ91Ra5fXoii7EjRK7WFJxSDDf3" `
  -NotificationUrl "https://YOUR-NGROK-URL/api/ProcessSharePointFile"
```

**If deployed to Azure:**
```powershell
.\SetupSubscription.ps1 `
  -SiteId "onblick.sharepoint.com,4b82e6b5-e79c-4787-8aa8-8c4aad49e9e0,af7fd8eb-328c-45b1-b7a7-8b79d27dd516" `
  -DriveId "b!teaCS5znh0eKqIxKrUnp4OvYf6-MMrFFt6eLedJ91Ra5fXoii7EjRK7WFJxSDDf3" `
  -NotificationUrl "https://a5fb7edc07fe.ngrok-free.app/api/ProcessSharePointFile"
```

**Replace `YOUR-NGROK-URL`** with your actual ngrok HTTPS URL.

## ✅ Expected Result

```
Setting up Graph Webhook Subscription for SMEPilot

Parameters:
  Site ID: onblick.sharepoint.com,4b82e6b5-e79c-4787-8aa8-8c4aad49e9e0,af7fd8eb-328c-45b1-b7a7-8b79d27dd516
  Drive ID: b!teaCS5znh0eKqIxKrUnp4OvYf6-MMrFFt6eLedJ91Ra5fXoii7EjRK7WFJxSDDf3
  Notification URL: https://...

Creating subscription...

Subscription created successfully!

Subscription Details:
{
  "success": true,
  "subscriptionId": "abc123...",
  "changeType": "created",  ← IMPORTANT: Should show "created"!
  "expiration": "2025-11-08T...",
  ...
}
```

## 🎯 Key Difference from Old Subscription

- **Old subscription:** `changeType: "updated"` ❌
- **New subscription:** `changeType: "created"` ✅

## 🧪 Test After Creation

1. **Upload a NEW file** to SharePoint Documents library
2. **Check Function App logs** - Should see:
   ```
   Change Type: created ✅
   Processing Graph notification: File test.docx...
   ```
3. **Document should be enriched** ✅

## 📝 Notes

- **Old subscription:** Will expire in 3 days (or you can ignore it)
- **New subscription:** Will listen to "created" events only
- **Code:** Will process "created" events and skip "updated" events

