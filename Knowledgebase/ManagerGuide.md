## SMEPilot – Manager Guide

This guide is for engineering / product managers who need to understand **what SMEPilot does**, how it is built, what risks it carries, and the main operational processes around it.

---

### 1. What SMEPilot Is

**Purpose**

- SMEPilot automatically **enriches documents** (primarily functional/technical specs) stored in SharePoint:
  - Converts raw customer documents into a standardized, branded template.
  - Ensures consistent headers/footers, sections, revision tables, and Table of Contents.
  - Optionally creates a PDF version optimized for SharePoint preview and Copilot.

**Target users**

- **Admins**: configure SMEPilot at the site level (libraries, templates, settings).
- **Authors**: upload raw documents; SMEPilot creates enriched copies.
- **Consumers / Copilot**: search, read, and query enriched specs.

---

### 2. High‑Level Architecture

**Components**

- **Azure Function App (`SMEPilot.FunctionApp`)**
  - Runs backend logic:
    - Graph webhook receiver (`ProcessSharePointFile`).
    - Subscription setup (`SetupSubscription`) and renewal (`WebhookRenewal`).
    - Health & tenant visibility (`TenantHealth`).
    - A simple admin-consent landing endpoint (`ConsentComplete`).

- **SharePoint + SPFx (`SMEPilot.SPFx`)**
  - Admin Panel web part used to:
    - Choose **Source folder** for raw documents.
    - Choose **Destination folder** for enriched documents.
    - Upload/select the `.dotx` template.
    - Control processing settings (max file size, retries, output type DOCX/PDF/Both).
    - Trigger creation of Graph webhooks for the source library.
  - `SMEPilotConfig` list on the site stores all configuration.

- **Word TOC Service (optional)**  
  - Small .NET service that can update TOC fields in Word documents after merge to ensure page numbers are correct.

- **Microsoft Graph**
  - Webhook subscriptions on SharePoint document libraries.
  - File download/upload, delta queries.
  - Export enriched DOCX to PDF.

**Data flow (document enrichment)**

1. User uploads or updates a file in the **Source folder**.
2. Graph webhook fires → Azure Function `ProcessSharePointFile` is called.
3. Function:
   - Validates notification (`clientState` secret).
   - Checks idempotency & file type/size.
   - Downloads file from SharePoint.
   - Extracts structure and content.
   - Applies rules + template to produce an enriched document.
   - Uploads enriched document to **Destination folder** (plus PDF if configured).
4. Processing metadata is recorded in a SharePoint tracking list (`SMEPilotRuns`) and sent to Application Insights.

**Data flow (subscription lifecycle)**

- **Setup**:
  - When an admin saves configuration in the SPFx Admin Panel, the web part calls `SetupSubscription` in the Function App.
  - Function creates/updates a Graph webhook subscription pointing to `/api/ProcessSharePointFile`.
  - Stores `SubscriptionId`, `SubscriptionExpiration`, and `ClientStateSecret` in `SMEPilotConfig`.

- **Renewal**:
  - `WebhookRenewal` timer runs **every 2 hours**.
  - For each tenant:
    - Fetches subscriptions via Graph.
    - Filters to SMEPilot‑owned ones (notification URL contains `/api/ProcessSharePointFile`).
    - Renews any subscription expiring within the configured window (default 24 hours).
    - Updates `SMEPilotConfig` with new IDs and expiration.

---

### 3. Security & Permissions

**Azure AD / Graph**

- SMEPilot is a **multi‑tenant** application:
  - Admins must grant consent once per tenant for the app to access:
    - SharePoint sites/libraries for reading/writing files.
    - Webhook subscription resources.
  - Consent is done via the Admin Panel “Grant permissions (Admin only)” button, which redirects to a standard Azure AD admin consent flow and finally to `ConsentComplete` endpoint.

- **Minimum Graph permissions** (application permissions for the SMEPilot Function App identity):
  - `Sites.ReadWrite.All` – required so SMEPilot can:
    - Read raw documents from the Source folder,
    - Write enriched documents and PDFs to the Destination folder,
    - Manage configuration and tracking lists (`SMEPilotConfig`, `SMEPilotRuns`) via Graph.
  - We intentionally **do not** request broader organization‑level permissions such as `Organization.Read.All`.

**SharePoint**

- SMEPilot needs:
  - Permission to read the configured **Source folder**.
  - Permission to write into the **Destination folder**.
  - Permission to manage items in `SMEPilotConfig` and `SMEPilotRuns` lists.

