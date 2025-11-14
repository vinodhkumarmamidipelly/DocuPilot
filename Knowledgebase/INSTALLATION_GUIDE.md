# SMEPilot Installation Guide

## Overview

This guide provides step-by-step instructions for installing and configuring SMEPilot in your SharePoint environment.

## Prerequisites

### Required Permissions
- **SharePoint Site Collection Admin** (for SPFx deployment and configuration)
- **Azure AD Global Admin** (for app registration and permissions)
- **M365 Global Admin OR Copilot Admin** (for Copilot Studio configuration)

### Required Resources
- SharePoint Online site
- Azure Function App (deployed and running)
- Azure AD App Registration (with Graph API permissions)
- Microsoft 365 Copilot license (for Copilot Agent)

## Step 1: Azure Function App Setup

### 1.1 Deploy Function App

1. Deploy the Function App to Azure
2. Configure Application Settings:
   ```
   Graph_TenantId = <Your Tenant ID>
   Graph_ClientId = <App Registration Client ID>
   Graph_ClientSecret = <App Registration Client Secret>
   EnrichedFolderRelativePath = /Shared Documents/SMEPilot Enriched Docs
   TemplateLibraryPath = /Shared Documents/Templates
   TemplateFileName = UniversalOrgTemplate.dotx
   MaxFileSizeBytes = 52428800 (50MB)
   ```

### 1.2 Verify Function App

1. Navigate to Function App URL
2. Test `/api/ProcessSharePointFile` endpoint (should return health check)
3. Verify Application Insights is enabled

## Step 2: Azure AD App Registration

### 2.1 Create App Registration

1. Go to Azure Portal → Azure Active Directory → App registrations
2. Click "New registration"
3. Name: "SMEPilot Function App"
4. Supported account types: "Single tenant"
5. Click "Register"

### 2.2 Configure API Permissions

1. Go to "API permissions"
2. Click "Add a permission" → "Microsoft Graph" → "Application permissions"
3. Add the following permissions:
   - `Sites.ReadWrite.All` (required)
   - `Webhooks.ReadWrite.All` (required for webhooks)
4. Click "Grant admin consent"

### 2.3 Create Client Secret

