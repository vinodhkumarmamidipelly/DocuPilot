# SharePoint Session Expired Fix

## Issue
After implementing the new code, SharePoint/Word Online was showing "Session Expired" errors when trying to view enriched documents, even after multiple refreshes and re-logins.

## Root Cause
The Drawing element wrapper for images was using the **wrong namespace**, creating invalid XML that SharePoint/Word Online couldn't parse:

**Before (WRONG):**
```csharp
var drawing = new OpenXmlUnknownElement(
    "drawing",
    "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"); // ❌ Wrong namespace
```

**After (CORRECT):**
```csharp
var drawing = new OpenXmlUnknownElement(
    "w:drawing",
    "http://schemas.openxmlformats.org/wordprocessingml/2006/main"); // ✅ Correct namespace
```

## Key Fix
The Drawing element in a Word document's Run must be in the **wordprocessingml** namespace (`w:drawing`), NOT the wordprocessingDrawing namespace. This is critical for SharePoint/Word Online compatibility.

## Additional Improvements

### 1. Bookmark Validation
Added sanitization for bookmark names and IDs to prevent invalid XML:
- Bookmark names limited to 40 characters
- Invalid characters removed (spaces, hyphens)
- Fallback IDs if section ID is invalid

### 2. Error Handling
Improved error handling for image embedding to prevent document corruption.

## Files Modified

- `SMEPilot.FunctionApp/Helpers/TemplateBuilder.cs`
  - Fixed Drawing element namespace
  - Added bookmark validation
  - Improved error handling

## Testing

After regenerating documents:
1. ✅ Documents should open in SharePoint/Word Online without session errors
2. ✅ Images should be properly embedded
3. ✅ TOC should work after updating field
4. ✅ Bookmarks should be valid

## Technical Details

### OpenXML Structure for Images
```
<w:r>  <!-- Run -->
  <w:drawing>  <!-- Drawing element in wordprocessingml namespace -->
    <wp:inline>  <!-- Inline element in wordprocessingDrawing namespace -->
      <!-- Image content -->
    </wp:inline>
  </w:drawing>
</w:r>
```

**Critical:** The outer `w:drawing` must be in wordprocessingml namespace, while the inner `wp:inline` is in wordprocessingDrawing namespace.

---

**Status:** ✅ Fixed
**Date:** 2024-12-19
**Build:** Successful

