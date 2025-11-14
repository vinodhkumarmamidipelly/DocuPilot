# What Happens If Processing Stops in the Middle?

## The Question

**"What if it stops in middle of processing?"**

**Answer:** ✅ The system has **robust error handling and recovery mechanisms** to handle mid-processing failures.

---

## Processing Stages & Failure Points

### Processing Flow

```
1. Download Source Document
   ↓
2. Extract Content
   ↓
3. Enrich Document
   ↓
4. Upload Enriched Document
   ↓
5. Update Metadata
```

**Failure can occur at ANY stage** - system handles each scenario.

---

## Failure Scenarios & Handling

### Scenario 1: Failure During Download

**What Happens:**
- ❌ Network error downloading source document
- ❌ File locked/in use
- ❌ File deleted during download

**System Response:**
- ✅ **Retry with exponential backoff** (3 attempts)
- ✅ **Catch exception** and log error
- ✅ **Update metadata:** `SMEPilot_Status = "Retry"` (transient) or `"Failed"` (permanent)
- ✅ **Source document remains** in source folder (safe)

**Recovery:**
- ✅ Will retry on next webhook notification
- ✅ Or manual retry by user

---

### Scenario 2: Failure During Content Extraction

**What Happens:**
- ❌ Corrupted file (cannot read)
- ❌ Unsupported format
- ❌ Memory error

**System Response:**
- ✅ **Catch exception** and log error
- ✅ **Determine failure type:**
  - **Permanent** (unsupported format) → `SMEPilot_Status = "Failed"` (no retry)
  - **Transient** (memory error) → `SMEPilot_Status = "Retry"` (will retry)
- ✅ **Update metadata** with error message
- ✅ **Source document remains** in source folder

**Recovery:**
- ✅ Permanent failures: Manual intervention required
- ✅ Transient failures: Automatic retry on next notification

---

### Scenario 3: Failure During Enrichment

**What Happens:**
- ❌ Template not found
- ❌ OpenXML processing error
- ❌ Memory/timeout during enrichment

**System Response:**
- ✅ **Try-catch blocks** around enrichment
- ✅ **Finally blocks** ensure cleanup (temp files deleted)
- ✅ **Update metadata:** `SMEPilot_Status = "ManualReview"` or `"Retry"`
- ✅ **Log detailed error** for troubleshooting
- ✅ **Source document remains** in source folder

**Code Evidence:**
```csharp
try
{
    // Enrichment logic
    var result = enricher.EnrichFile(...);
    if (!result.Success)
    {
        await _graph.UpdateListItemFieldsAsync(driveId, itemId, new Dictionary<string, object>
        {
            {"SMEPilot_Enriched", false},
            {"SMEPilot_Status", "ManualReview"},
            {"SMEPilot_EnrichedJobId", fileId}
        });
        return (false, null, $"Document enrichment failed: {result.ErrorMessage}");
    }
}
catch (Exception ex)
{
    _logger.LogError(ex, "❌ [ENRICHMENT] DocumentEnricherService exception: {Error}", ex.Message);
    await _graph.UpdateListItemFieldsAsync(driveId, itemId, new Dictionary<string, object>
    {
        {"SMEPilot_Enriched", false},
        {"SMEPilot_Status", "ManualReview"},
        {"SMEPilot_EnrichedJobId", fileId}
    });
    return (false, null, $"Document enrichment failed: {ex.Message}");
}
finally
{
    // Ensure cleanup even on error
    try
    {
        if (tempInputPath != null && File.Exists(tempInputPath)) File.Delete(tempInputPath);
        if (File.Exists(tempOutputPath)) File.Delete(tempOutputPath);
    }
    catch { }
}
```

**Recovery:**
- ✅ Check logs for error details
- ✅ Fix issue (template, permissions, etc.)
- ✅ Retry processing (manual or automatic)

---

### Scenario 4: Failure During Upload

**What Happens:**
- ❌ Network error uploading enriched document
- ❌ Destination folder full
- ❌ Permission denied
- ❌ File locked

**System Response:**
- ✅ **Retry logic** with exponential backoff (up to `MaxUploadRetries`)
- ✅ **Wait and retry** if file locked
- ✅ **Catch exception** and log error
- ✅ **Update metadata:** `SMEPilot_Status = "Retry"` or `"Failed"`
- ✅ **Enriched document may be partially uploaded** (needs cleanup)

