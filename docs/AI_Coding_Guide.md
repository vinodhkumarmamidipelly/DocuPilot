# SMEPilot AI Coding Guidelines

## 🔒 Core Principles
- Follow SOLID and clean-code principles.
- Treat all user and external inputs as untrusted.
- Never hardcode secrets or credentials — use environment variables or Azure Key Vault.
- Use async/await everywhere; avoid blocking I/O in Azure Functions.
- Every public method must include try-catch with centralized logging.
- Use dependency injection for all external service helpers.

---

## 🧠 Azure OpenAI Usage
- Always request **strict JSON schema** from GPT responses.
- Validate every LLM response with JSON schema before use.
- If invalid:
  1. Retry once with stricter system prompt.
  2. If still invalid, log the output and move the document to `/samples/output/llm_errors/`.
- Store LLM prompts and outputs for audit (no PII).
- Never invoke OpenAI API directly from UI — always through FunctionApp helpers.
- Implement exponential backoff for API calls.

---

## 🗂 SharePoint & Microsoft Graph
- Use Microsoft Graph SDK for all SharePoint interactions.
- Implement retry policy (e.g., Polly) for transient Graph errors (429, 503).
- Before processing, verify document is not already processed (`Jobs` container).
- Never store Graph tokens or user credentials in code or logs.
- Handle large files using upload sessions and pagination.
- All file operations should log: TenantId, FileName, Version, and Status.

---

## 💾 Cosmos DB
- Use partition keys = `TenantId` for scalability and isolation.
- All writes must use `Upsert` to avoid duplication.
- Include metadata: TenantId, FileId, FileVersion, ProcessedDate, Status.
- Log RU consumption for cost monitoring.
- Implement retry for rate-limited writes (HTTP 429).

---

## ⚙️ Azure Function Design Rules
- Function entry points should contain **no business logic** — delegate to services/helpers.
- Each Function must:
  - Validate request payloads.
  - Log key execution steps (Start, Success, Failure).
  - Return standard HTTP response structure `{status, message, data}`.
- All helper calls wrapped in try-catch; errors rethrown with custom message.
- Avoid static classes where DI possible (improves testability).

---

## 🧩 Error Handling & Logging
- Every async method must have a try-catch-finally block.
- Catch known exceptions separately: `GraphServiceException`, `CosmosException`, `HttpRequestException`.
- Log errors to Application Insights with correlationId and tenant context.
- Never expose internal errors to API consumers; use friendly messages.
- All logs include `TenantId`, `DocumentId`, `ErrorId`.

---

## 🧠 Validation Layer
- Validate all external data (SharePoint payloads, API responses, file inputs).
- Use FluentValidation or custom validators for request models.
- Reject or sanitize malformed input early.
- Enforce file-type whitelist (`.docx`, `.pdf`, `.png`, `.jpg` only).

---

## 🧪 Testing & QA
- All code must be covered by tests defined in `TestPlan.md`.
- Cursor should auto-generate unit tests for each new helper or service.
- Mock Azure services (OpenAI, Graph, Cosmos) during local tests.
- Maintain sample input/output docs under `/samples/tests/`.
- Execute `dotnet test` before every commit.

---

## ⚡ Performance & Scalability
- For parallel file operations use `Task.WhenAll` (async).
- For documents > 5MB, use Durable Functions for asynchronous processing.
- Avoid reloading config/environment variables repeatedly.
- Implement caching for frequent metadata lookups.
- Profile performance with Application Insights metrics (`FunctionExecutionTime`).

---

## 🔐 Security & Compliance
- Secrets are read only from environment variables or Azure Key Vault.
- Mask sensitive values in logs (`<REDACTED>`).
- Support data deletion requests per tenant.
- Use HTTPS for all external API calls.
- Implement `Managed Identity` for secure service-to-service communication.

---

## 📊 Monitoring & Telemetry
- Integrate Application Insights SDK in FunctionApp.
- Key metrics to track:
  - `llm_json_failure_rate`
  - `openai_token_usage`
  - `function_execution_time_p95`
  - `job_duplicate_count`
  - `graph_retry_count`
- Set up alerts for critical thresholds (defined in EnhancementPlan.md).

---

## 🧰 Code Review & Quality Enforcement
- No code merged without successful `dotnet build` and `dotnet test`.
- All PRs must pass static analysis (Roslyn analyzers or SonarLint).
- Cursor must not commit code with compilation warnings.
- Commit messages should describe intent and scope clearly.

---

## 🧩 SPFx Development Guidelines (REQUIRED FOR MVP)
- Use SPFx latest version (1.19+ recommended).
- Follow Fluent UI design system for consistent UI.
- All SPFx components must be TypeScript + React (functional components preferred).
- Use `@microsoft/sp-http` for API calls to Function App.
- Implement proper error handling and loading states in web parts.
- App package must include proper permissions in `package-solution.json`.
- Build `.sppkg` using `gulp bundle --ship && gulp package-solution --ship`.
- Test locally using `gulp serve` before packaging.
- Never commit `.sppkg` files to repo — generate during build.

---

## 🔗 O365 Copilot Integration Guidelines (REQUIRED FOR MVP)
- **Microsoft Search Connector** (Recommended):
  - Use Graph External Connection API to index enriched documents.
  - Push metadata (title, summary, URL, tenantId) to Microsoft Search.
  - Enable native Copilot indexing — no custom bot needed.
- **Teams Bot** (Alternative):
  - Use Bot Framework SDK v4.
  - Wrap QueryAnswer API with bot dialogs.
  - Deploy via Azure Bot Service.
- **User Context**: Always extract tenant/user from Azure AD token in `QueryAnswer`.
- Never require manual `tenantId` parameter from end users.

---

## 🧩 Cursor-Specific Instructions
Cursor should always:
1. Read `AI_Coding_Guide.md`, `TestPlan.md`, and `EnhancementPlan.md` before generating new code.
2. **For MVP:** Prioritize SPFx and Copilot integration features first (see EnhancementPlan.md MVP Phase).
3. Follow the retry, validation, and logging patterns from existing helpers.
4. Automatically add tests for every new class or helper (including SPFx components).
5. Regenerate functions if tests fail.
6. Never modify `local.settings.json` secrets — only reference environment variables.
7. **When scaffolding SPFx:** Use latest SPFx generator, React framework, and include both Main and Admin web parts.

---

## ✅ Summary
By adhering to these rules:
- SMEPilot remains secure, cost-efficient, and scalable.
- Cursor and junior developers can build safely without manual supervision.
- System reliability and auditability stay consistent across environments.

---
