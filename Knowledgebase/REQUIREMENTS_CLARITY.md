# 📋 SMEPilot - Requirements Clarity Document

> **Purpose:** This document provides clear, concise requirements for SMEPilot (DocuPilot) system.

---

## 🎯 System Purpose

**SMEPilot** automatically converts raw "scratch" documents uploaded to SharePoint into standardized, company-branded document templates so Microsoft 365 Copilot can reliably find, reference, and answer questions from those documents.

---

## ✅ Core Requirements Summary

### 1. Document Enrichment (Rule-Based, NO AI)

**What it does:**
- Automatically processes `.docx` files uploaded to a configured source folder
- Applies company template formatting using rule-based OpenXML transformations
- Organizes content into standard sections: Overview, Functional Details, Technical Details, Troubleshooting, Revision History
- Extracts and preserves images
- Generates Table of Contents (TOC)
- Formats revision tables

**Key Constraints:**
- ❌ **NO AI** - All logic is rule-based
- ❌ **NO Database** - All data stored in SharePoint metadata/files
- ✅ **OpenXML-based** - Uses DocumentFormat.OpenXml library
- ✅ **Idempotent** - Can safely reprocess same file

**Input:**
- Raw `.docx` files uploaded to Source Folder (configurable)

**Output:**
- Enriched `.docx` files saved to "SMEPilot Enriched Docs" library (required name)
- Metadata updated on source document

---

### 2. Auto-Trigger via Webhooks

**What it does:**
- Monitors configured source folder for new/updated documents
- Uses Microsoft Graph webhooks for real-time notifications
- Automatically processes files when uploaded or modified

**Technical Details:**
- Webhook subscription created during installation
- Subscription renews every 2 days automatically
- Function App endpoint: `/api/ProcessSharePointFile`
- Handles Graph API validation handshake

---

### 3. Configuration Management

**Storage:**
- SharePoint List: `SMEPilotConfig` (per site collection)
- Contains all installation-time settings

**Required Configurations:**

| Setting | Type | Description | Example |
|---------|------|-------------|---------|
| `SourceLibraryUrl` | Text | Source folder path | `/Shared Documents/RawDocs` |
| `TargetLibraryUrl` | Text | Destination folder (MUST be "SMEPilot Enriched Docs") | `/Shared Documents/SMEPilot Enriched Docs` |
| `TemplateFileUrl` | Text | Template file path | `/Templates/UniversalOrgTemplate.dotx` |
| `NotificationEmail` | Text | Admin email for alerts | `admin@company.com` |
| `EnableCopilotAgent` | Yes/No | Enable Copilot integration | `Yes` |
| `VisibilityGroup` | Text | AD group for access | `Everyone` |
| `MaxFileSizeMB` | Number | Max file size | `50` |
| `RetryAttempts` | Number | Retry attempts | `3` |

**Configuration UI:**
- SPFx Admin Panel for installation-time configuration
- Validates folders, permissions, template existence

---

### 4. Metadata Management

**Source Document Metadata (Updated after processing):**
- `SMEPilot_Enriched` (Yes/No) - Whether document was enriched
- `SMEPilot_Status` (Text) - Processing status: `Enriched`, `Failed`, `Processing`, `Rejected`
- `SMEPilot_EnrichedFileUrl` (Text) - Link to enriched document
- `SMEPilot_ProcessedDate` (Date) - When processing completed
- `SMEPilot_ErrorMessage` (Text) - Error message if failed

**Metadata Columns:**
- Created automatically during installation
- Updated by Function App after processing

---

### 5. Microsoft 365 Copilot Integration

**What it does:**
- Enriched documents are indexed by Microsoft Search
- O365 Copilot queries SharePoint directly (NO Function App involvement)
- Returns answers with citations from enriched documents

**Configuration:**
- Configured in O365 Copilot Studio (NOT custom development)
- Data source: "SMEPilot Enriched Docs" library
- Custom instructions set as system prompt:
  > "You are SMEPilot — the organization's knowledge assistant. Use only documents from the 'SMEPilot Enriched Docs' SharePoint library as the primary evidence. Provide a short summary (2-3 lines), then give a numbered step list for procedures or troubleshooting. Always cite the document title and link. If uncertain, say 'I couldn't find a definitive answer in SMEPilot docs.' Do not invent facts beyond the indexed docs."

**Access Points:**
- Microsoft Teams
- Web Interface
- O365 Copilot (integrated in Office apps)

---

## 🏗️ System Architecture Overview

### Components

1. **SharePoint Online**
   - Source Folder (input)
   - Templates Library
   - SMEPilot Enriched Docs (output - indexed by Copilot)
   - SMEPilotConfig list (configuration)

2. **Azure Function App**
   - `/api/ProcessSharePointFile` - Webhook receiver
   - DocumentEnricherService - Template application
   - GraphHelper - SharePoint operations
   - RuleBasedFormatter - Content mapping
   - TemplateFiller - Template population

3. **Microsoft Graph API**
   - Webhook subscriptions
   - File operations (download/upload)
   - Metadata operations

