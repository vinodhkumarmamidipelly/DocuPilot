# 🚀 SMEPilot - Next Steps & Action Plan

**Current Status:**
- ✅ Documentation: Complete and aligned (MR_Final1.md finalized)
- ✅ Enrichment: Implemented (rule-based, OpenXML)
- ⚠️ Copilot Agent: REQUIRED - Needs configuration (see Phase 6)
- ⚠️ Installation: Needs deployment
- ⚠️ Versioning Detection: Needs implementation (duplicate vs version handling)

---

## 📋 Phase 1: Pre-Deployment Validation (Days 1-2)

### 1.1 Code Review & Verification
- [ ] **Review Implementation**
  - Verify `TemplateFiller.cs` handles all edge cases from `EDGE_CASES_AND_PERMISSIONS.md`
  - Verify `ProcessSharePointFile.cs` uses correct destination folder configuration
  - Verify `Config.cs` default value won't cause issues (ensure env var is set)
  - Check all metadata fields are being set correctly
  - Verify 50MB file size limit is enforced (configurable, default 50MB, max 80MB)
  - Verify retry logic: 3 attempts with exponential backoff
  - Verify source document is KEPT after processing (default behavior)
  - Verify Rule-Based Classification Engine for content mapping (OpenXML limitation solution)

- [ ] **Configuration Validation**
  - Verify `SMEPilotConfig` list schema matches documentation
  - Verify environment variable names match installation guide
  - Verify template path resolution works correctly
  - Test configuration reading from SharePoint list
  - Verify `MaxFileSizeMB` default is 50MB (configurable up to 80MB)
  - Verify `MaxRetryAttempts` default is 3

### 1.2 Template Verification
- [ ] **Template File Check**
  - Verify `UniversalOrgTemplate.dotx` exists in Templates folder
  - Verify template has all required content controls (DocumentTitle, Overview, Functional, Technical, Screenshots, RevisionHistory)
  - Test template opens correctly in Word
  - Verify TOC field is properly configured

- [ ] **Template Inspection**
  - Run `TemplateFiller.InspectTemplate()` to verify all tags are detected
  - Document actual tags found vs. expected tags
  - Update documentation if discrepancies found

### 1.3 Test Data Preparation
- [ ] **Create Test Documents**
  - Document with headings (Overview, Functional, Technical)
  - Document without headings (test fallback/plain text handling)
  - Document with images
  - Document with tables
  - Large document (45-50MB) for size limit testing
  - Document exceeding 50MB limit (should be rejected)
  - Corrupted document for error handling
  - Duplicate document (same file uploaded twice)
  - Updated version document (same file, modified timestamp)

---

## 📋 Phase 2: Local Testing (Days 3-4)

### 2.1 Local Function App Testing
- [ ] **Setup Local Environment**
  - Configure `local.settings.json` with test SharePoint site
  - Set `EnrichedFolderRelativePath` to `/Shared Documents/SMEPilot Enriched Docs`
  - Configure Azure AD app credentials
  - Test connection to SharePoint

- [ ] **Manual Testing**
  - Upload test document to source folder
  - Trigger webhook manually (or simulate)
  - Verify enrichment process completes
  - Check enriched document in destination folder
  - Verify metadata fields are set correctly
  - Test all edge cases from `QA_CHECKLIST.md`

### 2.2 Template Filling Testing
- [ ] **Content Mapping Verification**
  - Test documents with different structures
  - Verify content maps to correct sections
  - Verify no content duplication
  - Verify images are inserted correctly
  - Verify revision history table expands

- [ ] **Output Quality Check**
  - Open enriched documents in Word
  - Verify no blank pages
  - Verify TOC field is present
  - Verify formatting matches template
  - Verify all sections populated correctly

### 2.3 Error Handling Testing
- [ ] **Edge Case Testing**
  - Test with locked files (retry logic)
  - Test with unsupported file types
  - Test with corrupted files
  - Test with files exceeding size limit
  - Test with missing template
  - Test with invalid configuration

---

## 📋 Phase 3: SharePoint Setup (Days 5-6)

### 3.1 SharePoint Site Preparation
- [ ] **Create Required Libraries**
  - Create "SMEPilot Enriched Docs" library (exact name required)
  - Create Templates library
  - Create Source folder/library (configurable)
  - Set appropriate permissions

