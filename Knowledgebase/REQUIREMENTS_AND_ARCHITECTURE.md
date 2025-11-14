# 📋 SMEPilot - Requirements & Architecture Documentation

> **Note:** This document is the authoritative source for both implementation and Copilot configuration. Cursor automation and human contributors must follow this version.

## 🎯 Executive Summary

**Purpose:** SMEPilot (DocuPilot) automatically converts raw "scratch" docs uploaded to SharePoint into a standardized, company-branded document template so Microsoft 365 Copilot (or internal users) can reliably find, reference, and answer questions from those documents. 

**Current Scope:** 
- **Enrichment:** Rule-based (NO AI) ✅ **IMPLEMENTED**
- **Storage:** NO Database persistence (all data in SharePoint metadata/files) ✅ **IMPLEMENTED**
- **Copilot Agent:** O365 Copilot with custom instructions ⚠️ **NEEDS CONFIGURATION**

**Timeline:** 
- **2 days:** Architecture diagram with configurations ✅ **COMPLETED**
- **1 week:** Complete implementation ⚠️ **ENRICHMENT READY, COPILOT NEEDS CONFIGURATION**
- **Note:** Copilot Agent configuration will not require any code changes; it's achieved via Copilot Studio setup in O365.

**Current Status:** 
- Document enrichment: ✅ **FULLY IMPLEMENTED**
- Copilot Agent: ⚠️ **NEEDS CONFIGURATION** (O365 Copilot Studio setup - see `QUICK_START_COPILOT.md`)

---

## 📝 Primary Functional Requirements

1. **Auto-trigger:** On creation or modification of a document in a configured source SharePoint folder, a webhook notifies the Function App.

2. **Rule-based enrichment:** The Function App transforms the raw DOCX into the corporate template (dotx), applying:
   - heading-aware mapping (Overview, Functional, Technical, Troubleshooting, Revision History)
   - proper Table Of Contents (TOC) field insertion
   - revision table formatting
   - image extraction/placement

3. **Save enriched doc:** Enriched DOCX saved to configurable target library/folder ("SMEPilot Enriched Docs").

4. **Metadata:** Source item updated with:
   - `SMEPilot_Enriched` (Yes/No)
   - `SMEPilot_Status` (e.g., Enriched/Failed/Processing)
   - `SMEPilot_EnrichedFileUrl` (link to enriched file)

5. **Copilot readiness:** Documents are stored and surfaced in SharePoint search so Copilot (or Copilot Agent) can index/search them.

---

## 📝 Detailed Requirements

### 1. Core Functionality

#### 1.1 Document Enrichment
- **Input:** Documents uploaded to configured source folder (`.docx` files)
- **Process:** Apply company template formatting using rule-based OpenXML transformations
- **Output:** Enriched documents saved to configured destination folder
- **Simplified:** No AI, no database - just template formatting
- **Standardization Rules:** Content is grouped into organizational sections:
  - *Overview*
  - *Functional Details*
  - *Technical Details*
  - *Troubleshooting*
  - *Revision History*
- **Processing:** Parse & extract (OpenXML): Headings, Paragraphs, Tables, Images, Lists
- **Sanitization:** Remove trailing empty sections, fix table widths, insert bookmarks for TOC

#### 1.2 Copilot Agent ⚠️ **NEEDS CONFIGURATION - NOT CUSTOM DEVELOPMENT**
- **Purpose:** Assist users by answering questions from enriched documents
- **Manager's Requirement:** "Copilot which assists the user" with specific instructions
- **Manager's Instructions:**
  > "You are SMEPilot — the organization's knowledge assistant. Use only documents from the 'SMEPilot Enriched Docs' SharePoint library as the primary evidence. Provide a short summary (2-3 lines), then give a numbered step list for procedures or troubleshooting. Always cite the document title and link. If uncertain, say 'I couldn't find a definitive answer in SMEPilot docs.' Do not invent facts beyond the indexed docs."
- **Approach:** ✅ **O365 Copilot with Custom Instructions** (NOT custom bot development)
- **What Needs to Be Done:**
  1. ✅ Ensure documents saved to "SMEPilot Enriched Docs" library
  2. ✅ Configure O365 Copilot in Copilot Studio
  3. ✅ Set data source to "SMEPilot Enriched Docs" library
  4. ✅ Add manager's instructions as system prompt
  5. ✅ Deploy to Teams
- **Timeline:** 1-2 weeks (mostly configuration, minimal code)
- **See:** `QUICK_START_COPILOT.md` for step-by-step implementation guide

