## SMEPilot – KT (Knowledge Transfer) Document

This document is intended for **handover / onboarding** of new engineers and functional owners.  
It combines both **functional** and **technical** perspectives into a single narrative.

---

### 1. Product Summary (Functional View)

**What SMEPilot solves**

- Many teams create functional and technical specs in different formats/styles.
- Downstream consumers (managers, customers, Copilot) need:
  - Consistent structure,
  - Clear sections and headings,
  - Searchable, well‑formatted documents.

**SMEPilot’s role**

- Monitor a SharePoint folder for new/updated documents.
- Automatically:
  - Extract key content and sections,
  - Map them into a **standard Word template** (`.dotx`),
  - Append tables like version history/change log,
  - Add/update a **Table of Contents**,
  - Optionally create a **PDF**.
- Save the enriched outputs into a dedicated destination folder for:
  - End users (reading, sharing),
  - Copilot / search indexing.

**Core flows (functional)**

1. **Author**
   - Uploads or updates a document in the Source folder.
2. **SMEPilot**
   - Detects the change via Graph webhook.
   - Processes and enriches the document.
   - Saves enriched copy + optional PDF in Destination folder.
3. **Consumer**
   - Reads the enriched version or queries via Copilot backed by those documents.

---

### 2. High‑Level Architecture (Technical)

**Components**

- **Azure Function App (`SMEPilot.FunctionApp`)**
  - Functions:
    - `ProcessSharePointFile` – webhook handler & enrichment pipeline.
    - `SetupSubscription` – create/update Graph webhook subscriptions.
    - `WebhookRenewal` – timer to renew expiring subscriptions.
    - `TenantHealth` – health & configuration per tenant.
    - `ConsentComplete` – admin consent landing page.

- **SPFx Admin Panel (`SMEPilot.SPFx`)**
  - React web part for site‑level configuration:
    - Source folder, Destination folder, Template file.
    - Processing settings, enriched output type.
    - Copilot prompt and access points.
    - Admin‑only button for Azure AD admin consent.

- **Word TOC Service (`WordTocService`)**
  - Optional microservice used by the Function App to update dynamic TOC fields post‑merge.

- **SharePoint**
  - Lists:
    - `SMEPilotConfig` – per site configuration.
    - `SMEPilotRuns` – per document processing history for idempotency and diagnostics.
  - Libraries/folders:
    - Source documents (raw).
    - Destination documents (enriched and PDF).

- **Microsoft Graph**
  - Webhooks (`/subscriptions`) for drive/list resources.
  - File read/write operations.
  - Export to PDF via `content?format=pdf`.

---

### 3. Detailed Runtime Flow

#### 3.1 Configuration and Installation

- Admin installs the SPFx solution and adds the **SMEPilot Admin Panel** web part to a page.
- Admin configures:
  - `SourceFolderPath`,
  - `DestinationFolderPath`,
  - `TemplateFileUrl` (and derived `TemplateLibraryPath` / `TemplateFileName`),
  - Processing settings (`MaxFileSizeMB`, `ProcessingTimeoutSeconds`, `MaxRetries`),
  - `EnrichedOutputType` (Docx/Pdf/Both),
  - `CopilotPrompt`, `AccessTeams`, `AccessWeb`, `AccessO365`.
- On save, SPFx:
  - Ensures **`SMEPilotConfig`** list and required columns exist.
  - Saves/updates the single configuration row.
  - Calls `SetupSubscription` to create/update the Graph webhook subscription.
  - `SetupSubscription` writes `SubscriptionId`, `SubscriptionExpiration`, and `ClientStateSecret` back to `SMEPilotConfig`.

#### 3.2 Document Processing (Upload/Update)

- Graph sends a notification to `ProcessSharePointFile` when a tracked file changes:
  - Payload includes subscriptionId, resource, optional `resourceData` with `driveId`, `id`, `siteId`, etc.