- [ ] **Create SMEPilotConfig List**
  - Create list with required columns:
    - SourceLibraryUrl
    - TargetLibraryUrl
    - TemplateFileUrl
    - TemplateLibraryPath
    - TemplateFileName
    - NotificationEmail
    - EnableCopilotAgent
    - VisibilityGroup
    - MaxFileSizeMB (default: 50MB, max: 80MB)
    - RetryAttempts (default: 3)
    - MetadataChangeHandling (Skip, Reprocess - default: Skip)
  - Add initial configuration entry

- [ ] **Create Metadata Columns**
  - SMEPilot_Enriched (Yes/No)
  - SMEPilot_Status (Text: Processing, Succeeded, Retrying, Failed, NeedsManualReview)
  - SMEPilot_EnrichedFileUrl (Hyperlink)
  - SMEPilot_LastEnrichedTime (DateTime) - **REQUIRED for versioning detection**
  - SMEPilot_EnrichedJobId (Text) - optional
  - SMEPilot_Classification (Text) - optional

### 3.2 Template Upload
- [ ] **Upload Template**
  - Upload `UniversalOrgTemplate.dotx` to Templates library
  - Verify template path matches configuration
  - Test template can be accessed by Function App

### 3.3 Permissions Configuration
- [ ] **Azure AD App Permissions**
  - Verify `Sites.ReadWrite.All` is granted with admin consent
  - Verify `Sites.Manage.All` if installer creates libraries
  - Test app-only authentication works

- [ ] **SharePoint Permissions**
  - Verify Function App can read from source folder
  - Verify Function App can write to destination folder
  - Verify Function App can read/write metadata
  - Verify users have appropriate permissions

---

## 📋 Phase 4: Azure Function App Deployment (Day 7)

### 4.1 Function App Setup
- [ ] **Create Function App**
  - Create Azure Function App (consumption or premium plan)
  - Configure runtime (.NET 6 or later)
  - Set up Application Insights for logging

- [ ] **Configure Application Settings**
  - Set `EnrichedFolderRelativePath` = `/Shared Documents/SMEPilot Enriched Docs`
  - Set `TemplateLibraryPath` = `/Shared Documents/Templates`
  - Set `TemplateFileName` = `UniversalOrgTemplate.dotx`
  - Configure Azure AD app credentials
  - Set retry and timeout configurations

### 4.2 Deploy Code
- [ ] **Deployment**
  - Deploy Function App code
  - Deploy Templates folder (with template file)
  - Verify all dependencies are included
  - Test Function App starts correctly

### 4.3 Webhook Subscription
- [ ] **Setup Webhook**
  - Create Microsoft Graph webhook subscription
  - Point to Function App endpoint `/api/ProcessSharePointFile`
  - Set expiration (renewal every 2 days)
  - Test webhook receives notifications
  - For dev: Use ngrok for public endpoint

---

## 📋 Phase 5: Integration Testing (Day 8)

### 5.1 End-to-End Testing
- [ ] **Full Workflow Test**
  - Upload document to source folder
  - Verify webhook triggers Function App
  - Verify enrichment completes successfully
  - Verify enriched document in "SMEPilot Enriched Docs" (DOCX format)
  - Verify source document REMAINS in source folder (KEEP - default behavior)
  - Verify metadata updated correctly (SMEPilot_Enriched, SMEPilot_Status, SMEPilot_EnrichedFileUrl, SMEPilot_LastEnrichedTime)
  - Verify no duplicate processing (idempotency check)
  - Verify versioning detection (reprocess if LastModified > LastEnriched)
  - Verify metadata-only change handling (configurable: skip or reprocess)
  - Verify SharePoint automatically maintains versions for enriched documents
  - Verify enriched document tagged with version metadata

### 5.2 Performance Testing
- [ ] **Performance Validation**
  - Test with files ≤50MB (should complete in <60 seconds)
  - Test files >50MB (should be rejected with clear error)
  - Test concurrent file uploads (1-10 files: perfect, 10-20: good, 20-50: slower)
  - Verify semaphore locks prevent duplicate processing of same file
  - Verify notification deduplication (30-second window)
  - Monitor Function App performance
  - Check Application Insights logs

