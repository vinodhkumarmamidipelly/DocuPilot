# 🚀 SMEPilot - Complete Implementation Plan

> **Strategy:** Start with SPFx (Admin Panel) → Then Function App Updates → Then Integration & Testing

---

## 📋 Overview

This plan breaks down the implementation into **5 phases**, starting with SPFx Admin Panel (the entry point for installation), then moving to Function App updates, and finally integration and testing.

**Estimated Timeline:** 5-7 days (depending on complexity and testing)

---

## 🎯 Phase 1: SPFx Admin Panel (Days 1-2) ⭐ **START HERE**

**Goal:** Create the installation configuration UI that collects all 6 configuration items and sets up SharePoint artifacts.

### Task 1.1: Admin Panel UI - Configuration Form

**File:** `SMEPilot.SPFx/src/webparts/adminPanel/components/AdminPanel.tsx`

**Requirements:**
- [ ] Create form with 6 configuration fields:
  1. **Source Folder** (User Selected)
     - Folder picker component
     - Validate folder exists and is accessible
     - Store: `SourceFolderPath` (e.g., `/sites/{siteId}/drives/{driveId}/root:/SourceFolder`)
  
  2. **Destination Folder** (User Selected)
     - Folder picker component
     - Validate folder exists or can be created
     - Store: `DestinationFolderPath`
  
  3. **Template File**
     - File picker component (filter: `.dotx` files)
     - Validate file exists and is accessible
     - Store: `TemplateFileUrl` (full SharePoint URL)
  
  4. **Processing Settings**
     - Max File Size: 50MB (default, editable)
     - Timeout: 60 seconds (default, editable)
     - Retry Count: 3 (default, editable)
     - Store: `MaxFileSizeMB`, `ProcessingTimeoutSeconds`, `MaxRetries`
  
  5. **Copilot Agent Prompt**
     - Multi-line text area
     - Default prompt text (from documentation)
     - Store: `CopilotPrompt` (text)
  
  6. **Access Points** (Checkboxes)
     - ☑ Teams
     - ☑ Web
     - ☑ O365
     - Store: `AccessTeams` (boolean), `AccessWeb` (boolean), `AccessO365` (boolean)

- [ ] Add validation:
  - All required fields must be filled
  - Source folder must exist
  - Template file must be `.dotx` and exist
  - Processing settings must be positive numbers

- [ ] Add "Save Configuration" button
- [ ] Add loading states and error messages
- [ ] Use Fluent UI components (TextField, Dropdown, Checkbox, etc.)

**Dependencies:** None (can start immediately)

**Estimated Time:** 4-6 hours

---

### Task 1.2: SharePoint Service - Create SMEPilotConfig List

**File:** `SMEPilot.SPFx/src/services/SharePointService.ts` (NEW)

**Requirements:**
- [ ] Create service class for SharePoint operations
- [ ] Method: `createSMEPilotConfigList()`
  - Create SharePoint list named "SMEPilotConfig"
  - Add columns:
    - `SourceFolderPath` (Text, Required)
    - `DestinationFolderPath` (Text, Required)
    - `TemplateFileUrl` (Text, Required)
    - `MaxFileSizeMB` (Number, Default: 50)
    - `ProcessingTimeoutSeconds` (Number, Default: 60)
    - `MaxRetries` (Number, Default: 3)
    - `CopilotPrompt` (Note/Multi-line Text, Required)
    - `AccessTeams` (Yes/No, Default: Yes)
    - `AccessWeb` (Yes/No, Default: Yes)
    - `AccessO365` (Yes/No, Default: Yes)
    - `SubscriptionId` (Text, Optional - for webhook subscription ID)
    - `LastUpdated` (DateTime, Auto-updated)
  - Set list permissions (only admins can edit)
  - Return success/failure

- [ ] Method: `saveConfiguration(config: ConfigurationModel)`
  - Save configuration to SMEPilotConfig list
  - If list is empty, create new item
  - If item exists, update existing item
  - Return success/failure