- `ProcessSharePointFile`:
  1. Parses the `GraphChangeNotification` body.
  2. Skips non‑`updated` change types.
  3. Performs early dedup (in‑memory key based on subscription/resource/item).
  4. Resolves `driveId`, `itemId`, `fileName` via:
     - `resourceData` for list items, or
     - Delta/recent‑items scan for pure drive notifications.
  5. Resolves `tenantId` from notification or config.
  6. Fetches `siteId` from the `DriveItem` or `GetSiteIdFromDriveAsync`.
  7. Loads configuration via `Config.LoadSharePointConfigAsync` (backed by `ConfigService` and `SMEPilotConfig`).
  8. Validates:
     - `ClientState` equals `ClientStateSecret` (security).
     - File is in `SourceFolderPath` (if configured).
     - File type and size within supported limits.
  9. Uses `RateLimitingService` to throttle per tenant if necessary.
  10. Checks the tracking list (`SMEPilotRuns`) for:
      - In‑progress runs (to avoid double‑processing),
      - Permanent failures (to skip unsupported types),
      - Last **succeeded** content hash for idempotency.
  11. Downloads the file (`GraphHelper.DownloadFileStreamAsync`) into a temp location.
  12. Extracts text/images via `DocumentExtractor` according to file type.
  13. Builds a structured `DocumentModel` using `DocumentEnricher` or structured DOCX extraction.
  14. Performs template merge:
      - DOCX → `DocumentMergeApi.MergeService`.
      - Non‑DOCX → `TemplateProcessor.FillTemplate` with `contentMap`.
  15. Optionally calls the TOC update pipeline (WordTocService or in‑process).
  16. Resolves the destination site/drive/folder and uploads:
      - Enriched DOCX (`_graph.UploadFileBytesAsync`),
      - PDF (if `EnrichedOutputType` is `Pdf` or `Both`).
  17. For `Pdf`‑only mode, attempts to delete the DOCX after successful PDF export.
  18. Records a `ProcessingRunRecord` in `SMEPilotRuns` with:
      - `RawDriveId`, `RawItemId`, `ContentHash`, `Version`, `Status`, `ErrorMessage`, `EnrichedUrl`, `EnrichedDriveId`, `EnrichedItemId`.
  19. Sends telemetry via `TelemetryService.TrackDocumentProcessing`.

#### 3.3 Subscription Renewal

- `WebhookRenewal` timer:
  - Runs every 2 hours.
  - For each tenant in `TenantRegistryService` (fallback to `GraphTenantId`):
    - Calls `GetSubscriptionsAsync`.
    - Filters to SMEPilot subscriptions with notification URL containing `/api/ProcessSharePointFile`.
    - Computes `renewalThreshold = now + SubscriptionRenewalWindowHours` (configurable, default 24h).
    - For each subscription expiring before `renewalThreshold`:
      - Calls `CreateSubscriptionAsync` to create a fresh one (3‑day expiry).
      - Reuses `ClientState` if present; otherwise generates new GUID.
      - Deletes the old subscription.
      - Writes `SubscriptionId`, `SubscriptionExpiration`, and `ClientStateSecret` to `SMEPilotConfig`.

---

### 4. Configuration Details

**Site‑level configuration (`SMEPilotConfig` list)**

- Key fields:
  - `SourceFolderPath`
  - `DestinationFolderPath`
  - `TemplateFileUrl`
  - `TemplateLibraryPath`
  - `TemplateFileName`
  - `MaxFileSizeMB`
  - `ProcessingTimeoutSeconds`
  - `MaxRetries`
  - `CopilotPrompt`
  - `AccessTeams`, `AccessWeb`, `AccessO365`
  - `SubscriptionId`
  - `SubscriptionExpiration`
  - `ClientStateSecret`
  - `EnrichedOutputType`
  - `LastUpdated`

**Global / environment configuration**

