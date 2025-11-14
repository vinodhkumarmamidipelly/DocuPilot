# SMEPilot — QA Checklist (Manual + Automated)

## Manual Checks (After Running Enrichment)

### Document Quality
- [ ] Generated DOCX opens in MS Word without a blank 2nd page
- [ ] First visible content contains Table of Contents placeholder text
- [ ] TOC field is properly inserted and will update when fields are updated
- [ ] Title is populated from raw DOCX first meaningful paragraph
- [ ] Overview section contains relevant first paragraphs or heading-based content
- [ ] Functional Details section populated (if applicable)
- [ ] Technical Details section populated (if applicable)
- [ ] Troubleshooting section populated (if applicable)
- [ ] Revision History table exists and columns are reasonably wide and aligned
- [ ] Revision History table has proper header row
- [ ] Images appear in enriched doc and captions preserved (if present)
- [ ] No trailing empty sections or unnecessary page breaks
- [ ] Document formatting matches company template

### SharePoint Integration
- [ ] SharePoint item metadata updated (`SMEPilot_Enriched=true`, `SMEPilot_Status=Enriched`)
- [ ] `SMEPilot_EnrichedFileUrl` contains link to enriched file
- [ ] Enriched file stored under `/Shared Documents/SMEPilot Enriched Docs` (or configured path)
- [ ] Source document metadata correctly updated
- [ ] Enriched document is searchable in SharePoint

### Processing
- [ ] Processing completes within 60 seconds for files ≤50MB
- [ ] Error handling works for invalid file types (moved to RejectedDocs)
- [ ] Error handling works for locked files (retry 3x, then FailedDocs)
- [ ] Idempotency check prevents duplicate processing
- [ ] Webhook subscription renewal works (every 2 days)

---

## Automated Tests to Add / Run

### Unit Tests
- [ ] `Test_Enrich_NoBlankPage` - Process sample and assert no blank pages
- [ ] `Test_TOC_FieldPresent` - Verify TOC field is inserted
- [ ] `Test_RevisionTable_ColumnCount` - Verify revision table has correct columns
- [ ] `Test_MetadataUpdate` - Verify metadata fields are set correctly
- [ ] `Test_ImageExtraction` - Verify images are extracted and placed correctly
- [ ] `Test_ContentMapping` - Verify content maps to correct sections
- [ ] `Test_TemplateSanitization` - Verify trailing empty sections removed

### Integration Tests
- [ ] `Test_EndToEnd_Enrichment` - Full flow from upload to enriched doc
- [ ] `Test_WebhookProcessing` - Verify webhook triggers processing
- [ ] `Test_ConfigService` - Verify config read from SMEPilotConfig list
- [ ] `Test_ErrorHandling_InvalidFile` - Verify invalid files handled correctly
- [ ] `Test_ErrorHandling_LockedFile` - Verify locked file retry logic

### Performance Tests
- [ ] `Test_ProcessingTime_Under50MB` - Verify processing completes within 60 seconds
- [ ] `Test_ConcurrentProcessing` - Verify multiple files processed correctly
- [ ] `Test_LargeFileHandling` - Verify files >50MB handled appropriately

---

## Test Data

### Sample Documents
- `Alerts_input_rulebased_raw.docx` - Standard test case
- Document with no headings - Test fallback logic
- Document with only images - Test image-only handling
- Document with embedded OLE - Test OLE preservation
- Document with trailing empty sections - Test sanitization
- Large document (>50MB) - Test size limits

---

## Acceptance Criteria

✅ Document is enriched, formatted, and uploaded within one minute (for ≤50MB files)  
✅ Output matches company template (no empty TOC, no blank pages)  
✅ Admin can configure libraries and template during install  
✅ Copilot can discover and answer from enriched content  
✅ All actions logged in Application Insights  
✅ Works without AI, database, or manual triggers