- [ ] Method: `getConfiguration()`
  - Read configuration from SMEPilotConfig list
  - Return configuration object or null if not configured

- [ ] Method: `validateConfiguration()`
  - Check if all required fields are set
  - Validate folder paths exist
  - Validate template file exists
  - Return validation result with errors

**Dependencies:** Task 1.1 (needs configuration model)

**Estimated Time:** 3-4 hours

---

### Task 1.3: SharePoint Service - Create Metadata Columns

**File:** `SMEPilot.SPFx/src/services/SharePointService.ts` (extend)

**Requirements:**
- [ ] Method: `createMetadataColumns(sourceFolderPath: string)`
  - Add columns to Source Folder's document library:
    - `SMEPilot_Enriched` (Yes/No, Default: No)
    - `SMEPilot_Status` (Text, Options: "Pending", "Processing", "Completed", "Failed", "Rejected")
    - `SMEPilot_EnrichedFileUrl` (Hyperlink, Optional)
    - `SMEPilot_ProcessedDate` (DateTime, Optional)
  - Handle errors (columns may already exist)
  - Return success/failure

**Dependencies:** Task 1.2

**Estimated Time:** 2-3 hours

---

### Task 1.4: SharePoint Service - Create Error Folders

**File:** `SMEPilot.SPFx/src/services/SharePointService.ts` (extend)

**Requirements:**
- [ ] Method: `createErrorFolders(sourceFolderPath: string)`
  - Create `RejectedDocs` folder in Source Folder location
  - Create `FailedDocs` folder in Source Folder location
  - Handle errors (folders may already exist)
  - Return success/failure

**Dependencies:** Task 1.2

**Estimated Time:** 1-2 hours

---

### Task 1.5: Function App Service - Webhook Subscription

**File:** `SMEPilot.SPFx/src/services/FunctionAppService.ts` (extend)

**Requirements:**
- [ ] Method: `createWebhookSubscription(config: WebhookSubscriptionRequest)`
  - Call Function App endpoint: `POST /api/SetupSubscription`
  - Parameters:
    - `sourceFolderPath` (from configuration)
    - `functionAppUrl` (from web part properties)
    - `tenantId` (from context)
  - Handle response (subscription ID)
  - Store subscription ID in SMEPilotConfig list
  - Return success/failure

- [ ] Method: `validateWebhookSubscription(subscriptionId: string)`
  - Check if webhook subscription is active
  - Return validation result

**Dependencies:** Task 1.2, Function App endpoint exists

**Estimated Time:** 2-3 hours

---

### Task 1.6: Admin Panel - Complete Installation Flow

**File:** `SMEPilot.SPFx/src/webparts/adminPanel/components/AdminPanel.tsx` (extend)

