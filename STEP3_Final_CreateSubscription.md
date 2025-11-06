# Step 3: Create Graph Subscription - Final Steps

## ✅ Credentials Configured

**Updated `local.settings.json` with:**
- ✅ Tenant ID: `8e8a9805-15bc-44e4-9fd5-8bf2cd01fe09`
- ✅ Client ID: `7ca14971-d9a3-4b9b-bad7-33a85c25f485`
- ✅ Client Secret: `x2u8Q~e9ZuDEzhf_uAlHzvwDTCVGOEfXTnu0bbIl`

---

## 🚀 Next Steps

### Step 1: Restart Function App

**In Visual Studio:**
1. **Stop** the Function App (if running)
2. **Press F5** to start again
3. **Wait for:** Functions listed in console
   ```
   Functions:
       ProcessSharePointFile: [POST] http://localhost:7071/api/ProcessSharePointFile
       QueryAnswer: [POST] http://localhost:7071/api/QueryAnswer
       SetupSubscription: [GET] http://localhost:7071/api/SetupSubscription
   ```

### Step 2: Ensure ngrok is Running

**In separate PowerShell window:**
```powershell
ngrok http 7071
```

**Keep this running** - don't close it!

**Verify ngrok URL** (should match what we used before):
- `https://562fbad9f946.ngrok-free.app` (or your current ngrok URL)

### Step 3: Create Subscription

**In PowerShell (D:\CodeBase\DocuPilot):**

```powershell
.\SetupSubscription.ps1 `
  -SiteId "onblick.sharepoint.com,4b82e6b5-e79c-4787-8aa8-8c4aad49e9e0,af7fd8eb-328c-45b1-b7a7-8b79d27dd516" `
  -DriveId "b!teaCS5znh0eKqIxKrUnp4OvYf6-MMrFFt6eLedJ91Ra5fXoii7EjRK7WFJxSDDf3" `
  -NotificationUrl "https://562fbad9f946.ngrok-free.app/api/ProcessSharePointFile"
```

**Note:** If your ngrok URL changed, update `-NotificationUrl` with the new URL.

---

## ✅ Expected Result

```
Setting up Graph Webhook Subscription for SMEPilot

Parameters:
  Site ID: onblick.sharepoint.com,4b82e6b5-e79c-4787-8aa8-8c4aad49e9e0,af7fd8eb-328c-45b1-b7a7-8b79d27dd516
  Drive ID: b!teaCS5znh0eKqIxKrUnp4OvYf6-MMrFFt6eLedJ91Ra5fXoii7EjRK7WFJxSDDf3
  Notification URL: https://562fbad9f946.ngrok-free.app/api/ProcessSharePointFile

Creating subscription...

Subscription created successfully!

Subscription Details:
{
  "success": true,
  "subscriptionId": "abc123...",
  "resource": "/sites/.../drives/.../root/children",
  "expiration": "2025-11-06T...",
  "notificationUrl": "https://562fbad9f946.ngrok-free.app/api/ProcessSharePointFile",
  "changeType": "created,updated",
  "message": "Subscription created successfully. It will expire in 3 days and needs renewal."
}

IMPORTANT: Subscription expires in 3 days!
```

---

## 🧪 Test the Subscription

Once subscription is created:

1. **Upload a document** to SharePoint Documents library
   - Go to: https://onblick.sharepoint.com/sites/DocEnricher-PoC/Shared%20Documents
   - Upload any `.docx` file

2. **Check Function App logs** in Visual Studio
   - Should see: "Graph notification received"
   - Should see: "Processing file..."

3. **Verify enriched document**
   - Check: `/Shared Documents/ProcessedDocs` folder
   - Should see enriched document

---

## ⚠️ Troubleshooting

### If subscription creation fails:

1. **Check Function App is running**
   - Must be running in Visual Studio

2. **Verify ngrok URL**
   - Check ngrok window for current URL
   - Update `-NotificationUrl` if changed

3. **Check admin consent**
   - Ensure admin granted consent for both permissions
   - Verify in Azure Portal → App Registration → API Permissions

4. **Check Visual Studio Output**
   - Look for error messages
   - Share error details if persists

---

## 📋 Quick Checklist

- [ ] Function App restarted (F5)
- [ ] ngrok running (`ngrok http 7071`)
- [ ] Verified ngrok URL
- [ ] Run SetupSubscription.ps1
- [ ] Subscription created successfully
- [ ] Test with file upload

---

**Ready? Restart Function App and create the subscription!** 🚀

