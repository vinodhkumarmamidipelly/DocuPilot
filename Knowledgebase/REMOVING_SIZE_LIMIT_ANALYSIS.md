# Analysis: Removing File Size Limit

## Short Answer: **Yes, but with significant risks and limitations**

Removing the size limit check **will work** for files up to **~100MB**, but you'll encounter several issues beyond that point.

---

## What Would Work

### ✅ Files Up to ~100MB
- **Azure Functions HTTP Limit:** Hard limit of **100MB** for HTTP request body
- **Current Code:** The code itself doesn't have hard-coded size limits beyond the check
- **Processing:** OpenXML can handle files up to this size, though performance degrades

### ✅ Technical Feasibility
The code is structured to handle variable file sizes:
- Uses `Stream` operations (not fixed-size buffers)
- Processes files incrementally where possible
- Uses temporary files for intermediate processing

---

## What Would Break

### ❌ Hard Platform Limits (Cannot Bypass)

#### 1. Azure Functions HTTP Request Body Limit: **100MB**
```
❌ Files > 100MB: Will FAIL at webhook reception
   Error: "Request body too large"
   Status: 413 Payload Too Large
```
**This is a hard limit** - cannot be changed or bypassed.

#### 2. Azure Functions Memory Limits
- **Consumption Plan:** Up to **1.5GB** per instance
- **Risk:** Files >50MB can easily consume 200-300MB+ during processing
- **Multiple Copies:** Code creates multiple in-memory copies:
  ```csharp
  // Copy 1: Download from SharePoint
  using var fileStream = await _graph.DownloadFileStreamAsync(...);
  
  // Copy 2: Save to temp file
  await fileStream.CopyToAsync(tempFileStream);
  
  // Copy 3: Load into MemoryStream for extraction
  using var ms = new MemoryStream();
  await docxStream.CopyToAsync(ms);
  
  // Copy 4: Extract images (each image in memory)
  images.Add(ims.ToArray());
  
  // Copy 5: Read enriched file back into memory
  enrichedBytes = await File.ReadAllBytesAsync(tempOutputPath);
  
  // Copy 6: Upload to SharePoint (in memory)
  await _graph.UploadFileAsync(..., enrichedBytes);
  ```

**Example Memory Usage for 50MB file:**
- Original file: 50MB
- Temp file copy: 50MB
- MemoryStream for extraction: 50MB
- Extracted images: 50-100MB (if many images)
- Enriched document: 50-60MB
- Upload buffer: 50-60MB
- **Total Peak Memory: 300-370MB+**

**For 100MB file:** Could easily exceed **600-700MB+** peak memory usage.

#### 3. Execution Timeout
- **Consumption Plan:** Default **5 minutes** (max 10 minutes)
- **Large Files:** Processing time increases non-linearly:
  - 50MB file: ~60-120 seconds
  - 100MB file: ~180-300+ seconds
  - **Risk:** Timeout before completion

---

## Code Analysis: Memory Usage Patterns

### Current Memory-Intensive Operations

#### 1. SimpleExtractor.cs (Line 31-33)
```csharp
using var ms = new MemoryStream();
await docxStream.CopyToAsync(ms);  // Full file in memory
doc = WordprocessingDocument.Open(ms, false);
```
**Issue:** Entire file loaded into memory for extraction.

#### 2. ProcessSharePointFile.cs (Line 811-813)
```csharp
using (var tempFileStream = File.Create(tempInputPath))
{
    await fileStream.CopyToAsync(tempFileStream);  // Copy to disk
}
```
**Good:** Uses temp file, but then reads back into memory.

#### 3. ProcessSharePointFile.cs (Line 927, 1070)
```csharp
enrichedBytes = await File.ReadAllBytesAsync(tempOutputPath);  // Full file in memory
```
**Issue:** Entire enriched file loaded into memory for upload.

#### 4. Image Extraction (SimpleExtractor.cs Line 52-54)
```csharp
using var ims = new MemoryStream();
await imgStream.CopyToAsync(ims);
images.Add(ims.ToArray());  // All images in memory simultaneously
```
**Issue:** All images kept in memory as byte arrays.

