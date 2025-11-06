# 🚀 SMEPilot – Developer Quick Start Guide

**Purpose:**  
You’ll use Cursor (AI coding tool) to build and test the **SMEPilot** project.  
Everything you need is already provided:  
- `Requirements.md` → what the tool does  
- `Instructions.md` → how to set it up  
- `TechnicalDoc.md` → contains all the code  
- (Optional) `RUNREPORT.md` → to write test results  

---

## 🧩 Step 1: Prepare your environment

1. **Install required tools (if not already):**
   - [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
   - [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
   - [Git](https://git-scm.com/)
   - [Cursor](https://cursor.sh/) (open it and sign in with GitHub)

2. **Create or open a local folder** for the project:  
   Example:  
   ```bash
   mkdir SMEPilot
   cd SMEPilot
   ```

3. **Clone the repo (if it already exists on GitHub)**  
   ```bash
   git clone <repo-url>
   cd <repo-folder>
   ```

4. If you got a `.patch` file instead of a repo, run:  
   ```bash
   git init
   git apply add_all_files.patch
   ```

---

## 🧠 Step 2: Let Cursor set everything up automatically

1. **Open the folder in Cursor**.  
   - You should see:
     ```
     Requirements.md
     Instructions.md
     TechnicalDoc.md
     SMEPilot.FunctionApp/
     SMEPilot.SPFx/ (after scaffolding)
     ```

2. **For Backend (Azure Functions):**  
   Paste this prompt into Cursor's chat:  
   ```
   Run the instructions in Instructions.md Phase 1 and use the code in TechnicalDoc.md 
   to set up, build, and test the SMEPilot backend locally. 
   Use mock mode (no Graph creds). 
   Generate RUNREPORT.md after successful test.
   ```
   Cursor will:
   - Create all backend project files
   - Run `dotnet build`
   - Start the function app (`func start`)
   - Run tests (enrichment + query)
   - Write `RUNREPORT.md` with test results

3. **For Frontend (SPFx - REQUIRED FOR MVP):**  
   After backend is working, scaffold SPFx:
   ```
   Follow Instructions.md Phase 2 to scaffold SPFx project.
   Implement Main Web Part for document upload using Fluent UI.
   Implement Admin Web Part for management.
   Build and package as .sppkg file.
   ```
   Cursor will:
   - Scaffold SPFx solution
   - Implement web parts
   - Build and package app
   - Create `.sppkg` for App Catalog deployment

4. **Just wait and watch the logs** — Cursor will handle most things itself.

---

## ⚙️ Step 3: Verify the results

After Cursor finishes:

### Backend Verification:
1. Check **`samples/output/`** folder:
   - It should have `<sample>_enriched.docx`
   - This is your enriched document

2. Check **`samples/output/llm_errors/`** (if any):
   - If files exist here, the AI couldn't understand a doc.  
     You can open these text files and see what went wrong.

3. Check the **`RUNREPORT.md`** file:
   - It should say **Build: succeeded**
   - Both test cases (Enrichment + Query) should show **passed**

### SPFx Verification:
4. Check **`SMEPilot.SPFx/sharepoint/solution/`**:
   - Should have `sme-pilot.sppkg` file
   - This is the installable SharePoint App

5. Test SPFx locally:
   ```bash
   cd SMEPilot.SPFx
   gulp serve
   ```
   - Open SharePoint workbench
   - Add DocumentUploader web part
   - Test upload functionality

---

## 🔑 Step 4: Add your Azure secrets (optional, for live test)

Once local mock test works, fill these in your `SMEPilot.FunctionApp/local.settings.json`:

| Key | What to put |
|-----|--------------|
| `AzureOpenAI_Endpoint` | Your Azure OpenAI endpoint |
| `AzureOpenAI_Key` | Key from Azure OpenAI resource |
| `AzureOpenAI_Deployment_GPT` | Deployment name for GPT model |
| `AzureOpenAI_Embedding_Deployment` | Deployment name for embedding model |
| `Cosmos_ConnectionString` | Your Cosmos DB connection |
| `Graph_TenantId`, `Graph_ClientId`, `Graph_ClientSecret` | From your Azure AD App |
| `AzureWebJobsStorage` | Storage connection string (from Azure Function) |

> ⚠️ **Don’t commit this file to GitHub** — it contains secrets!

---

## 🧪 Step 5: Run manually (if needed)

If you want to run without Cursor:

1. In terminal:
   ```bash
   cd SMEPilot.FunctionApp
   dotnet restore
   dotnet build
   func start
   ```

2. Test API using CURL (or Postman):
   ```bash
   curl -X POST http://localhost:7071/api/ProcessSharePointFile      -H "Content-Type: application/json"      -d '{"siteId":"local","driveId":"local","itemId":"local","fileName":"sample1.docx","uploaderEmail":"dev@example.com","tenantId":"default"}'
   ```

   Then:
   ```bash
   curl -X POST http://localhost:7071/api/QueryAnswer      -H "Content-Type: application/json"      -d '{"tenantId":"default","question":"What is this document about?"}'
   ```

3. You should get:
   - A processed doc (`_enriched.docx`) in `/samples/output`
   - An answer in terminal for your query

---

## 🧾 Step 6: Commit & push

When everything works:

```bash
git add .
git commit -m "SMEPilot local build success ✅"
git push -u origin smepilot/initial
```

---

## 🚀 Step 7: (Optional) Deploy to Azure

If you have Function App already created:

```bash
cd SMEPilot.FunctionApp
func azure functionapp publish <YourFunctionAppName>
```

Then test it with your actual SharePoint files.

---

## ✅ Done!

Your local environment is ready and verified.  
Once working, you can:
- Add OCR, PDF, or multi-language support later.
- Implement queues or Durable Functions for large files.

---

### Summary for the junior dev:
> “You just open the project in Cursor, paste the master prompt, and let Cursor build, test, and report everything automatically. If it passes, then fill your Azure credentials and deploy.”

---