**Tenant safety and isolation**

- Each notification carries the tenant context; all Graph calls are executed:
  - With per‑tenant client credentials, and
  - With `tenantId`/`siteId` looked up from the notification + configuration.
- The tracking list (`SMEPilotRuns`) is stored per site; no cross-tenant data mixing.

**Webhook hardening**

- Graph webhooks are configured with a secret `clientState` value:
  - Stored in `SMEPilotConfig.ClientStateSecret`.
  - Every incoming notification’s `ClientState` is compared to this secret in `ProcessSharePointFile`.
  - If mismatch → notification is ignored (prevents random/forged POSTs from being processed).

---

### 4. Operational Processes

**Deployment**

- Typical setup:
  1. Deploy Function App (CI/CD or manual publish).
  2. Deploy SPFx solution to tenant app catalog, then to target site (Admin Panel web part).
  3. Admin adds Admin Panel web part to a page and completes configuration (source/destination/template).
  4. Admin clicks “Grant permissions (Admin only)” for Azure AD consent if not already done.

**Monitoring**

- **Azure Monitor / Application Insights**
  - Telemetry for processing success/failure, durations, errors, and dependency calls.
  - Use to track:
    - Number of documents processed.
    - Failure rates and root causes (Graph throttling, invalid config, template issues).

- **SharePoint lists**
  - `SMEPilotConfig`:
    - Shows current configuration, subscription ID, client state, destination paths.
  - `SMEPilotRuns`:
    - Per‑document processing history:
      - Status: `Processing`, `Succeeded`, `Failed`.
      - ErrorMessage (if any).
      - Enriched URL, content hash, version.

**Scaling and performance**

- Function App scales horizontally based on load (depending on hosting plan).
- `RateLimitingService` provides simple in‑memory protection against overload from noisy tenants.
- File size limitations and processing timeouts are configurable to prevent large documents from exhausting resources.

---

### 5. Design Choices & Rationale

- **Template‑driven enrichment instead of arbitrary transformation**
  - Business users maintain a `.dotx` template that reflects the org’s document standard.
  - The system maps raw document content into that template deterministically (no hidden AI/ML).

- **Use of Graph webhooks vs. polling**
  - Webhooks ensure near real‑time processing without constant polling of SharePoint.
  - Lower cost and better responsiveness.

- **Tracking list (`SMEPilotRuns`)**
  - Captures each run’s status and content hash.
  - Enables idempotency (skip reprocessing identical content).
  - Provides an audit trail for support and operations.

- **Config in SharePoint (`SMEPilotConfig`)**
  - Configuration is co‑located with the site where documents live.
  - Admins can see and (carefully) inspect/change settings without redeployments.

---

### 6. What Managers Should Watch

**Key success metrics**

- Number of documents successfully enriched vs. total uploaded.
- Time from upload to enriched output.
- Error rate by category (permissions, unsupported format, template issues).
- Adoption:
  - Number of active sites/tenants.
  - Number of users uploading documents.

**Risk areas**

- **Permissions not granted or partially revoked**:
  - Symptoms: sudden spike in Graph errors, subscription failures, or processing failures.
- **Misconfigured source/destination folders**:
  - Source and destination accidentally pointed at the same location (guardrails exist, but misconfig can still cause confusion).
- **Template changes**:
  - Updating the `.dotx` without coordinating with devs can break placeholder mappings; may need developer review for major template updates.
- **Subscription expiration**:
  - `WebhookRenewal` mitigates this, but prolonged Function App downtime can still lead to expired subscriptions; should be visible in monitoring.

---

### 7. Summary for Managers

- SMEPilot is an **event‑driven, multi‑tenant** document enrichment system built on:
  - Azure Functions, SharePoint, SPFx, and Microsoft Graph.
- It standardizes documents into a consistent template and can optionally create PDFs for best‑effort Copilot searchability.
- Configuration is **per SharePoint site** via an Admin Panel, and per‑tenant **webhook lifecycle** is fully automated (setup + renewal).
- Health and diagnostics are available via:
  - Azure logs/telemetry, and
  - SharePoint tracking/config lists.

With this context, you can make informed decisions about:
- Where to deploy SMEPilot,
- What SLAs to promise,
- What monitoring you require, and
- How to plan future features (e.g., more templates, richer Copilot integration, analytics dashboards).