- `Graph_TenantId`, `Graph_ClientId`, `Graph_ClientSecret` – Graph app credentials.
- Retry/timeouts:
  - `MaxRetryAttempts`, `RetryDelaySeconds`, `MaxRetryDelaySeconds`.
  - `MaxUploadRetries`, `MaxMetadataRetries`, `FileLockWaitSeconds`.
- File size:
  - `MaxFileSizeBytes` (or `MaxFileSizeMB` in SharePoint).
- Notification dedup:
  - `NotificationDedupWindowSeconds`.
- Subscription renewal:
  - `SubscriptionRenewalWindowHours` (used by `WebhookRenewal`).
- Word TOC service:
  - `EnableWordTocService`, `WordTocServiceUrl`, `WordTocServiceTimeoutSeconds`.

---

### 5. Troubleshooting Playbook

**Issue: No enriched docs are produced**

1. Check **SMEPilotConfig**:
   - Are `SourceFolderPath` and `DestinationFolderPath` correct?
   - Is `SubscriptionId` populated?
2. Check **Function logs** for `ProcessSharePointFile`:
   - Look for security warnings (clientState mismatch),
   - Validation messages about source folder,
   - Errors from Graph (permissions, throttling).
3. Check **WebhookRenewal** logs:
   - Are subscriptions being found and renewed?
4. Check **SMEPilotRuns**:
   - Is there a recent `ProcessingRunRecord` for the file?
   - What is its `Status` and `ErrorMessage`?

**Issue: Template content shows in the wrong place**

1. Inspect `.dotx` template:
   - Confirm placeholder names and content controls match keys in `contentMap` (`TemplateProcessor` logs).
2. Use Markdown test harness:
   - Compare `Test/raw*.md` with `Test/TEMPLATE.md` and `Test/Generated*.md` to understand mapping behavior.
3. Adjust mapping rules in:
   - `Config/mapping.json`,
   - Or in `TemplateProcessor.BuildContentMapFromTemplate`.

**Issue: Too many Graph calls / throttling**

1. Check timer schedule and renewal window:
   - `WebhookRenewal` cron (`0 0 */2 * * *`) and `SubscriptionRenewalWindowHours`.
2. Ensure filters are working:
   - `IsSmepilotSubscription` must be narrowing to only our subscriptions.
3. Consider tuning:
   - Renewal window (e.g., 24h → 12h or vice versa),
   - Timer frequency (e.g., 2h → 4h) depending on real metrics.

---

### 6. Roles and Responsibilities

**Developers**

- Maintain Function App and SPFx code.
- Own templates for `mapping.json`, TOC logic, and content mapping.
- Respond to production incidents (Graph errors, template breakages).

**Admins**

- Configure sites via Admin Panel.
- Grant Azure AD admin consent.
- Monitor `SMEPilotConfig` and `SMEPilotRuns`.

**Managers / Product Owners**

- Decide where SMEPilot is deployed.
- Approve standards for templates and Copilot prompt behavior.

**End Users**

- Upload raw documents to Source folder.
- Consume enriched outputs and report issues.

---

### 7. Onboarding Checklist for New Team Members

1. Read:
   - `DeveloperGuide.md` (this repo),
   - `AdminInstallGuide.md`,
   - `EndUserGuide.md`,
   - Any architecture diagrams under `Knowledgebase/Diagrams`.
2. Understand:
   - The full document flow (upload → webhook → enrichment → destination).
   - Where configuration lives (`SMEPilotConfig`, env vars).
3. Set up a **local dev environment**:
   - Clone repo,
   - Configure local settings for `SMEPilot.FunctionApp` and `SMEPilot.SPFx`,
   - Run unit/manual tests with sample docs in `Test/`.
4. Practice:
   - Fix a simple template mapping issue in a non‑prod environment.
   - Trace an example file from upload to enriched output using logs and tracking lists.

This KT document should be used alongside the more focused Developer, Manager, Admin, and End User guides for a complete understanding of SMEPilot.