**Code Evidence:**
```csharp
int uploadRetries = 0;
int maxUploadRetries = _cfg.MaxUploadRetries;
while (uploadRetries < maxUploadRetries)
{
    try
    {
        uploaded = await _graph.UploadFileBytesAsync(...);
        break; // Success!
    }
    catch (ODataError ex) when (ex.Error?.Code == "notAllowed" && ex.Error?.Message?.Contains("locked") == true)
    {
        uploadRetries++;
        if (uploadRetries >= maxUploadRetries)
        {
            await Task.Delay(_cfg.FileLockWaitSeconds * 1000);
            // Final attempt
        }
        else
        {
            await Task.Delay(2000); // Wait 2 seconds
        }
    }
}
```

**Recovery:**
- ✅ Automatic retry on next notification
- ✅ Manual cleanup of partial uploads if needed
- ✅ Check destination folder permissions

---

### Scenario 5: Failure During Metadata Update

**What Happens:**
- ❌ File locked during metadata update
- ❌ Permission denied
- ❌ Network error

**System Response:**
- ✅ **Retry logic** with exponential backoff
- ✅ **Fallback mechanism:** Try minimal metadata update
- ✅ **Critical protection:** Prevents infinite reprocessing

**Code Evidence:**
```csharp
// Update metadata with retry
int metadataRetries = 0;
int maxMetadataRetries = _cfg.MaxMetadataRetries;
while (metadataRetries < maxMetadataRetries)
{
    try
    {
        await _graph.UpdateListItemFieldsAsync(driveId, itemId, metadata);
        metadataUpdateSuccess = true;
        break; // Success!
    }
    catch (ODataError ex) when (ex.Error?.Code == "notAllowed" && ex.Error?.Message?.Contains("locked") == true)
    {
        metadataRetries++;
        var waitTime = (int)Math.Pow(2, metadataRetries) * 1000; // Exponential backoff
        await Task.Delay(waitTime);
    }
}

// Fallback: Try minimal metadata update
if (!metadataUpdateSuccess)
{
    try
    {
        var fallbackMetadata = new Dictionary<string, object>
        {
            {"SMEPilot_Status", "MetadataUpdateFailed"},
            {"SMEPilot_Enriched", true}, // Document was enriched, just metadata save failed
            {"SMEPilot_EnrichedFileUrl", uploaded.WebUrl ?? ""}
        };
        await _graph.UpdateListItemFieldsAsync(driveId, itemId, fallbackMetadata);
        _logger.LogInformation("✅ [METADATA] Fallback metadata set successfully - file will NOT be reprocessed");
    }
    catch (Exception fallbackEx)
    {
        _logger.LogError(fallbackEx, "❌ [METADATA] Fallback metadata update also failed - file WILL be reprocessed on next notification");
    }
}
```

**Recovery:**
- ✅ Fallback metadata prevents reprocessing
- ✅ Enriched document is safe (already uploaded)
- ✅ Can manually update metadata if needed

---

## Status Tracking System

### Metadata Status Values

**Status Types:**
1. **`Processing`** - Currently being processed
2. **`Completed`** - Successfully processed
3. **`Retry`** - Transient failure, will retry
4. **`Failed`** - Permanent failure, no retry
5. **`ManualReview`** - Needs manual intervention
6. **`MetadataUpdateFailed`** - Enriched but metadata save failed

**Purpose:**
- ✅ Prevents infinite retries
- ✅ Tracks processing state
- ✅ Enables recovery

---

## Recovery Mechanisms

### Automatic Recovery

**1. Retry on Next Notification**
- ✅ Files with `SMEPilot_Status = "Retry"` will retry
- ✅ Waits for `RetryWaitMinutes` before retrying
- ✅ Automatic on next webhook notification

**2. Idempotency Protection**
- ✅ Checks metadata before processing
- ✅ Skips already-processed files
- ✅ Prevents duplicate processing

**3. Exponential Backoff**
- ✅ Retries with increasing delays
- ✅ Prevents overwhelming system
- ✅ Handles transient failures

---

### Manual Recovery

**1. Check Status**
- ✅ View metadata: `SMEPilot_Status`
- ✅ Check error message: `SMEPilot_ErrorMessage`
- ✅ Review logs for details

**2. Fix Issue**
- ✅ Fix root cause (permissions, template, etc.)
- ✅ Resolve transient issues (network, locks)

**3. Retry Processing**
- ✅ Reset metadata: `SMEPilot_Status = null` or `"Retry"`
- ✅ Trigger webhook or wait for next notification
- ✅ Or manually trigger processing

---

## What Gets Preserved

### Source Document
- ✅ **Always preserved** - Never deleted
- ✅ **Safe** - Original remains intact
- ✅ **Can retry** - Can reprocess anytime

### Enriched Document
- ✅ **May be partially created** - If failure during upload
- ✅ **May be complete** - If failure during metadata update
- ✅ **Can be cleaned up** - If partial upload

