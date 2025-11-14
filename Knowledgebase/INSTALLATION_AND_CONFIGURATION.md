# SMEPilot — Installation & Configuration

## Pre-requisites

- Admin account with SharePoint Site Owner permissions on target site.
- PowerShell with PnP.PowerShell module installed.
- Azure AD app registration (admin has added app and granted `Sites.ReadWrite.All`).

---

## 1. Upload Code and Create Branch

1. `git checkout -b install/smepilot-config`
2. Copy templates to `./Templates/UniversalOrgTemplate.dotx` (or use default)

---

## 2. Run Installer (Creates Libraries & Config List)

**Option A: Using Installer Script (Recommended)**

1. Connect to site:
   ```powershell
   Connect-PnPOnline -Url https://<tenant>.sharepoint.com/sites/<site> -UseWebLogin
   ```

2. Run installer:
   ```powershell
   .\scripts\Install-SMEPilot.ps1 -SiteUrl "https://<tenant>.sharepoint.com/sites/<site>" -TemplateLocalPath ".\Templates\UniversalOrgTemplate.dotx"
   ```

**Option B: Manual Setup**

1. Create libraries:
   - Source Library/Folder: Where users upload raw documents (configurable)
   - Templates Library: Holds organization `.dotx` template
   - **SMEPilot Enriched Docs** Library: Destination for enriched documents (required name for Copilot)

2. Create `SMEPilotConfig` list with columns:
   - SourceLibraryUrl (Text)
   - TargetLibraryUrl (Text)
   - TemplateFileUrl (Text)
   - NotificationEmail (Text)
   - EnableCopilotAgent (Yes/No)
   - VisibilityGroup (Text)
   - MaxFileSizeMB (Number)
   - RetryAttempts (Number)

---

## 3. Configure SMEPilotConfig

Open SharePoint list `SMEPilotConfig` and set:

- **TemplateFileName:** `UniversalOrgTemplate.dotx`
- **TemplateLibraryPath:** `/Shared Documents/Templates` ⚠️ **Note:** `TemplateLibraryPath` must remain constant across sites for consistent enrichment.
- **SourceFolderPath:** `/Shared Documents/SourceDocs` (or your folder)
- **DestinationFolderPath:** `/Shared Documents/SMEPilot Enriched Docs` (MUST be this name)
- **NotificationEmail:** `docadmin@company.com`
- **EnableCopilotAgent:** `Yes`
- **VisibilityGroup:** `Everyone` (or specific AD group)

---

## 4. Function App Configuration

In `local.settings.json` (local) or Azure App Settings (production):

```json
{
  "UseAIOrDB": "false",
  "TemplateLibraryPath": "/Shared Documents/Templates",
  "TemplateFileName": "UniversalOrgTemplate.dotx",
  "EnrichedFolderRelativePath": "/Shared Documents/SMEPilot Enriched Docs",
  "AzureWebJobsStorage": "UseDevelopmentStorage=true",
  "FUNCTIONS_WORKER_RUNTIME": "dotnet"
}
```

⚠️ **Note:** `TemplateLibraryPath` must remain constant across sites for consistent enrichment.

**Azure AD App Settings:**
- `ClientId`: Your Azure AD app client ID
- `ClientSecret`: Your Azure AD app client secret (or use Managed Identity)
- `TenantId`: Your tenant ID

---

## 5. Webhook Subscription

**Option A: Using SetupSubscription Function**

1. Deploy Function App
2. Call `SetupSubscription` function or use provided script
3. For dev, use ngrok for public tunneling:
   ```bash
   ngrok http 7071
   ```
   Then create subscription with ngrok URL

**Option B: Manual Setup**

Use Graph API to create subscription pointing to Function App webhook endpoint.

**Note:** Webhook subscription lifetime is limited by Graph API. The Function implements subscription renewal (installer will schedule renewal or admin can re-run subscription setup).

---

## 6. Verify Installation

**Optional: Verify Configuration List**
```powershell
Get-PnPListItem -List SMEPilotConfig
```
This confirms config entries are created correctly.

**Verify Processing:**
1. Upload a test `.docx` file to Source Library
2. Check Function App logs for processing
3. Verify enriched document appears in "SMEPilot Enriched Docs" library
4. Check source document metadata:
   - `SMEPilot_Enriched` = Yes
   - `SMEPilot_Status` = Enriched
   - `SMEPilot_EnrichedFileUrl` = Link to enriched file

---

## 7. Configure Copilot (Optional)

See `QUICK_START_COPILOT.md` for Copilot configuration steps.

---

## Troubleshooting

- **Function App not receiving webhooks:** Check subscription status, verify endpoint URL
- **Permission errors:** Verify Azure AD app has `Sites.ReadWrite.All` with admin consent
- **Template not found:** Verify template path in SMEPilotConfig
- **Documents not enriching:** Check Function App logs in Application Insights

