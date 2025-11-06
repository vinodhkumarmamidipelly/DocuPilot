# Cursor_Run_Commands.md

This file contains the exact prompts, commands, and validation steps to instruct **Cursor** to harden SMEPilot (JsonValidator, JobsHelper, RetryPolicy, Telemetry, Durable skeleton, OCR, tests, CI, hooks) and how to validate each step.

---
## Quick setup (run once locally)
```bash
# Apply patch if you have it
git init
git apply add_all_files.patch   # if using the provided patch
git checkout -b smepilot/hardening

# Ensure AI instruction files are present
# (these should already be in repo root)
ls AI_Coding_Guide.md Cursor_MasterPrompt.md TestPlan.md EnhancementPlan.md
```

---
## 1) One-shot MASTER PROMPT (paste as a single message into Cursor)

```
You are Cursor AI developer assistant. Read and fully obey these repo docs before writing or editing code:
- AI_Coding_Guide.md
- TestPlan.md
- EnhancementPlan.md
- TechnicalDoc.md
- Requirements.md
- Instructions.md
- Cursor_MasterPrompt.md

Goal: Implement all high-priority hardening and guardrails for SMEPilot MVP → Pilot (JsonValidator, JobsHelper, RetryPolicy, Telemetry, Durable skeleton, OCR helper, improved TemplateBuilder, unit tests, pre-commit hook, CI, run harness). Do NOT write secrets; use env placeholders.

Work plan and rules:
1) Describe the approach for the next task before coding.
2) Implement one task at a time; after each task run:
   - `dotnet build`
   - `dotnet test`
   - local mock-run (if relevant) to validate generated artifacts.
3) If tests fail, fix until green — do not commit failing code.
4) Commit each completed task to branch `smepilot/hardening` with atomic commits and clear messages.
5) Produce a patch `add_mitigation_changes.patch` at the end that contains all changes.
6) After all tasks, run `scripts/run_local_tests.sh` and create `RUNREPORT.md` summarizing build, tests, and mock-run results.
7) Always follow patterns in AI_Coding_Guide.md (schema validation, try-catch, telemetry). Add unit tests for any new helper.

Start by scanning the repo for any syntax issues (e.g., stray `import` lines) and fix them. Then proceed through the tasks in EnhancementPlan.md order. After each major task provide a short summary: files changed, tests added, build status, local-run result.
```

---
## 2) Stepwise prompts (paste these into Cursor one at a time if you want staged execution)

### A) Fix syntax issues (run first)
```
Task: Fix all invalid C# syntax tokens (e.g., "import" occurrences). Run `grep -R "import " -n` and replace invalid lines. Build solution. Commit as "fix: remove stray import tokens".
```

### B) JsonValidator
```
Task: Add Helpers/JsonValidator.cs to validate OpenAI JSON against DocumentModel. Add SMEPilot.Tests/JsonValidatorTests.cs with valid/invalid cases. Build and run tests. Commit "feat(helpers): add JsonValidator + tests".
```

### C) JobsHelper + idempotency
```
Task: Add Helpers/JobsHelper.cs to read/write job state to Cosmos (Jobs container). Update Functions/ProcessSharePointFile.cs to check job state: skip if Completed, resume if InProgress, insert InProgress at start. Add tests and an integration test skeleton. Commit "feat(helpers): add JobsHelper & idempotency".
```

### D) Retry/Backoff
```
Task: Add Helpers/RetryPolicy.cs (Polly or custom) and wrap GraphHelper, OpenAiHelper, and CosmosHelper calls with policies. Ensure exponential backoff and max retries configurable via env vars. Add unit tests. Commit "chore: add retry/backoff policies".
```

### E) Telemetry (App Insights)
```
Task: Add Helpers/TelemetryHelper.cs and register Application Insights in Program.cs. Instrument key steps: start processing, LLM call, LLM failure, upload, embedding store. Add smoke test. Commit "feat(monitoring): add telemetry".
```

### F) Durable functions skeleton
```
Task: Add DurableOrchestrator (starter + activity templates) and wire ProcessSharePointFile to submit large files to orchestrator. Ensure mock mode returns 202 for large files. Commit "feat(durable): add durable functions skeleton".
```

### G) OCR helper & PDF support
```
Task: Add Helpers/OcrHelper.cs that calls Azure Computer Vision (mock when env not set). Extend SimpleExtractor to detect PDF & images and call OcrHelper. Add tests. Commit "feat(helpers): add OCR helper and PDF support".
```

