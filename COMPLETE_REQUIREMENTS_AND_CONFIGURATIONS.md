# ✅ SMEPilot - Complete Requirements & Configurations

> **Purpose:** Complete documentation covering all requirements, configurations, edge cases, permissions, and access points.

---

## 🎯 System Overview

**SMEPilot** is a knowledge base application for **functional and technical documents** with two main functionalities:

1. **Document Enrichment** - Automatically formats uploaded documents using company template
2. **Copilot Agent** - Assists users with questions from enriched documents

**Both functionalities work immediately after app installation** once configuration is complete.

---

## 📋 Part 1: Document Enrichment

### What It Does

- **Simple Template-Based Formatting:**
  - User uploads a document to configured source folder
  - System applies company template formatting (rule-based, NO AI)
  - Enriched document saved to configured destination folder
  - Metadata updated on source document

### Key Features

- ✅ **Rule-Based Processing** (NO AI)
- ✅ **OpenXML-Based** - Uses DocumentFormat.OpenXml library
- ✅ **Idempotent** - Safe to reprocess same file
- ✅ **Automatic** - Triggered via webhooks on file upload

### Configuration Required (During Installation)

1. **Source Folder (Input)**
   - Where users upload documents
   - Example: `/Shared Documents/RawDocs`
   - Must exist and be accessible

2. **Destination Folder (Output)**
   - Where enriched documents are stored
   - ⚠️ **REQUIRED NAME:** "SMEPilot Enriched Docs"
   - Example: `/Shared Documents/SMEPilot Enriched Docs`
   - Will be created if doesn't exist

3. **Template File**
   - Company template (.dotx file)
   - Example: `/Templates/UniversalOrgTemplate.dotx`
   - Must exist and be accessible

---

## 📋 Part 2: Copilot Agent

### What It Does

- **Assists Users:**
  - User asks question via O365 Copilot (Teams/Web)
  - Copilot searches "SMEPilot Enriched Docs" library
  - Returns answer with citations from enriched documents
  - Uses custom instructions for consistent responses

### Key Features

- ✅ **O365 Copilot** (NOT custom bot development)
- ✅ **Direct SharePoint Query** (NO Function App involvement)
- ✅ **Custom Instructions** - Set as system prompt
- ✅ **Multiple Access Points** - Teams, Web, or Both

### Configuration Required (During Installation)

1. **Enable Copilot Agent**
   - Yes/No flag
   - Default: Yes

2. **Access Points**
   - Microsoft Teams
   - Web Interface
   - Both (Teams + Web)

3. **Visibility Group**
   - AD group or users who can access
   - Example: `Everyone` or `SMEPilot Users`

---

## 🔧 Installation-Time Configuration

### All Configurations Provided During Installation

When installing the app in SharePoint, **ALL configurations must be provided** via SPFx Admin Panel:

#### Document Enrichment Configuration

| Setting | Type | Required | Description |
|---------|------|----------|-------------|
| `SourceLibraryUrl` | Text | ✅ Yes | Source folder path (where documents uploaded) |
| `TargetLibraryUrl` | Text | ✅ Yes | Destination folder (MUST be "SMEPilot Enriched Docs") |
| `TemplateFileUrl` | Text | ✅ Yes | Template file path (.dotx) |

#### Copilot Agent Configuration

| Setting | Type | Required | Description |
|---------|------|----------|-------------|
| `EnableCopilotAgent` | Yes/No | ✅ Yes | Enable/disable Copilot |
| `CopilotAccessPoints` | Multi-select | ✅ Yes | Teams, Web, or Both |
| `VisibilityGroup` | Text | ✅ Yes | AD group for access control |

#### Processing Settings

| Setting | Type | Required | Description |
|---------|------|----------|-------------|
| `MaxFileSizeMB` | Number | ✅ Yes | Maximum file size (default: 50) |
| `RetryAttempts` | Number | ✅ Yes | Retry attempts (default: 3) |

#### Notification Settings

| Setting | Type | Required | Description |
|---------|------|----------|-------------|
| `NotificationEmail` | Text | ✅ Yes | Admin email for alerts |

**See:** `Knowledgebase/INSTALLATION_TIME_CONFIGURATION.md` for detailed UI mockup

---

## ⚠️ Edge Cases & Error Handling

