# SMEPilot — Architecture Diagram (Text)

Use this text with a diagram tool (draw.io / Lucidchart). This is the authoritative diagram description.

## Entities:

- **SharePoint (Tenant)**
  - Site: DocEnricher-PoC (or configured site)
    - Library: Templates
    - Library: 🔍 SMEPilot Enriched Docs (Indexed by Copilot/Search)
    - Library/Folder: SourceDocs (configurable)
    - List: SMEPilotConfig

- **Azure**
  - Azure AD App Registration (app-only credentials)
  - Function App: SMEPilot.FunctionApp
    - /api/ProcessSharePointFile (webhook receiver)
    - TemplateBuilder (OpenXML)
    - SharePoint Graph Client
    - Logging & Monitoring (App Insights)

- **Optional:** Copilot Studio / Microsoft Search connector (for indexing)
- **Optional:** ngrok (for dev webhook tunneling)

## Flow Arrows:

1. User -> SharePoint (upload)
2. SharePoint -> Microsoft Graph Webhook -> Function App
3. Function App -> Graph API (download source file & upload enriched)
4. Function App -> SharePoint list update (metadata)
5. SharePoint Search -> Copilot (indexes enriched docs)

## Notes:

- The Function App uses app-only auth. Admin must grant `Sites.ReadWrite.All`.
- Installer script runs with admin credentials (Connect-PnPOnline).

---

# 🏗️ SMEPilot Architecture Diagram

## System Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           SharePoint Online Tenant                           │
│                                                                               │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │                        SharePoint Site                                 │  │
│  │                                                                         │  │
│  │  ┌──────────────────┐                    ┌──────────────────┐        │  │
│  │  │  Source Folder   │                    │  🔍 Destination  │        │  │
│  │  │  (Input)         │                    │  Folder          │        │  │
│  │  │                  │                    │  (SMEPilot       │        │  │
│  │  │  📄 doc1.docx    │                    │   Enriched Docs) │        │  │
│  │  │  📄 doc2.pptx    │                    │   Indexed by     │        │  │
│  │  │  📄 doc3.pdf     │                    │   Copilot/Search │        │  │
│  │  └──────────────────┘                    └──────────────────┘        │  │
│  │                                                                         │  │
│  │  ┌──────────────────────────────────────────────────────────────────┐ │  │
│  │  │  SMEPilot App (SPFx)                                             │ │  │
│  │  │  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐  │ │  │
│  │  │  │  Configuration │  │  Status        │  │  Copilot       │  │ │  │
│  │  │  │  Panel         │  │  Dashboard      │  │  Interface     │  │ │  │
│  │  │  └────────────────┘  └────────────────┘  └────────────────┘  │ │  │
│  │  └──────────────────────────────────────────────────────────────────┘ │  │
│  │                                                                         │  │
│  │  ┌──────────────────────────────────────────────────────────────────┐ │  │
│  │  │  Document Library Metadata Columns                               │ │  │
│  │  │  - SMEPilot_Enriched (Yes/No)                                    │ │  │
│  │  │  - SMEPilot_Status (Text)                                       │ │  │
│  │  │  - SMEPilot_ProcessedDate (Date)                                 │ │  │
│  │  │  - SMEPilot_ErrorMessage (Text)                                  │ │  │
│  │  └──────────────────────────────────────────────────────────────────┘ │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│                                                                               │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │  Microsoft Teams / O365 Copilot                                        │  │
│  │  ┌──────────────────────────────────────────────────────────────────┐ │  │
│  │  │  Copilot Agent                                                    │ │  │
│  │  │  - Query Interface                                                │ │  │
│  │  │  - Answer Display                                                 │ │  │
│  │  │  - Source References                                               │ │  │
│  │  └──────────────────────────────────────────────────────────────────┘ │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ Microsoft Graph API
                                    │ Webhooks
                                    │
┌───────────────────────────────────▼───────────────────────────────────────────┐
│                        Azure Function App                                     │
│                                                                                │
│  ┌────────────────────────────────────────────────────────────────────────┐  │
│  │  Configuration Service                                                  │  │
│  │  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐    │  │
│  │  │  Config Storage  │  │  Config          │  │  Permission     │    │  │
│  │  │  (App Settings)  │  │  Validation     │  │  Validator      │    │  │
│  │  └──────────────────┘  └──────────────────┘  └──────────────────┘    │  │
│  └────────────────────────────────────────────────────────────────────────┘  │
│                                                                                │
│  ┌────────────────────────────────────────────────────────────────────────┐  │
│  │  Document Processing Pipeline                                          │  │
│  │                                                                          │  │
│  │  ┌──────────────┐     ┌──────────────┐     ┌──────────────┐            │  │
│  │  │  Webhook     │────▶│  Idempotency│────▶│  File        │            │  │
│  │  │  Listener    │     │  Check      │     │  Downloader   │            │  │
│  │  └──────────────┘     └──────────────┘     └──────┬───────┘            │  │
│  │                                                    │                     │  │
│  │                                                    ▼                     │  │
│  │                                          ┌──────────────┐              │  │
│  │                                          │  Content     │              │  │
│  │                                          │  Extractor   │              │  │
│  │                                          └──────┬───────┘              │  │
│  │                                                 │                       │  │
│  │                                                 ▼                       │  │
│  │                                          ┌──────────────┐              │  │
│  │                                          │  Template    │              │  │
│  │                                          │  Filler      │              │  │
│  │                                          └──────┬───────┘              │  │
│  │                                                 │                       │  │
│  │                                                 ▼                       │  │
│  │                                          ┌──────────────┐              │  │
│  │                                          │  File        │              │  │
│  │                                          │  Uploader    │              │  │
│  │                                          └──────┬───────┘              │  │
│  │                                                 │                       │  │
│  │                                                 ▼                       │  │
│  │                                          ┌──────────────┐              │  │
│  │                                          │  Metadata    │              │  │
│  │                                          │  Updater     │              │  │
│  │                                          └──────────────┘              │  │
│  └────────────────────────────────────────────────────────────────────────┘  │
│                                                                                │
│  ┌────────────────────────────────────────────────────────────────────────┐  │
│  │  Note: Copilot queries SharePoint directly via O365 Copilot             │  │
│  │  (No Function App service needed - O365 Copilot handles queries)        │  │
│  └────────────────────────────────────────────────────────────────────────┘  │
│                                                                                │
│  ┌────────────────────────────────────────────────────────────────────────┐  │
│  │  Error Handling & Logging                                              │  │
│  │  ┌──────────────┐     ┌──────────────┐     ┌──────────────┐            │  │
│  │  │  Retry       │     │  Error       │     │  Application │            │  │
│  │  │  Policy      │     │  Handler    │     │  Insights    │            │  │
│  │  └──────────────┘     └──────────────┘     └──────────────┘            │  │
│  └────────────────────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ Azure Storage
                                    │
