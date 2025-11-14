# SMEPilot Testing Guide

## Manual Testing Checklist

### Phase 1: Installation Testing

#### Test 1.1: Configuration Form Validation
- [ ] Empty source folder → Shows validation error
- [ ] Empty destination folder → Shows validation error
- [ ] Invalid template file (not .dotx) → Shows validation error
- [ ] Max file size < 1 or > 1000 → Shows validation error
- [ ] No access points selected → Shows validation error
- [ ] All fields valid → No validation errors

#### Test 1.2: Configuration Save
- [ ] Fill all required fields
- [ ] Click "Save Configuration"
- [ ] Verify SMEPilotConfig list is created
- [ ] Verify configuration is saved
- [ ] Verify metadata columns are created
- [ ] Verify error folders are created
- [ ] Verify webhook subscription is created
- [ ] Verify subscription ID is stored

#### Test 1.3: Configuration View/Edit
- [ ] After installation, verify view mode is shown
- [ ] Verify configuration details are displayed
- [ ] Verify last updated date is shown
- [ ] Verify webhook subscription status is shown
- [ ] Click "Edit Configuration" → Form becomes editable
- [ ] Make changes and save → Configuration is updated
- [ ] Click "Reset" → Configuration is cleared (with confirmation)

### Phase 2: Document Enrichment Testing

#### Test 2.1: Basic Document Processing
- [ ] Upload a .docx file to Source Folder
- [ ] Verify webhook triggers Function App
- [ ] Verify document is processed
- [ ] Verify enriched document is saved to Destination Folder
- [ ] Verify source document REMAINS in Source Folder
- [ ] Verify metadata is updated:
  - [ ] SMEPilot_Enriched = true
  - [ ] SMEPilot_Status = "Succeeded"
  - [ ] SMEPilot_EnrichedFileUrl is set
  - [ ] SMEPilot_LastEnrichedTime is set

#### Test 2.2: File Size Limits
- [ ] Upload file < 50MB → Processes successfully
- [ ] Upload file > 50MB → Rejected with clear error message
- [ ] Verify status = "Failed" for rejected files

#### Test 2.3: Duplicate Detection
- [ ] Upload same file twice (unchanged) → Second upload is skipped
- [ ] Verify idempotency check works
- [ ] Verify no duplicate enriched documents

#### Test 2.4: Version Detection
- [ ] Upload a document
- [ ] Wait for processing to complete
- [ ] Modify the source document
- [ ] Upload again → Should reprocess (new version)
- [ ] Verify new enriched document is created
- [ ] Verify SMEPilot_LastEnrichedTime is updated

#### Test 2.5: Error Handling
- [ ] Upload invalid format file → Status = "Failed"
- [ ] Simulate network error → Retry logic activates
- [ ] Verify error messages are logged
- [ ] Verify source document is preserved

### Phase 3: Copilot Agent Testing

#### Test 3.1: Copilot Agent Access
- [ ] Access Copilot Agent in Teams
- [ ] Access Copilot Agent in Web
- [ ] Access Copilot Agent in O365
- [ ] Verify agent responds to queries

#### Test 3.2: Document Queries
- [ ] Ask: "What is SMEPilot?" → Should return summary
- [ ] Ask procedure question → Should return numbered steps
- [ ] Verify citations include document title and link
- [ ] Ask question with no results → Should show uncertainty message

#### Test 3.3: Security
- [ ] Test with user who has access → Can see documents
- [ ] Test with user who lacks access → Cannot see documents
- [ ] Verify security trimming works

## Automated Testing (Future)

### Unit Tests
- [ ] SharePointService tests
- [ ] FunctionAppService tests
- [ ] AdminPanel component tests

### Integration Tests
- [ ] End-to-end installation flow
- [ ] Document processing flow
- [ ] Webhook subscription flow

## Performance Testing

- [ ] Test with 1 file → Processing time < 30s
- [ ] Test with 10 files → All processed within 5 minutes
- [ ] Test with 50 files → All processed within 30 minutes
- [ ] Verify no memory leaks during batch processing

## Edge Cases

- [ ] Very large file (49MB) → Processes successfully
- [ ] File with no headings → Uses fallback mapping
- [ ] File with only images → Preserves images
- [ ] File with embedded objects → Preserves objects
- [ ] Concurrent uploads (5 files at once) → All processed safely
- [ ] Network interruption during processing → Retries successfully

