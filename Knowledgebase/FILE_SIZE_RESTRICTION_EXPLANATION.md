# File Size Restriction Explanation (50MB)

## Why is there a 50MB restriction on files?

The 50MB file size restriction exists for several **technical and operational reasons**:

---

## 1. Azure Functions Platform Limitations

### HTTP Request Body Size Limit
- **Default Limit:** Azure Functions have a maximum HTTP request body size of **100MB** for HTTP-triggered functions
- **Our Restriction:** We set a **50MB limit** to stay well below the platform limit and provide buffer for:
  - Request headers
  - Webhook payload overhead
  - Processing margin

### Memory Constraints
- **Consumption Plan:** Azure Functions Consumption Plan provides limited memory (up to 1.5GB per instance)
- **Processing Memory:** Large files require significant memory for:
  - Downloading file content into memory
  - OpenXML document processing (DOM manipulation)
  - Image extraction and processing
  - Template application (creating new document structure)
  - Uploading enriched document back to SharePoint

**Example:** A 50MB DOCX file can easily consume 200-300MB+ of memory during processing due to:
- Document structure parsing
- Image extraction (images are often compressed in DOCX)
- Template merging operations
- Temporary file operations

### Execution Timeout
- **Consumption Plan:** Default timeout is **5 minutes** (configurable up to 10 minutes)
- **Processing Time:** Large files take longer to:
  - Download from SharePoint (network latency)
  - Process with OpenXML (CPU-intensive operations)
  - Upload back to SharePoint (network latency)

**Risk:** Files approaching 50MB may approach timeout limits, especially with:
- Slow network connections
- Complex document structures
- Many embedded images

---

## 2. SharePoint/Microsoft Graph API Considerations

### Download/Upload Performance
- **Graph API:** File operations via Microsoft Graph API have practical limits
- **Network Timeout:** Large file transfers are more susceptible to network interruptions
- **Retry Complexity:** Larger files = longer retry times if failures occur

### SharePoint Search Indexing
- **Search Index:** Very large documents (>50MB) may:
  - Take longer to index
  - Impact search performance
  - Reduce Copilot query response times

---

## 3. OpenXML Processing Constraints

### Document Processing Memory
- **OpenXML SDK:** Processes entire document in memory
- **DOM Operations:** Template merging, content mapping, and formatting require:
  - Full document structure in memory
  - Multiple passes through document elements
  - Temporary document construction

### Processing Complexity
- **Time Complexity:** Processing time increases **non-linearly** with file size:
  - Small files (<5MB): ~5-10 seconds
  - Medium files (10-20MB): ~15-30 seconds
  - Large files (40-50MB): ~60-120+ seconds

---

## 4. Cost and Resource Management

### Azure Functions Costs
- **Consumption Plan:** Billed per execution time and memory usage
- **Large Files:** Increase both execution time and memory consumption
- **Cost Control:** 50MB limit helps manage:
  - Function execution costs
  - Memory consumption costs
  - Storage costs (temporary files)

### Resource Fairness
- **Multi-tenant:** Prevents single large file from monopolizing Function App resources
- **Concurrent Processing:** Allows multiple smaller files to process simultaneously

---

## 5. Error Handling and Reliability

### Failure Recovery
- **Smaller Files:** Easier to retry on failure (faster re-processing)
- **Large Files:** Failed processing wastes significant time and resources
- **Idempotency:** Smaller files are easier to verify and re-process safely

### Monitoring and Debugging
- **Logging:** Large files generate more log data
- **Debugging:** Easier to troubleshoot issues with smaller files
- **Application Insights:** Reduced telemetry overhead

---

## Current Configuration

### Code Default vs Documentation
- **Code Default:** `Config.cs` sets default to **4MB** (`4 * 1024 * 1024 bytes`)
- **Documentation Default:** **50MB** (recommended maximum)
- **Configuration:** Can be overridden via `MaxFileSizeBytes` environment variable

### Recommended Settings
```json
{
  "MaxFileSizeBytes": "52428800"  // 50MB in bytes (50 * 1024 * 1024)
}
```

---

## What Happens When File Exceeds Limit?

### Current Behavior (from code)
```csharp
if (fileStream.Length > _cfg.MaxFileSizeBytes)
{
    _logger.LogWarning("File {FileName} is too large ({Size} bytes, max: {MaxSize} bytes), will process asynchronously", 
        fileName, fileStream.Length, _cfg.MaxFileSizeBytes);
    return (false, null, $"File too large for single-run processing (max: {_cfg.MaxFileSizeBytes / 1024 / 1024}MB)");
}
```

**Result:**
1. File is **rejected** (not processed)
2. Metadata updated: `SMEPilot_Status = "FileTooLarge"`
3. Warning logged with file size details
4. **Note:** Code comment says "will process asynchronously" but current implementation rejects the file

---

## Recommendations

### For Most Use Cases: **50MB is Appropriate**
- ✅ Handles 99% of typical business documents
- ✅ Provides good balance between capability and resource usage
- ✅ Stays well within Azure Functions platform limits
- ✅ Ensures reliable processing and timeout avoidance

### For Larger Files: **Consider Alternatives**
1. **Split Documents:** Break large documents into smaller sections
2. **Async Processing:** Implement Durable Functions for large file processing (future enhancement)
3. **Direct Upload:** Upload large files directly to SharePoint without enrichment
4. **Batch Processing:** Process large files during off-peak hours with higher timeout limits

### Configuration Flexibility
- **Adjustable:** The limit is configurable per environment
- **Per-Site:** Can be configured differently per SharePoint site if needed
- **Monitoring:** Track file size distribution to optimize the limit

---

## Summary

**The 50MB restriction exists because:**
1. ✅ **Platform Limits:** Azure Functions have practical memory and timeout constraints
2. ✅ **Processing Efficiency:** OpenXML processing is memory-intensive
3. ✅ **Cost Management:** Prevents excessive resource consumption
4. ✅ **Reliability:** Smaller files process more reliably and faster
5. ✅ **Error Recovery:** Easier to retry and debug smaller files

**This is a reasonable limit** that balances functionality with platform constraints and operational reliability.

---

## Questions to Consider

1. **What is the typical file size in your organization?**
   - If most files are <10MB, 50MB provides good headroom
   - If many files exceed 50MB, consider async processing enhancement

2. **Can large documents be split?**
   - Technical documentation can often be split by section
   - Each section can be enriched independently

3. **Is async processing needed?**
   - Current implementation rejects files > limit
   - Future enhancement could queue large files for async processing

