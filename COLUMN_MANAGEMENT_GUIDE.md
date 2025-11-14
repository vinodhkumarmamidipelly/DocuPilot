# Column Management Guide

## Issue 1: ListItemAllFields 404 Error

The error occurs because root folders in SharePoint don't have `ListItemAllFields`. The code has been fixed to:
- Detect library root paths (e.g., `/sites/SiteName/LibraryName`)
- Skip folder API calls for library roots
- Use library name extraction instead

**If you're still seeing the error:**
1. **Rebuild the SPFx solution:**
   ```bash
   cd SMEPilot.SPFx
   gulp clean
   gulp build
   gulp bundle --ship
   gulp package-solution --ship
   ```

2. **Redeploy the solution** to SharePoint

3. **Clear browser cache** or use an incognito window

4. **Check the console logs** - you should see:
   ```
   [createMetadataColumns] Path analysis: normalizedPath="/sites/DocEnricher-PoC/ScratchDocs", parts=[sites, DocEnricher-PoC, ScratchDocs], isLikelyLibraryRoot=true
   [createMetadataColumns] Path appears to be a library root, skipping folder API calls
   ```

## Issue 2: Column Limit Exceeded

SharePoint has a limit on the total size of columns in a list/library. When this limit is reached, you'll see:
```
"The column cannot be added because the total size of the columns in this list exceeds the limit."
```

### Solution: Delete Unnecessary Columns

The code now includes methods to help manage columns:

#### Method 1: Using Browser Console

1. Open your SharePoint site
2. Open browser Developer Tools (F12)
3. Go to Console tab
4. Run these commands:

```javascript
// List all columns in a library by path
const columns = await sharePointService.listColumnsByPath('/sites/DocEnricher-PoC/ScratchDocs');
console.table(columns);

// Delete a specific column (replace 'ColumnInternalName' with actual name)
await sharePointService.deleteColumn(libraryId, 'ColumnInternalName');
```

#### Method 2: Using SharePoint UI

1. Go to your SharePoint library
2. Click **Settings** (gear icon) → **Library settings**
3. Under **Columns**, you'll see all columns
4. Click on a column name to edit/delete it
5. **Delete unused columns** that are not needed

#### Method 3: Using PowerShell (Advanced)

```powershell
# Connect to SharePoint Online
Connect-PnPOnline -Url "https://onblick.sharepoint.com/sites/DocEnricher-PoC" -Interactive

# Get all fields in a list
$list = Get-PnPList -Identity "ScratchDocs"
$fields = Get-PnPField -List $list

# List all fields
$fields | Select-Object InternalName, Title, Type | Format-Table

# Delete a field (be careful!)
Remove-PnPField -List $list -Identity "ColumnInternalName" -Force
```

### Identifying Columns to Delete

**Safe to delete:**
- Test columns
- Duplicate columns
- Unused custom columns
- Old version columns (if you've recreated them)

**DO NOT delete:**
- System columns (Title, Created, Modified, etc.)
- Columns used by your application (SMEPilot_*, SourceFolderPath, etc.)
- Required columns

### Columns Created by SMEPilot

**SMEPilotConfig List columns:**
- SourceFolderPath
- DestinationFolderPath
- TemplateFileUrl
- MaxFileSizeMB
- ProcessingTimeoutSeconds
- MaxRetries
- CopilotPrompt
- AccessTeams
- AccessWeb
- AccessO365
- SubscriptionId
- SubscriptionExpiration
- TemplateLibraryPath
- TemplateFileName
- MetadataChangeHandling
- LastUpdated

**Document Library metadata columns:**
- SMEPilot_Enriched
- SMEPilot_Status
- SMEPilot_EnrichedFileUrl
- SMEPilot_LastEnrichedTime
- SMEPilot_EnrichedJobId
- SMEPilot_Confidence
- SMEPilot_Classification
- SMEPilot_ErrorMessage
- SMEPilot_LastErrorTime

### Prevention

To prevent hitting the column limit:
1. **Delete test columns** after testing
2. **Don't create duplicate columns** - check if they exist first
3. **Use the column management methods** to audit columns regularly
4. **Monitor column count** - SharePoint typically allows ~2000 columns total, but the size limit is more restrictive

## Code Methods Available

The `SharePointService` class now includes:

```typescript
// List all columns in a list/library
listColumns(listId: string): Promise<Array<{InternalName, Title, Type, ReadOnly, Required}>>

// Delete a column from a list/library
deleteColumn(listId: string, columnInternalName: string): Promise<boolean>

// List columns by folder path (helper)
listColumnsByPath(sourceFolderPath: string): Promise<Array<{InternalName, Title, Type, ReadOnly, Required}>>
```

## Troubleshooting

**If column deletion fails:**
- Check if the column is in use (referenced by views, workflows, etc.)
- Some system columns cannot be deleted
- You may need Site Collection Administrator permissions

**If you can't identify which columns to delete:**
- Use `listColumnsByPath()` to see all columns
- Look for columns with similar names (duplicates)
- Check creation dates - old test columns are usually safe to delete
- Contact your SharePoint administrator for help