4. **O365 Copilot**
   - Configured in Copilot Studio
   - Queries SharePoint directly
   - Returns answers with citations

---

## 🔄 Process Flow

### Document Enrichment Flow

1. **User Upload** → User uploads `.docx` to Source Folder
2. **Webhook Trigger** → SharePoint sends notification to Function App
3. **Validation** → Function App validates notification and checks idempotency
4. **Download** → Function App downloads file via Graph API
5. **Read Config** → Function App reads configuration from SMEPilotConfig list
6. **Get Template** → Function App downloads template file
7. **Enrich** → Function App applies template formatting (rule-based)
8. **Upload** → Function App uploads enriched document to "SMEPilot Enriched Docs"
9. **Update Metadata** → Function App updates source document metadata
10. **Index** → Microsoft Search indexes enriched document

### Copilot Query Flow

1. **User Query** → User asks question via O365 Copilot (Teams/Web)
2. **Search** → O365 Copilot queries SharePoint "SMEPilot Enriched Docs" library
3. **Generate Answer** → O365 Copilot generates answer using custom instructions
4. **Return** → O365 Copilot returns answer with citations

**Note:** Copilot queries SharePoint directly - NO Function App involvement.

---

## 🔐 Permissions Required

### Azure AD App Registration (Application Permissions)
- `Sites.ReadWrite.All` - Read/write documents and metadata
- `Files.ReadWrite.All` - Read/write files
- `Webhooks.ReadWrite.All` - Manage webhook subscriptions

### SharePoint Site Permissions
- **Source Folder:** Read, Write, Create
- **Destination Folder:** Read, Write, Create
- **Templates Library:** Read
- **SMEPilotConfig List:** Read, Write

### User Permissions
- **Business Users:** Contribute permission to Source Folder
- **End Users:** Read permission to "SMEPilot Enriched Docs" library
- **Admin:** Site Collection Admin for installation

---

## ⚠️ Edge Cases & Error Handling

### File Processing
- **Large files (≤50MB):** Process within 60 seconds, notify if exceeds
- **Unsupported formats:** Move to `RejectedDocs` folder, log error
- **Corrupted files:** Move to `RejectedDocs`, skip processing
- **Duplicate uploads:** Idempotency check, skip if already processed
- **Concurrent processing:** Lock mechanism prevents duplicates
- **File locked/in use:** Retry 3x (2s, 4s, 8s delays), move to `FailedDocs` after 3 tries
- **Network failures:** Retry with exponential backoff (3 attempts)
- **Template missing:** Notify admin via email, skip enrichment
- **Permission denied:** Log error, notify admin via email

### Copilot
- **No enriched documents:** Return "No documents available" message
- **Query timeout:** Return partial results or timeout message
- **Invalid queries:** Return clarification request
- **Permission denied:** Return "Access denied" message

---

## 📊 Success Criteria

### Installation Validation
- ✅ Source folder exists and is accessible
- ✅ Destination folder exists or can be created
- ✅ Template file exists
- ✅ Required permissions granted
- ✅ Metadata columns can be created
- ✅ Webhook subscription can be created

### Runtime Validation
- ✅ File processing works for all supported formats (≤50MB within 60 seconds)
- ✅ Template formatting applies correctly (no empty TOC, no blank pages)
- ✅ Metadata updates successfully
- ✅ O365 Copilot configured and queries return relevant answers
- ✅ Error handling works for all edge cases
- ✅ Logging captures all operations (Application Insights)
- ✅ Webhook renewal works (every 2 days)
- ✅ Admin notifications sent for failures

---

## 🚀 Implementation Status

### ✅ Completed
- Document enrichment (rule-based, OpenXML)
- Webhook processing
- Configuration management
- Metadata updates
- Error handling
- Edge case handling
- Architecture documentation

### ⚠️ Needs Configuration
- O365 Copilot setup in Copilot Studio
- Custom instructions configuration
- Teams deployment

---

## 📝 Key Decisions

1. **NO AI:** All enrichment is rule-based using OpenXML transformations
2. **NO Database:** All data stored in SharePoint (metadata + files)
3. **Destination Name:** MUST be "SMEPilot Enriched Docs" for Copilot integration
4. **Copilot Approach:** O365 Copilot with custom instructions (NOT custom bot)
5. **Idempotency:** Safe to reprocess same file multiple times
6. **Webhook Renewal:** Automatic renewal every 2 days

---

## 📚 Related Documentation

- **`REQUIREMENTS_AND_ARCHITECTURE.md`** - Complete detailed requirements
- **`ARCHITECTURE_DIAGRAM.md`** - System architecture details
- **`QUICK_START_COPILOT.md`** - Copilot setup guide
- **`INSTALLATION_AND_CONFIGURATION.md`** - Installation steps
- **`EDGE_CASES_AND_PERMISSIONS.md`** - Edge cases and permissions details

---

**Last Updated:** Based on current implementation status
**Status:** ✅ Requirements Clear and Documented

