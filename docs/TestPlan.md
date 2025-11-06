# 🧪 SMEPilot Test Plan

## 1. Objective
Define comprehensive testing strategy for verifying SMEPilot's document enrichment, SPFx SharePoint App, and O365 Copilot integration functionality.

---

## 2. Test Levels

| Level | Purpose |
|--------|----------|
| Unit | Validate helpers (extractor, template builder, OpenAI helper, SPFx components) |
| Integration | Validate full Azure Function + Graph + OpenAI pipeline + SPFx web parts |
| Functional | Ensure end-to-end enrichment (input → output) works via SPFx |
| UI/UX | Test SPFx web parts, App Catalog deployment, user flows |
| Copilot Integration | Test O365 Copilot query from Teams, Microsoft Search indexing |
| Regression | Prevent failures after enhancements |
| Performance | Validate throughput and latency under load |

---

## 3. Unit Test Cases

| Module | Test ID | Description | Expected Result |
|---------|----------|--------------|-----------------|
| SimpleExtractor | UT01 | Extract text from valid `.docx` | All text + image bytes returned |
| SimpleExtractor | UT02 | Handle corrupted `.docx` | Returns error message |
| TemplateBuilder | UT03 | Generate `.docx` from mock DocumentModel | File created successfully |
| OpenAiHelper | UT04 | Validate JSON parsing | Returns valid DocumentModel object |
| CosmosHelper | UT05 | Upsert embedding | Record visible in container |
| UserContextHelper | UT06 | Extract tenant ID from Azure AD token | Correct tenant ID returned |
| MicrosoftSearchConnectorHelper | UT07 | Index document metadata | Document indexed in Microsoft Search |
| SPFx DocumentUploader | UT08 | Upload file via web part | File uploaded to SharePoint library |
| SPFx AdminPanel | UT09 | Load enrichment history | Logs displayed correctly |

---

## 4. Integration Tests

| ID | Description | Steps | Expected Outcome |
|----|--------------|--------|------------------|
| IT01 | Upload event triggers function | POST JSON to /api/ProcessSharePointFile | 200 OK + enriched file generated |
| IT02 | Embeddings stored in Cosmos | Process test doc | Cosmos record count increases |
| IT03 | QueryAnswer returns relevant response | POST to /api/QueryAnswer with user token | JSON with answer + sources |
| IT04 | SPFx upload triggers enrichment | Upload via SPFx web part | Enriched doc appears in ProcessedDocs |
| IT05 | Microsoft Search indexing | Process document | Document appears in Microsoft Search results |
| IT06 | Copilot query from Teams | Ask question in Teams/Copilot | Relevant answer with sources returned |

---

## 5. Functional Tests (End-to-End)

| Scenario | Input | Expected Output |
|-----------|--------|----------------|
| Normal flow (SPFx) | Upload `sample1.docx` via SPFx | Enriched `.docx` generated in ProcessedDocs |
| Normal flow (API) | `sample1.docx` via API | Enriched `.docx` generated under /output |
| Empty doc | Empty `.docx` | Error message "Invalid content" |
| Large doc | >5 MB file | 202 Accepted (async mode) |
| Invalid JSON (LLM) | Corrupted schema | Saved under `/llm_errors` |
| Copilot query | Question via Teams/Copilot | Answer with sources from enriched docs |
| App Catalog deployment | Upload `.sppkg` | App installs successfully, permissions approved |
| Multi-user access | Multiple employees query | Each user sees only their tenant's documents |

---

## 6. Performance Tests

| Metric | Target |
|--------|--------|
| Average enrichment time | < 8s / document |
| Concurrent documents | 100 under 60s |
| Function cold start | < 3s |
| QueryAnswer response time | < 1s for 1K embeddings |

---

## 7. Manual Verification

| Step | Action |
|------|--------|
| 1 | Deploy SPFx app to SharePoint App Catalog |
| 2 | Add SPFx web part to SharePoint page |
| 3 | Upload a scratch `.docx` via SPFx web part |
| 4 | Verify enriched doc appears in ProcessedDocs library |
| 5 | Check Cosmos entry created |
| 6 | Query enriched docs via Teams/Copilot |
| 7 | Verify answer returned with relevant sources |
| 8 | Test with multiple users (tenant isolation) |

---

## 8. Reporting
- Test results stored in `RUNREPORT.md`
- Failures logged in `/samples/output/llm_errors/`
- Performance metrics summarized in Application Insights

---