## Non-Functional & Constraints (Manager Decisions)

- **Enrichment:** No AI for enrichment. All logic is rule-based.
- **Storage:** No external DB (Mongo/Cosmos) in the default configuration. All data is stored in SharePoint metadata/files.
- **Security:** Minimal required permissions only (listed below).
- **Extensibility:** Template file is configurable at install-time.

---

### 2. Configuration Requirements

#### 2.1 Installation-Time Configuration
During SharePoint app installation, user must configure:

1. **Source Folder (Input)**
   - Where documents are uploaded
   - Example: `/Shared Documents/Input`
   - Validation: Folder must exist and be accessible

2. **Destination Folder (Output)**
   - Where enriched documents are stored
   - **MUST BE:** `SMEPilot Enriched Docs` (for Copilot integration)
   - Example: `/Shared Documents/SMEPilot Enriched Docs`
   - Validation: Folder must exist or be created
   - **Important:** This library name is required for Copilot Agent to work

3. **Template Selection**
   - Choose template for formatting
   - Default: UniversalOrgTemplate.dotx
   - Validation: Template file must exist

4. **Copilot Settings**
   - Enable/disable Copilot integration (EnableCopilotAgent flag)
   - Configure Copilot access location
   - Set permissions for Copilot queries
   - Set visibility group for enriched documents

5. **Notification Settings**
   - Admin email for failures and alerts (NotificationEmail)
   - Error notification preferences

### 3. Edge Cases

#### 3.1 File Processing Edge Cases
- ✅ **Large files (≤50MB)** - Process within 60 seconds, notify if exceeds limit
- ✅ **Unsupported formats** - Move to `RejectedDocs` folder, log error
- ✅ **Corrupted files** - Log error, move to `RejectedDocs`, skip processing
- ✅ **Duplicate uploads** - Idempotency check, skip if already processed
- ✅ **Concurrent processing** - Lock mechanism to prevent duplicates
- ✅ **File locked/in use** - Retry 3x (2s, 4s, 8s delays), move to `FailedDocs` after 3 tries
- ✅ **File deletion during processing** - Handle gracefully, log warning
- ✅ **Network failures** - Retry with exponential backoff (3 attempts)
- ✅ **Template missing** - Notify admin via email, skip enrichment
- ✅ **Destination folder full** - Alert admin via email, pause processing
- ✅ **Permission denied** - Log error, notify admin via email, require intervention
- ✅ **Config not found** - Log critical error, send email to admin

#### 3.2 Copilot Edge Cases
- ✅ **No enriched documents** - Return "No documents available" message
- ✅ **Query timeout** - Return partial results or timeout message
- ✅ **Invalid queries** - Return clarification request
- ✅ **Permission denied** - Return "Access denied" message
- ✅ **Service unavailable** - Return "Service temporarily unavailable"

#### 3.3 Configuration Edge Cases
- ✅ **Invalid folder paths** - Validate and show error
- ✅ **Missing permissions** - Check and require admin permissions
- ✅ **Template not found** - Validate template exists
- ✅ **Configuration save failure** - Retry and log error

---

## 🏗️ Architecture (Logical)

- **SharePoint Site(s)**
  - Source Folder (where business users upload scratch docs)
  - Templates Library (holds org `.dotx`)
  - 🔍 Enriched Docs Library (destination - "SMEPilot Enriched Docs" - Indexed by Copilot/Search)
  - SMEPilotConfig list (installation & runtime config)

- **Azure Function App (SMEPilot.FunctionApp)**
  - Webhook receiver `/api/ProcessSharePointFile`
  - Config & orchestration
  - Template-based enrichment module (OpenXML)
  - SharePoint client using Microsoft Graph (app-only credentials)

- **Optional (Future)**
  - Copilot Agent registration or Copilot Studio configuration (for advanced indexing/agents)

---

## 🏗️ Architecture Diagram

**Note:** See `ARCHITECTURE_DIAGRAM.md` for detailed component architecture.

### High-Level Architecture (Rule-Based, No DB, No AI)

