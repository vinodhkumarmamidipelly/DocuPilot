# Graph API Credentials Required

## ❌ Current Issue

The subscription creation is failing because **Graph API credentials are not configured** in `local.settings.json`.

The following are empty:
- `Graph_TenantId`
- `Graph_ClientId`
- `Graph_ClientSecret`

---

## ✅ Solution: Configure Graph API Credentials

### Step 1: Create Azure AD App Registration

1. **Go to Azure Portal:** https://portal.azure.com
2. **Azure Active Directory** → **App registrations**
3. **New registration**
4. **Name:** `SMEPilot-GraphAPI`
5. **Supported account types:** Single tenant
6. **Register**

### Step 2: Get Client ID and Tenant ID

**After registration:**
- Copy **Application (client) ID** → This is `Graph_ClientId`
- Copy **Directory (tenant) ID** → This is `Graph_TenantId`

### Step 3: Create Client Secret

1. **Certificates & secrets** → **New client secret**
2. **Description:** `SMEPilot Secret`
3. **Expires:** 24 months (or your preference)
4. **Add** → **Copy the secret value immediately** → This is `Graph_ClientSecret`

⚠️ **Important:** Secret value is only shown once!

### Step 4: Grant API Permissions

1. **API permissions** → **Add a permission**
2. **Microsoft Graph** → **Application permissions**
3. **Add these permissions:**
   - `Sites.ReadWrite.All` (for SharePoint access)
   - `Subscription.ReadWrite.All` (for creating webhooks)
4. **Grant admin consent** (required for application permissions)

### Step 5: Update local.settings.json

**Edit `SMEPilot.FunctionApp/local.settings.json`:**

```json
{
  "Values": {
    "Graph_TenantId": "YOUR-TENANT-ID",
    "Graph_ClientId": "YOUR-CLIENT-ID",
    "Graph_ClientSecret": "YOUR-CLIENT-SECRET",
    ...
  }
}
```

**Replace:**
- `YOUR-TENANT-ID` with Directory (tenant) ID from Step 2
- `YOUR-CLIENT-ID` with Application (client) ID from Step 2
- `YOUR-CLIENT-SECRET` with the secret value from Step 3

### Step 6: Restart Function App

1. **Stop Function App** in Visual Studio
2. **Start again** (F5)
3. **Retry subscription creation**

---

## 🚀 Quick Checklist

- [ ] Azure AD App Registration created
- [ ] Client ID copied
- [ ] Tenant ID copied
- [ ] Client Secret created and copied
- [ ] API permissions granted (Sites.ReadWrite.All, Subscription.ReadWrite.All)
- [ ] Admin consent granted
- [ ] local.settings.json updated
- [ ] Function App restarted
- [ ] Subscription created successfully

---

## 📋 Summary

**You need:**
1. Azure AD App Registration
2. Client ID, Tenant ID, Client Secret
3. API permissions with admin consent
4. Updated local.settings.json
5. Restart Function App

**Once configured, run SetupSubscription.ps1 again!**

---

## ⚠️ Alternative: Use Graph Explorer Token (Not Recommended for Production)

For quick testing, you could use a token from Graph Explorer, but this is **not recommended for production** and won't work for subscriptions anyway since subscriptions require application permissions.

**For production, you MUST configure Azure AD App Registration.**

---

**Need help with any step? Let me know!** 🚀

