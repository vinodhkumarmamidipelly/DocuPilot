# 📘 SMEPilot Enhancement Plan  
**Author:** Vinodh Kumar Mamidipelly  
**Revised:** 2025-01-XX  
**Goal:** Build a sellable SharePoint App that enables organizations to manage documentation with AI-powered enrichment and O365 Copilot integration.

---

## 🎯 MVP Phase (CRITICAL FOR SELLING - Must Complete First)

**Status:** ✅ Backend enrichment pipeline complete | ❌ Frontend & Integration missing  
**Goal:** Enable selling as SharePoint App with O365 Copilot integration for all org employees

### MVP Deliverables (Priority 1 - Required for Selling)

#### 🚨 Feature MVP-1 – SPFx SharePoint App Package (CRITICAL)
**Description:**  
Build complete SharePoint App (SPFx) that can be packaged and sold through App Catalog.

**Components:**
1. **Main Web Part** - Document upload interface for business users
   - Upload scratch documents to SharePoint
   - Monitor enrichment status
   - View enriched documents
   
2. **Admin Web Part** - Management interface
   - View processing logs and error reports
   - Manage templates and settings
   - Trigger manual enrichment jobs

3. **App Package** - Distributable `.sppkg` file
   - App manifest with Graph API permissions
   - Tenant-wide deployment capability
   - App Catalog compatibility

**Technical Approach:**  
- Framework: SPFx (React-based, latest version)  
- UI: Fluent UI (DetailsList, MessageBar, FileUpload, ProgressIndicator)  
- Authentication: SharePoint context + Azure AD token  
- Backend: Secure API calls to Azure Function App

**Effort Level:** 5–7 days (one dev)  
**Dependencies:** Function App deployed, Graph permissions configured  
**Status:** ⚠️ NOT STARTED - Critical blocker for selling

---

#### 🚨 Feature MVP-2 – O365 Copilot Integration (CRITICAL)
**Description:**  
Enable all org employees to query enriched documents through Teams/O365 Copilot without technical setup.

**Two Implementation Options:**

**Option A: Microsoft Search Connector (Recommended - Native Integration)**
- Push enriched document metadata to Microsoft Search
- Native O365 Copilot automatically indexes content
- No custom bot required
- Better user experience

**Option B: Teams Bot Wrapper (Alternative)**
- Build Bot Framework bot that wraps QueryAnswer API
- Deploy as Teams app
- Users chat with bot in Teams
- More control but requires bot deployment

**Technical Approach:**  
- **Option A:** Implement `MicrosoftSearchConnectorHelper.cs` using Graph External Connection API
- **Option B:** Bot Framework SDK, deploy via Azure Bot Service
- Auto-detect user/tenant context (no manual tenantId)
- Update `QueryAnswer` function to extract tenant from user token

**Effort Level:** 3–4 days (Option A) or 5–6 days (Option B)  
**Dependencies:** Microsoft Search Admin access (Option A) or Azure Bot Service (Option B)  
**Status:** ⚠️ NOT STARTED - Critical blocker for org-wide access

---

#### ✅ Feature MVP-3 – Core Enrichment Pipeline (COMPLETE)
**Status:** ✅ DONE
- Document extraction (text + images)
- OpenAI sectioning and enrichment
- Template generation
- Embeddings storage
- Cosmos DB integration

---

#### 🔧 Feature MVP-4 – Org-wide Access & User Context
**Description:**  
Enable automatic tenant/user detection so all employees can use without technical knowledge.

**Technical Approach:**  
- Update `QueryAnswer` to extract tenant from user's Azure AD token
- Implement `UserContextHelper.cs` for token parsing
- Remove requirement for manual tenantId in queries
- Add RBAC support for multi-tenant isolation

**Effort Level:** 2–3 days  
**Dependencies:** Azure AD token validation  
**Status:** ⚠️ PARTIALLY DONE - QueryAnswer updated, needs testing

---

## 🔷 Phase 2 – Production Enhancements (After MVP Launch)

**Goal:** Enhance system with usability, automation, and resilience features for production use.

### Phase 2 Focus Areas:  
1. Image OCR & PDF support  
2. Notification & audit trail  
3. Role-based access control (advanced)  
4. Telemetry & monitoring  
5. Human-in-the-loop review portal  
6. Advanced template customization  

---

## 🔠 Feature 2-1 – OCR Integration (Images & PDFs)

**Description:**  
Automatically extract text from screenshots or PDF pages during enrichment.

**Technical Approach:**  
- Use **Azure Computer Vision OCR API**  
- Modify `SimpleExtractor` to handle `.pdf` and `.png/.jpg`  
- Add a new helper `OcrHelper.cs` that sends base64 image to OCR endpoint  
- Append OCR text to `imageOcrs` list before LLM enrichment  
- Add config keys:
  ```json
  {
    "AzureVision_Endpoint": "https://<your-resource>.cognitiveservices.azure.com/",
    "AzureVision_Key": "<your-key>"
  }
  ```

**Effort Level:** 2–3 days  
**Dependencies:** Azure Cognitive Services resource  

---

## 🧾 Feature 2-2 – Notification & Audit Trail

**Description:**  
Notify users and maintain a detailed history of document processing events.

**Technical Approach:**  
- Create a `Notifications` collection in Cosmos DB  
- Store metadata: FileId, FileName, Status, Timestamp, UserEmail  
- Add Function App trigger (`NotifyUser.cs`) to send email via **Graph Mail API** or **Teams Adaptive Card**  
- Add audit UI in SPFx to list processed docs & statuses  

**Effort Level:** 3–4 days  
**Dependencies:** Graph Mail permissions (`Mail.Send`)  

---

## 🔐 Feature 2-3 – Role-Based Access Control (RBAC - Advanced)