```
+────────────────────────────────────────────────────────────+
|                       SharePoint Online                    |
|  ┌──────────────────────────────┐   ┌───────────────────┐  |
|  │ Source Library (RawDocs)     │   │ 🔍 Target Library │  |
|  │ - Uploads trigger webhook    │   │ (SMEPilot         │  |
|  │ - Supports .docx only        │   │  Enriched Docs)   │  |
|  │                              │   │  Indexed by       │  |
|  │                              │   │  Copilot/Search   │  |
|  └──────────────┬───────────────┘   └──────────┬────────┘  |
|                 │ Graph Webhook                 │           |
|                 ▼                               │           |
|        +──────────────────────────────────────────────+     |
|        |      Azure Function App (SMEPilot)           |     |
|        |----------------------------------------------|     |
|        | ProcessSharePointFile.cs                     |     |
|        |  - Validates webhook                         |     |
|        |  - Downloads raw file                        |     |
|        | DocumentEnricherService.cs                   |     |
|        |  - OpenXML rule-based formatter              |     |
|        |  - Template mapping engine                   |     |
|        | ConfigService.cs                             |     |
|        |  - Reads SMEPilotConfig list                 |     |
|        | Logging: App Insights                        |     |
|        +──────────────────────────────────────────────+     |
|                 │ Upload enriched doc via Graph API          |
|                 ▼                                            |
|        +──────────────────────────────────────────────+      |
|        |  SPFx Admin App (React)                      |      |
|        |----------------------------------------------|      |
|        | - Library pickers & template upload          |      |
|        | - Saves to SMEPilotConfig list               |      |
|        | - Shows logs & processing status             |      |
|        +──────────────────────────────────────────────+      |
|                                                             |
|  Copilot Studio (Admin)                                     |
|  - Create SMEPilot Agent                                    |
|  - Knowledge source: SMEPilot Enriched Docs library         |
|  - Publish to Teams / Org                                   |
+────────────────────────────────────────────────────────────+
```

### Detailed Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    SharePoint Site (User Interface)              │
│                                                                   │
│  ┌──────────────────┐              ┌──────────────────┐          │
│  │  App Installation │              │  Copilot Agent   │          │
│  │  Configuration   │              │  (Teams/Web)     │          │
│  │  UI              │              │                  │          │
│  └────────┬─────────┘              └────────┬─────────┘          │
│           │                                  │                    │
│           │ Configure                        │ Query              │
│           │                                  │                    │
└───────────┼──────────────────────────────────┼────────────────────┘
            │                                  │
            │                                  │
┌───────────▼──────────────────────────────────▼────────────────────┐
│                    Azure Function App                            │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Configuration Service                                    │   │
│  │  - Store source/destination folders                       │   │
│  │  - Store template selection                              │   │
│  │  - Validate permissions                                  │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Document Processing Pipeline                            │   │
│  │                                                           │   │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐ │   │
│  │  │  Webhook     │→ │  File        │→ │  Template     │ │   │
│  │  │  Listener    │  │  Extractor   │  │  Filler       │ │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘ │   │
│  │         │                  │                  │          │   │
│  │         └──────────────────┼──────────────────┘          │   │
│  │                            │                             │   │
│  │                            ▼                             │   │
│  │                  ┌──────────────┐                        │   │
│  │                  │  Upload to   │                        │   │
│  │                  │  Destination │                        │   │
│  │                  └──────────────┘                        │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Copilot Query Service                                    │   │
│  │  - Receive queries from Copilot agent                     │   │
│  │  - Search enriched documents                              │   │
│  │  - Return answers with sources                            │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
            │                                  │
            │                                  │
            │ Microsoft Graph API              │
            │                                  │
┌───────────▼──────────────────────────────────▼────────────────────┐
│                    SharePoint Online                             │
│                                                                   │
│  ┌──────────────────┐              ┌──────────────────┐          │
│  │  Source Folder   │              │  🔍 Destination  │          │
│  │  (Input)         │              │  Folder          │          │
│  │  - Documents     │              │  (SMEPilot       │          │
│  │    uploaded here │              │   Enriched Docs) │          │
│  │                  │              │  - Indexed by    │          │
│  │                  │              │    Copilot/Search│          │
│  └──────────────────┘              └──────────────────┘          │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Document Library Metadata                                │   │
│  │  - SMEPilot_Enriched (Yes/No)                            │   │
│  │  - SMEPilot_Status (e.g., Enriched/Failed)               │   │
│  │  - SMEPilot_EnrichedFileUrl (link to enriched file)       │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

---

## High-Level Flow

1. Admin installs app / runs `Install-SMEPilot.ps1` to create libraries and SMEPilotConfig entries.
2. User uploads scratch doc to Source Folder.
3. Graph webhook triggers Function App.
4. Function downloads raw doc, applies `TemplateBuilder` (rule-based), writes enriched doc to Enriched folder and updates metadata.
5. SharePoint Search picks up new file; Copilot can use it for queries.

