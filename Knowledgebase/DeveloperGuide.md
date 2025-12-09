## SMEPilot – Developer Guide

This guide is for engineers who need to understand, maintain, or extend SMEPilot.  
It explains the architecture, core flows, important files, and how to debug issues.

---

### 1. Solution Overview

**High‑level components**

- **Function App (`SMEPilot.FunctionApp`)**  
  - Azure Functions (.NET 8, isolated worker).  
  - Handles Graph webhooks, document enrichment, template merge, subscription setup/renewal, and health checks.
- **SPFx Admin Panel (`SMEPilot.SPFx`)**  
  - SharePoint Framework web part used to configure SMEPilot on a site (source/destination folders, template, etc.).
- **Word TOC Service (`WordTocService`)**  
  - Optional auxiliary service to update Word Table of Contents fields after merge.
- **SharePoint**  
  - Stores configuration (`SMEPilotConfig` list) and raw/enriched documents.
- **Microsoft Graph**  
  - Webhook subscriptions + file CRUD + export to PDF.

**Main runtime flows**

- **Document Enrichment Flow**
  1. File is added/updated in the configured **Source folder**.
  2. Graph sends a **change notification** to `ProcessSharePointFile` (Function App).
  3. Function resolves `siteId/driveId/itemId`, validates `clientState`, and checks idempotency.
  4. File is downloaded, text/images are extracted (`DocumentExtractor`).
  5. Content is sectioned and mapped (`DocumentEnricher` + `TemplateProcessor` or `DocumentMergeApi` for DOCX).
  6. A formatted enriched document is written to the **Destination folder** (and optionally a PDF).
  7. Processing metadata is written to the tracking list (`SMEPilotRuns`) and telemetry.

- **Subscription Lifecycle**
  - **Setup**: `SetupSubscription` function (or Admin Panel) creates a Graph subscription for the source library and stores `SubscriptionId`, `SubscriptionExpiration`, `ClientStateSecret` in `SMEPilotConfig`.
  - **Renewal**: `WebhookRenewal` timer function runs every 2 hours and renews **only** subscriptions whose `NotificationUrl` points to `/api/ProcessSharePointFile` and whose expiry is within `_cfg.SubscriptionRenewalWindowHours` (default 24h).

- **Configuration Loading**
  - `Config.LoadSharePointConfigAsync` loads `SMEPilotConfig` for the site and caches it.
  - `ConfigService` maps SharePoint fields into a dictionary used by `Config` getters (`SourceFolderPath`, `DestinationFolderPath`, `TemplateFileUrl`, etc.).

---

### 2. Key Projects and Files

**Function App**

- `SMEPilot.FunctionApp/Program.cs`  
  - Dependency injection setup for:
    - `GraphHelper`, `Config`, `DocumentExtractor`, `TemplateProcessor`, `TelemetryService`,
    - `RateLimitingService`, `TenantRegistryService`, etc.
  - Registers function classes explicitly (`ProcessSharePointFile`, `SetupSubscription`, `WebhookRenewal`, `TenantHealth`, `ConsentComplete`).

- `SMEPilot.FunctionApp/Functions/ProcessSharePointFile.cs`  
  - **Central webhook handler and manual test endpoint.**
  - Responsibilities:
    - Parse Graph notifications (`GraphChangeNotification` model).
    - Extract `tenantId` (`GetTenantIdFromNotification`) and validate `ClientState`.
    - Resolve `driveId`, `itemId`, `fileName` via:
      - list‑based notifications (Site/List/ListItem), or
      - drive‑based (`ResourceData.DriveId`, fallback delta/recent-items scan).
    - Load SharePoint config via `Config.LoadSharePointConfigAsync`.
    - Validate that the file is inside `SourceFolderPath`.
    - Apply idempotency and rate limiting:
      - Tracking list (`SMEPilotRuns` via `GraphHelper.GetLatestProcessingRunAsync`).
      - `_cfg.NotificationDedupWindowSeconds` and `_rateLimiter`.
    - Call the enrichment pipeline:
      - For **DOCX**: `DocumentMergeApi` (core merge).
      - For **non‑DOCX**: `DocumentEnricher` + `TemplateProcessor`.
    - Upload enriched doc to destination folder, mirror subfolders, and optionally export to PDF (via `GraphHelper.DownloadFileAsPdfStreamAsync`).
    - Record processing result in tracking list (`UpsertProcessingRunAsync`) and telemetry.

- `SMEPilot.FunctionApp/Functions/SetupSubscription.cs`  
  - Creates a Graph webhook subscription for a given `siteId`/`driveId`/`sourceFolderPath`.
  - Generates a `ClientStateSecret` (GUID) and saves it into `SMEPilotConfig`.
  - Used by the Admin Panel to set up subscriptions from SharePoint.

