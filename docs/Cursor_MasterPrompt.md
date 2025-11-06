# 🧠 SMEPilot Master Prompt for Cursor

Read and fully follow all project documentation before writing or editing any code.

---

## 1️⃣ Load and internalize the following files
- `AI_Coding_Guide.md`
- `TestPlan.md`
- `EnhancementPlan.md`
- `TechnicalDoc.md`
- `Requirements.md`
- `Instructions.md`
- `DeveloperGuide.md`

---

## 2️⃣ Apply the following global rules

- Follow all security, validation, retry, and error-handling standards defined in `AI_Coding_Guide.md`.
- For any new helper or function:
  - Add complete unit tests referencing `TestPlan.md`.
  - Implement schema validation, proper try-catch, and telemetry.
- For any new feature request:
  - Follow the roadmap and structure from `EnhancementPlan.md`.
  - Respect existing folder structure (e.g., `/Helpers`, `/Functions`, `/Models`, `/samples`).
- All code must compile, run locally in mock mode, and pass unit tests.
- Do not modify secrets or connection strings in `local.settings.json`.

---

## 3️⃣ While working

- Always describe your approach before generating code.
- Auto-run tests after generating or refactoring functions.
- Stop and ask for clarification if:
  - A Graph API permission is missing.
  - An OpenAI response format is unclear.
  - A large file (>5 MB) is detected without durable orchestration.

---

## 4️⃣ Output formatting

- Always include summary comments in generated code explaining logic and error-handling decisions.
- Include `// TODO:` annotations for future items listed in `EnhancementPlan.md`.
- After completing a file, generate a short validation summary describing:
  - Compilation status
  - Test results
  - Potential improvements.

---

## 🧩 Purpose
Ensure SMEPilot code generation is consistent, secure, testable, and aligned with CTO architecture goals.

Now confirm by saying:  
**“Ready to implement SMEPilot according to master documentation.”**
