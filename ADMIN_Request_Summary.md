# Quick Reference: Information Needed from Admin

## 📋 Request for Admin

Please create an Azure AD App Registration with the following configuration:

### App Details
- **Name:** `SMEPilot-GraphAPI`
- **Type:** Single tenant (Accounts in this organizational directory only)

### Required Permissions (Application Permissions)
1. **Sites.ReadWrite.All** - For SharePoint document access
2. **Subscription.ReadWrite.All** - For creating Graph webhooks

### Required Credentials
After setup, please provide:

1. **Tenant ID** (Directory ID)
   - Format: `12345678-1234-1234-1234-123456789012`

2. **Client ID** (Application ID)
   - Format: `87654321-4321-4321-4321-210987654321`

3. **Client Secret** (Value)
   - Format: `abc123DEF456ghi789...`
   - ⚠️ Copy the VALUE immediately (shown only once)

### Important Notes
- ✅ Admin consent MUST be granted for both permissions
- ✅ Use Application permissions (not Delegated)
- ✅ Client Secret expires in 24 months (recommended)

---

## 📖 Detailed Instructions

**Full step-by-step guide:** `ADMIN_Guide_AzureAD_Setup.md`

The guide includes:
- Screenshot descriptions
- Troubleshooting tips
- Security best practices
- Verification steps

---

## ✅ After Admin Completes Setup

1. **Receive credentials** from admin
2. **Update `local.settings.json`:**
   ```json
   {
     "Values": {
       "Graph_TenantId": "YOUR-TENANT-ID",
       "Graph_ClientId": "YOUR-CLIENT-ID",
       "Graph_ClientSecret": "YOUR-CLIENT-SECRET"
     }
   }
   ```
3. **Restart Function App** (F5 in Visual Studio)
4. **Run SetupSubscription.ps1** again

---

**Share `ADMIN_Guide_AzureAD_Setup.md` with your admin!** 📧