---

## 🔄 Flow Diagram

### Document Enrichment Flow

```
┌─────────────────┐
│ User uploads    │
│ document to     │
│ Source Folder   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ SharePoint      │
│ sends webhook   │
│ notification    │
└────────┬────────┘
         │
         ▼
┌─────────────────┐      ┌─────────────────┐
│ Function App    │─────▶│ Check if already│
│ receives        │      │ processed       │
│ notification    │      └────────┬────────┘
└────────┬────────┘               │
         │                        │
         │                        ▼
         │              ┌─────────────────┐
         │              │ Already         │
         │              │ processed?      │
         │              └────────┬────────┘
         │                       │
         │              ┌────────┴────────┐
         │              │                 │
         │              ▼                 ▼
         │      ┌───────────┐    ┌──────────────┐
         │      │ Skip       │    │ Continue     │
         │      │ processing │    │ processing   │
         │      └───────────┘    └──────┬───────┘
         │                               │
         │                               ▼
         │                      ┌─────────────────┐
         │                      │ Mark as         │
         │                      │ "Processing"    │
         │                      └────────┬────────┘
         │                               │
         │                               ▼
         │                      ┌─────────────────┐
         │                      │ Download file   │
         │                      │ from SharePoint│
         │                      └────────┬────────┘
         │                               │
         │                               ▼
         │                      ┌─────────────────┐
         │                      │ Extract text &  │
         │                      │ images          │
         │                      └────────┬────────┘
         │                               │
         │                               ▼
         │                      ┌─────────────────┐
         │                      │ Apply template  │
         │                      │ formatting      │
         │                      └────────┬────────┘
         │                               │
         │                               ▼
         │                      ┌─────────────────┐
         │                      │ Upload enriched │
         │                      │ doc to          │
         │                      │ Destination     │
         │                      └────────┬────────┘
         │                               │
         │                               ▼
         │                      ┌─────────────────┐
         │                      │ Update metadata │
         │                      │ SMEPilot_Enriched│
         │                      │ = True          │
         │                      └────────┬────────┘
         │                               │
         │                               ▼
         └───────────────────────────────┘
                    │
                    ▼
         ┌─────────────────┐
         │ Processing      │
         │ Complete        │
         └─────────────────┘
```

### Copilot Query Flow

```
┌─────────────────┐
│ User asks       │
│ question via    │
│ O365 Copilot    │
│ (Teams/Web)     │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ O365 Copilot    │
│ queries         │
│ SharePoint      │
│ directly        │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Searches        │
│ "SMEPilot       │
│ Enriched Docs"  │
│ library         │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ O365 Copilot    │
│ generates       │
│ answer with     │
│ citations       │
│ (using custom   │
│ instructions)   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Display answer  │
│ to user         │
└─────────────────┘
```

**Note:** O365 Copilot queries SharePoint directly - NO Function App involvement.

---

## ⚙️ Configuration Specification

### Configuration Storage
- **Location:** SharePoint List `SMEPilotConfig` (per site collection)
- **Format:** SharePoint list with columns for each configuration parameter
- **Access:** Read during app initialization, write during installation via SPFx Admin UI
- **Purpose:** Allows configuration without redeploying Function App

### Configuration Schema (SMEPilotConfig List)

| Column Name | Type | Description | Example |
|------------|------|-------------|---------|
| SourceLibraryUrl | Text | Folder/library where users upload raw docs | `/sites/SMEPilot/Shared Documents/RawDocs` |
| TargetLibraryUrl | Text | Destination for enriched docs | `/sites/SMEPilot/Shared Documents/SMEPilot Enriched Docs` |
| TemplateFileUrl | Text | .dotx file used for enrichment | `/Templates/UniversalOrgTemplate.dotx` |
| NotificationEmail | Text | Admin email for failures and alerts | `docadmin@company.com` |
| EnableCopilotAgent | Yes/No | Boolean flag to enable Copilot integration | `Yes` |
| VisibilityGroup | Text | AD group or users who can view enriched docs | `Everyone` |
| MaxFileSizeMB | Number | Maximum file size for processing | `50` |
| RetryAttempts | Number | Number of retry attempts for failed processing | `3` |

**Note:** TargetLibraryUrl MUST be "SMEPilot Enriched Docs" for Copilot integration to work.

### Configuration UI (During Installation)

