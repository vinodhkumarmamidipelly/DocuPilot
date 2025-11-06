# SMEPilot Instructions

## Setup Steps

### Phase 1: Backend (Azure Functions)

1. **Clone Repo or Apply Patch**
   ```bash
   git clone <repo-url>
   cd SMEPilot
   # or if you have a patch
   git init
   git apply add_all_files.patch
   ```

2. **Install Backend Dependencies**
   - Install .NET 8 SDK
   - Install Azure Functions Core Tools
   - Install Git

3. **Build and Run Backend**
   ```bash
   cd SMEPilot.FunctionApp
   dotnet restore
   dotnet build
   func start
   ```

### Phase 2: SPFx Frontend (REQUIRED FOR MVP)

4. **Install SPFx Prerequisites**
   - Install Node.js (v18.x or v20.x LTS)
   - Install Yeoman: `npm install -g yo`
   - Install SharePoint Generator: `npm install -g @microsoft/generator-sharepoint`
   - Install Gulp CLI: `npm install -g gulp-cli`

5. **Scaffold SPFx Project**
   ```bash
   cd ..
   yo @microsoft/sharepoint
   # Answer prompts:
   # - Solution name: smepilot
   # - Target environment: SharePoint Online only (latest)
   # - Deployment option: N
   # - Permissions: Will have full control
   # - Type of client-side component: WebPart
   # - Web part name: DocumentUploader
   # - Description: SMEPilot document upload
   # - Framework: React
   ```

6. **Build SPFx Solution**
   ```bash
   cd smepilot
   npm install
   gulp build
   ```

7. **Package SPFx App**
   ```bash
   gulp bundle --ship
   gulp package-solution --ship
   # Output: sharepoint/solution/sme-pilot.sppkg
   ```

4. **Mock Mode Testing**
   - Ensure `samples/sample1.docx` exists.
   - POST to:
     ```bash
     http://localhost:7071/api/ProcessSharePointFile
     ```
   - Example payload:
     ```json
     {
       "siteId": "local",
       "driveId": "local",
       "itemId": "local",
       "fileName": "sample1.docx",
       "uploaderEmail": "dev@example.com",
       "tenantId": "default"
     }
     ```

   - Check `samples/output/` for results.

5. **Query Endpoint**
   ```bash
   curl -X POST http://localhost:7071/api/QueryAnswer      -H "Content-Type: application/json"      -d '{"tenantId":"default","question":"What is this document about?"}'
   ```

6. **Expected Output**
   - `sample1_enriched.docx` file in `samples/output/`
   - JSON response from `/api/QueryAnswer`

7. **Deploy SPFx App to SharePoint**
   - Navigate to SharePoint Admin Center → More features → Apps
   - Go to App Catalog
   - Upload `sharepoint/solution/sme-pilot.sppkg`
   - Approve API permissions when prompted
   - Add app to sites where users need access

8. **Configure Microsoft Search Connector (for Copilot)**
   - Navigate to Microsoft 365 Admin Center → Settings → Search & Intelligence → Microsoft Search
   - Create new connector for SMEPilot
   - Configure to index enriched documents from ProcessedDocs library
   - Enable for O365 Copilot

9. **Commit Results**
   ```bash
   git add .
   git commit -m "SMEPilot build success ✅"
   git push -u origin smepilot/initial
   ```