- `SMEPilot.FunctionApp/Functions/WebhookRenewal.cs`  
  - Timer trigger (`0 0 */2 * * *` → every 2 hours).
  - For each active tenant (`TenantRegistryService` + `GraphTenantId` fallback):
    - Calls `GraphHelper.GetSubscriptionsAsync(tenantId)`.
    - Filters to **SMEPilot‑owned** subscriptions via `IsSmepilotSubscription` (notification URL contains `/api/ProcessSharePointFile`).
    - Computes `renewalThreshold = UtcNow + _cfg.SubscriptionRenewalWindowHours` (default 24h).
    - For each subscription expiring within that window:
      - Creates a new subscription with same resource/notificationUrl and a 3‑day expiry.
      - Reuses existing `ClientState` or generates a new one if missing.
      - Deletes the old subscription.
      - Updates `SubscriptionId`, `SubscriptionExpiration`, and `ClientStateSecret` in `SMEPilotConfig`.

- `SMEPilot.FunctionApp/Functions/TenantHealth.cs`  
  - Health endpoint for listing configured tenants and their status.
  - Reads tenant info from `MultiTenant_TenantInfo` (JSON) via `TenantRegistryService`.

- `SMEPilot.FunctionApp/Functions/ConsentComplete.cs`  
  - Simple GET/OPTIONS endpoint returning a static HTML “permissions granted” page.
  - Used as redirect URL after Azure AD admin-consent for the multi-tenant app.

**Core services**

- `Helpers/Config.cs`  
  - Central config facade.
  - Reads:
    - Environment vars (Graph credentials, retry settings, file size limits, TOC service).
    - SharePoint config values via `ConfigService` (`SourceFolderPath`, `DestinationFolderPath`, `TemplateFileUrl`, `TemplateLibraryPath`, `TemplateFileName`, `ClientStateSecret`, etc.).
    - `SubscriptionRenewalWindowHours` (renewal window for `WebhookRenewal`).

- `Services/ConfigService.cs`  
  - Loads the `SMEPilotConfig` list item for a site and builds a `Dictionary<string,object>`.
  - Handles defaults when values are missing or list doesn’t exist.

- `Helpers/GraphHelper.cs`  
  - Wrapper around Microsoft Graph SDK + raw HTTP for:
    - File download/upload, delta queries, recent-items, PDF export.
    - Site/drive/list resolution (`GetSiteIdFromDriveAsync`, `ResolveFolderPathAsync`, `GetDriveItemForListItemAsync`).
    - Webhook subscription CRUD.
    - Tracking list (`SMEPilotRuns`) CRUD and mapping to `ProcessingRunRecord`.

- `Helpers/DocumentExtractor.cs`  
  - Extracts text + images from DOCX/PPTX/XLSX/PDF/images.
  - Also supports a “structured” extraction for DOCX (paragraphs, tables, images) for the core merge engine.

- `Services/DocumentEnricher.cs`  
  - Rule‑based document sectioning and classification:
    - Uses `Config/mapping.json` + tag mappings to build a `DocumentModel` (sections, headings, bodies).
    - Used for non‑DOCX template flows and older enrichment logic.

- `Services/TemplateProcessor.cs`  
  - Large, consolidated service handling:
    - Template‑driven mapping from `DocumentModel` to placeholders.
    - Filling content controls (SDT), plain‑text tokens (`[Tag]`, `{Tag}`, `{{Tag}}`).
    - Inserting raw content at markers (`[Document Content Starts Here]`) with style preservation.
    - TOC insertion at specific tokens (`[TABLE_OF_CONTENTS]`, `[TOC]`, `[Table of Contents]`).
    - Revision history and change log table expansion.
    - Image/table insertion, header/footer/numbering preservation.

- `Services/RateLimitingService.cs`  
  - In‑memory rate limiter used by `ProcessSharePointFile` to avoid overload from noisy tenants.

- `Services/TelemetryService.cs`  
  - Application Insights wrapper (custom events, metrics, dependency tracking).
  - `TrackDocumentProcessing` now includes `EnrichedOutputType`.

**SPFx Admin Panel**

- `SMEPilot.SPFx/src/webparts/adminPanel/components/AdminPanel.tsx`  
  - Main React component for configuration wizard:
    - Part 1: Site/library/folder/template selection.
    - Part 2: Copilot agent prompt + access points.
    - Part 3: Processing settings (max file size, timeout, retries, **Enriched Output Type**).
    - Buttons: Save configuration, Test configuration.
    - “Grant permissions (Admin only)” button linking to `ConsentComplete` URL.

- `SMEPilot.SPFx/src/services/SharePointService.ts`  
  - REST wrapper for:
    - Ensuring `SMEPilotConfig` list and all columns exist (idempotent).
    - Saving/loading configuration (including `EnrichedOutputType`).
    - Uploading template files.
    - Validating source/destination folders and template presence.