```
┌─────────────────────────────────────────────────┐
│  SMEPilot Configuration                          │
├─────────────────────────────────────────────────┤
│                                                  │
│  Source Folder (Input)                          │
│  ┌───────────────────────────────────────────┐ │
│  │ [Browse...] /Shared Documents/Input       │ │
│  └───────────────────────────────────────────┘ │
│  ℹ️ Where documents are uploaded                │
│                                                  │
│  Destination Folder (Output)                    │
│  ┌───────────────────────────────────────────┐ │
│  │ [Browse...] /Shared Documents/Enriched   │ │
│  └───────────────────────────────────────────┘ │
│  ℹ️ Where enriched documents are stored          │
│                                                  │
│  Template Selection                             │
│  ┌───────────────────────────────────────────┐ │
│  │ [Dropdown] UniversalOrgTemplate.dotx     │ │
│  └───────────────────────────────────────────┘ │
│                                                  │
│  Copilot Integration                            │
│  ☑ Enable Copilot                               │
│  Access Location:                               │
│  ○ Teams  ○ Web  ● Both                        │
│                                                  │
│  [Cancel]  [Save Configuration]                 │
└─────────────────────────────────────────────────┘
```

---

## 🔐 Minimal Permissions Required (App / Admin)

### App Registration (Azure AD app; admin consent required)

- `Sites.ReadWrite.All` (application permission)
  - **Purpose:** Read/write site content and metadata
  - **Required:** Yes
  - **Scope:** All sites in tenant

- `Sites.Manage.All` (optional)
  - **Purpose:** Only if installer must create libraries
  - **Required:** No (only if installer needs to create libraries)
  - **Scope:** All sites in tenant

### Installer (human running scripts)

- **Site Owner** or **Site Collection Admin**
  - **Purpose:** For PnP operations in installer
  - **Required:** Yes
  - **Scope:** Target site collection

### End-users

- **Contribute permission** to Source folder
  - **Purpose:** Upload documents to source folder
  - **Required:** Yes

- **Read permission** to Enriched Docs library
  - **Purpose:** View enriched documents
  - **Required:** Yes

---

## 🔐 Permissions Required (Detailed)

### Minimal SharePoint Permissions

#### Application Permissions (Azure AD App Registration)
```
Sites.ReadWrite.All          - Read/write documents
Files.ReadWrite.All          - Read/write files
Webhooks.ReadWrite.All       - Manage change subscriptions (webhook renewal)
Sites.Read.All               - Read site information
User.Read.All                - Read user information (optional)
```

**Authentication:** Managed Identity or Client Credentials (app-only)

#### SharePoint Site Permissions
- **Source Folder:** Read, Write, Create
- **Destination Folder:** Read, Write, Create
- **Document Library:** Read, Write metadata
- **Site Collection:** Read (for folder browsing)

#### Admin Permissions Required
- **Site Collection Admin** - For initial app installation
- **List/Library Admin** - For creating metadata columns
- **No Global Admin Required** - App-level permissions only

### Permission Validation During Installation

```csharp
// Pseudo-code for permission validation
public async Task<ValidationResult> ValidatePermissions(
    string sourceFolderPath,
    string destinationFolderPath)
{
    var results = new ValidationResult();
    
    // Check source folder access
    if (!await CanReadFolder(sourceFolderPath))
        results.Errors.Add("Cannot read source folder");
    
    if (!await CanWriteFolder(sourceFolderPath))
        results.Errors.Add("Cannot write to source folder");
    
    // Check destination folder access
    if (!await CanReadFolder(destinationFolderPath))
        results.Errors.Add("Cannot read destination folder");
    
    if (!await CanWriteFolder(destinationFolderPath))
        results.Errors.Add("Cannot write to destination folder");
    
    // Check metadata column creation
    if (!await CanCreateColumns())
        results.Errors.Add("Cannot create metadata columns");
    
    return results;
}
```

---

## 📍 Copilot Access Points

### 1. Microsoft Teams
- **Location:** Teams app installed in tenant
- **Access:** Via Teams chat or dedicated channel
- **Usage:** Users can ask questions in Teams
- **Example:** "@SMEPilot What is the alert configuration?"

### 2. Web Interface
- **Location:** SharePoint web part or standalone page
- **Access:** Via SharePoint site navigation
- **Usage:** Users can query via web interface
- **Example:** Search box on SharePoint page

### 3. Microsoft Copilot (O365 Copilot)
- **Location:** Integrated with Microsoft Copilot
- **Access:** Via Copilot interface in Office apps
- **Usage:** Users can query from Word, Excel, etc.
- **Example:** "What documents mention API endpoints?"

