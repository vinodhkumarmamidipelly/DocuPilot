# ⚠️ Important: Folder vs Document Library

## The Issue

You created a **folder** called "ScratchDocs" in SharePoint, but the code needs a **document library**.

## Difference

- **Folder**: A container inside a library (like a directory)
- **Document Library**: A SharePoint list with BaseTemplate 101 (like "Documents" or "Shared Documents")

## Solution

### Option 1: Create a Document Library (Recommended)

1. Go to: `https://onblick.sharepoint.com/sites/DocEnricher-PoC`
2. Click **"New"** → **"Document library"** (NOT "Folder")
3. Name: `ScratchDocs`
4. Click **"Create"**
5. Delete the old "ScratchDocs" folder if you want

### Option 2: Use Existing Library

If you can't create a new library, use the existing "Documents" library:

1. Edit the web part
2. Change "ScratchDocs Library Name" to **"Documents"**
3. Save

### Option 3: Use the Folder (Not Recommended)

If you want to use the folder you created, you'll need to:
- Upload files to: `Documents/ScratchDocs/` (folder path)
- But this won't work with the current code which expects a library

## Why This Matters

The code uses SharePoint REST API to:
- Check if library exists: `/_api/web/lists/getbytitle('ScratchDocs')`
- Upload files: `/_api/web/lists/getbytitle('ScratchDocs')/RootFolder/Files/Add(...)`

These APIs work with **libraries**, not **folders**.