**Description:**  
Restrict SMEPilot admin functions to authorized users.

**Technical Approach:**  
- Use Azure AD App Roles (`Admin`, `Reviewer`, `User`)  
- SPFx checks user roles via Graph `/me/appRoleAssignments`  
- Function App validates token roles using `[Authorize]` middleware  
- Add role claims in `local.settings.json` mock mode for dev testing  

**Effort Level:** 2–3 days  
**Dependencies:** Azure AD App registration updated  

---

## 📊 Feature 2-4 – Telemetry & Monitoring

**Description:**  
Provide visibility into Function App performance, errors, and LLM calls.

**Technical Approach:**  
- Integrate **Application Insights SDK** in `Program.cs`  
- Add custom telemetry:  
  - File processing duration  
  - LLM validation failures  
  - Retry counts  
- Create Azure Dashboard for SMEPilot telemetry metrics  

**Effort Level:** 1–2 days  
**Dependencies:** Application Insights resource  

---

## 🔍 Feature 2-5 – Cognitive Search Integration (Optional - if not using Microsoft Search Connector)

**Description:**  
Enable large-scale search indexing for multi-tenant Copilot integration.

**Technical Approach:**  
- Push embeddings and metadata into **Azure Cognitive Search**  
- Create Index: `Documents`, Fields (`title`, `summary`, `content`, `embedding`)  
- Use Vector Search for semantic queries  
- Replace direct Cosmos similarity with Search API calls  

**Effort Level:** 4–6 days  
**Dependencies:** Azure Cognitive Search (Vector Search enabled)  

---

## 🧑‍💻 Feature 2-6 – Human-in-the-Loop Review Portal

**Description:**  
Enable reviewers to approve, edit, or reject LLM-enriched docs.

**Technical Approach:**  
- Build lightweight **Review Portal** (SPFx or Blazor WASM)  
- Fetch failed enrichments from `/samples/output/llm_errors` or Cosmos  
- Allow inline edits & re-enrichment trigger  
- Store reviewer feedback in Cosmos (`ManualReview` container)  

**Effort Level:** 5–6 days  
**Dependencies:** SPFx Admin UI base  

---

## 🧱 Technical Enhancements (Backend Improvements)

| Enhancement | Description | Approach |
|--------------|--------------|-----------|
| Retry Policies | Add exponential backoff (Polly) to Graph & OpenAI calls | Update helper methods |
| Durable Functions | Handle large files asynchronously | Add `DurableOrchestrator` function |
| Key Vault Integration | Move secrets out of `local.settings.json` | Use `Azure.Identity` & Key Vault references |
| Test Coverage | Add xUnit tests for helpers (Extractor, TemplateBuilder) | Setup `tests/SMEPilot.Tests/` project |
| CI/CD | Add GitHub Actions for build/test/deploy | `.github/workflows/build.yml` |

---

## 🌐 Phase 3 – Future Scalability

| Area | Description | Tools / Services |
|-------|--------------|------------------|
| Multi-language support | Enrich docs in multiple languages | Azure Translator API |
| Knowledge Graph | Build cross-document relationships | Neo4j / Azure Cosmos Gremlin |
| Cost optimization | Use batch embeddings, local caching | Redis / Durable storage |
| Multi-tenant portal | Centralized admin for all org tenants | Azure AD B2C / Multi-tenant AAD app |
| Licensing model | SaaS metering and billing | Azure Marketplace / ISV subscription |

---

## ✅ MVP Deliverables Checklist (Priority 1)
| Deliverable | Owner | ETA | Status | Blocker? |
|--------------|--------|-----|--------|----------|
| **SPFx Main Web Part** | Frontend Dev | 3 days | ☐ | 🚨 YES |
| **SPFx Admin Web Part** | Frontend Dev | 2 days | ☐ | 🚨 YES |
| **SPFx App Package (.sppkg)** | Frontend Dev | 1 day | ☐ | 🚨 YES |
| **Microsoft Search Connector** | Backend Dev | 3 days | ☐ | 🚨 YES |
| **User Context Auto-Detection** | Backend Dev | 2 days | ⚠️ Partial | NO |
| **App Catalog Deployment Guide** | DevOps | 1 day | ☐ | YES |

## ✅ Phase 2 Deliverables Checklist
| Deliverable | Owner | ETA | Status |
|--------------|--------|-----|--------|
| OCR + PDF Support | Backend Dev | 3 days | ☐ |
| Notifications | Backend Dev | 2 days | ☐ |
| Advanced RBAC | Infra | 2 days | ☐ |
| App Insights Integration | DevOps | 1 day | ☐ |
| Review Portal | Frontend Dev | 5 days | ☐ |

---

### 🧩 Cursor Implementation Notes (REVISED PRIORITIES)

**MVP Phase (Do First):**
1. **CRITICAL:** Scaffold SPFx project in `/SMEPilot.SPFx/` folder
2. **CRITICAL:** Implement Main Web Part for document upload
3. **CRITICAL:** Implement Admin Web Part for management
4. **CRITICAL:** Create App Package (`package-solution.json`) and build `.sppkg`
5. **CRITICAL:** Implement MicrosoftSearchConnectorHelper.cs
6. **CRITICAL:** Update QueryAnswer to auto-detect user context

**Phase 2 (After MVP):**
7. Generate missing helpers (`OcrHelper.cs`, `NotifyUser.cs`) under `Helpers/` and `Functions/`.  
8. Extend `SimpleExtractor.cs` to support PDF extraction.  
9. Create new `DurableOrchestrator.cs` for large file async handling.  
10. Modify `Program.cs` to register telemetry and retry policies.  
11. Setup `.github/workflows/build.yml` for CI/CD.

---

### 🧠 End of Document
