# SMEPilot User Guide

## Overview

SMEPilot automatically enriches your documents and makes them searchable via Microsoft 365 Copilot. This guide explains how to use SMEPilot as an end user.

## How It Works

1. **Upload**: Upload a raw document to the Source Folder
2. **Enrichment**: SMEPilot automatically processes and enriches the document
3. **Search**: Use Copilot Agent to search and query enriched documents

## Uploading Documents

### Step 1: Prepare Your Document

- Document must be in `.docx` format
- Maximum file size: 50 MB
- Document can be plain text or have headings

### Step 2: Upload to Source Folder

1. Navigate to the Source Folder (configured by admin)
2. Click "Upload" → "Files"
3. Select your `.docx` file
4. Click "Open"

### Step 3: Wait for Processing

- Processing typically takes 30-60 seconds
- Check document metadata for status:
  - `SMEPilot_Status` = "Processing" → Still processing
  - `SMEPilot_Status` = "Succeeded" → Processing complete
  - `SMEPilot_Status` = "Failed" → Processing failed (check error message)

### Step 4: Access Enriched Document

1. Once processing is complete, enriched document is saved to Destination Folder
2. Link to enriched document is in `SMEPilot_EnrichedFileUrl` metadata
3. Original document remains in Source Folder

## Using Copilot Agent

### Accessing Copilot Agent

Copilot Agent can be accessed from:
- **Microsoft Teams**: In any chat, type `@SMEPilot Knowledge Assistant`
- **Web Interface**: Via SharePoint portal (if configured)
- **O365 Apps**: In Word, Excel, PowerPoint (if configured)

### Asking Questions

#### General Questions
```
What is SMEPilot?
What documents are available?
```

#### Procedure Questions
```
How do I configure SMEPilot?
What are the steps to deploy?
```

#### Troubleshooting Questions
```
What are known issues with SMEPilot?
How do I fix [specific problem]?
```

### Understanding Responses

Copilot Agent responses include:
1. **Summary** (2-3 lines) - Brief overview
2. **Numbered Steps** - For procedures or troubleshooting
3. **Citations** - Links to source documents

### Example Response

**Question**: "How do I configure SMEPilot?"

**Response**:
```
SMEPilot configuration involves setting up the source folder, destination folder, and template file.

1. Navigate to the Admin Panel
2. Fill in the configuration form
3. Click "Save Configuration"
4. Wait for installation to complete

Source: SMEPilot Installation Guide (link)
```

## Document Status

### Checking Document Status

1. Go to Source Folder
2. Select your document
3. View metadata columns:
   - **SMEPilot_Enriched**: Yes/No
   - **SMEPilot_Status**: Processing/Succeeded/Failed/Retrying
   - **SMEPilot_EnrichedFileUrl**: Link to enriched document
   - **SMEPilot_LastEnrichedTime**: When document was last processed

### Status Values

- **Processing**: Document is being processed
- **Succeeded**: Processing completed successfully
- **Failed**: Processing failed (check error message)
- **Retrying**: Processing failed, will retry automatically

## Common Issues

### Document Not Processing

**Possible Causes**:
- File size > 50MB
- Invalid file format
- Function App not running
- Network issues

**Solutions**:
- Check file size (must be < 50MB)
- Ensure file is `.docx` format
- Contact admin if Function App is down
- Wait and retry (automatic retry after 5 minutes)

### Copilot Agent Not Finding Documents

**Possible Causes**:
- Document not yet indexed (wait 24-48 hours)
- Document not in "SMEPilot Enriched Docs" library
- Insufficient permissions
- Indexing delay

**Solutions**:
- Wait 24-48 hours for indexing
- Verify document is in correct library
- Check you have read permissions
- Ask admin to trigger reindex

### Enriched Document Not Found

**Possible Causes**:
- Processing failed
- Document was deleted
- Permissions issue

**Solutions**:
- Check document status in metadata
- Check error message if status = "Failed"
- Verify you have access to Destination Folder
- Contact admin if issue persists

## Best Practices

### Document Preparation

1. **Use Clear Headings**: Documents with headings are processed better
2. **Organize Content**: Use sections (Overview, Functional, Technical, Troubleshooting)
3. **Keep File Size Reasonable**: Smaller files process faster
4. **Use Standard Format**: Stick to `.docx` format

### Asking Questions

1. **Be Specific**: Ask specific questions for better results
2. **Use Keywords**: Include relevant keywords from your documents
3. **Ask Follow-ups**: Build on previous answers
4. **Check Citations**: Always verify information from source documents

### Document Management

1. **Keep Originals**: Source documents are preserved for audit trail
2. **Check Status**: Monitor document processing status
3. **Review Enriched Documents**: Verify enrichment quality
4. **Update Documents**: Modified documents are automatically reprocessed

## FAQ

### Q: How long does processing take?
**A**: Typically 30-60 seconds per document, depending on file size.

### Q: Can I upload multiple files at once?
**A**: Yes, but they are processed sequentially. Large batches may take longer.

### Q: What happens if processing fails?
**A**: The system automatically retries up to 3 times. If all retries fail, status is set to "Failed" and you can check the error message.

### Q: Can I edit the enriched document?
**A**: Yes, but edits will not be reflected in Copilot search until the document is reprocessed.

### Q: How do I update a document?
**A**: Simply modify the source document and upload it again. The system will detect the change and reprocess it.

### Q: What file formats are supported?
**A**: Currently only `.docx` format is supported.

### Q: Can I delete the source document?
**A**: Yes, but it's recommended to keep it for audit trail. The enriched document will remain in the Destination Folder.

## Support

For issues or questions:
- Check document status in metadata
- Review error messages
- Contact your SharePoint admin
- Check Application Insights logs (admin access required)

