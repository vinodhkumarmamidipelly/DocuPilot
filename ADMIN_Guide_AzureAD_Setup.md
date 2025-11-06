# Admin Guide: Azure AD App Registration for SMEPilot

## 📋 Overview

SMEPilot requires an Azure AD App Registration with Microsoft Graph API permissions to enable automatic SharePoint document processing.

**Required Permissions:**
- `Sites.ReadWrite.All` (Application permission)
- `Subscription.ReadWrite.All` (Application permission)

**Estimated Time:** 10-15 minutes

---

## ✅ Step-by-Step Instructions

### Step 1: Create App Registration

1. **Go to Azure Portal**
   - URL: https://portal.azure.com
   - Sign in with admin account

2. **Navigate to Azure Active Directory**
   - Click "Azure Active Directory" in left menu
   - Or search "Azure Active Directory" in top search bar

3. **Go to App Registrations**
   - Click "App registrations" in left menu
   - Click **"+ New registration"** button

4. **Fill Registration Form**
   - **Name:** `SMEPilot-GraphAPI` (or your naming convention)
   - **Supported account types:** 
     - Select **"Accounts in this organizational directory only"** (Single tenant)
   - **Redirect URI:** Leave blank (not needed for this app)
   - Click **"Register"**

5. **Copy Application Details**
   - After registration, you'll see the Overview page
   - **Copy these values:**
     - **Application (client) ID** → Save as `Graph_ClientId`
     - **Directory (tenant) ID** → Save as `Graph_TenantId`
     - ⚠️ **Keep these secure** - you'll need them later

---

### Step 2: Create Client Secret

1. **Navigate to Certificates & Secrets**
   - In the App Registration page, click **"Certificates & secrets"** in left menu

2. **Create New Client Secret**
   - Click **"+ New client secret"** button
   - **Description:** `SMEPilot Secret` (or your description)
   - **Expires:** Select **24 months** (or your organization's policy)
   - Click **"Add"**

3. **Copy Secret Value**
   - ⚠️ **IMPORTANT:** Copy the **Value** (not Secret ID) immediately
   - This value is shown **ONLY ONCE** and cannot be retrieved later
   - Save as `Graph_ClientSecret`
   - If missed, create a new secret

---

### Step 3: Configure API Permissions

1. **Navigate to API Permissions**
   - In the App Registration page, click **"API permissions"** in left menu

2. **Add Microsoft Graph Permission**
   - Click **"+ Add a permission"** button
   - Select **"Microsoft Graph"**
   - Select **"Application permissions"** (not Delegated permissions)

3. **Add Required Permissions**
   
   **Permission 1: Sites.ReadWrite.All**
   - Search for: `Sites.ReadWrite.All`
   - Check the box
   - Click **"Add permissions"**

   **Permission 2: Subscription.ReadWrite.All**
   - Click **"+ Add a permission"** again
   - Select **"Microsoft Graph"** → **"Application permissions"**
   - Search for: `Subscription.ReadWrite.All`
   - Check the box
   - Click **"Add permissions"**

4. **Verify Permissions Added**
   - You should see:
     - ✅ `Sites.ReadWrite.All` (Application)
     - ✅ `Subscription.ReadWrite.All` (Application)
     - ⚠️ Both should show **"Not granted"** status

---

### Step 4: Grant Admin Consent

**⚠️ CRITICAL:** Application permissions require admin consent.

1. **Click "Grant admin consent for [Your Organization]"**
   - Blue button at the top of API permissions page
   - You'll see a confirmation dialog

2. **Confirm Grant**
   - Click **"Yes"** to grant admin consent
   - Wait for confirmation message

3. **Verify Status**
   - After consent, both permissions should show:
     - ✅ Status: **"Granted for [Your Organization]"**
     - ✅ Green checkmark icon

---

### Step 5: Provide Credentials to Developer

**Provide these 3 values to the developer:**

1. **Tenant ID** (Directory ID)
   - Format: `12345678-1234-1234-1234-123456789012`
   - Location: App Registration → Overview → Directory (tenant) ID

2. **Client ID** (Application ID)
   - Format: `87654321-4321-4321-4321-210987654321`
   - Location: App Registration → Overview → Application (client) ID

3. **Client Secret** (Secret Value)
   - Format: `abc123DEF456ghi789...`
   - Location: Created in Step 2 (Certificates & secrets)
   - ⚠️ If lost, create a new secret

---

## 📋 Summary Checklist

- [ ] App Registration created: `SMEPilot-GraphAPI`
- [ ] Tenant ID copied
- [ ] Client ID copied
- [ ] Client Secret created and copied
- [ ] Permission added: `Sites.ReadWrite.All` (Application)
- [ ] Permission added: `Subscription.ReadWrite.All` (Application)
- [ ] Admin consent granted for both permissions
- [ ] Credentials provided to developer securely

---

## 🔒 Security Notes

1. **Client Secret**
   - Treat as sensitive credential
   - Store securely (password manager recommended)
   - Set appropriate expiration (24 months recommended)
   - Rotate if compromised

2. **Permissions**
   - `Sites.ReadWrite.All` grants full SharePoint access
   - `Subscription.ReadWrite.All` allows creating webhooks
   - Both are application-level permissions (app-only access)

3. **Access Control**
   - This app registration uses application permissions
   - No user sign-in required
   - Operates with the permissions granted, not user context

---

## 🆘 Troubleshooting

### "Admin consent required" error
- **Solution:** Ensure Step 4 (Grant Admin Consent) is completed
- Both permissions must show "Granted for [Your Organization]"

### Client Secret not shown
- **Solution:** Create a new secret (value is only shown once)

### Permissions not available
- **Solution:** Ensure "Application permissions" is selected (not Delegated)

### Tenant ID not found
- **Solution:** Go to Azure AD → Overview → Tenant ID is shown there

---

## 📞 Support

If you encounter issues:
1. Check Azure AD → App Registrations → Select your app
2. Verify all steps completed
3. Contact developer if credentials are needed

---

## ✅ Completion

Once all steps are complete:
- Developer will update `local.settings.json` with the credentials
- Function App will be restarted
- Graph subscription will be created successfully

**Thank you for setting up the App Registration!** 🚀