### 5.3 Error Scenarios
- [ ] **Error Handling Validation**
  - Test file locked scenario (retry logic: 3 attempts, exponential backoff)
  - Test invalid file types (moved to RejectedDocs)
  - Test processing failures (moved to FailedDocs, status = "Failed")
  - Test mid-processing failure (source document preserved, partial files cleaned up)
  - Test retry scenarios (status = "Retrying")
  - Test manual review scenarios (status = "NeedsManualReview")
  - Verify admin notifications sent
  - Verify logs saved in SharePoint + App Insights

---

## 📋 Phase 6: Copilot Agent Configuration (Days 9-10) ⚠️ **REQUIRED**

> **Note:** Copilot Agent configuration is **REQUIRED** (not optional). See `MR_Final1.md` Question 2, Level C.

### 6.1 Verify Prerequisites
- [ ] **SharePoint Search**
  - Verify "SMEPilot Enriched Docs" library is indexed
  - Verify site is visible to enterprise search
  - Wait for search crawl (24-48 hours) or trigger reindex
  - Verify DOCX format is indexed (better than PDF for Copilot search)

- [ ] **Library Permissions**
  - Verify users have read access to "SMEPilot Enriched Docs"
  - Verify Copilot can access the library
  - Verify M365 Global Admin or Copilot Admin has access

