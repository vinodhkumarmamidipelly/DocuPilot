# Automatic Column Creation

## ✅ Feature: Auto-Create SharePoint Columns

**Problem Solved**: No more manual column creation! The app now automatically creates required SharePoint columns when they're missing.

---

## How It Works

### Automatic Detection & Creation

1. **When**: The app tries to update metadata on a file
2. **If**: SharePoint returns `Field 'SMEPilot_Enriched' is not recognized`
3. **Then**: The app automatically:
   - Detects missing columns
   - Creates all required columns programmatically
   - Retries the metadata update
   - Continues processing seamlessly

### Columns Created Automatically

The app creates these 8 columns if they don't exist:

| Column Name | Type | Purpose |
|-------------|------|---------|
| `SMEPilot_Enriched` | Yes/No | Marks if document is processed |
| `SMEPilot_Status` | Text | Processing status (Processing/Completed/Failed/Retry) |
| `SMEPilot_EnrichedFileUrl` | Hyperlink | URL to enriched document |
| `SMEPilot_EnrichedJobId` | Text | Unique job ID |
| `SMEPilot_Confidence` | Number | Confidence score |
| `SMEPilot_Classification` | Text | Document classification |
| `SMEPilot_ErrorMessage` | Text | Error message (if failed) |
| `SMEPilot_LastErrorTime` | DateTime | Last error timestamp |

---

## Benefits for Customers

✅ **Zero Manual Setup** - Columns are created automatically  
✅ **No Configuration Required** - Works out of the box  
✅ **Idempotent** - Safe to run multiple times (skips existing columns)  
✅ **Error Resilient** - Continues even if some columns fail to create  
✅ **Race Condition Safe** - Handles concurrent column creation  

---

## Technical Details

### Implementation

- **Method**: `GraphHelper.EnsureColumnsExistAsync(siteId, listId)`
- **Trigger**: Automatically called when "field not recognized" error occurs
- **API**: Uses Microsoft Graph API `POST /sites/{site-id}/lists/{list-id}/columns`
- **Permissions**: Requires `Sites.Manage.All` or `Sites.ReadWrite.All`

### Error Handling

- **Column Already Exists**: Skips gracefully (idempotent)
- **Permission Denied**: Logs error, falls back to manual instructions
- **Partial Failure**: Creates as many columns as possible, continues with others

### Logging

The app logs all column creation activities:

```
🔍 [EnsureColumnsExistAsync] Checking for required columns...
📋 [EnsureColumnsExistAsync] Found 15 existing columns
➕ [EnsureColumnsExistAsync] Creating column 'SMEPilot_Enriched' (Type: boolean)...
✅ [EnsureColumnsExistAsync] Successfully created column 'SMEPilot_Enriched' (ID: abc123)
✅ [EnsureColumnsExistAsync] Column check complete. Created 8 new columns
```

---

## Required Permissions

Your Azure AD App Registration needs:

- **Microsoft Graph API Permission**: `Sites.Manage.All` or `Sites.ReadWrite.All`
- **Type**: Application permission (not delegated)
- **Admin Consent**: Required

### How to Grant Permissions

1. Go to Azure Portal → Azure Active Directory → App registrations
2. Select your app registration
3. Go to **API permissions**
4. Click **Add a permission** → **Microsoft Graph** → **Application permissions**
5. Search for `Sites.Manage.All` or `Sites.ReadWrite.All`
6. Click **Add permissions**
7. Click **Grant admin consent for [Your Tenant]**

---

## Fallback Behavior

If automatic column creation fails (e.g., insufficient permissions):

1. App logs detailed error message
2. Processing continues (document is still enriched)
3. Logs include link to manual setup guide
4. Files may be reprocessed until columns are created manually

---

## Testing

### Test Automatic Creation

1. **Delete columns** (or use a new library without columns)
2. **Upload a file**
3. **Check logs** - you should see:
   ```
   ⚠️ Custom metadata fields do not exist in SharePoint list!
   Attempting to create columns automatically...
   ✅ Columns created successfully! Retrying metadata update...
   ✅ Metadata update succeeded after creating columns!
   ```

### Verify Columns Created

1. Go to SharePoint library
2. Click **⚙️ Settings** → **Library settings**
3. Check **Columns** section
4. You should see all `SMEPilot_*` columns listed

---

## Manual Override

If you prefer manual setup (or automatic creation fails):

- See `CREATE_SHAREPOINT_COLUMNS.md` for manual instructions
- See `QUICK_COLUMN_SETUP.md` for quick checklist

---

## Code Location

- **Method**: `SMEPilot.FunctionApp/Helpers/GraphHelper.cs`
- **Method Name**: `EnsureColumnsExistAsync(string siteId, string listId)`
- **Called From**: `UpdateListItemFieldsAsync` error handler

---

## Summary

🎉 **Your app is now production-ready!** Customers don't need to manually create columns - the app does it automatically on first use.

