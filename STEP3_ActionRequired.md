# Step 3: Create Graph Subscription - Action Required

## ✅ Completed
- **Site ID:** `onblick.sharepoint.com,4b82e6b5-e79c-4787-8aa8-8c4aad49e9e0,af7fd8eb-328c-45b1-b7a7-8b79d27dd516`
- **Drive ID:** `b!teaCS5znh0eKqIxKrUnp4OvYf6-MMrFFt6eLedJ91Ra5fXoii7EjRK7WFJxSDDf3`
- **Tenant ID:** `4b82e6b5-e79c-4787-8aa8-8c4aad49e9e0`
- **ngrok URL:** `https://562fbad9f946.ngrok-free.app`

## ⚠️ Current Issue
Function App is not running. Need to start it first.

---

## 🚀 Steps to Complete

### Step 1: Start Function App in Visual Studio

1. **Open Visual Studio**
2. **Press F5** (or Debug → Start Debugging)
3. **Wait for console output:**
   ```
   Functions:
       ProcessSharePointFile: [POST] http://localhost:7071/api/ProcessSharePointFile
       QueryAnswer: [POST] http://localhost:7071/api/QueryAnswer
       SetupSubscription: [GET] http://localhost:7071/api/SetupSubscription
   ```

### Step 2: Keep ngrok Running

**In a separate terminal/PowerShell window:**
```powershell
ngrok http 7071
```

**Keep this running** - don't close it!

### Step 3: Create Subscription

**Once Function App is running, run this command:**

```powershell
cd D:\CodeBase\DocuPilot
.\SetupSubscription.ps1 `
  -SiteId "onblick.sharepoint.com,4b82e6b5-e79c-4787-8aa8-8c4aad49e9e0,af7fd8eb-328c-45b1-b7a7-8b79d27dd516" `
  -DriveId "b!teaCS5znh0eKqIxKrUnp4OvYf6-MMrFFt6eLedJ91Ra5fXoii7EjRK7WFJxSDDf3" `
  -NotificationUrl "https://562fbad9f946.ngrok-free.app/api/ProcessSharePointFile"
```

---

## ✅ Expected Result

```
Subscription created successfully!

Subscription Details:
{
  "success": true,
  "subscriptionId": "abc123...",
  "resource": "/sites/.../drives/.../root/children",
  "expiration": "2025-11-06T...",
  ...
}

IMPORTANT: Subscription expires in 3 days!
```

---

## 🧪 Test After Creation

1. **Upload a document** to SharePoint Documents library
2. **Check Function App logs** → Should see notification received
3. **Verify enriched document** in ProcessedDocs folder

---

## 📋 Quick Checklist

- [ ] Function App running in Visual Studio (F5)
- [ ] ngrok running (`ngrok http 7071`)
- [ ] Run SetupSubscription.ps1 script
- [ ] Subscription created successfully
- [ ] Test with file upload

---

**Start the Function App in Visual Studio, then let me know when it's running!** 🚀

