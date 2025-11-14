# 🔧 SMEPilot - Installation-Time Configuration Guide

> **Purpose:** This document details ALL configuration options that must be provided during SharePoint app installation.

---

## 🎯 Overview

When installing SMEPilot in SharePoint, **ALL configurations must be provided during installation** via the SPFx Admin Panel. This ensures both functionalities work immediately after installation:

1. **Document Enrichment** - Automatically formats uploaded documents
2. **Copilot Agent** - Assists users with questions from enriched documents

---

## 📋 Installation Configuration UI

### Configuration Panel Layout

```
┌─────────────────────────────────────────────────────────────────────┐
│                    SMEPilot Installation Configuration                │
├─────────────────────────────────────────────────────────────────────┤
│                                                                       │
│  ╔═══════════════════════════════════════════════════════════════╗  │
│  ║  PART 1: Document Enrichment Configuration                    ║  │
│  ╚═══════════════════════════════════════════════════════════════╝  │
│                                                                       │
│  📁 Source Folder (Where documents are uploaded)                    │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │ [Browse Folder...]                                           │  │
│  │ Selected: /Shared Documents/RawDocs                          │  │
│  └─────────────────────────────────────────────────────────────┘  │
│  ℹ️ Users will upload .docx files to this folder                  │
│  ⚠️ Folder must exist and be accessible                            │
│                                                                       │
│  📁 Destination Folder (Where enriched documents are stored)       │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │ [Browse Folder...]                                           │  │
│  │ Selected: /Shared Documents/SMEPilot Enriched Docs         │  │
│  └─────────────────────────────────────────────────────────────┘  │
│  ℹ️ Enriched documents will be saved here                         │
│  ⚠️ REQUIRED NAME: "SMEPilot Enriched Docs" (for Copilot)         │
│  ✅ Folder will be created if it doesn't exist                    │
│                                                                       │
│  📄 Template Selection                                             │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │ [Browse Template...]                                         │  │
│  │ Selected: /Templates/UniversalOrgTemplate.dotx               │  │
│  └─────────────────────────────────────────────────────────────┘  │
│  ℹ️ Company template for document formatting                      │
│  ⚠️ Template file must exist                                      │
│                                                                       │
│  ╔═══════════════════════════════════════════════════════════════╗  │
│  ║  PART 2: Copilot Agent Configuration                          ║  │
│  ╚═══════════════════════════════════════════════════════════════╝  │
│                                                                       │
│  ☑ Enable Copilot Agent                                            │
│  ℹ️ Copilot will assist users with questions from enriched docs     │
│                                                                       │
│  📍 Copilot Access Points                                           │
│  ○ Microsoft Teams only                                             │
│  ○ Web Interface only                                               │
│  ● Both (Teams + Web)                                               │
│                                                                       │
│  👥 Visibility Group                                                │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │ [Enter AD Group or Users...]                                 │  │
│  │ Example: Everyone, or "SMEPilot Users"                      │  │
│  └─────────────────────────────────────────────────────────────┘  │
│  ℹ️ Who can access enriched documents and use Copilot              │
│                                                                       │
│  ╔═══════════════════════════════════════════════════════════════╗  │
│  ║  PART 3: Processing Settings                                  ║  │
│  ╚═══════════════════════════════════════════════════════════════╝  │
│                                                                       │
│  📊 Maximum File Size (MB)                                           │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │ [50] MB                                                      │  │
│  └─────────────────────────────────────────────────────────────┘  │
│  ℹ️ Files larger than this will be rejected                      │
│                                                                       │
│  🔄 Retry Attempts                                                  │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │ [3] attempts                                                 │  │
│  └─────────────────────────────────────────────────────────────┘  │
│  ℹ️ Number of retries for failed processing                       │
│                                                                       │
│  ╔═══════════════════════════════════════════════════════════════╗  │
│  ║  PART 4: Notification Settings                                ║  │
│  ╚═══════════════════════════════════════════════════════════════╝  │
│                                                                       │
│  📧 Admin Email for Alerts                                          │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │ [admin@company.com]                                         │  │
│  └─────────────────────────────────────────────────────────────┘  │
│  ℹ️ Email address for failure notifications and alerts            │
│                                                                       │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │ [Cancel]                    [Validate & Install]            │  │
│  └─────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 📝 Configuration Parameters

### Part 1: Document Enrichment Configuration

#### 1. Source Folder (Input)
- **Parameter:** `SourceLibraryUrl`
- **Type:** Text (Folder Path)
- **Required:** ✅ Yes
- **Description:** Where users upload raw documents (.docx files)
- **Example:** `/Shared Documents/RawDocs` or `/Shared Documents/SourceDocs`
- **Validation:**
  - Folder must exist
  - Must be accessible (read/write permissions)
  - Must be a valid SharePoint folder path

#### 2. Destination Folder (Output)
- **Parameter:** `TargetLibraryUrl`
- **Type:** Text (Folder Path)
- **Required:** ✅ Yes
- **Description:** Where enriched documents are stored
- **⚠️ REQUIRED NAME:** MUST contain "SMEPilot Enriched Docs"
- **Example:** `/Shared Documents/SMEPilot Enriched Docs`
- **Validation:**
  - Folder will be created if it doesn't exist
  - Must be accessible (read/write permissions)
  - **CRITICAL:** Name must be "SMEPilot Enriched Docs" for Copilot integration

#### 3. Template Selection
- **Parameter:** `TemplateFileUrl`
- **Type:** Text (File Path)
- **Required:** ✅ Yes
- **Description:** Company template file (.dotx) for document formatting
- **Example:** `/Templates/UniversalOrgTemplate.dotx`
- **Validation:**
  - Template file must exist
  - Must be a valid .dotx file
  - Must be accessible (read permission)

---

### Part 2: Copilot Agent Configuration

#### 4. Enable Copilot Agent
- **Parameter:** `EnableCopilotAgent`
- **Type:** Yes/No (Boolean)
- **Required:** ✅ Yes
- **Description:** Enable/disable Copilot integration
- **Default:** Yes
- **Note:** If disabled, only document enrichment will work

#### 5. Copilot Access Points
- **Parameter:** `CopilotAccessPoints`
- **Type:** Multi-select (Teams, Web, Both)
- **Required:** ✅ Yes (if Copilot enabled)
- **Options:**
  - Microsoft Teams
  - Web Interface
  - Both (Teams + Web)
- **Default:** Both

#### 6. Visibility Group
- **Parameter:** `VisibilityGroup`
- **Type:** Text (AD Group or Users)
- **Required:** ✅ Yes (if Copilot enabled)
- **Description:** Who can access enriched documents and use Copilot
- **Example:** `Everyone` or `SMEPilot Users` (AD group)
- **Validation:**
  - Must be valid AD group or user list
  - Used for access control on enriched documents

---

### Part 3: Processing Settings

#### 7. Maximum File Size
- **Parameter:** `MaxFileSizeMB`
- **Type:** Number
- **Required:** ✅ Yes
- **Description:** Maximum file size for processing (in MB)
- **Default:** 50 MB
- **Validation:**
  - Must be positive number
  - Recommended: 50 MB or less
  - Files larger than this will be rejected

#### 8. Retry Attempts
- **Parameter:** `RetryAttempts`
- **Type:** Number
- **Required:** ✅ Yes
- **Description:** Number of retry attempts for failed processing
- **Default:** 3
- **Validation:**
  - Must be positive number (1-10)
  - Used for network failures, file locks, etc.

---

### Part 4: Notification Settings

#### 9. Admin Email
- **Parameter:** `NotificationEmail`
- **Type:** Text (Email Address)
- **Required:** ✅ Yes
- **Description:** Email address for failure notifications and alerts
- **Example:** `admin@company.com` or `docadmin@company.com`
- **Validation:**
  - Must be valid email format
  - Used for:
    - Processing failures
    - Template missing errors
    - Permission denied errors
    - Large file notifications

---

## ✅ Installation Validation

During installation, the system validates:

1. ✅ **Source Folder:**
   - Exists and is accessible
   - Has read/write permissions
   - Valid SharePoint path

2. ✅ **Destination Folder:**
   - Can be created (if doesn't exist)
   - Has read/write permissions
   - Name contains "SMEPilot Enriched Docs"

3. ✅ **Template File:**
   - Exists
   - Is a valid .dotx file
   - Has read permission

4. ✅ **Permissions:**
   - Azure AD app has required permissions
   - Site Collection Admin permissions available
   - Can create metadata columns
   - Can create webhook subscriptions

5. ✅ **Copilot Configuration:**
   - Access points selected (if enabled)
   - Visibility group is valid
   - Can access "SMEPilot Enriched Docs" library

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

### Azure AD App Registration

- **Application Permissions:**
  - `Sites.ReadWrite.All` - Read/write documents and metadata
  - `Files.ReadWrite.All` - Read/write files
  - `Webhooks.ReadWrite.All` - Manage webhook subscriptions

### Runtime Permissions

- **Source Folder:** Read, Write, Create (for users)
- **Destination Folder:** Read, Write, Create (for Function App)
- **Templates Library:** Read (for Function App)
- **SMEPilotConfig List:** Read (for Function App), Write (for Admin)
- **Enriched Docs Library:** Read (for users and Copilot)

---

## 🎯 Post-Installation

After successful installation:

1. ✅ **Document Enrichment** is active:
   - Users can upload documents to Source Folder
   - Documents are automatically enriched
   - Enriched documents saved to "SMEPilot Enriched Docs"

2. ✅ **Copilot Agent** is active (if enabled):
   - Users can query via Teams or Web
   - Copilot searches "SMEPilot Enriched Docs" library
   - Returns answers with citations

3. ✅ **Configuration** is stored in:
   - SharePoint List: `SMEPilotConfig`
   - Can be updated later without redeploying app

---

## 📊 Configuration Storage

All configurations are stored in:
- **Location:** SharePoint List `SMEPilotConfig`
- **Format:** One row per site collection
- **Access:** Read by Function App, Write by Admin Panel
- **Update:** Can be updated via Admin Panel without code changes

---

## ⚠️ Important Notes

1. **Destination Folder Name:** MUST be "SMEPilot Enriched Docs" for Copilot integration
2. **Template Path:** Should remain constant across sites for consistency
3. **Permissions:** Minimal permissions required - no Global Admin needed
4. **Both Parts Work:** Document Enrichment and Copilot both work after installation
5. **Knowledge Base:** Designed for functional and technical documents

---

## 🚀 Quick Start After Installation

1. Upload a test `.docx` file to Source Folder
2. Wait for enrichment (check Function App logs)
3. Verify enriched document in "SMEPilot Enriched Docs"
4. Test Copilot query (if enabled):
   - Open Teams or Web interface
   - Ask: "What documents mention [topic]?"
   - Verify answer with citations

---

**Status:** ✅ All installation-time configurations documented
**Ready for:** Implementation and deployment

