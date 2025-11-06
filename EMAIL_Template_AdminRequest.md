# Email Template: Request for Azure AD App Registration

## 📧 Copy-Paste Email

**Subject:** Azure AD App Registration Required for SMEPilot Project

---

Hi [Admin Name],

I'm working on the SMEPilot project (SharePoint document enrichment tool) and need an Azure AD App Registration configured to enable Microsoft Graph API integration.

**What's Needed:**
- Azure AD App Registration with Microsoft Graph API permissions
- Application permissions: `Sites.ReadWrite.All` and `Subscription.ReadWrite.All`
- Admin consent for both permissions
- Client ID, Tenant ID, and Client Secret

**Detailed Instructions:**
I've prepared a step-by-step guide: `ADMIN_Guide_AzureAD_Setup.md`

**Quick Summary:**
1. Create App Registration: `SMEPilot-GraphAPI`
2. Add Application permissions: `Sites.ReadWrite.All` and `Subscription.ReadWrite.All`
3. Grant admin consent
4. Create Client Secret and provide credentials

**Estimated Time:** 10-15 minutes

**After Setup:**
Please provide these 3 values:
- Tenant ID (Directory ID)
- Client ID (Application ID)
- Client Secret (Value - shown only once)

The guide includes screenshots, troubleshooting tips, and security best practices.

Let me know if you need any clarification or have questions!

Thanks,
[Your Name]

---

**Attachments:**
- `ADMIN_Guide_AzureAD_Setup.md` (detailed guide)
- `ADMIN_Request_Summary.md` (quick reference)

---

## 📋 What Admin Needs to Do

1. **Read:** `ADMIN_Guide_AzureAD_Setup.md`
2. **Follow:** Step-by-step instructions
3. **Provide:** Tenant ID, Client ID, Client Secret
4. **Verify:** Both permissions show "Granted for [Organization]"

---

## ✅ After You Receive Credentials

1. **Update `SMEPilot.FunctionApp/local.settings.json`:**
   ```json
   {
     "Values": {
       "Graph_TenantId": "PASTE-TENANT-ID-HERE",
       "Graph_ClientId": "PASTE-CLIENT-ID-HERE",
       "Graph_ClientSecret": "PASTE-CLIENT-SECRET-HERE"
     }
   }
   ```

2. **Restart Function App** in Visual Studio (F5)

3. **Run SetupSubscription.ps1** again:
   ```powershell
   .\SetupSubscription.ps1 `
     -SiteId "onblick.sharepoint.com,4b82e6b5-e79c-4787-8aa8-8c4aad49e9e0,af7fd8eb-328c-45b1-b7a7-8b79d27dd516" `
     -DriveId "b!teaCS5znh0eKqIxKrUnp4OvYf6-MMrFFt6eLedJ91Ra5fXoii7EjRK7WFJxSDDf3" `
     -NotificationUrl "https://562fbad9f946.ngrok-free.app/api/ProcessSharePointFile"
   ```

4. **Verify subscription created** successfully

---

**Ready to send? Share the guide with your admin!** 📧