### Configuration
```json
{
  "copilot": {
    "accessPoints": ["Teams", "Web", "O365Copilot"],
    "defaultAccessPoint": "Teams",
    "requireAuthentication": true
  }
}
```

---

## 🎯 Use Case: Knowledge Base

### Purpose
Store and query functional and technical documents:
- **Functional Documents:** Process flows, user guides, requirements
- **Technical Documents:** API docs, architecture, troubleshooting guides

### Business Context
Organizations often maintain internal documentation (technical, functional, support, or process-related) in SharePoint. These documents are created by multiple teams with inconsistent formatting, incomplete sections, and varying quality. **SMEPilot** automates document standardization and makes content discoverable through Microsoft 365 Copilot.

### Workflow
1. **Upload** - User uploads `.docx` file to Source Library (configurable)
2. **Trigger** - SharePoint webhook triggers Function App
3. **Config** - Function App reads configuration from SMEPilotConfig list
4. **Download** - Function App downloads raw document via Graph API
5. **Enrich** - System applies template formatting using OpenXML rule-based logic
6. **Store** - Enriched document saved to "SMEPilot Enriched Docs" library
7. **Index** - Microsoft Search indexes enriched documents
8. **Query** - User asks questions via O365 Copilot (Teams/Web)
9. **Answer** - Copilot searches indexed documents and returns answer with citations

---

## 📊 Data Flow

### Document Enrichment Flow
```
User Upload → Source Folder → Webhook → Function App
                                              │
                                              ├─→ Extract Content
                                              ├─→ Apply Template
                                              └─→ Upload to Destination
                                                     │
                                                     ▼
                                            Enriched Document (in "SMEPilot Enriched Docs")
```

### Copilot Query Flow
```
User Query → O365 Copilot (Teams/Web)
                     │
                     ▼
            O365 Copilot queries SharePoint directly
                     │
                     ▼
            Searches "SMEPilot Enriched Docs" library
                     │
                     ▼
            Returns answer with citations
```

**Note:** O365 Copilot queries SharePoint directly - NO Function App involvement in queries.

---

## ✅ Validation Checklist

### Installation Validation
- [ ] Source folder exists and is accessible
- [ ] Destination folder exists or can be created
- [ ] Template file exists
- [ ] Required permissions granted
- [ ] Metadata columns can be created
- [ ] Webhook subscription can be created
- [ ] Copilot agent can be deployed

### Runtime Validation
- [ ] File processing works for all supported formats (≤50MB within 60 seconds)
- [ ] Template formatting applies correctly (no empty TOC, no blank pages)
- [ ] Metadata updates successfully
- [ ] O365 Copilot configured and queries return relevant answers
- [ ] Error handling works for all edge cases (RejectedDocs, FailedDocs)
- [ ] Logging captures all operations (Application Insights)
- [ ] Webhook renewal works (every 2 days)
- [ ] Admin notifications sent for failures

---

## 🚀 Implementation Priority

### Phase 1 (Days 1-2): Architecture & Configuration
1. Create architecture diagram ✅ **COMPLETED**
2. Design configuration UI (SPFx Admin App with install wizard)
3. Implement configuration storage (SMEPilotConfig SharePoint list)
4. Document all edge cases ✅ **COMPLETED**
5. Document permissions ✅ **COMPLETED**

### Phase 2 (Days 3-4): Document Enrichment
1. Implement ConfigService to read from SMEPilotConfig list
2. Implement configuration-based folder selection
3. Update file processing to use configured folders
4. Implement DocumentEnricherService with OpenXML rule-based formatter
5. Implement error handling (RejectedDocs, FailedDocs folders)
6. Implement webhook renewal timer (every 2 days)
7. Test all edge cases

### Phase 3 (Days 5-6): Copilot Integration
1. Configure O365 Copilot in Copilot Studio
2. Set data source to "SMEPilot Enriched Docs" library
3. Add manager's custom instructions as system prompt
4. Deploy Copilot to Teams
5. Test query functionality

### Phase 4 (Day 7): Testing & Documentation
1. End-to-end testing
2. User documentation
3. Admin documentation
4. Deployment guide

---

## 📝 Next Steps

1. **Review this document** with team
2. **Validate architecture** with stakeholders
3. **Create detailed technical specs** for each component
4. **Begin implementation** following priority order
5. **Daily progress updates** to manager

