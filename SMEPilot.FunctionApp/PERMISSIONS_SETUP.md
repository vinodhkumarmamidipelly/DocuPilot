# SharePoint Permissions Setup for Automatic Column Creation

## Problem

The app is getting `accessDenied` errors when trying to create SharePoint columns automatically. This means the Azure AD App Registration doesn't have the required permissions.

## ⚠️ Important: Sites.ReadWrite.All May Not Be Sufficient

If you already have `Sites.ReadWrite.All` (Application) granted but still get `accessDenied` errors, you likely need **`Sites.Manage.All`** instead.

**Why?**
- `Sites.ReadWrite.All` allows reading/writing **items** in lists
- Creating **columns** (list schema changes) requires **full management** permissions
- `Sites.Manage.All` explicitly covers list and column management

## Solution: Grant Sites.Manage.All Permission

### Step 1: Go to Azure Portal

1. Navigate to [Azure Portal](https://portal.azure.com)
2. Go to **Azure Active Directory** → **App registrations**
3. Find and select your app registration (Client ID: `7ca14971-d9a3-4b9b-bad7-33a85c25f485`)

### Step 2: Add Sites.Manage.All Permission

1. In your app registration, click **API permissions** in the left menu
2. Click **+ Add a permission**
3. Select **Microsoft Graph**
4. Select **Application permissions** (NOT Delegated permissions)
5. Search for and select **Sites.Manage.All**
   - **Sites.Manage.All**: Full control including column creation (REQUIRED for automatic column creation)
6. Click **Add permissions**

### Step 3: Grant Admin Consent

**CRITICAL**: Application permissions require admin consent!

1. After adding the permission, you'll see it listed with a yellow warning icon
2. Click **Grant admin consent for [Your Tenant Name]**
3. Confirm the consent
4. The status should change to green checkmark: **✓ Granted for [Your Tenant]**

### Step 4: Verify Permissions

1. Check that the permission shows:
   - **Status**: ✅ **Granted for [Your Tenant]**
   - **Type**: **Application** (NOT Delegated)
   - **Admin consent required**: **Yes**

### Step 5: Restart Function App (CRITICAL)

**IMPORTANT**: After granting new permissions, you MUST restart the Function App to clear cached tokens!

1. Stop your Function App (if running locally)
2. Wait 10-15 seconds
3. Start the Function App again
4. The app will acquire a new access token with the updated permissions

### Step 6: Wait for Propagation

- Permissions can take **5-15 minutes** to propagate across Azure AD
- After restarting, wait a few minutes before testing
- Try uploading a file again

---

## ⚠️ Why Sites.ReadWrite.All May Not Work

If you already have `Sites.ReadWrite.All` (Application) but still get `accessDenied`:

- **`Sites.ReadWrite.All`** allows reading/writing **list items** (data)
- **Creating columns** requires modifying the **list schema** (structure)
- **`Sites.Manage.All`** is required for schema changes like column creation

**Solution**: Add `Sites.Manage.All` (Application permission) in addition to or instead of `Sites.ReadWrite.All`.

---

## Verification

After granting permissions, check your logs. You should see:

```
✅ [EnsureColumnsExistAsync] Successfully created column 'SMEPilot_Enriched' (ID: ...)
✅ [EnsureColumnsExistAsync] Column check complete. Created 8 new columns
```

Instead of:

```
❌ OData Error Code: accessDenied, Message: Access denied
```

---

## Troubleshooting

### Still Getting "Access Denied" After Granting Permissions?

1. **Wait 5-15 minutes** - Permissions need time to propagate
2. **Restart Function App** - Clear any cached tokens
3. **Check Admin Consent** - Ensure it shows "Granted for [Your Tenant]" (green checkmark)
4. **Verify Permission Type** - Must be **Application** permission, NOT Delegated
5. **Check Tenant Admin** - Only Global Admin or Privileged Role Admin can grant consent

### Permission Not Available?

- Some tenants may have restricted which permissions can be granted
- Contact your Azure AD administrator
- You may need to use manual column creation (see `CREATE_SHAREPOINT_COLUMNS.md`)

---

## Security Note

**Sites.Manage.All** grants full control over all SharePoint sites in your tenant. This is appropriate for:
- Internal tools
- Admin-managed applications
- Production environments where the app is trusted

For more restricted scenarios, use **Sites.ReadWrite.All** which provides read/write access without full management capabilities.

---

## Quick Checklist

- [ ] Added `Sites.Manage.All` or `Sites.ReadWrite.All` (Application permission)
- [ ] Granted admin consent (green checkmark visible)
- [ ] Waited 5-15 minutes for propagation
- [ ] Restarted Function App
- [ ] Tested by uploading a file

---

## Next Steps

Once permissions are granted:
1. The app will automatically create columns on first file upload
2. No manual setup required
3. Columns will be created in the SharePoint list automatically
4. Metadata updates will work seamlessly

