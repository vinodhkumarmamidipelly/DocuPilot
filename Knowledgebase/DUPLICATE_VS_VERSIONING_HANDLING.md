# Duplicate Document Processing vs Versioning

## The Question

**"How do you want to handle duplicate doc processing v/s versioning?"**

**Analysis:** This question addresses two scenarios:
1. **Duplicate Documents** - Same document uploaded multiple times
2. **Document Versioning** - Same document updated (new version)

---

## Current Implementation

### How It Currently Works

**1. Duplicate Detection:**
- ✅ Checks `SMEPilot_Enriched` metadata flag
- ✅ If `true`, **skips processing** (idempotency)
- ✅ Prevents duplicate processing

**2. Version Handling:**
- ⚠️ **Webhooks trigger on "updated" events**
- ⚠️ **But skips if already enriched** (same as duplicates)
- ⚠️ **No version detection** - treats updates as duplicates

**Code Evidence:**
```csharp
// Check if already enriched
if (existingMetadata.ContainsKey("SMEPilot_Enriched"))
{
    var enrichedValue = existingMetadata["SMEPilot_Enriched"]?.ToString();
    var isEnriched = enrichedValue == "True" || enrichedValue == "true" || enrichedValue == "1";
    if (isEnriched)
    {
        _logger.LogInformation("⏭️ [IDEMPOTENCY] File {FileName} already processed, skipping", fileName);
        shouldSkip = true; // SKIPS - even if document was updated!
    }
}
```

**Current Behavior:**
- ✅ **Duplicates:** Skipped (correct)
- ⚠️ **Versions:** Also skipped (may not be desired)

---

## The Problem: Versioning Not Handled

### Scenario: Document Updated

