# Webhook Duplicate Trigger Analysis

## 🔍 **Problem Identified**

### **Issue: Multiple Webhook Notifications for Single File Upload**

**From Logs:**
- Single file upload (`Alerts - Copy.docx`) triggered **8+ webhook notifications**
- All notifications arrive within seconds of each other
- Each notification tries to process the same file
- Concurrency control works (skips duplicates), but still causes:
  - Unnecessary Graph API calls
  - File lock errors
  - Metadata check failures

### **Root Causes:**

1. **Graph API Behavior:**
   - SharePoint triggers multiple "updated" events for a single file upload:
     - File creation event
     - Metadata update event
     - Indexing event
     - Permission update event
   - This is **normal Graph API behavior** - not a bug

2. **Metadata Check Timing:**
   - Multiple notifications arrive **before** metadata is updated
   - All see "has no metadata" and try to process
   - First one starts processing, others see "already processing" and skip
   - But metadata update fails due to file locks

3. **File Lock Issues:**
   - SharePoint locks files during:
     - Upload
     - Indexing
     - Metadata updates
   - Multiple concurrent upload attempts fail with "file locked"

---

## ✅ **Current Protections (Working)**

1. **Concurrency Control (Semaphore):**
   - ✅ Prevents concurrent processing of same file
   - ✅ Logs show: "already being processed, skipping"

2. **Idempotency Check:**
   - ✅ Checks `SMEPilot_Enriched` metadata
   - ✅ Checks `SMEPilot_Status` metadata
   - ⚠️ **Issue:** Metadata check happens AFTER lock acquisition, but multiple notifications arrive before any metadata is set

3. **Retry Logic:**
   - ✅ Retries for file locks (exponential backoff)
   - ⚠️ **Issue:** Still fails after 3 retries

---

## 🔧 **Solutions Needed**

### **1. Notification Deduplication**
- Track processed notifications by subscription ID + resource + timestamp
- Ignore duplicate notifications within 30 seconds
- Use in-memory cache with TTL

### **2. Improve Metadata Check**
- Check metadata BEFORE acquiring lock
- Add retry with delay for metadata reads
- Check for "Processing" status more aggressively

### **3. Better File Lock Handling**
- Increase retry count
- Longer wait times
- Check file lock status before retry

### **4. Early Deduplication**
- Deduplicate notifications at entry point
- Before any processing starts
- Use notification signature (subscriptionId + resource + changeType)

---

## 📊 **Impact**

**Current:**
- 8+ notifications → 8+ function executions
- 1 successful processing
- 7 skipped (concurrency control)
- Multiple file lock errors
- Metadata update failures

**After Fix:**
- 8+ notifications → 1 function execution
- 0 skipped (deduplicated early)
- No file lock errors
- Successful metadata update

---

## 🎯 **Priority**

**High Priority:** Notification deduplication (reduces unnecessary processing)
**Medium Priority:** Better metadata check (improves reliability)
**Low Priority:** File lock handling (already has retry logic)