- `SMEPilot.SPFx/src/services/FunctionAppService.ts`  
  - Calls backend `SetupSubscription` to create/refresh Graph webhook subscriptions.

---

### 3. Debugging and Troubleshooting

**Where to start (common scenarios)**

- **Uploaded doc is not enriched**
  - Check Azure Function logs for `ProcessSharePointFile`:
    - Look for warnings: `[SECURITY] ClientState mismatch`, `[VALIDATION] file not in source folder`, `[IDEMPOTENCY] Skipping`, file too large, unsupported extension.
  - Confirm Graph subscription:
    - In `SMEPilotConfig` list: `SubscriptionId`, `SubscriptionExpiration` populated.
    - In Azure portal → `WebhookRenewal` logs: is it renewing successfully?
  - Check `SMEPilotRuns` tracking list:
    - Look for latest record for `RawDriveId` + `RawItemId`.
    - `Status` (`Succeeded`, `Processing`, `Failed`) and `ErrorMessage`.

- **Template issues (wrong content placement / TOC)**
  - Inspect the `.dotx` template:
    - Confirm placeholders (`[Tag]`, `{{Tag}}`, or content controls) match keys in `contentMap` (see logs from `TemplateProcessor.BuildContentMapFromTemplate`).
    - For TOC: ensure one of `[TABLE_OF_CONTENTS]`, `[TOC]`, `[Table of Contents]` exists where TOC should appear.
  - Look at `SMEPilot.FunctionApp/Test/*.md`:
    - `TEMPLATE.md`, `raw*.md`, `Generated*.md` help simulate and diff the merge behavior.

- **PDF not generated / wrong output type**
  - Check `SMEPilotConfig.EnrichedOutputType`:
    - `"Both"` → DOCX + PDF (default).
    - `"Docx"` → only DOCX.
    - `"Pdf"` → attempt to create PDF and delete DOCX.
  - Logs in `ProcessSharePointFile` under `[PDF]` namespace explain export/upload failures.

- **Webhook not firing / renewals not working**
  - Verify subscription in Graph (via Graph Explorer or portal).
  - Check `WebhookRenewal` logs:
    - Confirm it finds subscriptions for your tenant.
    - Confirm they pass `IsSmepilotSubscription` (notification URL contains `/api/ProcessSharePointFile`).
    - Confirm renewals happen before expiration.

**Dev tips**

- When modifying `ProcessSharePointFile`, be careful of:
  - Idempotency logic (tracking list + dedup cache).
  - Multi‑tenant behavior (always pass `tenantId` to `GraphHelper`).
  - Source/destination folder validation (never process outside configured Source).

- When modifying `TemplateProcessor` or `DocumentEnricher`:
  - Use the Markdown test harness (`Test/*.md`) and local `.dotx` templates under `Templates/`.
  - Keep placeholder naming consistent; avoid breaking existing customer templates.

---

### 4. How to Add or Change Features

**Add a new config field**

1. Add column to `SMEPilotConfig` via `SharePointService.addListColumns` (SPFx).  
2. Load it in `ConfigService.GetConfigurationAsync`.  
3. Expose via `Config` property.  
4. Use in backend services or functions.  
5. Wire to Admin Panel (`IConfiguration` in SPFx) and UI controls.

**Add a new webhook‑driven flow**

1. Add a new function under `Functions/` with `[Function("NewEndpoint")]` and appropriate trigger.  
2. Register any new services in `Program.cs`.  
3. Expose necessary config via `Config`.  
4. Add SPFx UI / config changes as needed.

---

### 5. Quick Reference – Important Classes

- **Functions**
  - `ProcessSharePointFile` – main enrichment pipeline.
  - `SetupSubscription` – create/update Graph subscription + save config.
  - `WebhookRenewal` – periodic subscription renewal.
  - `TenantHealth` – tenants health overview.
  - `ConsentComplete` – admin consent landing page.

- **Services / Helpers**
  - `Config`, `ConfigService` – configuration loading.
  - `GraphHelper` – all Graph interactions.
  - `DocumentExtractor` – file format extraction.
  - `DocumentEnricher` – rule‑based sectioning/classification.
  - `TemplateProcessor` – template merge / TOC / revision history.
  - `RateLimitingService` – per‑tenant throttling.
  - `TelemetryService` – Application Insights wrapper.

- **Models**
  - `GraphChangeNotification`, `GraphNotificationItem` – webhook payloads.
  - `DocumentModel`, `Section` – logical structure used by template engine.
  - `ProcessingRunRecord` – tracking record for SMEPilotRuns list.

Use this guide as your first stop when debugging or making structural changes. For very detailed behavior of the merge engine or template mappings, see comments inside `TemplateProcessor.cs`, `DocumentMergeApi/*`, and `ENRICHMENT_ANALYSIS.md`.