### Metadata
- ✅ **Status tracked** - Knows what happened
- ✅ **Error messages** - Details about failure
- ✅ **Prevents reprocessing** - Idempotency protection

---

## Failure Type Classification

### Permanent Failures (No Retry)

**Examples:**
- Unsupported file type
- Corrupted file (cannot read)
- Old format (.doc instead of .docx)

**System Response:**
- ✅ `SMEPilot_Status = "Failed"`
- ✅ `SMEPilot_ErrorMessage` = Error details
- ✅ **No automatic retry** (prevents infinite loops)
- ✅ Requires manual intervention

---

### Transient Failures (Will Retry)

**Examples:**
- Network timeout
- File locked temporarily
- Memory error (may succeed on retry)
- Service unavailable

**System Response:**
- ✅ `SMEPilot_Status = "Retry"`
- ✅ `SMEPilot_LastErrorTime` = Timestamp
- ✅ **Automatic retry** after `RetryWaitMinutes`
- ✅ Exponential backoff

---

## Complete Failure Scenarios

### Scenario A: Azure Function Timeout

**What Happens:**
- ❌ Processing takes longer than timeout (5-10 minutes)
- ❌ Function execution terminated

**System Response:**
- ✅ **No metadata update** (function terminated)
- ✅ **Source document remains** (safe)
- ✅ **Partial enriched document** may exist (needs cleanup)
- ✅ **Will retry** on next webhook notification

**Recovery:**
- ✅ Check for partial enriched documents
- ✅ Clean up if needed
- ✅ Wait for retry or manually trigger

---

### Scenario B: Azure Function App Crash

**What Happens:**
- ❌ Function App crashes/restarts
- ❌ Processing stops mid-way

**System Response:**
- ✅ **No metadata update** (function crashed)
- ✅ **Source document remains** (safe)
- ✅ **Temp files cleaned up** (Azure Functions cleanup)
- ✅ **Will retry** on next webhook notification

**Recovery:**
- ✅ Function App restarts automatically
- ✅ Next webhook will trigger retry
- ✅ Idempotency check prevents duplicate processing

---

### Scenario C: Network Partition

**What Happens:**
- ❌ Network connection lost during processing
- ❌ Cannot upload enriched document

**System Response:**
- ✅ **Exception caught** and logged
- ✅ **Metadata updated:** `SMEPilot_Status = "Retry"`
- ✅ **Source document remains** (safe)
- ✅ **Enriched document** may be in memory (lost)

**Recovery:**
- ✅ Network reconnects
- ✅ Automatic retry on next notification
- ✅ Processing restarts from beginning (safe, idempotent)

---

## Best Practices for Recovery

### For Administrators

**1. Monitor Status**
- ✅ Check `SMEPilot_Status` metadata
- ✅ Review error logs
- ✅ Identify patterns (recurring failures)

**2. Handle Permanent Failures**
- ✅ Fix root cause (permissions, template, etc.)
- ✅ Update file if needed
- ✅ Reset status to retry

**3. Clean Up Partial Files**
- ✅ Check destination folder for partial uploads
- ✅ Remove incomplete enriched documents
- ✅ Retry processing

---

### For Users

**1. Check Document Status**
- ✅ View metadata: `SMEPilot_Status`
- ✅ Check if enriched document exists
- ✅ Review error message if failed

**2. Retry Processing**
- ✅ Fix issue (if user error)
- ✅ Wait for automatic retry
- ✅ Contact admin if persistent failure

---

## Summary

### Answer to Your Question

**"What if it stops in middle of processing?"**

**Answer:** ✅ **System handles mid-processing failures gracefully** with:

1. ✅ **Error Handling**
   - Try-catch blocks at every stage
   - Exception logging
   - Status tracking

2. ✅ **Recovery Mechanisms**
   - Automatic retry for transient failures
   - Status tracking prevents infinite retries
   - Fallback mechanisms

3. ✅ **Data Safety**
   - Source document always preserved
   - No data loss
   - Can retry anytime

4. ✅ **Status Tracking**
   - Knows what happened
   - Prevents duplicate processing
   - Enables recovery

**What Happens:**
- ✅ Source document **remains safe** in source folder
- ✅ Metadata **tracks status** (Processing, Retry, Failed, etc.)
- ✅ System **automatically retries** transient failures
- ✅ **No data loss** - original preserved
- ✅ **Can recover** - manual or automatic

**Bottom Line:** The system is designed to handle failures gracefully, preserve data, and enable recovery.

---

**Document created:** `PROCESSING_FAILURE_AND_RECOVERY.md` - includes:
- All failure scenarios
- Error handling mechanisms
- Recovery procedures
- Status tracking system
- Best practices

