# Quick Setup: SharePoint Columns (5 Minutes)

## 🎯 Goal
Create 6 required columns in your SharePoint document library to prevent file reprocessing.

---

## 📋 Quick Checklist

### Step 1: Open Library Settings (30 seconds)
1. Go to: `https://onblick.sharepoint.com/sites/DocEnricher-PoC`
2. Navigate to your document library (where you upload files)
3. Click **⚙️ Settings** (top right) → **Library settings**

### Step 2: Create Columns (4 minutes)

For each column below, click **"Create column"** and fill in:

| # | Column Name | Type | Default | Notes |
|---|-------------|------|---------|-------|
| 1 | `SMEPilot_Enriched` | **Yes/No** | No | Checkbox - marks if processed |
| 2 | `SMEPilot_Status` | **Single line of text** | (blank) | Status: Processing/Completed/Failed |
| 3 | `SMEPilot_EnrichedFileUrl` | **Hyperlink or Picture** | (blank) | URL to enriched file |
| 4 | `SMEPilot_EnrichedJobId` | **Single line of text** | (blank) | Unique job ID |
| 5 | `SMEPilot_Confidence` | **Number** | 0 | Confidence score |
| 6 | `SMEPilot_Classification` | **Single line of text** | (blank) | Document type |

**⚠️ IMPORTANT**: Column names must match **exactly** (case-sensitive):
- ✅ `SMEPilot_Enriched` (correct)
- ❌ `SMEPilot_enriched` (wrong - lowercase 'e')
- ❌ `SMEPilot Enriched` (wrong - space instead of underscore)

---

## 📝 Detailed Steps (Copy-Paste Ready)

### Column 1: SMEPilot_Enriched
1. Click **"Create column"**
2. **Name**: `SMEPilot_Enriched`
3. **Type**: Select **"Yes/No"**
4. **Default value**: Select **"No"**
5. Click **OK**

### Column 2: SMEPilot_Status
1. Click **"Create column"**
2. **Name**: `SMEPilot_Status`
3. **Type**: Select **"Single line of text"**
4. Click **OK**

### Column 3: SMEPilot_EnrichedFileUrl
1. Click **"Create column"**
2. **Name**: `SMEPilot_EnrichedFileUrl`
3. **Type**: Select **"Hyperlink or Picture"**
4. Click **OK**

### Column 4: SMEPilot_EnrichedJobId
1. Click **"Create column"**
2. **Name**: `SMEPilot_EnrichedJobId`
3. **Type**: Select **"Single line of text"**
4. Click **OK**

### Column 5: SMEPilot_Confidence
1. Click **"Create column"**
2. **Name**: `SMEPilot_Confidence`
3. **Type**: Select **"Number"**
4. Click **OK**

### Column 6: SMEPilot_Classification
1. Click **"Create column"**
2. **Name**: `SMEPilot_Classification`
3. **Type**: Select **"Single line of text"**
4. Click **OK**

---

## ✅ Verification (30 seconds)

1. Go back to your document library view
2. Click **"Add column"** or **"Show/Hide columns"**
3. You should see all 6 `SMEPilot_*` columns listed
4. ✅ **Done!**

---

## 🧪 Test It

1. Upload a new file to your library
2. Check the Function App logs
3. You should see: `✅ [UpdateListItemFieldsAsync] PatchAsync call completed successfully`
4. Upload the same file again - it should **NOT** be reprocessed

---

## 🆘 Troubleshooting

### "Column already exists"
- ✅ Good! Skip that column and continue with the rest

### "Invalid column name"
- Check for typos
- No spaces - use underscores: `SMEPilot_Enriched` not `SMEPilot Enriched`
- Case-sensitive: `SMEPilot_Enriched` not `SMEPilot_enriched`

### Still seeing "Field not recognized" error
- Wait 1-2 minutes after creating columns (SharePoint needs time to sync)
- Refresh the page
- Check column names match exactly (case-sensitive)

---

## 📸 Visual Guide

**Where to find Library Settings:**
```
SharePoint Site
  └── Your Document Library
      └── ⚙️ Settings (top right)
          └── Library settings
              └── Columns section
                  └── "Create column" button
```

---

## ⚡ PowerShell Alternative (Advanced)

If you prefer automation, run this PowerShell script:

```powershell
# Install PnP PowerShell (one-time)
Install-Module -Name PnP.PowerShell -Scope CurrentUser

# Connect to your site
Connect-PnPOnline -Url "https://onblick.sharepoint.com/sites/DocEnricher-PoC" -Interactive

# Replace "Documents" with your actual library name
$list = Get-PnPList -Identity "Documents"

# Create all columns at once
Add-PnPField -List $list -Type Boolean -InternalName "SMEPilot_Enriched" -DisplayName "SMEPilot_Enriched"
Add-PnPField -List $list -Type Text -InternalName "SMEPilot_Status" -DisplayName "SMEPilot_Status"
Add-PnPField -List $list -Type URL -InternalName "SMEPilot_EnrichedFileUrl" -DisplayName "SMEPilot_EnrichedFileUrl"
Add-PnPField -List $list -Type Text -InternalName "SMEPilot_EnrichedJobId" -DisplayName "SMEPilot_EnrichedJobId"
Add-PnPField -List $list -Type Number -InternalName "SMEPilot_Confidence" -DisplayName "SMEPilot_Confidence"
Add-PnPField -List $list -Type Text -InternalName "SMEPilot_Classification" -DisplayName "SMEPilot_Classification"

Write-Host "✅ All columns created successfully!" -ForegroundColor Green
```

---

**Time needed**: ~5 minutes  
**Difficulty**: Easy (just clicking buttons)  
**Result**: Files will process once and stop reprocessing! 🎉