### File Processing Edge Cases

- ✅ **Large files (≤50MB)** - Process within 60 seconds, notify if exceeds
- ✅ **Unsupported formats** - Move to `RejectedDocs` folder, log error
- ✅ **Corrupted files** - Move to `RejectedDocs`, skip processing
- ✅ **Duplicate uploads** - Idempotency check, skip if already processed
- ✅ **Concurrent processing** - Lock mechanism prevents duplicates
- ✅ **File locked/in use** - Retry 3x (2s, 4s, 8s delays), move to `FailedDocs` after 3 tries
- ✅ **Network failures** - Retry with exponential backoff (3 attempts)
- ✅ **Template missing** - Notify admin via email, skip enrichment
- ✅ **Permission denied** - Log error, notify admin via email
- ✅ **Destination folder full** - Alert admin via email, pause processing
- ✅ **Config not found** - Log critical error, send email to admin

### Copilot Edge Cases

- ✅ **No enriched documents** - Return "No documents available" message
- ✅ **Query timeout** - Return partial results or timeout message
- ✅ **Invalid queries** - Return clarification request
- ✅ **Permission denied** - Return "Access denied" message
- ✅ **Service unavailable** - Return "Service temporarily unavailable"

### Configuration Edge Cases

- ✅ **Invalid folder paths** - Validate and show error
- ✅ **Missing permissions** - Check and require admin permissions
- ✅ **Template not found** - Validate template exists
- ✅ **Configuration save failure** - Retry and log error

**See:** `Knowledgebase/EDGE_CASES_AND_PERMISSIONS.md` for detailed edge cases

---

## 🔐 Minimal Permissions Required

### During Installation (Admin)

- **Site Collection Admin** or **Site Owner**
  - Required for:
    - Creating libraries/folders
    - Creating SMEPilotConfig list
    - Creating metadata columns
    - Setting up webhook subscriptions
    - Configuring Copilot access

### Azure AD App Registration (Application Permissions)

- `Sites.ReadWrite.All` - Read/write documents and metadata
- `Files.ReadWrite.All` - Read/write files
- `Webhooks.ReadWrite.All` - Manage webhook subscriptions

**Note:** `Sites.Manage.All` is optional (only if installer needs to create libraries)

### Runtime Permissions

- **Source Folder:** Read, Write, Create (for users)
- **Destination Folder:** Read, Write, Create (for Function App)
- **Templates Library:** Read (for Function App)
- **SMEPilotConfig List:** Read (for Function App), Write (for Admin)
- **Enriched Docs Library:** Read (for users and Copilot)

### User Permissions

- **Business Users:** Contribute permission to Source Folder
- **End Users:** Read permission to "SMEPilot Enriched Docs" library
- **Admin:** Site Collection Admin for installation

**See:** `Knowledgebase/REQUIREMENTS_AND_ARCHITECTURE.md` for detailed permissions

---

## 📍 Copilot Access Points

Once Copilot Agent is installed, it can be accessed from:

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

**Configuration:** Set during installation (Teams, Web, or Both)

---

## 🏗️ Architecture Overview

### Components

1. **SharePoint Online**
   - Source Folder (input - configurable)
   - Templates Library
   - SMEPilot Enriched Docs (output - required name)
   - SMEPilotConfig list (configuration storage)

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

**See:** `Knowledgebase/Diagrams/SMEPilot_Architecture_Diagram.drawio` for visual architecture

---

## 🔄 Process Flows

### Document Enrichment Flow

1. User uploads `.docx` to Source Folder
2. SharePoint sends webhook to Function App
3. Function App validates and checks idempotency
4. Function App downloads file via Graph API
5. Function App reads configuration from SMEPilotConfig
6. Function App downloads template file
7. Function App applies template formatting (rule-based)
8. Function App uploads enriched document to "SMEPilot Enriched Docs"
9. Function App updates source document metadata
10. Microsoft Search indexes enriched document

**See:** `Knowledgebase/Diagrams/SMEPilot_Enrichment_Flow.drawio` for detailed flow

### Copilot Query Flow

1. User asks question via O365 Copilot (Teams/Web)
2. O365 Copilot queries SharePoint "SMEPilot Enriched Docs" library directly
3. O365 Copilot generates answer using custom instructions
4. O365 Copilot returns answer with citations

