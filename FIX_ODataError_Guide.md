# Fix: ODataError from Graph API - Most Likely Causes

## 🔍 Current Issue

Getting `ODataError` from Microsoft Graph API when creating subscription.

**This usually means:**
- ❌ Admin consent not granted
- ❌ Permissions missing
- ❌ Invalid resource format

---

## ✅ Enhanced Error Handling Added

I've updated the code to extract detailed Graph API error messages. 

**After restarting Function App, you'll see:**
- Specific error code (e.g., "Authorization_RequestDenied")
- Error message (e.g., "Insufficient privileges")
- Additional error details

---

## 🔧 Next Steps

### Step 1: Restart Function App

**In Visual Studio:**
1. **Stop** Function App
2. **Restart** (F5)

### Step 2: Run SetupSubscription Again

```powershell
.\SetupSubscription.ps1 `
  -SiteId "onblick.sharepoint.com,4b82e6b5-e79c-4787-8aa8-8c4aad49e9e0,af7fd8eb-328c-45b1-b7a7-8b79d27dd516" `
  -DriveId "b!teaCS5znh0eKqIxKrUnp4OvYf6-MMrFFt6eLedJ91Ra5fXoii7EjRK7WFJxSDDf3" `
  -NotificationUrl "https://562fbad9f946.ngrok-free.app/api/ProcessSharePointFile"
```

### Step 3: Check Error Details

**The PowerShell output will now show:**
```
Graph API Error: Authorization_RequestDenied - Insufficient privileges to complete the operation.
```

**Or similar detailed error message.**

---

## 🔍 Most Common Issues

### Issue 1: Admin Consent Not Granted ⚠️ MOST LIKELY

**Error Code:** `Authorization_RequestDenied` or `InsufficientPrivileges`

**Solution:**
1. **Ask admin to verify:**
   - Azure Portal → App Registrations → Your App
   - API Permissions
   - Both permissions should show:
     - ✅ `Sites.ReadWrite.All` - Status: "Granted for [Organization]"
     - ✅ `Subscription.ReadWrite.All` - Status: "Granted for [Organization]"
   - If not, click **"Grant admin consent"** button

### Issue 2: Permissions Not Added

**Error Code:** `MissingPermissions`

**Solution:**
1. **Verify both permissions are added:**
   - `Sites.ReadWrite.All` (Application permission)
   - `Subscription.ReadWrite.All` (Application permission)
2. **Both must be Application permissions** (not Delegated)

### Issue 3: Resource Format Issue

**Error Code:** `BadRequest` or `InvalidResource`

**Solution:**
- The resource format should be: `/sites/{siteId}/drives/{driveId}/root/children`
- Verify Site ID and Drive ID are correct

---

## 📋 Admin Checklist

**Ask admin to verify in Azure Portal:**

1. **Go to:** https://portal.azure.com
2. **Azure Active Directory** → **App registrations**
3. **Find:** `SMEPilot-GraphAPI` (or your app name)
4. **API permissions** tab
5. **Verify:**
   - ✅ `Sites.ReadWrite.All` is listed
   - ✅ `Subscription.ReadWrite.All` is listed
   - ✅ Both show "Granted for [Your Organization]"
   - ✅ Green checkmark icon visible
6. **If not granted:**
   - Click **"Grant admin consent for [Your Organization]"**
   - Confirm with "Yes"
   - Wait for confirmation

---

## 🎯 Quick Test

**After admin grants consent:**

1. **Restart Function App**
2. **Run SetupSubscription.ps1** again
3. **Should succeed** if consent was granted

---

## 📞 Share Error Details

**After restarting Function App and running SetupSubscription.ps1:**

Please share:
1. **The full error message** from PowerShell
2. **Error code** (if shown)
3. **Any additional details**

This will help identify the exact issue.

---

**Most likely fix: Admin needs to grant consent for both permissions!** ✅