### H) TemplateBuilder images
```
Task: Update TemplateBuilder to embed images properly with alt text and add a test ensuring images embedded. Commit "fix(docx): embed images correctly in TemplateBuilder".
```

### I) Unit tests & CI
```
Task: Create SMEPilot.Tests (xUnit) with tests for SimpleExtractor, TemplateBuilder, JsonValidator, JobsHelper, and mocked OpenAiHelper. Add .github/workflows/ci.yml to run restore/build/test. Commit "chore(test): add tests and CI".
```

### J) Pre-commit hook & run harness
```
Task: Add scripts/prebuild.sh and .git/hooks/pre-commit sample that runs `dotnet build` and `dotnet test`. Add scripts/run_local_tests.sh to run mock scenario and produce RUNREPORT.md. Commit "chore: add precommit + run harness".
```

---
## 3) Validation checklist (run after each completed step)

**Commands to run locally:**
```bash
# build & tests
cd SMEPilot.FunctionApp
dotnet restore
dotnet build
dotnet test  # run from solution root if tests project added
```

**Mock-run (start functions & test endpoints):**
```bash
# from repo root
cd SMEPilot.FunctionApp
func start
# In a new terminal, call the endpoints:
curl -X POST http://localhost:7071/api/ProcessSharePointFile -H "Content-Type: application/json"   -d '{"siteId":"local","driveId":"local","itemId":"local","fileName":"sample1.docx","uploaderEmail":"dev@example.com","tenantId":"default"}'

curl -X POST http://localhost:7071/api/QueryAnswer -H "Content-Type: application/json"   -d '{"tenantId":"default","question":"What is this document about?"}'
```

**What to verify after each step:**
- `dotnet build` succeeds with zero errors.
- `dotnet test` passes all tests.
- If step was functional, verify enriched doc exists in `samples/output/`.
- For JsonValidator: intentionally feed malformed JSON to unit test and ensure it is detected and sent to `/samples/output/llm_errors/` after retry.
- For JobsHelper: simulate duplicate notifications and verify no duplicate uploads/embeddings.
- For RetryPolicy: simulate transient failures and ensure retries occur (look at logs/telemetry).
- For Durable: upload simulated large file (>4MB) and confirm 202 Accepted and job recorded.
- For OCR: verify `imageOcrs` appear in logs/openai prompt payload (or check Telemetry event).

---
## 4) Git & branch workflow (commands)

```bash
# create feature branch
git checkout -b smepilot/hardening

# after each task
git add <changed-files>
git commit -m "feat(scope): short description"

# at end, create a patch file (optional)
git format-patch origin/main --stdout > add_mitigation_changes.patch

# push branch & open PR
git push -u origin smepilot/hardening
```

---
## 5) Final acceptance criteria (before merging to main)
- All unit & integration tests pass in CI.
- Local mock-run successfully enriches sample doc and QueryAnswer returns a valid answer.
- LLM invalid outputs are handled by JsonValidator and saved to `/samples/output/llm_errors/` after retry.
- Duplicate notifications do not cause duplicate data (JobsHelper works).
- Retry/backoff behavior prevents immediate failures on transient errors.
- Basic telemetry events are logged for LLM calls and failures.
- `RUNREPORT.md` exists and summarizes success of local run harness.

---
## 6) Troubleshooting tips
- If `func start` fails: verify Azure Functions Core Tools installed and `local.settings.json` present with `FUNCTIONS_WORKER_RUNTIME=dotnet`.
- If OpenAI calls fail in mock mode: ensure mock mode is used (leave AzureOpenAI_* env vars empty) or configure mock responses in tests.
- If Graph calls fail: use mock mode or provide Graph creds for live tests; check Graph permissions (Sites.Selected vs Sites.ReadWrite.All).
- Always inspect `samples/output/llm_errors/` for LLM response issues and open saved files for debugging.

---
## 7) Handy snippets to paste in Cursor during work
- "Describe your approach for implementing JsonValidator.cs in 3–4 bullets, then generate the file and tests."
- "Run tests and report back with failures and stack traces."
- "Create a DurableOrchestrator skeleton and a starter function that queues large files."

---
## 8) Contacts & escalation
- If Cursor is stuck on infra (creating Key Vault or granting Graph permissions), escalate to cloud admin / tenant admin to run provisioning scripts.
- For prompt tuning, ask the ML lead to review `OpenAiHelper.cs` prompts and the system messages in TechnicalDoc.md.

---
End of Cursor_Run_Commands.md
