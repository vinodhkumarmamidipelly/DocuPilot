# Fix: Graph Explorer 400 Error

## Problem

When querying `/organization` endpoint, you get:
```
Bad Request - 400
```

**Reason**: `/organization` endpoint requires **Organization.Read.All** permission, which regular users don't have.

## Solutions

---

## ✅ Solution 1: Extract from SharePoint Site (Recommended)

### Step 1: Query Your SharePoint Site

**In Graph Explorer, change query to:**

```
https://graph.microsoft.com/v1.0/sites/onblick.sharepoint.com
```

### Step 2: Run Query

Click **"Run query"**

### Step 3: Find Tenant ID in Response

**Response will look like:**
```json
{
  "id": "onblick.sharepoint.com,12345678-1234-1234-1234-123456789012,abc123-def456-...",
  "displayName": "OnBlick Inc. Team Site",
  ...
}
```

**The Site ID format is:**
```
domain.sharepoint.com,TENANT-UUID,SITE-GUID
```

**Extract the middle part** (between first and second comma):
```
12345678-1234-1234-1234-123456789012
```

**That's your Tenant ID!** ✅

---

## ✅ Solution 2: Use PowerShell Script

I've created `GetTenantID_Simple.ps1` that works without admin permissions:

1. **Open PowerShell**
2. **Navigate to**: `D:\CodeBase\DocuPilot`
3. **Run**: `.\GetTenantID_Simple.ps1`
4. **Sign in** when prompted
5. **Tenant ID** will be displayed

This uses `User.Read` permission which you already have.

---

## ✅ Solution 3: Get from Your Profile (Might Not Have Tenant ID)

**Query:**
```
https://graph.microsoft.com/v1.0/me
```

**Response won't directly show Tenant ID**, but you can see:
- Your domain: `rigaps.com` or `rigaps.onmicrosoft.com`
- Your user ID

**This doesn't give Tenant ID directly**, but confirms your organization.

---

## ✅ Solution 4: Ask IT Admin

**Fastest option:**

Ask your IT administrator:
> "I need the Azure AD Tenant ID for SMEPilot configuration. Can you get it from Azure Portal → Azure AD → Overview?"

They can provide it in 30 seconds.

---

## ✅ Solution 5: Check Existing Configurations

If your organization has:
- Existing Azure services
- SharePoint Admin Center access
- Other Microsoft 365 integrations

Check those configurations - Tenant ID might already be documented.

---

## Quick Test Workaround

**For Power Automate testing**, you can temporarily use:

```json
{
  "tenantId": "rigaps"
}
```

Or check if SMEPilot accepts domain format for testing.

**Note**: Production needs UUID format, but this works for flow testing.

---

## Recommended Action

1. **Try Solution 1** (SharePoint Site) - Query:
   ```
   https://graph.microsoft.com/v1.0/sites/onblick.sharepoint.com
   ```
   Extract UUID from Site ID.

2. **If that doesn't work**, use **Solution 2** (PowerShell script)

3. **If still stuck**, **Solution 4** (Ask IT admin)

---

**Start with Solution 1 - It's the easiest!** 🚀

