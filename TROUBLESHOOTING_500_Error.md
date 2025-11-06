# Troubleshooting: 500 Error on Subscription Creation

## 🔍 Current Issue

Getting 500 Internal Server Error when creating Graph subscription.

## ✅ Enhanced Error Logging Added

I've updated `SetupSubscription.cs` to provide more detailed error information.

---

## 🔧 Steps to Get Detailed Error

### Step 1: Restart Function App

**In Visual Studio:**
1. **Stop** the Function App (if running)
2. **Rebuild** or just **Restart** (F5)
3. This loads the enhanced error logging

### Step 2: Run SetupSubscription Again

```powershell
.\SetupSubscription.ps1 `
  -SiteId "onblick.sharepoint.com,4b82e6b5-e79c-4787-8aa8-8c4aad49e9e0,af7fd8eb-328c-45b1-b7a7-8b79d27dd516" `
  -DriveId "b!teaCS5znh0eKqIxKrUnp4OvYf6-MMrFFt6eLedJ91Ra5fXoii7EjRK7WFJxSDDf3" `
  -NotificationUrl "https://562fbad9f946.ngrok-free.app/api/ProcessSharePointFile"
```

### Step 3: Check Error Response

**The PowerShell output will now show:**
- Detailed error message
- Exception type
- Inner exception (if any)
- Stack trace
- Whether credentials are configured

### Step 4: Check Visual Studio Output Window

**In Visual Studio:**
- View → Output
- Select "Debug" or "Azure Functions" from dropdown
- Look for error messages starting with "ERROR in SetupSubscription"

---

## 🔍 Common Issues & Solutions

### Issue 1: Admin Consent Not Granted

**Symptoms:**
- Error: "Insufficient privileges" or "AADSTS70011"
- Error: "The application does not have the required permissions"

**Solution:**
- Admin must grant consent for both permissions:
  - `Sites.ReadWrite.All`
  - `Subscription.ReadWrite.All`
- Verify in Azure Portal → App Registration → API Permissions
- Both should show "Granted for [Your Organization]"

### Issue 2: Incorrect Credentials

**Symptoms:**
- Error: "AADSTS7000215" or "Invalid client secret"
- Error: "Application not found"

**Solution:**
- Verify credentials in `local.settings.json`:
  - Tenant ID: `8e8a9805-15bc-44e4-9fd5-8bf2cd01fe09`
  - Client ID: `7ca14971-d9a3-4b9b-bad7-33a85c25f485`
  - Client Secret: `x2u8Q~e9ZuDEzhf_uAlHzvwDTCVGOEfXTnu0bbIl`
- Ensure no extra spaces or quotes
- Verify secret hasn't expired

### Issue 3: Permissions Missing

**Symptoms:**
- Error: "Insufficient privileges to complete the operation"
- Error: "Access denied"

**Solution:**
- Verify both permissions are added:
  - `Sites.ReadWrite.All` (Application)
  - `Subscription.ReadWrite.All` (Application)
- Ensure admin consent is granted

### Issue 4: Graph API Authentication Failure

**Symptoms:**
- Error: "Unauthorized" or "401"
- Error: "Token acquisition failed"

**Solution:**
- Verify credentials are correct
- Check if Client Secret expired
- Ensure Tenant ID matches your organization

---

## 📋 Diagnostic Checklist

After restarting Function App and running SetupSubscription.ps1:

- [ ] Check PowerShell output for detailed error
- [ ] Check Visual Studio Output window
- [ ] Verify credentials in `local.settings.json`
- [ ] Verify admin consent granted in Azure Portal
- [ ] Verify both permissions are added
- [ ] Check if Client Secret expired

---

## 🔗 Verify Admin Consent

**Ask admin to verify in Azure Portal:**

1. Go to: https://portal.azure.com
2. Azure Active Directory → App registrations
3. Find: `SMEPilot-GraphAPI` (or your app name)
4. API permissions
5. Check both permissions show:
   - ✅ Status: "Granted for [Your Organization]"
   - ✅ Green checkmark icon

---

## 📞 Next Steps

**After restarting Function App:**

1. **Run SetupSubscription.ps1** again
2. **Copy the full error response** from PowerShell
3. **Check Visual Studio Output** window
4. **Share the error details** so we can diagnose further

---

**Restart Function App and try again - the enhanced error logging will help us identify the issue!** 🔍