---

## Realistic Scenarios

### Scenario 1: Remove Check, Set Limit to 100MB
**What Happens:**
- ✅ Files <100MB: **Will work** (with performance degradation)
- ❌ Files >100MB: **Will fail** at webhook reception (hard limit)
- ⚠️ Files 80-100MB: **High risk** of timeout or memory issues

**Recommendation:** Not recommended without code changes.

### Scenario 2: Remove Check Completely (No Limit)
**What Happens:**
- ✅ Files <100MB: **Will work** (with performance issues)
- ❌ Files >100MB: **Will fail** at webhook (hard limit)
- ⚠️ Files 50-100MB: **High risk** of:
  - OutOfMemoryException
  - Timeout errors
  - Failed processing

**Recommendation:** **Not recommended** - will cause production issues.

### Scenario 3: Increase Limit to 100MB + Code Optimizations
**What Happens:**
- ✅ Files <100MB: **Will work reliably**
- ⚠️ Files 80-100MB: **Moderate risk** of timeout
- ❌ Files >100MB: **Will fail** (hard limit)

**Recommendation:** **Possible** with code changes (see below).

---

## Required Code Changes for Larger Files

### 1. Stream-Based Processing (Reduce Memory)
```csharp
// Instead of loading entire file into memory:
enrichedBytes = await File.ReadAllBytesAsync(tempOutputPath);

// Use streaming upload:
using var uploadStream = File.OpenRead(tempOutputPath);
await _graph.UploadFileStreamAsync(..., uploadStream);
```

### 2. Incremental Image Processing
```csharp
// Instead of storing all images in memory:
List<byte[]> images = new List<byte[]>();

// Process images one at a time:
foreach (var imgPart in imageParts)
{
    // Process and insert immediately, don't store
    await ProcessAndInsertImageAsync(imgPart);
}
```

### 3. Increase Timeout Configuration
```json
// host.json
{
  "functionTimeout": "00:10:00"  // 10 minutes (max for Consumption)
}
```

### 4. Use Premium Plan (If Needed)
- **Premium Plan:** Up to 14GB memory, 30-minute timeout
- **Cost:** Higher than Consumption Plan
- **Benefit:** Can handle much larger files

---

## Recommendations

### Option 1: Keep Current Limit (Recommended)
- ✅ **50MB limit** is reasonable for most use cases
- ✅ Prevents production issues
- ✅ Good balance of capability vs. reliability
- ✅ Most business documents are <10MB

### Option 2: Increase to 100MB with Monitoring
- ⚠️ Set limit to **100MB** (near platform max)
- ⚠️ Add memory monitoring and alerts
- ⚠️ Implement timeout handling
- ⚠️ Monitor failure rates

### Option 3: Implement Async Processing for Large Files
- ✅ Remove synchronous size limit
- ✅ Queue files >50MB for async processing
- ✅ Use Durable Functions or Service Bus
- ✅ Process during off-peak hours
- ✅ **Best solution** for handling large files

---

## Testing Recommendations

If you decide to remove/increase the limit:

### 1. Test with Various File Sizes
- 10MB (baseline)
- 30MB (moderate)
- 50MB (current limit)
- 75MB (high risk)
- 100MB (platform limit)

### 2. Monitor Key Metrics
- Memory usage (Application Insights)
- Execution time
- Failure rate
- Timeout occurrences
- OutOfMemoryException frequency

### 3. Set Up Alerts
- Alert if memory >1GB
- Alert if execution time >8 minutes
- Alert if failure rate >5%

---

## Conclusion

**Can you remove the size limit?**
- **Technically:** Yes, up to 100MB (hard platform limit)
- **Practically:** **Not recommended** without code optimizations
- **Safely:** Only if you implement async processing for large files

**Best Approach:**
1. **Keep 50MB limit** for synchronous processing
2. **Implement async queue** for files >50MB
3. **Monitor and adjust** based on actual usage patterns

**Risk Level:**
- Remove limit without changes: **HIGH RISK** ⚠️
- Remove limit with optimizations: **MODERATE RISK** ⚠️
- Keep limit + async processing: **LOW RISK** ✅