### 6.2 Copilot Studio Setup (REQUIRED)
- [ ] **Create Copilot Agent**
  - Access Copilot Studio (https://copilotstudio.microsoft.com)
  - Create new Copilot: "SMEPilot Knowledge Assistant"
  - Set description: "Organization's knowledge assistant for functional and technical documents"
  - **Required role:** M365 Global Admin OR Copilot Admin

- [ ] **Configure Data Source**
  - Add SharePoint as data source
  - Select "SMEPilot Enriched Docs" library
  - Set as primary data source
  - Enable Microsoft Search integration

- [ ] **Add Custom Instructions (REQUIRED)**
  - Go to Settings → Generative AI → System message
  - Add manager's exact instructions:
    > "You are SMEPilot — the organization's knowledge assistant. Use only documents from the 'SMEPilot Enriched Docs' SharePoint library as the primary evidence. Provide a short summary (2-3 lines), then give a numbered step list for procedures or troubleshooting. Always cite the document title and link. If uncertain, say 'I couldn't find a definitive answer in SMEPilot docs.' Do not invent facts beyond the indexed docs."

### 6.3 Deploy to Teams
- [ ] **Deployment**
  - Publish Copilot Agent
  - Deploy to Teams
  - Make available to organization
  - Test access from Teams

### 6.4 Copilot Testing
- [ ] **Test Queries**
  - Ask: "What is SMEPilot?" (verify context loading)
  - Ask procedure questions
  - Verify response format (summary + numbered steps)
  - Verify citations include document title and link
  - Test uncertainty message when no answer found
  - Verify Copilot uses latest version of documents

---

## 📋 Phase 7: Documentation & Handoff (Day 11)

### 7.1 Final Documentation
- [ ] **Update Documentation**
  - Document any deviations from original plan
  - Update installation guide with actual steps taken
  - Document any configuration gotchas
  - Create troubleshooting guide based on issues encountered

### 7.2 User Training
- [ ] **Create User Guide**
  - How to upload documents
  - How to access enriched documents
  - How to use Copilot Agent
  - How to check processing status

### 7.3 Admin Guide
- [ ] **Create Admin Guide**
  - How to configure SMEPilotConfig
  - How to monitor Function App
  - How to troubleshoot issues
  - How to renew webhook subscriptions

---

## 📋 Phase 8: Production Readiness (Day 12)

### 8.1 Production Deployment
- [ ] **Deploy to Production**
  - Deploy Function App to production environment
  - Configure production SharePoint site
  - Set up production webhook subscriptions
  - Configure production monitoring

### 8.2 Monitoring Setup
- [ ] **Application Insights**
  - Set up alerts for errors
  - Set up alerts for processing failures
  - Configure dashboards for monitoring
  - Set up email notifications for admins

### 8.3 Final Validation
- [ ] **Production Testing**
  - Test with production data
  - Verify all functionality works
  - Verify Copilot queries work
  - Verify monitoring and alerts

---

## 🎯 Success Criteria

### Technical Success
- ✅ Documents enrich successfully (≤50MB in <60 seconds)
- ✅ Files >50MB are rejected with clear error message
- ✅ Enriched documents appear in "SMEPilot Enriched Docs" (DOCX format)
- ✅ Source documents remain in source folder (not deleted)
- ✅ Metadata fields updated correctly (including SMEPilot_LastEnrichedTime)
- ✅ No duplicate processing (idempotency check)
- ✅ Versioning detection works (reprocess if LastModified > LastEnriched)
- ✅ Concurrent uploads handled safely (semaphore locks, deduplication)
- ✅ Error handling works (RejectedDocs, FailedDocs, status tracking)
- ✅ Retry logic works (3 attempts, exponential backoff)
- ✅ Webhook renewal works

### Copilot Success
- ✅ Copilot Agent created and configured (REQUIRED)
- ✅ Copilot can find enriched documents (DOCX format preferred)
- ✅ Copilot responses match manager's format requirements (summary + numbered steps)
- ✅ Citations include document title and link
- ✅ Uncertainty message works correctly
- ✅ Copilot uses latest version of documents

### Operational Success
- ✅ Admin can configure system via SMEPilotConfig
- ✅ Monitoring and alerts work
- ✅ Users can upload and access documents
- ✅ Documentation is complete and accurate

---

## ⚠️ Risk Mitigation

### High Priority Risks
1. **Webhook Subscription Expiration**
   - Mitigation: Implement automatic renewal (every 2 days)
   - Monitor subscription status

2. **Template Path Issues**
   - Mitigation: Verify template path in configuration
   - Test template access before deployment

3. **Copilot Indexing Delay**
   - Mitigation: Wait 24-48 hours or trigger reindex
   - Document expected delay in user guide

4. **File Processing Failures**
   - Mitigation: Comprehensive error handling
   - Move failed files to FailedDocs folder
   - Status tracking (Processing, Retrying, Failed, NeedsManualReview)
   - Automatic retry (3 attempts, exponential backoff)
   - Source document preserved (never lost)
   - Notify admin of failures

5. **Versioning Detection**
   - Mitigation: Compare LastModifiedDateTime with SMEPilot_LastEnrichedTime
   - Reprocess if document updated (version)
   - Skip if unchanged (duplicate)
   - Ensure SMEPilot_LastEnrichedTime metadata field is set

### Medium Priority Risks
1. **Configuration Errors**
   - Mitigation: Validation during installation
   - Clear error messages
   - Verify 50MB limit is configurable (default 50MB, max 80MB)

2. **Permission Issues**
   - Mitigation: Document minimal permissions required (see MR_Final1.md Question 2)
   - Test permissions before deployment
   - Verify all 5 permission levels (A-E) are configured

3. **Concurrent Upload Performance**
   - Mitigation: Current implementation handles 1-20 files well
   - For 50+ files, consider future queue-based enhancement
   - Monitor performance metrics

4. **Copilot Indexing Delay**
   - Mitigation: DOCX format indexes faster than PDF
   - Wait 24-48 hours or trigger reindex
   - Document expected delay in user guide

---

## 📝 Notes

- **Timeline:** 12 days total (can be adjusted based on priorities)
- **Dependencies:** 
  - Azure AD app registration must be done before Phase 3
  - Copilot Agent configuration (Phase 6) is REQUIRED, not optional
- **Testing:** Each phase should be tested before moving to next
- **Documentation:** Update as you go, don't wait until end
- **Key Decisions (from MR_Final1.md):**
  - 50MB file size limit (configurable, default 50MB, max 80MB)
  - Output format: DOCX (default, best for Copilot) - configurable options deferred
  - Source document handling: Keep (default) - configurable options deferred
  - Versioning detection: Compare LastModifiedDateTime with SMEPilot_LastEnrichedTime
  - Metadata-only change: Configurable (skip or reprocess, default: skip)
  - SharePoint automatically maintains versions for enriched documents
  - Enriched documents tagged with version metadata
  - Retry logic: 3 attempts with exponential backoff
  - Concurrent processing: Semaphore locks + idempotency + notification deduplication
  - Rule-Based Classification Engine: Solves OpenXML limitation (no AI, keyword/formatting-based)
  - ML.NET optional enhancement: Can be integrated for intelligent content categorization (optional)

---

**Next Immediate Action:** Start with Phase 1.1 - Code Review & Verification

