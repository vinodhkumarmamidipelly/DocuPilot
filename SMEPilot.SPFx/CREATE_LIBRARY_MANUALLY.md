# Create ScratchDocs Library Manually (Quick Fix)

## ⚠️ If Auto-Creation Fails

If the automatic library creation doesn't work (permissions issue), you can create it manually:

## Option 1: Create via SharePoint UI (Easiest)

1. **Go to your SharePoint site:**
   ```
   https://onblick.sharepoint.com/sites/DocEnricher-PoC
   ```

2. **Click "New" → "Document library"**

3. **Name it:** `ScratchDocs`

4. **Click "Create"**

5. **Done!** Now try uploading again.

## Option 2: Use PowerShell (If you have admin access)

```powershell
# Connect to SharePoint
Connect-PnPOnline -Url "https://onblick.sharepoint.com/sites/DocEnricher-PoC" -Interactive

# Create document library
New-PnPList -Title "ScratchDocs" -Template DocumentLibrary
```

## Option 3: Use Existing Library

If you can't create a new library, you can use an existing one:

1. **Update web part property:**
   - Edit the web part
   - Change "ScratchDocs Library Name" to "Documents" (or "Shared Documents")
   - Save

2. **Or update default in code:**
   - The default library name is `ScratchDocs`
   - You can change it to use an existing library

## Why Auto-Creation Might Fail

- **Permissions:** User might not have "Manage Lists" permission
- **Site Settings:** Site might have restrictions on creating lists
- **API Format:** SharePoint REST API might require different format

## Check Console Logs

When you try to upload, check browser console for:
- `"Library 'ScratchDocs' not found, creating..."`
- `"Library 'ScratchDocs' created successfully"` OR error message
- This will tell you if creation is being attempted and if it's failing

## Quick Test

After creating the library manually:
1. Refresh the workbench page
2. Try uploading again
3. Should work now!

