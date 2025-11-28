## SMEPilot – Single Source Documentation

This file is the **only document you need** to understand and use SMEPilot.  
Everything else (requirements, implementation, permissions, configuration, dev notes, user info) is summarized below.

---

## 1. Actual requirement (what product must do)

- **Goal**: Turn messy functional/technical `.docx` documents into **standardized, enriched documents** stored in SharePoint, and make them queryable via **Microsoft 365 Copilot**.
- **Two main functionalities**:
  - **Document Enrichment (Function App)**  
    - Watch a **Source library/folder** for new/updated `.docx` files.  
    - Apply a **company Word template** and rules to:  
      - Build standard sections (Overview, Functional Details, Technical Details, Troubleshooting, Revision History, Screenshots).  
      - Preserve images, generate TOC, format revision tables.  
    - Save enriched docs to a **Destination library** and update metadata on the source item.
    - Purely **rule-based OpenXML**, **no AI**, **no database** (SharePoint-only).
  - **Copilot Agent (O365)**  
    - Microsoft 365 Copilot Agent uses the **Destination/enriched library** as its knowledge base.  
    - Users ask questions; Copilot answers from enriched documents with **citations** and **links**.

---

## 2. What we understand (architecture & key principles)

- **Architecture**:
  - **SharePoint Online** hosts the Source + Destination libraries and the **SPFx Admin Panel** app.
  - **Azure Functions (.NET 8)** runs the backend enrichment pipeline (`SMEPilot.FunctionApp`):
    - Webhook handler: `ProcessSharePointFile` (Graph notifications → download → enrich → upload).  
    - Subscription setup/renewal: `SetupSubscription`, `WebhookRenewal`.  
    - Core engine: `DocumentEnricher` + `DocumentExtractor` + helpers.  
  - **Graph webhooks** trigger the Function App when files change in the Source library.
  - **Microsoft 365 Copilot** connects directly to the Destination library (no custom bot).
- **Key principles**:
  - **No AI in enrichment** – deterministic OpenXML-based rules only.  
  - **No database** – all state is files + metadata in SharePoint.  
  - **Event-driven, serverless** – Functions + Graph webhooks, no polling.  
  - **Minimal permissions** – app-only Graph permissions for install, SharePoint permissions for runtime.  
  - **Observability** – Serilog file logs + Application Insights, rich telemetry and error details.

---

## 3. What we implemented (feature & code-level view)

- **Implemented features**:
  - Full enrichment pipeline from Source → Function App → Destination.  
  - OpenXML-based `DocumentEnricher` that:
    - Reads mapping/config from JSON (`Config/mapping.json`, `FieldMappingSpec.json`, `RuleMapping.json`, `TagMapping.json`).  
    - Builds standardized sections, TOC, revision history, and screenshot sections.  
    - Tracks confidence and flags low-confidence mappings for manual review.  
  - Robust file handling:
    - Supports `.docx` with text, tables, and images; optional **OCR for images/PDF** when configured.  
    - Handles corrupted docs, timeouts, duplicates, and large files with clear status.  
  - Telemetry & resilience:
    - **Serilog** to `SMEPilot.FunctionApp/Logs` (compact JSON).  
    - **Application Insights** for traces/metrics.  
    - **NotificationService** (optional email via Azure Communication Services).  
    - **RateLimitingService** to control throughput.  
  - **SPFx Admin Panel** web part:
    - Lets admins configure folders, template, limits, and Copilot prompt from SharePoint UI.
- **Not implemented in code (configuration-only)**:
  - Copilot Agent itself – configured in **Copilot Studio / O365 Admin**, not in this repo.

---

## 4. Permissions required while installing (tenant/app level)

- **SharePoint / App Catalog**:
  - SharePoint Admin (or delegated) to:
    - Upload and deploy the `.sppkg` package.  
    - Approve and deploy the SPFx app to sites.
- **Azure AD / Microsoft Graph** (app-only):
  - App registration used by the Function App needs at least:
    - `Sites.ReadWrite.All`  
    - `Files.ReadWrite.All` (or similar Files.ReadWrite variant)  
  - Admin consent required in Azure AD.
- **Azure subscription**:
  - Permission to create/configure:
    - Function App (Consumption or App Service plan).  
    - Application Insights.  
    - Optional: Azure Communication Services (email) and Azure Computer Vision (OCR).

---

## 5. Permissions required while developing (local dev/test)

- **SharePoint / SPFx dev**:
  - Access to a **developer site collection** where you can:
    - Add the SPFx app and web part.  
    - Create/manage document libraries.  
  - Typically **Site Owner** or equivalent permissions.
- **Function App / Azure**:
  - Rights to:
    - Deploy to the Azure Function App (Contributor on the resource group is enough).  
    - Manage App Settings (for Graph credentials, telemetry, etc.).
- **Graph / AAD**:
  - Ability to **register apps** and **update secrets/certificates** for the SMEPilot AAD app.  
  - Ability to request or view granted Graph permissions for troubleshooting.
- **Local tooling**:
  - Node.js LTS (for SPFx) and npm.  
  - .NET SDK 8.0 (for Function App).  
  - Access to `ngrok` or similar if you test webhooks locally.

