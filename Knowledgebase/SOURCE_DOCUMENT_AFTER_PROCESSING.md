# Source Document After Processing: Remains or Deleted?

## The Question

**"After processing, is the document from source go away or still remains?"**

**Answer:** ✅ **The source document REMAINS in the source folder** - it is NOT deleted.

---

## Current Implementation

### What Happens to Source Documents

**From Code Analysis:**

1. ✅ **Source Document is Downloaded**
   - System downloads source document from source folder
   - Used for processing/enrichment

2. ✅ **Enriched Document is Created**
   - New enriched document is created
   - Saved to destination folder ("SMEPilot Enriched Docs")

3. ✅ **Source Document Metadata is Updated**
   - Metadata updated on **source document** (not deleted)
   - Sets: `SMEPilot_Enriched = true`, `SMEPilot_Status = "Completed"`
   - Adds link to enriched document: `SMEPilot_EnrichedFileUrl`

4. ✅ **Source Document REMAINS**
   - **Source document stays in source folder**
   - **No deletion code** found in implementation
   - Source document is preserved

---

## Code Evidence

### Process Flow (from `ProcessSharePointFile.cs`)

```csharp
// 1. Download source document
var fileStream = await _graph.DownloadFileAsync(driveId, itemId);

// 2. Process and enrich
// ... enrichment logic ...

// 3. Upload enriched document to destination
uploaded = await _graph.UploadFileBytesAsync(
    driveId, 
    _cfg.EnrichedFolderRelativePath,  // "SMEPilot Enriched Docs"
    enrichedName, 
    enrichedBytes);

// 4. Update SOURCE document metadata (NOT delete)
await _graph.UpdateListItemFieldsAsync(driveId, itemId, new Dictionary<string, object>
{
    {"SMEPilot_Enriched", true},
    {"SMEPilot_Status", "Completed"},
    {"SMEPilot_EnrichedFileUrl", uploaded.WebUrl}  // Link to enriched doc
});

// NO DELETE CODE - Source document remains!
```

**Key Point:** No `DeleteFile` or `RemoveFile` calls found in the code.

---

## File Locations After Processing

### Source Folder (Input)
- ✅ **Source document REMAINS here**
- ✅ Original file stays in source folder
- ✅ Metadata updated with enrichment status
- ✅ Link to enriched document added

### Destination Folder (Output)
- ✅ **Enriched document saved here**
- ✅ New file: `{originalName}_enriched.docx`
- ✅ Located in "SMEPilot Enriched Docs" library
- ✅ This is what Copilot searches

---

## Why Source Document Remains

### Benefits of Keeping Source Document

**1. Audit Trail**
- ✅ Original document preserved
- ✅ Can compare original vs enriched
- ✅ Historical record maintained

**2. Reprocessing Capability**
- ✅ Can reprocess if needed
- ✅ Idempotent processing (safe to reprocess)
- ✅ Metadata prevents duplicate processing

**3. User Access**
- ✅ Users can still access original
- ✅ Can download original if needed
- ✅ Source folder serves as archive

**4. Error Recovery**
- ✅ If enrichment fails, original is safe
- ✅ Can retry processing
- ✅ No data loss risk

---

## Current Behavior Summary

### What Happens

```
1. User uploads document → Source Folder
   ↓
2. Webhook triggers processing
   ↓
3. System downloads source document
   ↓
4. System enriches document
   ↓
5. System uploads enriched document → Destination Folder
   ↓
6. System updates source document metadata
   ↓
7. Source document REMAINS in source folder ✅
```

**Result:**
- ✅ Source document: **REMAINS** in source folder
- ✅ Enriched document: **SAVED** to destination folder
- ✅ Both documents exist
- ✅ Source document metadata links to enriched document

---

## Metadata on Source Document

### What Gets Updated

**After Processing:**
- `SMEPilot_Enriched` = `true`
- `SMEPilot_Status` = `"Completed"`
- `SMEPilot_EnrichedFileUrl` = Link to enriched document
- `SMEPilot_EnrichedJobId` = Processing job ID

**Purpose:**
- Prevents reprocessing (idempotency check)
- Links source to enriched document
- Tracks processing status

---

## If You Want to Delete Source Documents

### Option 1: Manual Deletion (Current)

**How:**
- Users manually delete source documents if needed
- After verifying enriched document is correct
- No automatic deletion

**Pros:**
- ✅ Safe (no accidental deletion)
- ✅ User control
- ✅ Can keep for audit trail

**Cons:**
- ⚠️ Manual effort required
- ⚠️ Source folder may accumulate files

---

### Option 2: Automatic Deletion (Not Implemented)

**If You Want This:**
- Would need to add deletion code
- Delete source document after successful enrichment
- Add configuration option: `DeleteSourceAfterProcessing`

**Implementation:**
```csharp
// After successful enrichment
if (_cfg.DeleteSourceAfterProcessing)
{
    await _graph.DeleteFileAsync(driveId, itemId);
    _logger.LogInformation("🗑️ [DELETE] Source document deleted: {FileName}", fileName);
}
```

**Pros:**
- ✅ Keeps source folder clean
- ✅ Automatic cleanup

**Cons:**
- ⚠️ No audit trail
- ⚠️ Can't reprocess easily
- ⚠️ Risk of data loss if enrichment fails
- ⚠️ Can't compare original vs enriched

---

### Option 3: Move to Archive Folder (Not Implemented)

**If You Want This:**
- Move source document to archive folder
- Keep for audit trail
- Source folder stays clean

**Implementation:**
```csharp
// After successful enrichment
if (_cfg.ArchiveSourceAfterProcessing)
{
    await _graph.MoveFileAsync(
        driveId, 
        itemId, 
        _cfg.ArchiveFolderPath);
    _logger.LogInformation("📦 [ARCHIVE] Source document moved to archive: {FileName}", fileName);
}
```

**Pros:**
- ✅ Keeps source folder clean
- ✅ Preserves audit trail
- ✅ Can still access original

**Cons:**
- ⚠️ Requires archive folder configuration
- ⚠️ More complex

---

## Recommendation

### ✅ Keep Current Behavior (Source Document Remains)

**Why:**
1. ✅ **Safe** - No risk of data loss
2. ✅ **Audit Trail** - Original preserved
3. ✅ **Reprocessing** - Can reprocess if needed
4. ✅ **User Control** - Users can delete manually if needed
5. ✅ **Error Recovery** - Original safe if enrichment fails

**If Source Folder Gets Cluttered:**
- Users can manually delete after verification
- Or implement archive folder option
- Or add automatic deletion (with caution)

---

## Summary

### Answer to Your Question

**"After processing, is the document from source go away or still remains?"**

**Answer:** ✅ **The source document REMAINS in the source folder** - it is NOT deleted.

**What Happens:**
1. ✅ Source document stays in source folder
2. ✅ Enriched document saved to destination folder
3. ✅ Source document metadata updated with link to enriched document
4. ✅ Both documents exist (source + enriched)

**Why:**
- ✅ Safe (no data loss)
- ✅ Audit trail preserved
- ✅ Can reprocess if needed
- ✅ User can manually delete if needed

**If You Want Automatic Deletion:**
- ⚠️ Not currently implemented
- ⚠️ Would need to add deletion code
- ⚠️ Consider risks (data loss, no audit trail)

---

**Current behavior: Source documents remain in source folder after processing. This is the safe, recommended approach.**