┌───────────────────────────────────▼───────────────────────────────────────────┐
│                        Azure Resources                                         │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐         │
│  │  App Settings     │  │  Key Vault       │  │  Application    │         │
│  │  (Configuration)  │  │  (Secrets)       │  │  Insights       │         │
│  └──────────────────┘  └──────────────────┘  └──────────────────┘         │
└───────────────────────────────────────────────────────────────────────────────┘
```

## Component Details

### 1. SharePoint Site Components

#### Source Folder (Input)
- **Purpose:** Where users upload documents
- **Configuration:** Set during app installation
- **Monitoring:** Webhook subscription for file changes
- **Permissions:** Read/Write for users, Full Control for app

#### 🔍 Destination Folder (Output)
- **Purpose:** Where enriched documents are stored
- **Configuration:** Set during app installation
- **Naming:** `{original_name}_enriched.{extension}`
- **Permissions:** Read/Write for users, Full Control for app
- **Indexing:** Indexed by Copilot/Search for querying

#### SMEPilot App (SPFx)
- **Configuration Panel:** Set source/destination folders
- **Status Dashboard:** View processing status
- **Copilot Interface:** Query enriched documents

### 2. Azure Function App Components

#### Configuration Service
- **Config Storage:** Azure App Settings or SharePoint List
- **Config Validation:** Validate folders, permissions, template
- **Permission Validator:** Check required permissions

#### Document Processing Pipeline
- **Webhook Listener:** Receive SharePoint notifications
- **Idempotency Check:** Prevent duplicate processing
- **File Downloader:** Download from source folder
- **Content Extractor:** Extract text and images
- **Template Filler:** Apply template formatting
- **File Uploader:** Upload to destination folder
- **Metadata Updater:** Update processing status

#### O365 Copilot Integration
- **Configuration:** O365 Copilot configured in Copilot Studio
- **Data Source:** "SMEPilot Enriched Docs" SharePoint library
- **Query Handling:** O365 Copilot queries SharePoint directly (no Function App)
- **Custom Instructions:** Manager's instructions set as system prompt

### 3. External Services

#### Microsoft Graph API
- **Webhooks:** File change notifications
- **File Operations:** Download/upload files
- **Metadata:** Read/write document metadata
- **Permissions:** Validate access

#### Application Insights
- **Logging:** All operations logged
- **Metrics:** Processing times, success rates
- **Alerts:** Error notifications
- **Dashboards:** Monitoring views

## Data Flow

### Document Enrichment Flow
```
1. User uploads document → Source Folder
2. SharePoint sends webhook → Function App
3. Function App validates → Idempotency check
4. Function App downloads → File from SharePoint
5. Function App extracts → Text and images
6. Function App applies → Template formatting
7. Function App uploads → Enriched document to Destination
8. Function App updates → Metadata (SMEPilot_Enriched = True)
```

### Copilot Query Flow
```
1. User asks question → O365 Copilot (Teams/Web)
2. O365 Copilot queries → "SMEPilot Enriched Docs" library directly
3. O365 Copilot searches → Enriched documents using Microsoft Search
4. O365 Copilot generates → Answer with citations (using custom instructions)
5. O365 Copilot displays → Answer to user
```

**Note:** O365 Copilot queries SharePoint directly - no Function App involvement.

## Configuration Flow

```
1. User installs app → SharePoint
2. App shows configuration UI → User configures
3. App validates configuration → Check folders, permissions
4. App saves configuration → Azure App Settings
5. App creates webhook → Subscribe to source folder
6. App deploys Copilot agent → Teams/Web
7. App ready → Processing starts
```

## Security & Permissions

### Azure AD App Registration
- **Application Permissions:**
  - `Sites.ReadWrite.All`
  - `Files.ReadWrite.All`
  - `Sites.Read.All`

### SharePoint Permissions
- **Source Folder:** Read, Write, Create
- **Destination Folder:** Read, Write, Create
- **Metadata Columns:** Create, Update

### Access Control
- **Document Processing:** App-only authentication
- **Copilot Queries:** User authentication required
- **Configuration:** Site Collection Admin only