**Requirements:**
- [ ] Integrate all services:
  - On "Save Configuration" click:
    1. Validate form inputs
    2. Create SMEPilotConfig list (if doesn't exist)
    3. Save configuration to list
    4. Create metadata columns on Source Folder
    5. Create error folders (RejectedDocs, FailedDocs)
    6. Call Function App to create webhook subscription
    7. Save subscription ID to config
    8. Show success message with next steps

- [ ] Add installation status check:
  - On component mount, check if configuration exists
  - If configured: Show "Configuration Complete" message
  - If not configured: Show configuration form

- [ ] Add "Test Configuration" button:
  - Validate all settings
  - Test folder access
  - Test template file access
  - Show validation results

- [ ] Add error handling:
  - Show specific error messages for each step
  - Allow retry on failures
  - Log errors to console

**Dependencies:** Tasks 1.1, 1.2, 1.3, 1.4, 1.5

**Estimated Time:** 4-5 hours

---

### Task 1.7: Admin Panel - Configuration Display & Edit

**File:** `SMEPilot.SPFx/src/webparts/adminPanel/components/AdminPanel.tsx` (extend)

**Requirements:**
- [ ] Add "View Configuration" section:
  - Display current configuration in read-only mode
  - Show last updated date
  - Show webhook subscription status

- [ ] Add "Edit Configuration" button:
  - Load existing configuration into form
  - Allow editing
  - Save updates

- [ ] Add "Reset Configuration" option (admin only):
  - Clear all configuration
  - Delete webhook subscription
  - Reset to initial state

**Dependencies:** Task 1.6

**Estimated Time:** 2-3 hours

---

### Task 1.8: Build & Package SPFx Solution

**Requirements:**
- [ ] Update `package-solution.json`:
  - Set solution name: "SMEPilot"
  - Set solution ID (GUID)
  - Configure features and resources

- [ ] Build solution:
  ```bash
  npm install
  gulp build
  gulp bundle --ship
  gulp package-solution --ship
  ```

- [ ] Test `.sppkg` file:
  - Verify package structure
  - Check manifest files
  - Validate web parts are included

- [ ] Create deployment guide:
  - How to upload to App Catalog
  - How to deploy to site
  - How to add web part to page

**Dependencies:** All Phase 1 tasks

**Estimated Time:** 2-3 hours

---

## 🔧 Phase 2: Function App Updates (Days 3-4)

**Goal:** Update Function App to read configuration from SharePoint and handle webhook subscription creation.

### Task 2.1: ConfigService - Read from SharePoint

**File:** `SMEPilot.FunctionApp/Helpers/Config.cs` (update)

**Requirements:**
- [ ] Update `Config` class to read from SMEPilotConfig list:
  - Method: `GetConfigurationFromSharePoint()`
  - Use Graph API to read SMEPilotConfig list
  - Parse configuration values
  - Cache configuration (refresh every 5 minutes)
  - Handle missing configuration (throw error)

- [ ] Update configuration properties:
  - `SourceFolderPath` (from SharePoint)
  - `DestinationFolderPath` (from SharePoint)
  - `TemplateFileUrl` (from SharePoint)
  - `MaxFileSizeMB` (from SharePoint, default: 50)
  - `ProcessingTimeoutSeconds` (from SharePoint, default: 60)
  - `MaxRetries` (from SharePoint, default: 3)
  - `CopilotPrompt` (from SharePoint - for future use)

- [ ] Remove hardcoded values
- [ ] Add error handling for missing configuration

**Dependencies:** Phase 1 complete (SMEPilotConfig list exists)

**Estimated Time:** 3-4 hours

---

### Task 2.2: SetupSubscription Function

**File:** `SMEPilot.FunctionApp/Functions/SetupSubscription.cs` (update or create)

**Requirements:**
- [ ] Create HTTP trigger function: `SetupSubscription`
- [ ] Accept parameters:
  - `sourceFolderPath` (from SPFx)
  - `tenantId` (from SPFx)
  - `functionAppUrl` (from environment variable)

- [ ] Create webhook subscription via Graph API:
  - POST to `/v1.0/subscriptions`
  - Set `changeType`: "created,updated"
  - Set `notificationUrl`: Function App endpoint
  - Set `resource`: Source Folder path
  - Set `expirationDateTime`: +3 days (max)
  - Set `clientState`: Unique identifier

- [ ] Handle webhook validation:
  - Check for `Validation-Token` header
  - Return token immediately (within 10 seconds)

- [ ] Return subscription ID to SPFx
- [ ] Store subscription ID in SMEPilotConfig list (for renewal)

- [ ] Add error handling:
  - Invalid folder path
  - Graph API errors
  - Permission errors

**Dependencies:** Task 2.1

**Estimated Time:** 4-5 hours

---

### Task 2.3: Webhook Renewal Timer Function

**File:** `SMEPilot.FunctionApp/Functions/WebhookRenewal.cs` (create)

**Requirements:**
- [ ] Create timer trigger function: `WebhookRenewal`
  - Runs every 2 days (CRON: `0 0 */2 * * *`)

- [ ] Read all active subscriptions from SMEPilotConfig list
- [ ] Check expiration date for each subscription
- [ ] If expires within 24 hours:
  - Create new subscription (same configuration)
  - Update subscription ID in SMEPilotConfig list
  - Log renewal to Application Insights

- [ ] Handle errors:
  - Missing configuration
  - Graph API errors
  - Log all errors

**Dependencies:** Task 2.2

**Estimated Time:** 3-4 hours

---

### Task 2.4: ProcessSharePointFile - Use Configuration from SharePoint

**File:** `SMEPilot.FunctionApp/Functions/ProcessSharePointFile.cs` (update)

**Requirements:**
- [ ] Update to use `Config.GetConfigurationFromSharePoint()` instead of hardcoded values
- [ ] Read Source Folder path from configuration
- [ ] Read Destination Folder path from configuration
- [ ] Read Template File URL from configuration
- [ ] Read processing settings (max size, timeout, retries) from configuration
- [ ] Update metadata field names to match SharePoint columns
- [ ] Handle missing configuration (return error)

**Dependencies:** Task 2.1

**Estimated Time:** 2-3 hours

---

### Task 2.5: Error Handling - Use Error Folders from Configuration

**File:** `SMEPilot.FunctionApp/Functions/ProcessSharePointFile.cs` (update)

**Requirements:**
- [ ] Update error handling to use configured error folders:
  - `RejectedDocs` folder (in Source Folder location)
  - `FailedDocs` folder (in Source Folder location)

- [ ] Move files to appropriate folder:
  - Validation failures → RejectedDocs
  - Processing failures → FailedDocs

- [ ] Update error messages to include folder paths

**Dependencies:** Task 2.4

**Estimated Time:** 1-2 hours

---

## 🔗 Phase 3: Integration & Testing (Day 5)

**Goal:** Integrate SPFx and Function App, test end-to-end flow.

### Task 3.1: End-to-End Installation Test

**Requirements:**
- [ ] Deploy SPFx solution to SharePoint
- [ ] Add Admin Panel web part to page
- [ ] Run installation flow:
  1. Fill configuration form
  2. Save configuration
  3. Verify SMEPilotConfig list created
  4. Verify metadata columns created
  5. Verify error folders created
  6. Verify webhook subscription created
  7. Verify subscription ID stored

- [ ] Test configuration validation
- [ ] Test error handling (invalid paths, missing files, etc.)

**Dependencies:** Phase 1 & 2 complete

**Estimated Time:** 3-4 hours

---

### Task 3.2: Document Enrichment Flow Test

**Requirements:**
- [ ] Upload test document to Source Folder
- [ ] Verify webhook triggers Function App
- [ ] Verify document is processed:
  - File downloaded
  - Template applied
  - Enriched document saved to Destination Folder
  - Metadata updated on source document

- [ ] Test edge cases:
  - Large file (>50MB) → RejectedDocs
  - Invalid format → RejectedDocs
  - Processing timeout → FailedDocs
  - Duplicate upload → Idempotency check

**Dependencies:** Task 3.1

**Estimated Time:** 2-3 hours

---

### Task 3.3: Webhook Renewal Test

**Requirements:**
- [ ] Manually trigger webhook renewal function
- [ ] Verify subscription is renewed before expiration
- [ ] Verify subscription ID updated in SMEPilotConfig
- [ ] Test with expired subscription
- [ ] Verify logs in Application Insights

**Dependencies:** Task 2.3

**Estimated Time:** 1-2 hours

---

## 🤖 Phase 4: Copilot Agent Configuration (Day 6)

**Goal:** Configure O365 Copilot Agent (no code changes, configuration only).

### Task 4.1: Copilot Studio Setup

**Requirements:**
- [ ] Access Microsoft 365 Admin Center → Copilot Studio
- [ ] Create new Copilot Agent or use existing
- [ ] Configure data source:
  - Set to Destination Folder (from SMEPilotConfig)
  - Enable Microsoft Search integration

- [ ] Configure system prompt:
  - Add custom prompt from SMEPilotConfig (`CopilotPrompt`)
  - Set to analyze enriched documents
  - Set response format

- [ ] Configure access points:
  - Teams: Enable if `AccessTeams = true`
  - Web: Enable if `AccessWeb = true`
  - O365: Enable if `AccessO365 = true`

- [ ] Deploy Copilot Agent

**Dependencies:** Phase 3 complete (enriched documents exist)

**Estimated Time:** 2-3 hours (configuration only, no code)

---

### Task 4.2: Copilot Agent Testing

**Requirements:**
- [ ] Test Copilot Agent in Teams:
  - Ask question: "@SMEPilot What is the alert configuration?"
  - Verify answer with citations
  - Verify source links work

- [ ] Test Copilot Agent in Web:
  - Access via SharePoint portal
  - Ask questions
  - Verify responses

- [ ] Test Copilot Agent in O365:
  - Access via Word/Excel/PPT
  - Ask questions
  - Verify responses

- [ ] Test edge cases:
  - No relevant documents → "No results" message
  - User lacks permissions → Security trimming
  - Recent documents → Indexing delay handling

**Dependencies:** Task 4.1

**Estimated Time:** 2-3 hours

---

## 📝 Phase 5: Documentation & Deployment (Day 7)

**Goal:** Finalize documentation and prepare for production deployment.

### Task 5.1: Installation Guide

**Requirements:**
- [ ] Create step-by-step installation guide:
  - Prerequisites (Azure AD App Registration, Function App)
  - SPFx deployment steps
  - Configuration steps
  - Verification steps

- [ ] Include screenshots
- [ ] Include troubleshooting section

**Dependencies:** All phases complete

**Estimated Time:** 2-3 hours

---

### Task 5.2: User Guide

**Requirements:**
- [ ] Create user guide:
  - How to upload documents
  - How to access Copilot Agent
  - How to query documents
  - FAQ section

**Dependencies:** All phases complete

**Estimated Time:** 1-2 hours

---

### Task 5.3: Production Deployment Checklist

**Requirements:**
- [ ] Create deployment checklist:
  - Pre-deployment validation
  - Deployment steps
  - Post-deployment verification
  - Rollback plan

**Dependencies:** All phases complete

**Estimated Time:** 1-2 hours

---

## 📊 Summary

### Task Breakdown by Phase

| Phase | Tasks | Estimated Time | Dependencies |
|-------|-------|----------------|--------------|
| **Phase 1: SPFx Admin Panel** | 8 tasks | 20-28 hours | None (START HERE) |
| **Phase 2: Function App Updates** | 5 tasks | 13-18 hours | Phase 1 |
| **Phase 3: Integration & Testing** | 3 tasks | 6-9 hours | Phase 1 & 2 |
| **Phase 4: Copilot Agent** | 2 tasks | 4-6 hours | Phase 3 |
| **Phase 5: Documentation** | 3 tasks | 4-7 hours | All phases |
| **TOTAL** | **21 tasks** | **47-68 hours** | **5-7 days** |

### Critical Path

1. **SPFx Admin Panel** (Phase 1) - Foundation, must complete first
2. **Function App Updates** (Phase 2) - Depends on Phase 1
3. **Integration & Testing** (Phase 3) - Depends on Phase 1 & 2
4. **Copilot Agent** (Phase 4) - Depends on Phase 3
5. **Documentation** (Phase 5) - Can be done in parallel with testing

### Recommended Approach

1. **Start with Phase 1, Task 1.1** (Admin Panel UI) - This is the entry point
2. **Complete Phase 1 fully** before moving to Phase 2
3. **Test each phase** before moving to next
4. **Document as you go** (don't wait for Phase 5)

---

## ✅ Next Steps

1. **Review this plan** - Confirm approach and timeline
2. **Start Phase 1, Task 1.1** - Create Admin Panel configuration form
3. **Work through tasks sequentially** - Complete each task before moving to next
4. **Test frequently** - Don't wait until the end to test

---

**Ready to start? Let's begin with Phase 1, Task 1.1!** 🚀

