# 📋 SMEPilot - Complete Documentation
## Single Source of Truth for Manager Review

> **Document Version:** 1.0  
> **Date:** 2024-01-01  
> **Status:** Ready for Review  
> **Purpose:** Knowledge Base for Functional & Technical Documents

---

## 📑 Table of Contents

1. [Executive Summary](#executive-summary)
2. [Core Requirements](#core-requirements)
3. [What We Planned to Do](#what-we-planned-to-do)
4. [System Architecture](#system-architecture)
5. [Process Flow](#process-flow)
6. [Installation Configuration](#installation-configuration)
7. [Edge Cases & Error Handling](#edge-cases--error-handling)
8. [Permissions & Security](#permissions--security)
9. [Access Points](#access-points)
10. [Technical Implementation Details](#technical-implementation-details)
11. [Implementation Timeline](#implementation-timeline)
12. [Success Criteria](#success-criteria)

---

## 🎯 Executive Summary

**SMEPilot** is a SharePoint-based knowledge base application designed to automatically standardize and enrich functional and technical documents, making them discoverable and queryable through Microsoft 365 Copilot.

### Key Highlights

- ✅ **Two Main Functionalities:**
  1. **Document Enrichment** - Automatically formats uploaded documents using company template (rule-based, NO AI)
  2. **Copilot Agent** - Assists users with questions from enriched documents via Microsoft 365 Copilot

- ✅ **Key Principles:**
  - Rule-Based Processing (NO AI in enrichment)
  - No Database (SharePoint-only storage)
  - Event-Driven (Webhook-based triggers)
  - Serverless (Azure Functions)
  - Direct Integration (Copilot queries SharePoint directly)

- ✅ **Both functionalities work immediately after app installation** once configuration is complete.

---

## 📋 Core Requirements

### 1. Document Enrichment Functionality

**What It Does:**
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
- ✅ **Automatic** - Triggered via webhooks on file upload

**Input:**
- Raw `.docx` files uploaded to Source Folder (configurable during installation)

**Output:**
- Enriched `.docx` files saved to Destination Folder (user-selected during installation)
- Metadata updated on source document (Status, Enriched File URL, Processed Date)

---

### 2. Copilot Agent Functionality

**What It Does:**
- **Copilot Agent with Prompt** - User asks a question, Copilot Agent analyzes enriched documents and provides answers
- Uses Microsoft 365 Copilot Agent (NOT custom bot development)
- Queries SharePoint directly via Microsoft Search (NO Function App involvement)
- Analyzes enriched documents from configured destination folder
- Returns answers with citations and source links
- Uses custom prompt/instructions for consistent responses

**Key Features:**
- ✅ **O365 Copilot Agent** (NOT custom bot development)
- ✅ **Prompt-Based Analysis** - Analyzes enriched documents based on user questions
- ✅ **Direct SharePoint Query** (NO Function App involvement)
- ✅ **Custom Prompt** - Set as system prompt during configuration
- ✅ **Always Enabled** - Copilot Agent is enabled for everyone by default
- ✅ **Multiple Access Points** - Teams, Web, O365 apps (configurable)
- ✅ **Security Trimming** - Respects user permissions

**Access Points:**
- Microsoft Teams (Chat & Channels, @SMEPilot)
- Web Interface (SharePoint Portal, Direct Access)
- O365 Copilot (Word, Excel, PPT, Office Apps)

---

### 3. Auto-Trigger via Webhooks

**What It Does:**
- Monitors configured source folder for new/updated documents
- Uses Microsoft Graph webhooks for real-time notifications
- Automatically processes files when uploaded or modified

**Technical Details:**
- Webhook subscription created during installation
- Subscription renews every 2 days automatically
- Function App endpoint: `/api/ProcessSharePointFile`
- Handles Graph API validation handshake
- Idempotency check prevents duplicate processing

---

### 4. Configuration Management

**Storage:**
- SharePoint List: `SMEPilotConfig` (per site collection)
- Contains all installation-time settings
- Read by Function App at runtime

**All Configurations Provided During Installation:**
- Source Folder path (user-selected during installation)
- Destination Folder path (user-selected during installation)
- Template file path
- Processing settings (max size, timeout, retries)
- Copilot Agent prompt/custom instructions
- Access points (Teams/Web/O365)

**Note:** Copilot Agent is always enabled for everyone - no enable/disable option.

---

## 🎯 What We Planned to Do

### Phase 1: Architecture & Configuration (Days 1-2) ✅ **COMPLETED**

**Deliverables:**
1. ✅ Complete architecture diagram with all configurations
2. ✅ Flow diagram showing document enrichment process
3. ✅ Configuration specification document
4. ✅ Requirements clarity document
5. ✅ Edge cases documentation
6. ✅ Permissions documentation

**Status:** ✅ **COMPLETED**

---

### Phase 2: Document Enrichment Implementation (Days 3-4)

**Planned Work:**
1. Implement ConfigService to read from SMEPilotConfig list
2. Implement configuration-based folder selection
3. Update file processing to use configured folders
4. Implement DocumentEnricherService with OpenXML rule-based formatter (can use third-party libraries if required for better functionality)
5. Implement error handling (RejectedDocs, FailedDocs folders)
6. Implement webhook renewal timer (every 2 days)
7. Test all edge cases

**Status:** ⚠️ **READY TO START**

---

### Phase 3: Copilot Integration (Days 5-6)

**Planned Work:**
1. Configure O365 Copilot in Copilot Studio
2. Set data source to "SMEPilot Enriched Docs" library
3. Add manager's custom instructions as system prompt
4. Deploy Copilot to Teams
5. Test query functionality

**Status:** ⚠️ **READY TO START**

**Note:** Copilot Agent configuration will not require any code changes; it's achieved via Copilot Studio setup in O365.

---

## 🏗️ System Architecture

### Architecture Diagram

**File Location:** `Knowledgebase/Diagrams/SMEPilot_Architecture_Diagram.drawio`  
**Open with:** https://app.diagrams.net

**What It Shows:**

#### Top Section: Installation Configuration (6 Items)
1. Source Folder (Input Documents - User Selected)
2. Destination Folder (Output - User Selected)
3. Template File (Company Template)
4. Processing Settings (Max Size: 50MB, Timeout: 60s, Retries: 3)
5. Copilot Agent Prompt (Custom Instructions)
6. Copilot Access Points (Teams/Web/O365)

#### Left Side: Document Enrichment Functionality

**Components:**
- **Business User** - Uploads documents
- **SharePoint Online:**
  - Source Folder (Input - User Selected)
  - Templates Library
  - Destination Folder (Output - User Selected)
  - SMEPilotConfig List
  - Source Metadata
  - Error Folders (RejectedDocs/FailedDocs)
  - Microsoft Search (Indexing)
- **Azure Function App (.NET):**
  - ProcessSharePointFile (HTTP Trigger, Webhook Handler)
  - Document Enricher (Rule-Based, OpenXML, NO AI)
  - GraphHelper (Graph API Client, File Operations)
  - ConfigService (Read Configuration)
  - Error Handler (Retry Logic)
  - Application Insights (Logging, Monitoring)
  - Idempotency Check (Deduplication)
  - Webhook Renewal (Every 2 Days)
- **Platform Services:**
  - Azure AD (App-Only Authentication)
  - Microsoft Graph API (Webhooks, Files, Metadata)
  - Application Insights (Telemetry & Monitoring)

**Data Flow:**
1. Business User uploads document → Source Folder (user-selected)
2. SharePoint webhook → Function App
3. Function App processes & enriches → Destination Folder (user-selected)
4. Microsoft Search indexes → Enriched Documents

#### Right Side: Copilot Agent Functionality

**Components:**
- **End User** - Asks questions
- **Access Points (Configurable):**
  - Microsoft Teams
  - Web Interface
  - O365 Copilot
- **Microsoft 365 Copilot Agent:**
  - Copilot Studio (Configuration)
  - Custom Prompt (System Prompt - Analyzes enriched docs)
  - Query Processing (User asks question)
  - Document Analysis (Analyzes enriched documents from destination folder)
  - Answer Generation (With Citations)
  - Microsoft Search (Query Enriched Docs)
  - Direct SharePoint Query (NO Function App)
  - Security Trimming
  - Contextual Memory
- **Data Source:**
  - Destination Folder (User-Selected, Indexed by Microsoft Search)

**Data Flow:**
1. End User asks question → Access Points
2. Copilot Agent processes query → Analyzes enriched documents
3. Microsoft Search queries → Destination Folder (user-selected)
4. Copilot Agent generates answer → End User (with citations)

#### Connection Between Parts
- Enriched Documents from Document Enrichment feed into Copilot Data Source
- Both functionalities work together seamlessly

#### Bottom Section: Summary
- Key Principles: Rule-Based, No Database, Event-Driven, Serverless, Direct Integration
- Minimal Permissions: Azure AD App Registration, SharePoint Admin (Install only), End Users (Read access)
- All Configuration During Installation: Via SPFx Admin Panel

---

## 🔄 Process Flow

### Flow Diagram

**File Location:** `Knowledgebase/Diagrams/SMEPilot_Enrichment_Flow.drawio`  
**Open with:** https://app.diagrams.net

### Document Enrichment Flow (10 Steps)

1. **User Upload** → Business User uploads `.docx` file to Source Folder
2. **Webhook Notification** → SharePoint webhook triggers Function App
3. **Function App Receives** → Validates webhook notification
4. **Idempotency Check** → Checks if file already processed (prevents duplicates)
5. **Download File** → Function App downloads file from SharePoint via Graph API
6. **File Validation** → Validates file size (≤50MB), format (.docx), accessibility
7. **Read Configuration** → Reads settings from SMEPilotConfig list
8. **Get Template** → Downloads company template from Templates Library
9. **Enrich Document** → Applies template formatting using OpenXML (rule-based)
10. **Upload Enriched Document** → Saves to Destination Folder (user-selected during installation)
11. **Update Metadata** → Updates source document with status, enriched file URL, processed date
12. **Index for Search** → Microsoft Search indexes enriched document (up to 15 min delay)

**Decision Points:**
- If file already processed → Skip (idempotency)
- If file invalid (>50MB, wrong format) → Move to RejectedDocs folder
- If processing fails → Retry (up to 3 times), then move to FailedDocs folder
- If processing succeeds → Update metadata, notify success

**Error Paths:**
- RejectedDocs folder: Files that don't meet validation criteria
- FailedDocs folder: Files that failed processing after retries

---

### Copilot Query Flow (4 Steps)

1. **User Query** → End User asks question via Teams/Web/O365 Copilot Agent
2. **Copilot Agent Processes** → O365 Copilot Agent processes natural language query
3. **Analyzes Enriched Documents** → Copilot Agent analyzes documents from Destination Folder via Microsoft Search (NO Function App)
4. **Answer with Citations** → Copilot Agent returns answer with source document links

**Note:** Copilot Agent queries SharePoint directly and analyzes enriched documents - NO Function App involvement in query processing.

---

## ⚙️ Installation Configuration

### All Configurations Provided During Installation

**Via SPFx Admin Panel:**

#### Document Enrichment Configuration

| Setting | Type | Required | Description | Example |
|---------|------|----------|-------------|---------|
| `SourceLibraryUrl` | Text | ✅ Yes | Source folder path (user-selected during installation) | `/Shared Documents/RawDocs` |
| `TargetLibraryUrl` | Text | ✅ Yes | Destination folder path (user-selected during installation) | `/Shared Documents/EnrichedDocs` |
| `TemplatePath` | Text | ✅ Yes | Company template file path (.dotx) | `/Templates/UniversalOrgTemplate.dotx` |
| `MaxFileSizeMB` | Number | No | Maximum file size (default: 50) | `50` |
| `ProcessingTimeoutSeconds` | Number | No | Processing timeout (default: 60) | `60` |
| `MaxRetries` | Number | No | Maximum retry attempts (default: 3) | `3` |

#### Copilot Agent Configuration

| Setting | Type | Required | Description | Example |
|---------|------|----------|-------------|---------|
| `CopilotPrompt` | Text | ✅ Yes | Custom prompt/instructions for Copilot Agent | Manager-defined prompt for analyzing enriched docs |
| `AccessTeams` | Boolean | No | Enable Teams access (default: true) | `true` |
| `AccessWeb` | Boolean | No | Enable Web access (default: true) | `true` |
| `AccessO365` | Boolean | No | Enable O365 apps access (default: true) | `true` |

**Note:** Copilot Agent is always enabled for everyone - no enable/disable option.

**Storage:** All configurations stored in `SMEPilotConfig` SharePoint list.

---

## ⚠️ Edge Cases & Error Handling

### Document Enrichment Edge Cases

| Edge Case | Handling | Result |
|-----------|----------|--------|
| **Large files (>50MB)** | Validation check | Move to RejectedDocs folder |
| **Locked files** | Retry logic (up to 3 times) | If still locked after retries → FailedDocs folder |
| **Duplicate webhooks** | Idempotency check (checks processed status) | Skip processing, log duplicate |
| **Missing template** | Validation check | Move to FailedDocs folder |
| **Processing timeout (>60s)** | Timeout check | Move to FailedDocs folder |
| **Invalid file format** | Format validation | Move to RejectedDocs folder |
| **Network errors** | Retry logic | Retry up to 3 times, then FailedDocs |
| **Graph API errors** | Error handler | Log error, move to FailedDocs |

### Copilot Edge Cases

| Edge Case | Handling | Result |
|-----------|----------|--------|
| **No relevant documents** | Search returns empty | Copilot says "I couldn't find a definitive answer in SMEPilot docs." |
| **User lacks permissions** | Security trimming | User only sees documents they have access to |
| **Indexing delay (up to 15 min)** | Search may not find recent docs | Recent documents may not appear in results immediately |
| **Large result set** | Microsoft Search ranking | Copilot ranks results by relevance, shows top matches |

### Error Folders

- **RejectedDocs:** Files that don't meet validation criteria (size, format)
- **FailedDocs:** Files that failed processing after retries
- **Admin notifications:** Email sent for all errors

---

## 🔐 Permissions & Security

### Installation Permissions (One-Time)

**SharePoint Admin:**
- Install SPFx app
- Create SMEPilotConfig list
- Grant app permissions

**Azure AD App Registration:**
- **App-Only Authentication** (no user credentials)
- **Minimal Permissions Required:**
  - `Sites.ReadWrite.All` - Read/write SharePoint files and metadata
  - `Files.ReadWrite.All` - Read/write files
  - `Webhooks.ReadWrite.All` - Create/manage webhook subscriptions

**Note:** These are minimal permissions - app can only access SharePoint, nothing else.

---

### Runtime Permissions (Ongoing)

**Business Users:**
- **Contribute** access to Source Folder (upload documents)
- No special permissions required

**End Users:**
- **Read** access to Destination Folder (query via Copilot Agent)
- No SharePoint admin permissions required
- Uses existing M365 Copilot license

**Function App:**
- Uses Azure AD app-only authentication
- Accesses SharePoint via Graph API
- No user credentials stored

**Security Features:**
- App-only authentication (no user credentials)
- Security trimming (users only see documents they have access to)
- Minimal permissions (least privilege principle)
- No database (all data in SharePoint, respects existing permissions)

---

## 📍 Access Points

### Copilot Agent Access Points (Configurable During Installation)

**1. Microsoft Teams**
- Chat interface: `@SMEPilot`
- Channels integration
- Direct access from Teams

**2. Web Interface**
- SharePoint portal
- Direct web access
- Browser-based interface

**3. O365 Copilot**
- Word, Excel, PowerPoint
- Office apps integration
- In-app Copilot access

**Note:** 
- Copilot Agent is always enabled for everyone
- All access points configured during installation via SPFx Admin Panel
- Security trimming automatically applied (users only see documents they have access to)

---

## 🔧 Technical Implementation Details

> **📊 Visual Guides:** For step-by-step visual flows, see:
> - **Installation & Setup Flow:** `Knowledgebase/Diagrams/SMEPilot_Installation_Setup_Flow.drawio` (complete 7-step process)
> - **Webhook Subscription Flow:** `Knowledgebase/Diagrams/SMEPilot_Webhook_Subscription_Flow.drawio` (creation, validation, renewal)

### Step 1: Azure AD App Registration

#### 1.1 Create App Registration

**Location:** Azure Portal → Azure Active Directory (Microsoft Entra ID)

**Steps:**
1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to **Azure Active Directory** (or **Microsoft Entra ID**)
3. Click **"App registrations"** (left menu)
4. Click **"+ New registration"**

**Configuration:**
- **Name:** `SMEPilot` (or your preferred name)
- **Supported account types:** Accounts in this organizational directory only
- **Redirect URI:** Leave blank (not needed for app-only authentication)
- Click **"Register"**

**Result:** App Registration created with:
- Application (client) ID → `Graph_ClientId`
- Directory (tenant) ID → `Graph_TenantId`

---

#### 1.2 Configure API Permissions

**Location:** App Registration → API permissions

**Steps:**
1. In your App Registration, go to **"API permissions"**
2. Click **"+ Add a permission"**
3. Select **"Microsoft Graph"**
4. Select **"Application permissions"** (NOT Delegated)
5. Add these permissions:

| Permission | Type | Purpose | Required |
|------------|------|---------|----------|
| `Sites.ReadWrite.All` | Application | Read/write SharePoint sites, documents, and metadata | ✅ Yes |
| `Files.ReadWrite.All` | Application | Read/write files in SharePoint | ✅ Yes |
| `Webhooks.ReadWrite.All` | Application | Create and manage webhook subscriptions | ✅ Yes |

6. Click **"Add permissions"**

**Important:** After adding permissions:
1. Click **"Grant admin consent for [Your Organization]"**
2. Confirm the consent
3. Verify all permissions show **"Granted for [Your Organization]"** with green checkmark

**Note:** These are minimal permissions - app can only access SharePoint, nothing else.

---

#### 1.3 Create Client Secret

**Location:** App Registration → Certificates & secrets

**Steps:**
1. In App Registration, go to **"Certificates & secrets"**
2. Click **"+ New client secret"**
3. **Description:** `SMEPilot Secret`
4. **Expires:** Choose expiration (recommended: 24 months)
5. Click **"Add"**
6. **IMPORTANT:** Copy the **Value** immediately (you won't see it again!)
   - This is your `Graph_ClientSecret`
   - Store securely (Azure Key Vault recommended)

**Result:** Client Secret created → `Graph_ClientSecret`

---

#### 1.4 Get App Registration Details

**Location:** App Registration → Overview

**Copy these values:**
- **Application (client) ID** → `Graph_ClientId`
- **Directory (tenant) ID** → `Graph_TenantId`
- **Client Secret Value** → `Graph_ClientSecret` (from Step 1.3)

**Store in:**
- Azure Function App → Configuration → Application Settings
- Or Azure Key Vault (recommended for production)

---

### Step 2: Azure Function App Configuration

#### 2.1 Create Function App (if not exists)

**Location:** Azure Portal → Function Apps

**Steps:**
1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to **Function Apps**
3. Click **"+ Create"**
4. Configure:
   - **Subscription:** Your Azure subscription
   - **Resource Group:** Create new or use existing
   - **Function App name:** `SMEPilot-FunctionApp` (must be globally unique)
   - **Publish:** Code
   - **Runtime stack:** .NET
   - **Version:** 8.0 or later
   - **Region:** Your preferred region
5. Click **"Review + create"** → **"Create"**

---

#### 2.2 Configure Application Settings

**Location:** Function App → Configuration → Application settings

**Add these environment variables:**

| Setting Name | Value | Description |
|--------------|-------|-------------|
| `Graph_TenantId` | From App Registration | Azure AD Tenant ID |
| `Graph_ClientId` | From App Registration | App Registration Client ID |
| `Graph_ClientSecret` | From App Registration | Client Secret Value |
| `WEBSITE_TIME_ZONE` | UTC | Time zone for scheduling |

**Optional Settings:**
- `MaxFileSizeMB` (default: 50)
- `ProcessingTimeoutSeconds` (default: 60)
- `MaxRetries` (default: 3)

**Note:** Store `Graph_ClientSecret` in Azure Key Vault for production (recommended).

---

#### 2.3 Enable Application Insights

**Location:** Function App → Application Insights

**Steps:**
1. Go to **"Application Insights"** in Function App
2. Click **"Turn on Application Insights"**
3. Create new or link existing Application Insights resource
4. Enable:
   - Request tracking
   - Exception tracking
   - Performance monitoring
   - Custom telemetry

**Result:** All Function App operations logged and monitored.

---

### Step 3: Webhook Subscription Creation

#### 3.1 Create Webhook Subscription (During Installation)

**Location:** Azure Function App → ProcessSharePointFile endpoint

**Process:**
1. During app installation, SPFx Admin Panel calls Function App endpoint
2. Function App creates webhook subscription via Microsoft Graph API
3. Subscription monitors Source Folder (user-selected) for changes

**API Call:**
```http
POST https://graph.microsoft.com/v1.0/subscriptions
Content-Type: application/json

{
  "changeType": "created,updated",
  "notificationUrl": "https://your-function-app.azurewebsites.net/api/ProcessSharePointFile",
  "resource": "/sites/{siteId}/drives/{driveId}/root",
  "expirationDateTime": "2024-01-03T00:00:00Z",
  "clientState": "SMEPilot-Webhook-{timestamp}"
}
```

**Parameters:**
- `changeType`: `created,updated` (monitor file uploads and updates)
- `notificationUrl`: Function App endpoint URL
- `resource`: Source Folder path (user-selected)
- `expirationDateTime`: Subscription expiration (max 3 days)
- `clientState`: Unique identifier for deduplication

**Response:**
- Returns subscription ID
- Store subscription ID in SMEPilotConfig list for renewal

---

#### 3.2 Webhook Validation

**Process:**
1. Microsoft Graph sends validation request to Function App
2. Function App must return validation token within 10 seconds
3. Validation token is in request header: `Validation-Token`

**Implementation:**
```csharp
if (request.Headers.ContainsKey("Validation-Token"))
{
    var validationToken = request.Headers["Validation-Token"].FirstOrDefault();
    return new OkObjectResult(validationToken);
}
```

**Result:** Webhook subscription validated and active.

---

#### 3.3 Webhook Renewal (Automatic)

**Process:**
1. Function App checks subscription expiration every 2 days
2. If subscription expires within 24 hours, renew automatically
3. Create new subscription with same configuration
4. Update subscription ID in SMEPilotConfig list

**Implementation:**
- Timer trigger function runs every 2 days
- Checks all active subscriptions
- Renews subscriptions expiring soon
- Logs renewal status to Application Insights

**API Call (Renewal):**
```http
POST https://graph.microsoft.com/v1.0/subscriptions
Content-Type: application/json

{
  "changeType": "created,updated",
  "notificationUrl": "https://your-function-app.azurewebsites.net/api/ProcessSharePointFile",
  "resource": "/sites/{siteId}/drives/{driveId}/root",
  "expirationDateTime": "2024-01-05T00:00:00Z",
  "clientState": "SMEPilot-Webhook-{new-timestamp}"
}
```

**Note:** Graph API webhook subscriptions expire after maximum 3 days. Automatic renewal ensures continuous monitoring.

---

### Step 4: SharePoint Configuration

#### 4.1 Create SMEPilotConfig List

**Location:** SharePoint Site → Site Contents

**Process:**
1. During app installation, SPFx app creates SharePoint list
2. List name: `SMEPilotConfig`
3. Columns created:

| Column Name | Type | Required | Description |
|-------------|------|----------|-------------|
| `SourceLibraryUrl` | Single line of text | ✅ Yes | Source folder path (user-selected) |
| `TargetLibraryUrl` | Single line of text | ✅ Yes | Destination folder path (user-selected) |
| `TemplateFileUrl` | Single line of text | ✅ Yes | Template file path |
| `MaxFileSizeMB` | Number | No | Max file size (default: 50) |
| `ProcessingTimeoutSeconds` | Number | No | Timeout (default: 60) |
| `MaxRetries` | Number | No | Retry attempts (default: 3) |
| `CopilotPrompt` | Multiple lines of text | ✅ Yes | Copilot Agent prompt |
| `AccessTeams` | Yes/No | No | Teams access (default: Yes) |
| `AccessWeb` | Yes/No | No | Web access (default: Yes) |
| `AccessO365` | Yes/No | No | O365 access (default: Yes) |

**Permissions:**
- Function App: Read access (app-only authentication)
- Admin: Write access (for configuration updates)

---

#### 4.2 Create Metadata Columns

**Location:** Source Folder → Library Settings → Columns

**Process:**
1. During installation, create columns on Source Folder library:

| Column Name | Type | Required | Description |
|-------------|------|----------|-------------|
| `SMEPilot_Enriched` | Yes/No | No | Whether document is enriched |
| `SMEPilot_Status` | Choice | No | Status: Processing, Enriched, Failed, Rejected |
| `SMEPilot_EnrichedFileUrl` | Hyperlink | No | Link to enriched document |
| `SMEPilot_ProcessedDate` | Date and Time | No | Processing completion date |

**Permissions:**
- Function App: Read/Write (updates metadata)
- Users: Read (view status)

---

#### 4.3 Create Error Folders

**Location:** SharePoint Site → Source Folder location

**Process:**
1. During installation, create folders in Source Folder location:

**Folders:**
- `RejectedDocs` - Files that don't meet validation criteria
- `FailedDocs` - Files that failed processing after retries

**Permissions:**
- Function App: Write access (moves files)
- Admin: Read access (monitor errors)

---

### Step 5: SPFx Admin Panel Setup

#### 5.1 Install SPFx App

**Location:** SharePoint Site → Site Contents → New → App

**Process:**
1. Upload SPFx app package (.sppkg file)
2. Deploy to App Catalog
3. Add to SharePoint site
4. Grant permissions:
   - Site Collection Admin (for installation)
   - Read/Write to SMEPilotConfig list

---

#### 5.2 Configuration UI

**Location:** SPFx Admin Panel (accessible to Site Collection Admins)

**Features:**
1. **Folder Selection:**
   - Browse and select Source Folder (user-selected)
   - Browse and select Destination Folder (user-selected)
   - Browse and select Template file

2. **Processing Settings:**
   - Max file size (default: 50MB)
   - Timeout (default: 60 seconds)
   - Retry attempts (default: 3)

3. **Copilot Agent Configuration:**
   - Custom prompt/instructions (text area)
   - Access points (Teams/Web/O365 checkboxes)

4. **Validation:**
   - Check folder existence
   - Check folder permissions
   - Check template file exists
   - Validate all required fields

5. **Save Configuration:**
   - Save to SMEPilotConfig list
   - Create webhook subscription
   - Create metadata columns
   - Create error folders
   - Show success message

---

### Step 6: Copilot Agent Configuration

#### 6.1 Configure in Copilot Studio

**Location:** Microsoft 365 Admin Center → Copilot Studio

**Process:**
1. Go to [Microsoft 365 Admin Center](https://admin.microsoft.com)
2. Navigate to **Copilot Studio**
3. Create new Copilot Agent or use existing
4. Configure:

**Data Source:**
- Set to Destination Folder (user-selected during installation)
- Enable Microsoft Search integration

**System Prompt:**
- Add custom prompt from SMEPilotConfig (`CopilotPrompt`)
- Configure to analyze enriched documents
- Set response format (summary + numbered steps)

**Access Points:**
- Teams: Enable if `AccessTeams = true`
- Web: Enable if `AccessWeb = true`
- O365: Enable if `AccessO365 = true`

**Deployment:**
- Deploy to Teams (if enabled)
- Deploy to Web (if enabled)
- Deploy to O365 (if enabled)

**Note:** Copilot Agent is always enabled - no enable/disable option.

---

### Step 7: Verification & Testing

#### 7.1 Verify Installation

**Checklist:**
- ✅ Azure AD App Registration created
- ✅ Permissions granted and consented
- ✅ Client Secret created and stored
- ✅ Function App configured with credentials
- ✅ Application Insights enabled
- ✅ SMEPilotConfig list created
- ✅ Metadata columns created
- ✅ Error folders created
- ✅ Webhook subscription created
- ✅ Copilot Agent configured

---

#### 7.2 Test Document Enrichment

**Steps:**
1. Upload test `.docx` file to Source Folder
2. Verify webhook notification received
3. Check Function App logs (Application Insights)
4. Verify enriched document in Destination Folder
5. Check source document metadata updated
6. Verify Microsoft Search indexing (may take up to 15 minutes)

**Expected Results:**
- File processed within 60 seconds
- Enriched document in Destination Folder
- Metadata shows `SMEPilot_Status = Enriched`
- No errors in Application Insights

---

#### 7.3 Test Copilot Agent

**Steps:**
1. Wait for Microsoft Search indexing (up to 15 minutes)
2. Access Copilot Agent via Teams/Web/O365
3. Ask question about enriched documents
4. Verify answer with citations
5. Check source document links work

**Expected Results:**
- Copilot Agent responds with answer
- Answer includes citations from enriched documents
- Source links are accessible
- Security trimming works (users only see documents they have access to)

---

### Step 8: Additional Technical Considerations

#### 8.1 Idempotency Implementation

**Purpose:** Prevent duplicate processing of same file

**Implementation:**
- Check `SMEPilot_Enriched` metadata field before processing
- If `SMEPilot_Enriched = Yes`, skip processing
- Use file ID + modification date as deduplication key
- Store processed files in memory cache (5-minute window)

**Code Pattern:**
```csharp
var metadata = await GetFileMetadataAsync(fileId);
if (metadata.SMEPilot_Enriched == true)
{
    _logger.LogInformation("File already processed, skipping");
    return; // Idempotent - safe to skip
}
```

---

#### 8.2 Error Handling & Retry Logic

**Implementation:**
- Retry failed operations up to 3 times (configurable)
- Exponential backoff between retries (2s, 4s, 8s)
- Move files to error folders after max retries:
  - `RejectedDocs` - Validation failures
  - `FailedDocs` - Processing failures
- Log all errors to Application Insights

**Retry Pattern:**
```csharp
for (int attempt = 1; attempt <= maxRetries; attempt++)
{
    try
    {
        await ProcessFileAsync(file);
        break; // Success
    }
    catch (Exception ex)
    {
        if (attempt == maxRetries)
        {
            await MoveToFailedFolderAsync(file);
            throw;
        }
        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
    }
}
```

---

#### 8.3 Security Best Practices

**Client Secret Storage:**
- **Development:** Azure Function App Application Settings
- **Production:** Azure Key Vault (recommended)
- **Rotation:** Rotate secrets before expiration
- **Access:** Limit access to Function App only

**App-Only Authentication:**
- Use client credentials flow (not user credentials)
- No user impersonation
- Minimal permissions (least privilege)
- Regular permission audits

**Network Security:**
- Function App should be in private endpoint (if required)
- Webhook endpoint should be HTTPS only
- Validate webhook requests (clientState verification)

---

#### 8.4 Monitoring & Logging

**Application Insights Integration:**
- Track all Function App executions
- Monitor webhook notifications
- Track processing times
- Alert on errors/failures
- Custom metrics:
  - Files processed per day
  - Average processing time
  - Error rate
  - Webhook renewal status

**Logging Levels:**
- **Information:** Normal operations (file processed, metadata updated)
- **Warning:** Retries, validation issues
- **Error:** Processing failures, webhook errors
- **Critical:** System failures, permission errors

---

#### 8.5 Performance Optimization

**File Processing:**
- Process files asynchronously
- Use streaming for large files
- Parallel processing (if multiple files)
- Timeout handling (60 seconds default)

**Graph API Calls:**
- Batch API calls where possible
- Cache frequently accessed data
- Use delta queries for metadata updates
- Implement rate limiting handling

**Webhook Processing:**
- Process notifications asynchronously
- Deduplicate notifications (5-minute window)
- Queue processing for high-volume scenarios

---

### Technical Implementation Summary

**Major Steps Covered:**

1. ✅ **Azure AD App Registration**
   - Create app registration
   - Configure API permissions (Sites.ReadWrite.All, Files.ReadWrite.All, Webhooks.ReadWrite.All)
   - Create client secret
   - Get credentials (Tenant ID, Client ID, Client Secret)

2. ✅ **Azure Function App Setup**
   - Create Function App (if needed)
   - Configure application settings
   - Enable Application Insights

3. ✅ **Webhook Subscription**
   - Create subscription during installation
   - Webhook validation
   - Automatic renewal (every 2 days)

4. ✅ **SharePoint Configuration**
   - Create SMEPilotConfig list
   - Create metadata columns
   - Create error folders

5. ✅ **SPFx Admin Panel**
   - Install SPFx app
   - Configuration UI with folder selection
   - Validation and save

6. ✅ **Copilot Agent Configuration**
   - Configure in Copilot Studio
   - Set data source (Destination Folder)
   - Configure system prompt
   - Deploy to access points

7. ✅ **Verification & Testing**
   - Installation verification checklist
   - Document enrichment testing
   - Copilot Agent testing

8. ✅ **Additional Considerations**
   - Idempotency implementation
   - Error handling & retry logic
   - Security best practices
   - Monitoring & logging
   - Performance optimization

**All major technical steps are now documented and ready for implementation.**

---

## 📅 Implementation Timeline

### Phase 1: Architecture & Configuration ✅ **COMPLETED**
**Timeline:** Days 1-2  
**Status:** ✅ **COMPLETED**

**Deliverables:**
- ✅ Architecture diagram with all configurations
- ✅ Flow diagram
- ✅ Requirements documentation
- ✅ Configuration specification
- ✅ Edge cases documentation
- ✅ Permissions documentation

---

### Phase 2: Document Enrichment Implementation
**Timeline:** Days 3-4  
**Status:** ⚠️ **READY TO START**

**Tasks:**
1. Implement ConfigService (read from SMEPilotConfig)
2. Implement configuration-based folder selection (user-selected source and destination)
3. Update file processing to use configured folders
4. Implement DocumentEnricherService (OpenXML rule-based, can use third-party libraries if required)
5. Implement error handling (RejectedDocs, FailedDocs folders)
6. Implement webhook renewal timer (every 2 days)
7. Test all edge cases

---

### Phase 3: Copilot Integration
**Timeline:** Days 5-6  
**Status:** ⚠️ **READY TO START**

**Tasks:**
1. Configure O365 Copilot Agent in Copilot Studio
2. Set data source to Destination Folder (user-selected during installation)
3. Add manager's custom prompt/instructions as system prompt
4. Deploy Copilot Agent to Teams
5. Test query functionality (user asks question, Copilot analyzes enriched docs)

**Note:** Copilot Agent configuration requires NO code changes - done via Copilot Studio. Copilot Agent is always enabled.

---

## ✅ Success Criteria

### Document Enrichment Success Criteria

- ✅ User uploads `.docx` file to Source Folder
- ✅ Webhook triggers Function App within seconds
- ✅ Function App processes file within 60 seconds (for files ≤50MB)
- ✅ Enriched document appears in Destination Folder (user-selected)
- ✅ Source document metadata updated (Status, Enriched File URL, Processed Date)
- ✅ Microsoft Search indexes enriched document (within 15 minutes)
- ✅ No duplicate processing (idempotency works)
- ✅ Error handling works (RejectedDocs, FailedDocs folders)

---

### Copilot Agent Success Criteria

- ✅ End User can access Copilot Agent via Teams/Web/O365
- ✅ Copilot Agent can query and analyze Destination Folder (user-selected)
- ✅ Copilot Agent analyzes enriched documents and returns answers with citations
- ✅ Custom prompt applied (consistent responses)
- ✅ Security trimming works (users only see documents they have access to)
- ✅ No Function App involvement in query processing (direct SharePoint query)

---

## 📊 Diagrams Reference

### Architecture Diagram
**File:** `Knowledgebase/Diagrams/SMEPilot_Architecture_Diagram.drawio`  
**Open with:** https://app.diagrams.net  
**Shows:** Complete system architecture, all components, data flows, configurations

### Flow Diagram
**File:** `Knowledgebase/Diagrams/SMEPilot_Enrichment_Flow.drawio`  
**Open with:** https://app.diagrams.net  
**Shows:** Step-by-step document enrichment process, decision points, error paths

### Configuration Diagram
**File:** `Knowledgebase/Diagrams/SMEPilot_Configuration_Diagram.drawio`  
**Open with:** https://app.diagrams.net  
**Shows:** All installation-time configurations and their storage

### Installation & Setup Flow Diagram ⭐ **NEW**
**File:** `Knowledgebase/Diagrams/SMEPilot_Installation_Setup_Flow.drawio`  
**Open with:** https://app.diagrams.net  
**Shows:** Complete 7-step installation process: Azure AD registration, Function App setup, SPFx installation, configuration, SharePoint setup, webhook subscription, Copilot Agent configuration

### Webhook Subscription Flow Diagram ⭐ **NEW**
**File:** `Knowledgebase/Diagrams/SMEPilot_Webhook_Subscription_Flow.drawio`  
**Open with:** https://app.diagrams.net  
**Shows:** Detailed webhook lifecycle: creation, validation, automatic renewal process, technical parameters

---

## 📝 Key Decisions

### 1. Rule-Based Processing (NO AI)
**Decision:** Document enrichment uses rule-based OpenXML transformations, NOT AI.  
**Rationale:** Deterministic, predictable, no AI costs, faster processing.

### 2. No Database
**Decision:** All data stored in SharePoint (files + metadata), NO separate database.  
**Rationale:** Simpler architecture, uses existing SharePoint infrastructure, respects existing permissions.

### 3. O365 Copilot Agent (NOT Custom Bot)
**Decision:** Use Microsoft 365 Copilot Agent with custom prompt, NOT custom bot development. Copilot Agent is always enabled.  
**Rationale:** Faster implementation, no custom development needed, leverages existing M365 Copilot. User asks question, Copilot Agent analyzes enriched documents and provides answers.

### 4. Direct SharePoint Query
**Decision:** Copilot queries SharePoint directly via Microsoft Search, NO Function App involvement.  
**Rationale:** Faster queries, simpler architecture, leverages Microsoft Search indexing.

### 5. All Configuration During Installation
**Decision:** All settings provided during installation via SPFx Admin Panel. Source and Destination folders are user-selected (not static).  
**Rationale:** Clear setup process, no runtime configuration needed, easier management, flexible folder selection.

### 6. Third-Party Libraries for Document Enrichment
**Decision:** Can use third-party libraries for DocumentEnricherService if required for better functionality.  
**Rationale:** Flexibility to choose best tools for OpenXML processing, better performance and features.

---

## 🎯 Business Value

### For Organizations

- ✅ **Standardized Documentation:** All documents follow company template
- ✅ **Knowledge Discovery:** Users can easily find information via Copilot
- ✅ **Time Savings:** Automatic document formatting (no manual work)
- ✅ **Consistency:** All documents have same structure and format
- ✅ **Searchability:** Enriched documents indexed and searchable

### For Users

- ✅ **Easy Upload:** Just upload document to Source Folder
- ✅ **Automatic Processing:** No manual formatting needed
- ✅ **Quick Answers:** Ask questions via Copilot, get instant answers
- ✅ **Source Citations:** Always know where information came from

---

## 📞 Next Steps

### Immediate Actions

1. ✅ **Review this document** - Manager review and approval
2. ✅ **Review architecture diagram** - Open in draw.io
3. ✅ **Review flow diagram** - Open in draw.io
4. ⚠️ **Approve implementation** - Sign off on approach

### After Approval

1. **Start Phase 2** - Document Enrichment Implementation
2. **Start Phase 3** - Copilot Integration
3. **Testing** - Test all edge cases
4. **Deployment** - Deploy to production

---

## 📎 Supporting Documents

### Additional Documentation

- **Requirements Clarity:** `Knowledgebase/REQUIREMENTS_CLARITY.md`
- **Complete Requirements:** `COMPLETE_REQUIREMENTS_AND_CONFIGURATIONS.md`
- **Installation Configuration:** `Knowledgebase/INSTALLATION_TIME_CONFIGURATION.md`
- **Architecture Requirements:** `Knowledgebase/ARCHITECTURE_DIAGRAM_REQUIREMENTS.md`
- **Architecture Finalized:** `ARCHITECTURE_FINALIZED.md`

### Diagram Files

- **Architecture Diagram:** `Knowledgebase/Diagrams/SMEPilot_Architecture_Diagram.drawio`
- **Flow Diagram:** `Knowledgebase/Diagrams/SMEPilot_Enrichment_Flow.drawio`
- **Configuration Diagram:** `Knowledgebase/Diagrams/SMEPilot_Configuration_Diagram.drawio`
- **Installation & Setup Flow:** `Knowledgebase/Diagrams/SMEPilot_Installation_Setup_Flow.drawio` ⭐ **NEW**
- **Webhook Subscription Flow:** `Knowledgebase/Diagrams/SMEPilot_Webhook_Subscription_Flow.drawio` ⭐ **NEW**

**All diagrams can be opened at:** https://app.diagrams.net

---

## ✅ Document Status

**Version:** 1.3  
**Date:** 2024-01-01  
**Status:** ✅ **READY FOR MANAGER REVIEW**  
**Changes in v1.3:**
- Added Installation & Setup Flow diagram (7-step installation process)
- Added Webhook Subscription Flow diagram (creation, validation, renewal)
- Updated Diagrams Reference section with new diagrams
- Updated Supporting Documents section

**Changes in v1.2:**
- Added comprehensive Technical Implementation Details section
- Azure AD App Registration process (Step 1)
- Azure Function App configuration (Step 2)
- Webhook subscription creation and renewal (Step 3)
- SharePoint configuration (Step 4)
- SPFx Admin Panel setup (Step 5)
- Copilot Agent configuration (Step 6)
- Verification & testing procedures (Step 7)
- Additional technical considerations (Step 8)

**Changes in v1.1:**
- Source and Destination folders are user-selected (not static)
- Copilot Agent always enabled (no enable/disable option)
- Removed: Visibility groups, Admin email, Error handling settings, Monitoring preferences
- DocumentEnricherService can use third-party libraries
- Clarified Copilot Agent analyzes enriched documents based on user questions

**Approved by:** Pending  
**Next Review:** After manager feedback

---

**END OF DOCUMENT**

