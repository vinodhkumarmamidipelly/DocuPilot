# 🏗️ Technical Architecture – Contents Checklist (for SMEPilot)

A technical architecture document should be concise, decision-oriented, and implementation-ready. For SMEPilot, include the following sections.

## 1) Executive context (1–2 paragraphs)
- Problem and scope: Standardize uploaded docs; enable Copilot Q&A.
- In/Out of scope: Rule-based enrichment only (no AI); no database.

## 2) Quality attributes (ranked)
- Determinism (rule-based formatting) – High
- Operability/Observability (logs, alerts) – High
- Security/Least-privilege – High
- Performance (≤60s for ≤50MB) – Medium
- Scalability (event-driven bursts) – Medium

## 3) Constraints and key decisions
- No database; storage is SharePoint only
- Enrichment is OpenXML rule-based (NO AI)
- Copilot queries SharePoint directly (Function App not in query path)
- Destination must be “SMEPilot Enriched Docs” for indexing

## 4) System context (C4: System) – Who interacts with what
- Actors: Business User (uploads), Admin (installs/configures), End User (queries)
- Systems: SharePoint Online, Azure Function App, Microsoft 365 Copilot
- Services: Microsoft Graph API, Azure AD, Application Insights, Microsoft Search

## 5) Containers (C4: Container) – Major deployables and responsibilities
- SharePoint Online
  - Source Folder (input docs)
  - Templates Library (.dotx)
  - SMEPilot Enriched Docs (output; indexed)
  - SMEPilotConfig list (installation-time configuration)
  - Metadata on source items (status, links)
- Azure Function App (.NET)
  - HTTP Trigger `/api/ProcessSharePointFile` (webhook handler)
  - Document Enricher (OpenXML, rule-based)
  - Graph Client (download/upload, metadata)
  - Config Reader (reads SMEPilotConfig)
  - Error/Retry and Logging (App Insights)
- Microsoft 365 Copilot
  - Copilot Studio config (data source, instructions)
  - Query path uses Microsoft Search → SharePoint

## 6) Data view
- Storage locations: Only SharePoint (files + metadata)
- Enriched outputs: Saved under SMEPilot Enriched Docs
- Metadata on source: Enriched flag, status, enriched link, processed date

## 7) Integration/API view
- Webhooks: SharePoint drive/webhook via Microsoft Graph → Function App
- File ops: Graph API download/upload; metadata read/write
- AuthN: Azure AD app-only credentials (least privilege)

## 8) Security model
- App Registration permissions: Sites.ReadWrite.All, Files.ReadWrite.All, Webhooks.ReadWrite.All (app-only)
- SharePoint permissions: end users (Contribute to Source, Read Enriched)
- No PII or external data stores introduced by SMEPilot

## 9) Operational concerns
- Observability: Application Insights (logs, metrics, alerts)
- Reliability: Retries, idempotency checks, error folders (Rejected/Failed)
- Webhook renewal cadence (per Graph limits)

## 10) Deployment & configuration
- Installation-time configuration (SPFx admin UI):
  - Source folder, destination folder (required name), template path
  - Copilot settings (enable, access points, visibility)
  - Processing limits and notifications

## 11) Risks & mitigations
- Webhook duplication → idempotency, dedup window
- Large/locked files → exponential backoff, failure routing
- Missing template/config → validation + admin notifications

## 12) Future extensibility (non-commitment)
- Pluggable mapping rules, multi-template support, optional OCR
- Optional AI enrichment (out of scope now)

---

# ✅ Diagram Acceptance Checklist (Manager-facing)
- Emphasizes two core capabilities (Enrichment + Copilot) side-by-side
- Shows only the essential systems and services (no clutter)
- Clear boundaries: SharePoint, Function App, Copilot, platform services
- Minimal arrows with meaningful labels (Upload → Webhook → Enrich → Index; Query → Search → Answer)
- Notes critical decisions: Rule-based, No DB, Direct Copilot → SharePoint
- Highlights installation-time configuration at a glance
- Uses consistent, readable styles (looks professional/human-made)