**What Happens:**
1. User uploads `Document_v1.docx` → Processed → Enriched
2. User updates `Document_v1.docx` (new content) → Webhook triggered
3. System checks: `SMEPilot_Enriched = true` → **SKIPS processing**
4. **Result:** Enriched document is **outdated** (doesn't reflect new content)

**Issue:**
- ❌ Updated documents are **not reprocessed**
- ❌ Enriched documents become **stale**
- ❌ Copilot searches **outdated content**

---

## Options for Handling

### Option 1: Current Behavior (Skip Both Duplicates and Versions)

**How It Works:**
- ✅ Duplicates: Skipped (correct)
- ⚠️ Versions: Skipped (may not be desired)

**Pros:**
- ✅ Simple implementation
- ✅ Prevents unnecessary processing
- ✅ Fast (no reprocessing)

**Cons:**
- ❌ Updated documents not reprocessed
- ❌ Enriched documents become stale
- ❌ Copilot may return outdated information

**Use Case:** When documents are **never updated** after initial upload

---

### Option 2: Detect Versions and Reprocess

**How It Works:**
- ✅ Duplicates: Skipped (same content, same timestamp)
- ✅ Versions: Reprocessed (different content or timestamp)

**Implementation:**
```csharp
// Check if document was updated since last enrichment
var lastModified = item.LastModifiedDateTime;
var lastEnriched = existingMetadata.ContainsKey("SMEPilot_LastEnrichedTime") 
    ? DateTime.Parse(existingMetadata["SMEPilot_LastEnrichedTime"].ToString())
    : DateTime.MinValue;

if (lastModified > lastEnriched)
{
    // Document was updated - reprocess
    _logger.LogInformation("🔄 [VERSION] Document {FileName} was updated, will reprocess", fileName);
    shouldSkip = false;
}
else
{
    // Same document, not updated - skip
    _logger.LogInformation("⏭️ [DUPLICATE] Document {FileName} unchanged, skipping", fileName);
    shouldSkip = true;
}
```

**Pros:**
- ✅ Handles versioning correctly
- ✅ Enriched documents stay up-to-date
- ✅ Copilot searches current content

**Cons:**
- ⚠️ More complex implementation
- ⚠️ Reprocesses on every update
- ⚠️ May process unnecessary updates (metadata changes)

**Use Case:** When documents are **frequently updated** and need to stay current

---

### Option 3: Content Hash Comparison

**How It Works:**
- ✅ Duplicates: Skipped (same content hash)
- ✅ Versions: Reprocessed (different content hash)

**Implementation:**
```csharp
// Calculate content hash
var contentHash = CalculateFileHash(fileStream);

// Check if content changed
var lastHash = existingMetadata.ContainsKey("SMEPilot_ContentHash")?.ToString();
if (contentHash == lastHash)
{
    // Same content - skip (duplicate)
    _logger.LogInformation("⏭️ [DUPLICATE] Document {FileName} content unchanged, skipping", fileName);
    shouldSkip = true;
}
else
{
    // Content changed - reprocess (version)
    _logger.LogInformation("🔄 [VERSION] Document {FileName} content changed, will reprocess", fileName);
    shouldSkip = false;
}
```

**Pros:**
- ✅ Most accurate (detects actual content changes)
- ✅ Ignores metadata-only updates
- ✅ Handles versioning correctly

**Cons:**
- ⚠️ Requires hash calculation (processing overhead)
- ⚠️ More complex implementation
- ⚠️ Need to store hash in metadata

**Use Case:** When you want to **only reprocess if content actually changed**

---

### Option 4: Configurable Behavior

**How It Works:**
- ✅ Configuration option: `ReprocessOnUpdate`
- ✅ User chooses: Skip versions or reprocess versions

**Implementation:**
```csharp
// Configuration
public bool ReprocessOnUpdate { get; set; } = false; // Default: skip versions

// Check based on configuration
if (existingMetadata.ContainsKey("SMEPilot_Enriched"))
{
    var isEnriched = existingMetadata["SMEPilot_Enriched"] == true;
    if (isEnriched)
    {
        if (_cfg.ReprocessOnUpdate)
        {
            // Check if document was updated
            var lastModified = item.LastModifiedDateTime;
            var lastEnriched = GetLastEnrichedTime(existingMetadata);
            
            if (lastModified > lastEnriched)
            {
                // Version - reprocess
                shouldSkip = false;
            }
            else
            {
                // Duplicate - skip
                shouldSkip = true;
            }
        }
        else
        {
            // Skip both duplicates and versions
            shouldSkip = true;
        }
    }
}
```

**Pros:**
- ✅ Flexible (user chooses behavior)
- ✅ Can change without code changes
- ✅ Supports both use cases

**Cons:**
- ⚠️ More complex implementation
- ⚠️ Need to explain options to users

**Use Case:** When you want **flexibility** to choose behavior

---

## Recommended Solution

### ✅ Option 2: Detect Versions and Reprocess (Recommended)

**Why:**
1. ✅ **Handles versioning correctly** - Updated documents are reprocessed
2. ✅ **Keeps enriched documents current** - Copilot searches latest content
3. ✅ **Still prevents duplicates** - Same document (not updated) is skipped
4. ✅ **Balanced approach** - Not too complex, handles both scenarios

**Implementation:**
- Compare `LastModifiedDateTime` with `SMEPilot_LastEnrichedTime`
- If document was modified after enrichment → Reprocess (version)
- If document not modified → Skip (duplicate)

---

## Implementation Details

### Enhanced Idempotency Check

```csharp
// Check if already enriched
if (existingMetadata.ContainsKey("SMEPilot_Enriched"))
{
    var enrichedValue = existingMetadata["SMEPilot_Enriched"]?.ToString();
    var isEnriched = enrichedValue == "True" || enrichedValue == "true" || enrichedValue == "1";
    
    if (isEnriched)
    {
        // Check if document was updated since last enrichment
        var item = await _graph.GetDriveItemAsync(driveId, itemId);
        var lastModified = item.LastModifiedDateTime ?? DateTime.MinValue;
        
        var lastEnrichedTime = existingMetadata.ContainsKey("SMEPilot_LastEnrichedTime")
            ? DateTime.Parse(existingMetadata["SMEPilot_LastEnrichedTime"].ToString())
            : DateTime.MinValue;
        
        if (lastModified > lastEnrichedTime)
        {
            // Document was updated - reprocess (version)
            _logger.LogInformation("🔄 [VERSION] Document {FileName} was updated after enrichment (Modified: {Modified}, Last Enriched: {LastEnriched}), will reprocess", 
                fileName, lastModified, lastEnrichedTime);
            shouldSkip = false; // REPROCESS
        }
        else
        {
            // Document not updated - skip (duplicate)
            _logger.LogInformation("⏭️ [DUPLICATE] Document {FileName} unchanged since enrichment (Modified: {Modified}, Last Enriched: {LastEnriched}), skipping", 
                fileName, lastModified, lastEnrichedTime);
            shouldSkip = true; // SKIP
        }
    }
}
```

### Update Metadata After Processing

```csharp
// After successful enrichment
var metadata = new Dictionary<string, object>
{
    {"SMEPilot_Enriched", true},
    {"SMEPilot_Status", "Completed"},
    {"SMEPilot_EnrichedFileUrl", uploaded.WebUrl},
    {"SMEPilot_LastEnrichedTime", DateTime.UtcNow.ToString("O")}, // NEW: Track enrichment time
    {"SMEPilot_EnrichedJobId", fileId}
};
```

---

## Comparison Matrix

| Scenario | Current Behavior | Option 2 (Recommended) | Option 3 (Hash) | Option 4 (Configurable) |
|----------|------------------|------------------------|-----------------|-------------------------|
| **Same file uploaded twice** | ✅ Skip | ✅ Skip | ✅ Skip | ✅ Skip |
| **Document updated (version)** | ❌ Skip (wrong) | ✅ Reprocess | ✅ Reprocess | ⚠️ Configurable |
| **Metadata-only update** | ✅ Skip | ⚠️ Reprocess | ✅ Skip | ⚠️ Configurable |
| **Complexity** | ✅ Simple | ⚠️ Medium | ❌ Complex | ❌ Most Complex |
| **Accuracy** | ⚠️ Medium | ✅ Good | ✅✅ Best | ⚠️ Depends |

---

## Edge Cases

### Edge Case 1: Metadata-Only Updates

**Scenario:** Document metadata updated (author, tags) but content unchanged

**Option 2 Behavior:**
- ⚠️ Will reprocess (because `LastModified` changed)
- ⚠️ Unnecessary processing

**Option 3 Behavior:**
- ✅ Will skip (content hash unchanged)
- ✅ More efficient

**Recommendation:** Option 3 (Hash) is better for this, but Option 2 is simpler and acceptable.

---

### Edge Case 2: Rapid Updates

**Scenario:** Document updated multiple times quickly

**Behavior:**
- ✅ Each update triggers reprocessing
- ✅ Enriched document stays current
- ⚠️ May process multiple times in short period

**Mitigation:**
- ✅ Idempotency check prevents concurrent processing
- ✅ Can add rate limiting if needed

---

### Edge Case 3: File Renamed

**Scenario:** Same file, different name

**Behavior:**
- ✅ Treated as new file (different `itemId`)
- ✅ Will be processed
- ✅ Creates new enriched document

**Consideration:**
- ⚠️ May create duplicate enriched documents
- ⚠️ Need content hash to detect true duplicates

---

## Recommendations

### ✅ Recommended: Option 2 (Detect Versions and Reprocess)

**Why:**
1. ✅ **Handles both duplicates and versions correctly**
2. ✅ **Keeps enriched documents current**
3. ✅ **Not too complex** - Simple timestamp comparison
4. ✅ **Good balance** - Efficiency vs accuracy

**Implementation:**
- Compare `LastModifiedDateTime` with `SMEPilot_LastEnrichedTime`
- Reprocess if document was updated
- Skip if document unchanged

---

### Alternative: Option 3 (Content Hash) - If Accuracy Critical

**When to Use:**
- If metadata-only updates are common
- If you want to avoid unnecessary processing
- If accuracy is more important than simplicity

**Trade-off:**
- More complex implementation
- Requires hash calculation
- Need to store hash in metadata

---

## Summary

### Answer to Your Question

**"How do you want to handle duplicate doc processing v/s versioning?"**

**Current Behavior:**
- ✅ **Duplicates:** Skipped (correct)
- ⚠️ **Versions:** Also skipped (may not be desired)

**Recommended Solution:**
- ✅ **Duplicates:** Skip (same document, not updated)
- ✅ **Versions:** Reprocess (document updated, new content)

**Implementation:**
- Compare `LastModifiedDateTime` with `SMEPilot_LastEnrichedTime`
- If updated → Reprocess (version)
- If unchanged → Skip (duplicate)

**Benefits:**
- ✅ Handles versioning correctly
- ✅ Keeps enriched documents current
- ✅ Copilot searches latest content
- ✅ Still prevents duplicate processing

---

**Document created:** `DUPLICATE_VS_VERSIONING_HANDLING.md` - includes:
- Current behavior analysis
- Options for handling duplicates vs versions
- Implementation details
- Recommendations
- Edge cases

**Bottom Line:** Current implementation skips both duplicates and versions. Recommended: Detect versions and reprocess to keep enriched documents current.