---

## 6. User configurations (what admins can set)

All user-facing configuration is done through the **SPFx Admin Panel** web part:

- **Document processing**:
  - **Source folder/library path**: where users upload raw `.docx` documents.  
  - **Destination library path**: where enriched documents are stored (e.g., `SMEPilot Enriched Docs`).  
  - **Template file URL**: path to the `.dotx` (or `.docx`) template used by the enricher.  
  - **Max file size (MB)**: default ~50 MB; files above are rejected.  
  - **Processing timeout (seconds)** and **max retries**: guard rails for the Function App.
- **Copilot**:
  - **Copilot prompt / system instructions**: default prompt for SMEPilot Agent (editable).  
  - **Access points flags**: whether Copilot is intended for Teams/Web/O365 (documentation only – actual deployment still in O365).
- **Notifications & monitoring** (where implemented):
  - **Admin email** for failure/alert notifications.  
  - Settings for error-handling behavior (e.g., whether to move failed docs to a special library).

These configuration values are stored in SharePoint (e.g., `SMEPilotConfig` list) and read by the Function App at runtime.

---

## 7. Important data for developers

- **Code structure**:
  - `SMEPilot.FunctionApp/Program.cs` – DI wiring, logging, Application Insights, config validation, OCR and Spire licensing.  
  - `SMEPilot.FunctionApp/Functions/` – Azure Functions:
    - `ProcessSharePointFile.cs` – main webhook handler and enrichment orchestrator.  
    - `SetupSubscription.cs`, `WebhookRenewal.cs` – Graph webhook management.  
    - `TestTemplateEnrichment.cs` – diagnostic/test entry point (large, but very useful for debugging enrichment).  
  - `SMEPilot.FunctionApp/Services/`:
    - `DocumentEnricher.cs` – **core enrichment engine** (large “god class”; start here to understand sectioning & mapping).  
    - `TemplateProcessor.cs`, `AzureOpenAIService.cs` (optional enhancements; keep enrichment rule-based).  
    - `NotificationService.cs`, `RateLimitingService.cs`, `TelemetryService.cs`, `TemplateProcessor.cs`, etc.
  - `SMEPilot.FunctionApp/Helpers/`:
    - `Config.cs` – reads environment/config values; central for limits and feature flags.  
    - `DocumentExtractor.cs`, `StructureBuilder.cs`, `RichSectionExtractor.cs` – document parsing and structure building.  
    - `GraphHelper.cs` – Graph/SharePoint I/O.  
    - Sanitizers, scoring, signal generation, deduplication helpers, etc.
  - `SMEPilot.FunctionApp/Config/`:
    - `FieldMappingSpec.json`, `mapping.json`, `RuleMapping.json`, `TagMapping.json` – define how headings/content map to template sections and tags.
  - `SMEPilot.SPFx/`:
    - `src/webparts/adminPanel/` – Admin Panel web part and React component.  
    - `src/services/FunctionAppService.ts` – client for calling the Function App.  
    - `src/services/SharePointService.ts` – wraps SharePoint list/folder/file APIs.
- **Architecture diagram**:
  - File: `Knowledgebase/Diagrams/SMEPilot_Architecture_Diagram.drawio` (open in diagrams.net).  
  - Shows high-level flows: User → SharePoint → Function App → Enriched Docs, and Copilot → SharePoint.
- **Key implementation notes**:
  - Enrichment is **idempotent** where possible; repeated processing of same file is safe.  
  - `DocumentEnricher` uses a custom property (`SMEPilot_Enriched`) to mark already-processed documents.  
  - Many features are driven by JSON configs – prefer changing mapping/config over hard-coding logic.

---

## 8. Important data for users (how to actually use it)

- **For business users (document authors)**:
  - Upload your **functional/technical `.docx` documents** into the **configured Source library** (ask your admin if unsure).  
  - Wait for processing (a few seconds to a few minutes, depending on size).  
  - Find the **enriched version** in the configured **Destination library** (name and location decided by your admin).  
  - Enriched documents:
    - Have a standard layout and sections.  
    - Are the **only documents** Copilot uses for answers.
- **For Copilot users (consumers of the knowledge base)**:
  - Use Microsoft 365 Copilot entry points configured by your organization (Teams, web, Office apps).  
  - Ask questions about the content covered by enriched docs (e.g., “How do we configure product X?”).  
  - Copilot will:
    - Answer based on enriched documents in the Destination library.  
    - Provide **citations and links** back to the source documents.  
    - Respect your **SharePoint permissions** (you only see answers from documents you can read).
- **Known limitations**:
  - Only **`.docx`** (and optionally OCR’d PDFs/images when configured) are supported.  
  - Files larger than the configured **max size** are skipped with a clear error status.  
  - Indexing delay in SharePoint Search means newly enriched docs may not be immediately available to Copilot.

---

**Where to start:**
- **Admin / Architect** – read sections **1–6** and configure the Admin Panel + Copilot.  
- **Developer** – read sections **2, 3, 5, 7** and explore the Function App + SPFx code.  
- **End user** – read section **8** (plus any local guidance from your admin).