1. Go to "Certificates & secrets"
2. Click "New client secret"
3. Description: "SMEPilot Function App Secret"
4. Expires: Choose appropriate duration
5. Click "Add"
6. **Copy the secret value immediately** (you won't see it again)

### 2.4 Note App Details

- **Application (client) ID**: Copy this value
- **Directory (tenant) ID**: Copy this value
- **Client secret value**: Copy this value (from step 2.3)

## Step 3: SPFx Solution Deployment

### 3.1 Build SPFx Solution

```bash
cd SMEPilot.SPFx
npm install
gulp bundle --ship
gulp package-solution --ship
```

### 3.2 Upload to App Catalog

1. Go to SharePoint Admin Center → Apps → App Catalog
2. Upload the `.sppkg` file from `sharepoint/solution/`
3. Click "Deploy" when prompted
4. Approve API permission requests:
   - `Sites.ReadWrite.All`
   - `Files.ReadWrite`

### 3.3 Add Web Part to Page

1. Navigate to your SharePoint site
2. Edit any page (or create a new page)
3. Click "+" to add a web part
4. Search for "SMEPilot Admin Panel"
5. Add the web part to the page
6. Configure web part:
   - Function App URL: `https://your-function-app.azurewebsites.net`
   - Click "Apply"

## Step 4: Initial Configuration

### 4.1 Fill Configuration Form

1. **Source Folder**: Where users will upload raw documents
   - Example: `/Shared Documents/RawDocs`
   - Folder must exist

2. **Destination Folder**: Where enriched documents will be saved
   - Example: `/Shared Documents/SMEPilot Enriched Docs`
   - Folder will be created if it doesn't exist

3. **Template File**: Company template file (.dotx)
   - Example: `/Templates/UniversalOrgTemplate.dotx`
   - File must exist and be accessible

4. **Processing Settings**:
   - Max File Size: 50 MB (default, configurable up to 80MB)
   - Processing Timeout: 60 seconds
   - Max Retries: 3

5. **Copilot Agent Prompt**: Custom instructions for Copilot
   - Default prompt is provided
   - Can be customized

6. **Access Points**: Where users can access Copilot Agent
   - Microsoft Teams
   - Web Interface (SharePoint Portal)
   - O365 Copilot (Word, Excel, PPT)

### 4.2 Save Configuration

1. Click "Save Configuration"
2. Wait for installation to complete
3. Verify all steps completed successfully:
   - ✓ SMEPilotConfig list created
   - ✓ Configuration saved
   - ✓ Metadata columns created
   - ✓ Error folders created
   - ✓ Webhook subscription created

### 4.3 Verify Installation

1. Check SMEPilotConfig list exists
2. Check metadata columns are added to source library
3. Check error folders (RejectedDocs, FailedDocs) exist
4. Check webhook subscription is active

## Step 5: Copilot Agent Configuration (REQUIRED)

### 5.1 Access Copilot Studio

1. Go to https://copilotstudio.microsoft.com
2. Sign in with M365 Global Admin or Copilot Admin account

### 5.2 Create Copilot Agent

1. Click "Create" → "Copilot Agent"
2. Name: "SMEPilot Knowledge Assistant"
3. Description: "Organization's knowledge assistant for functional and technical documents"
4. Click "Create"

### 5.3 Configure Data Source

1. Go to "Data sources" → "Add data source"
2. Select "SharePoint"
3. Configure:
   - Site: Your SharePoint site
   - Library: "SMEPilot Enriched Docs"
   - Enable "Microsoft Search integration"
4. Click "Save"

### 5.4 Configure System Prompt

1. Go to "Settings" → "System prompt"
2. Enter the exact prompt from your configuration:
   ```
   You are SMEPilot — the organization's knowledge assistant. Use only documents from the 'SMEPilot Enriched Docs' SharePoint library as the primary evidence. Provide a short summary (2-3 lines), then give a numbered step list for procedures or troubleshooting. Always cite the document title and link. If uncertain, say 'I couldn't find a definitive answer in SMEPilot docs.' Do not invent facts beyond the indexed docs.
   ```
3. Click "Save"

### 5.5 Configure Access Points

1. Go to "Settings" → "Access points"
2. Enable:
   - Microsoft Teams (if configured)
   - Web Interface (if configured)
   - O365 Copilot (if configured)
3. Click "Save"

### 5.6 Deploy Copilot Agent

1. Click "Publish" → "Deploy"
2. Wait for deployment to complete
3. Note the agent URL/ID

## Step 6: Testing

### 6.1 Test Document Enrichment

1. Upload a test .docx file to Source Folder
2. Wait for processing (check Function App logs)
3. Verify enriched document appears in Destination Folder
4. Verify metadata is updated on source document
5. Verify source document remains in Source Folder

### 6.2 Test Copilot Agent

1. Access Copilot Agent in Teams/Web/O365
2. Ask: "What is SMEPilot?"
3. Verify response includes summary and citations
4. Ask a procedure question
5. Verify response includes numbered steps

### 6.3 Test Edge Cases

- Upload file > 50MB → Should be rejected
- Upload duplicate file → Should be skipped
- Upload modified file → Should reprocess (version detection)

## Troubleshooting

### Function App Not Responding

- Check Function App is running
- Check Application Insights for errors
- Verify environment variables are set correctly
- Check CORS settings allow SharePoint domain

### Webhook Subscription Failed

- Verify Function App URL is correct
- Check Function App is accessible from internet
- Verify Graph API permissions are granted
- Check Application Insights for detailed errors

### Copilot Agent Not Finding Documents

- Wait 24-48 hours for indexing (or trigger reindex)
- Verify documents are in "SMEPilot Enriched Docs" library
- Check library permissions (users need read access)
- Verify Microsoft Search is enabled for the site

### Configuration Not Saving

- Check Site Collection Admin permissions
- Verify SMEPilotConfig list exists
- Check browser console for errors
- Verify Function App is accessible

## Next Steps

After successful installation:

1. Train users on document upload process
2. Train users on Copilot Agent usage
3. Monitor Application Insights for issues
4. Set up alerts for processing failures
5. Review enriched documents for quality

## Support

For issues or questions:
- Check Application Insights logs
- Review Function App logs
- Check SharePoint ULS logs
- Review Copilot Studio analytics