**Note:** Copilot queries SharePoint directly - NO Function App involvement.

**See:** `Knowledgebase/Diagrams/SMEPilot_Copilot_Flow.drawio` for Copilot flow

---

## 📊 Knowledge Base Use Case

### Purpose

Store and query **functional and technical documents**:
- **Functional Documents:** Process flows, user guides, requirements
- **Technical Documents:** API docs, architecture, troubleshooting guides

### Business Context

Organizations maintain internal documentation (technical, functional, support, or process-related) in SharePoint. These documents are created by multiple teams with inconsistent formatting, incomplete sections, and varying quality. **SMEPilot** automates document standardization and makes content discoverable through Microsoft 365 Copilot.

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

## ✅ Post-Installation Checklist

After installing the app:

### Document Enrichment
- [ ] Source folder exists and is accessible
- [ ] Destination folder exists or can be created
- [ ] Template file exists
- [ ] Webhook subscription created
- [ ] Test upload works (upload test .docx file)
- [ ] Enriched document appears in "SMEPilot Enriched Docs"
- [ ] Metadata updated on source document

### Copilot Agent
- [ ] Copilot enabled in configuration
- [ ] Access points configured (Teams/Web/Both)
- [ ] Visibility group set
- [ ] O365 Copilot configured in Copilot Studio
- [ ] Data source set to "SMEPilot Enriched Docs" library
- [ ] Custom instructions added
- [ ] Test query works (ask question via Teams/Web)
- [ ] Answer returned with citations

---

## 📚 Documentation Files

### Requirements & Architecture
- **`Knowledgebase/REQUIREMENTS_CLARITY.md`** - Requirements summary
- **`Knowledgebase/REQUIREMENTS_AND_ARCHITECTURE.md`** - Complete detailed requirements
- **`Knowledgebase/ARCHITECTURE_DIAGRAM.md`** - System architecture details

### Configuration
- **`Knowledgebase/INSTALLATION_TIME_CONFIGURATION.md`** ⭐ - Installation-time configuration guide
- **`Knowledgebase/Diagrams/SMEPilot_Configuration_Diagram.drawio`** - Configuration diagram

### Diagrams
- **`Knowledgebase/Diagrams/SMEPilot_Architecture_Diagram.drawio`** - System architecture
- **`Knowledgebase/Diagrams/SMEPilot_Enrichment_Flow.drawio`** - Document enrichment flow
- **`Knowledgebase/Diagrams/SMEPilot_Copilot_Flow.drawio`** - Copilot query flow

### Installation & Setup
- **`Knowledgebase/INSTALLATION_AND_CONFIGURATION.md`** - Installation steps
- **`Knowledgebase/QUICK_START_COPILOT.md`** - Copilot setup guide

### Reference
- **`Knowledgebase/EDGE_CASES_AND_PERMISSIONS.md`** - Edge cases and permissions details
- **`Knowledgebase/DIAGRAMS_SUMMARY.md`** - Diagrams overview

---

## 🎯 Key Decisions

1. **NO AI:** All enrichment is rule-based using OpenXML transformations
2. **NO Database:** All data stored in SharePoint (metadata + files)
3. **Destination Name:** MUST be "SMEPilot Enriched Docs" for Copilot integration
4. **Copilot Approach:** O365 Copilot with custom instructions (NOT custom bot)
5. **Idempotency:** Safe to reprocess same file multiple times
6. **Webhook Renewal:** Automatic renewal every 2 days
7. **Configuration:** All configurations provided during installation
8. **Both Parts Work:** Document Enrichment and Copilot both work after installation

---

## ✅ Status

- ✅ **Requirements:** Clear and documented
- ✅ **Flow Diagram:** Complete with all steps and decision points
- ✅ **Architecture Diagram:** Complete with all components and data flows
- ✅ **Configuration Diagram:** Complete with all parameters
- ✅ **Edge Cases:** Documented
- ✅ **Permissions:** Documented (minimal permissions)
- ✅ **Access Points:** Documented (Teams, Web, O365 Copilot)
- ✅ **Installation Configuration:** Complete guide

**Ready for implementation and deployment!**

---

**Last Updated:** Current date
**Status:** ✅ All requirements, configurations, and diagrams complete

