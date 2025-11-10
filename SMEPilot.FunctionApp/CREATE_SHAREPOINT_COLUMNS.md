# Create SharePoint Columns for SMEPilot

## Problem
The error `Field 'SMEPilot_Enriched' is not recognized` means the custom metadata columns don't exist in your SharePoint list.

## Solution: Create Columns Manually

### Step 1: Go to Your SharePoint List
1. Navigate to your SharePoint site (e.g., `https://onblick.sharepoint.com/sites/DocEnricher-PoC`)
2. Go to the document library where files are uploaded
3. Click the **gear icon** (⚙️) → **List settings** (or **Library settings**)

### Step 2: Create Required Columns

Click **"Create column"** for each of the following:

#### Column 1: `SMEPilot_Enriched`
- **Name**: `SMEPilot_Enriched`
- **Type**: **Yes/No** (checkbox)
- **Default value**: **No**
- **Description**: "Indicates if document has been processed by SMEPilot"
- Click **OK**

#### Column 2: `SMEPilot_Status`
- **Name**: `SMEPilot_Status`
- **Type**: **Single line of text**
- **Description**: "Processing status: Processing, Completed, Failed, Retry, MetadataUpdateFailed"
- Click **OK**

#### Column 3: `SMEPilot_EnrichedFileUrl`
- **Name**: `SMEPilot_EnrichedFileUrl`
- **Type**: **Hyperlink or Picture**
- **Description**: "URL to the enriched document"
- Click **OK**

#### Column 4: `SMEPilot_EnrichedJobId`
- **Name**: `SMEPilot_EnrichedJobId`
- **Type**: **Single line of text**
- **Description**: "Unique job ID for this processing run"
- Click **OK**

#### Column 5: `SMEPilot_Confidence`
- **Name**: `SMEPilot_Confidence`
- **Type**: **Number**
- **Description**: "Confidence score (0-100)"
- Click **OK**

#### Column 6: `SMEPilot_Classification`
- **Name**: `SMEPilot_Classification`
- **Type**: **Single line of text**
- **Description**: "Document classification: Functional, Technical, etc."
- Click **OK**

#### Column 7: `SMEPilot_ErrorMessage` (Optional)
- **Name**: `SMEPilot_ErrorMessage`
- **Type**: **Multiple lines of text**
- **Description**: "Error message if processing failed"
- Click **OK**

#### Column 8: `SMEPilot_LastErrorTime` (Optional)
- **Name**: `SMEPilot_LastErrorTime`
- **Type**: **Date and Time**
- **Description**: "Timestamp of last error (for retry logic)"
- Click **OK**

### Step 3: Verify Columns
1. Go back to your document library
2. Click **"Add column"** or check the column selector
3. You should see all the `SMEPilot_*` columns listed

### Step 4: Test
1. Upload a new file
2. Check the logs - you should see `✅ [UpdateListItemFieldsAsync] PatchAsync call completed successfully`
3. The file should NOT be reprocessed on subsequent webhook notifications

## Alternative: PowerShell Script (Advanced)

If you have SharePoint Admin permissions, you can use PowerShell PnP:

```powershell
# Install PnP PowerShell if needed
# Install-Module -Name PnP.PowerShell

# Connect to your site
Connect-PnPOnline -Url "https://onblick.sharepoint.com/sites/DocEnricher-PoC" -Interactive

# Get the list (replace "Documents" with your library name)
$list = Get-PnPList -Identity "Documents"

# Create columns
Add-PnPField -List $list -Type Boolean -InternalName "SMEPilot_Enriched" -DisplayName "SMEPilot_Enriched"
Add-PnPField -List $list -Type Text -InternalName "SMEPilot_Status" -DisplayName "SMEPilot_Status"
Add-PnPField -List $list -Type URL -InternalName "SMEPilot_EnrichedFileUrl" -DisplayName "SMEPilot_EnrichedFileUrl"
Add-PnPField -List $list -Type Text -InternalName "SMEPilot_EnrichedJobId" -DisplayName "SMEPilot_EnrichedJobId"
Add-PnPField -List $list -Type Number -InternalName "SMEPilot_Confidence" -DisplayName "SMEPilot_Confidence"
Add-PnPField -List $list -Type Text -InternalName "SMEPilot_Classification" -DisplayName "SMEPilot_Classification"
Add-PnPField -List $list -Type Note -InternalName "SMEPilot_ErrorMessage" -DisplayName "SMEPilot_ErrorMessage"
Add-PnPField -List $list -Type DateTime -InternalName "SMEPilot_LastErrorTime" -DisplayName "SMEPilot_LastErrorTime"
```

## Important Notes

- **Column names must match exactly** (case-sensitive): `SMEPilot_Enriched`, `SMEPilot_Status`, etc.
- After creating columns, **existing files won't have these fields** - only new uploads will
- The app will work without these columns, but **files will be reprocessed** until columns are created
- Once columns exist, metadata will be saved and reprocessing will stop

## Quick Check

After creating columns, check the logs. You should see:
```
✅ [UpdateListItemFieldsAsync] PatchAsync call completed successfully
```

Instead of:
```
❌ Field 'SMEPilot_Enriched' is not recognized
```

