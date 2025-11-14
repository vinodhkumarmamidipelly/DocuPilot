# SMEPilot Permissions Required by Level

> **Purpose:** This document clearly outlines all permissions required at each level for app setup and Copilot configuration.

---

## 📋 Table of Contents

1. [App Setup Permissions](#1-app-setup-permissions)
2. [Copilot Configuration Permissions](#2-copilot-configuration-permissions)
3. [Runtime Permissions](#3-runtime-permissions)
4. [User Permissions](#4-user-permissions)
5. [Permission Summary Matrix](#5-permission-summary-matrix)

---

## 1. App Setup Permissions

### 1.1 Azure AD App Registration Setup

**Who:** Azure AD Global Administrator or Application Administrator

**Required Actions:**
1. Create Azure AD App Registration
2. Configure API Permissions
3. Grant Admin Consent

**Permissions Required:**

| Permission | Type | Purpose | Required |
|------------|------|---------|----------|
| `Sites.ReadWrite.All` | Application | Read/write SharePoint files and metadata | ✅ **Yes** |
| `Files.ReadWrite.All` | Application | Read/write files in SharePoint | ✅ **Yes** |
| `Webhooks.ReadWrite.All` | Application | Create/manage webhook subscriptions | ✅ **Yes** |
| `Sites.Manage.All` | Application | Create libraries/folders (if installer needs to create) | ⚠️ **Optional** |

**Steps:**
1. Azure Portal → Azure Active Directory → App registrations
2. Create new app registration
3. Go to "API permissions"
4. Add permissions → Microsoft Graph → Application permissions
5. Select: `Sites.ReadWrite.All`, `Files.ReadWrite.All`, `Webhooks.ReadWrite.All`
6. Click "Grant admin consent for [tenant]"
7. Create Client Secret (Certificates & secrets)
8. Note: Application (client) ID, Directory (tenant) ID, Client Secret

**Note:** These are **Application Permissions** (app-only), not Delegated Permissions. Admin consent is required.

---

### 1.2 SharePoint Site Setup

**Who:** SharePoint Site Collection Administrator or Site Owner

**Required Actions:**
1. Install SPFx app
2. Create libraries/folders
3. Create SMEPilotConfig list
4. Create metadata columns
5. Configure app settings

**Permissions Required:**

| Permission Level | Purpose | Required |
|-----------------|---------|----------|
| **Site Collection Administrator** | Full control over site collection | ✅ **Preferred** |
| **Site Owner** | Can create lists, libraries, columns | ✅ **Alternative** |

**Specific Capabilities Needed:**
- ✅ Create document libraries
- ✅ Create folders
- ✅ Create SharePoint lists (`SMEPilotConfig`)
- ✅ Create site columns (metadata columns)
- ✅ Modify site permissions
- ✅ Install SPFx apps
- ✅ Access Site Settings

**Steps:**
1. Navigate to SharePoint Site
2. Site Settings → Site Collection Administration
3. Install SPFx app (if using app package)
4. Create libraries:
   - Source Folder (where users upload documents)
   - Templates Library (holds `.dotx` template)
   - **SMEPilot Enriched Docs** Library (destination - required name)
5. Create `SMEPilotConfig` list
6. Create metadata columns:
   - `SMEPilot_Enriched` (Yes/No)
   - `SMEPilot_Status` (Text)
   - `SMEPilot_EnrichedFileUrl` (Hyperlink)

**Note:** Site Collection Admin is **only needed during installation**. After setup, no ongoing admin permissions required.

---

### 1.3 Azure Function App Setup

**Who:** Azure Subscription Owner or Contributor

**Required Actions:**
1. Create Function App
2. Configure App Settings
3. Deploy Function App code
4. Set up Application Insights

**Permissions Required:**

| Permission Level | Purpose | Required |
|-----------------|---------|----------|
| **Subscription Owner** | Full control over subscription | ✅ **Yes** |
| **Subscription Contributor** | Can create/manage resources | ✅ **Alternative** |
| **Resource Group Contributor** | Can manage Function App | ⚠️ **Limited** |

**Specific Capabilities Needed:**
- ✅ Create Function App
- ✅ Configure App Settings (environment variables)
- ✅ Deploy code
- ✅ Create Application Insights
- ✅ Configure authentication
- ✅ Set up managed identity (optional)

**Steps:**
1. Azure Portal → Create Resource → Function App
2. Configure:
   - Runtime: .NET 8.0
   - Hosting Plan: Consumption or Premium
   - Storage Account
3. Add App Settings:
   - `Graph_TenantId`
   - `Graph_ClientId`
   - `Graph_ClientSecret`
   - `EnrichedFolderRelativePath`
   - `MaxFileSizeBytes`
4. Deploy Function App code
5. Enable Application Insights

---

### 1.4 Webhook Subscription Setup

**Who:** Azure AD App (with `Webhooks.ReadWrite.All` permission)

**Required Actions:**
1. Create webhook subscription
2. Configure subscription renewal

**Permissions Required:**

| Permission | Type | Purpose | Required |
|------------|------|---------|----------|
| `Webhooks.ReadWrite.All` | Application | Create/manage webhook subscriptions | ✅ **Yes** |

**Note:** This is handled automatically by the Function App using the Azure AD app credentials. No manual action needed if app permissions are correctly configured.

**What Happens:**
- Function App creates subscription via Microsoft Graph API
- Subscription points to Function App webhook endpoint (`/api/ProcessSharePointFile`)
- Subscription auto-renews every 2 days (handled by Function App)

---

## 2. Copilot Configuration Permissions

### 2.1 Standard Copilot (Auto Configuration)

**Who:** No additional permissions needed (automatic)

**What Happens:**
- Documents in "SMEPilot Enriched Docs" are automatically indexed by Microsoft Search
- O365 Copilot automatically uses SharePoint search results
- No manual configuration required

**Permissions Required:**
- ✅ **None** - Works automatically if:
  - Documents are in SharePoint library
  - Library is included in search index (default)
  - Users have read access to library

---

### 2.2 Copilot Studio Configuration (Manual Setup)

**Who:** Microsoft 365 Global Administrator or Copilot Studio Administrator

**Required Actions:**
1. Access Copilot Studio
2. Create Copilot Agent
3. Configure data source
4. Set custom instructions
5. Configure access points
6. Deploy to Teams/Web

**Permissions Required:**

| Permission Level | Purpose | Required |
|-----------------|---------|----------|
| **Global Administrator** | Full tenant control | ✅ **Yes** |
| **Copilot Studio Administrator** | Can create/manage Copilot Agents | ✅ **Alternative** |
| **Teams Administrator** | Can deploy to Teams | ✅ **If deploying to Teams** |

**Specific Capabilities Needed:**
- ✅ Access Microsoft 365 Admin Center
- ✅ Navigate to Copilot Studio
- ✅ Create new Copilot Agent
- ✅ Configure data sources (SharePoint)
- ✅ Set system prompts/custom instructions
- ✅ Configure access points (Teams, Web, O365)
- ✅ Deploy Copilot Agent
- ✅ Manage Copilot Agent settings

**Steps:**

#### Step 1: Access Copilot Studio
1. Go to [Microsoft 365 Admin Center](https://admin.microsoft.com)
2. Navigate to **Copilot Studio** (or [https://copilotstudio.microsoft.com](https://copilotstudio.microsoft.com))
3. Sign in with Global Admin or Copilot Studio Admin account

#### Step 2: Create Copilot Agent
1. Click "Create" → "Copilot Agent"
2. Name: "SMEPilot" (or your preferred name)
3. Description: "Knowledge assistant for SMEPilot enriched documents"

#### Step 3: Configure Data Source
1. Go to "Data sources" → "Add data source"
2. Select "SharePoint"
3. Enter SharePoint site URL
4. Select library: **"SMEPilot Enriched Docs"**
5. Enable "Microsoft Search integration"
6. Save data source

**Permissions Needed for Data Source:**
- ✅ Read access to SharePoint site
- ✅ Read access to "SMEPilot Enriched Docs" library
- ✅ Microsoft Search must have indexed the library

#### Step 4: Set Custom Instructions
1. Go to "Settings" → "System instructions"
2. Add custom prompt:
   ```
   You are SMEPilot — the organization's knowledge assistant. 
   Use only documents from the 'SMEPilot Enriched Docs' SharePoint library 
   as the primary evidence. Provide a short summary (2-3 lines), then give 
   a numbered step list for procedures or troubleshooting. Always cite the 
   document title and link. If uncertain, say 'I couldn't find a definitive 
   answer in SMEPilot docs.' Do not invent facts beyond the indexed docs.
   ```
3. Save instructions

#### Step 5: Configure Access Points
1. Go to "Channels" → "Microsoft Teams"
   - Enable Teams channel
   - Configure Teams app settings
2. Go to "Channels" → "Web"
   - Enable Web channel (if needed)
   - Configure web interface settings
3. Go to "Channels" → "Microsoft 365 Copilot"
   - Enable O365 Copilot integration (if needed)

**Permissions Needed for Access Points:**
- ✅ **Teams:** Teams Administrator role (to deploy Teams app)
- ✅ **Web:** No additional permissions (web interface accessible to all)
- ✅ **O365 Copilot:** Requires M365 Copilot license (user-level)

#### Step 6: Deploy Copilot Agent
1. Click "Publish" → "Publish to Teams"
2. Select Teams to deploy to (or "All teams")
3. Configure app settings:
   - App name: "SMEPilot"
   - App icon (optional)
   - App description
4. Click "Publish"

**Permissions Needed for Deployment:**
- ✅ Teams Administrator role (to publish Teams app)
- ✅ Or approval from Teams Administrator

#### Step 7: Verify Configuration
1. Test in Teams: `@SMEPilot What is SMEPilot?`
2. Verify Copilot Agent responds with information from enriched documents
3. Check that citations include document links

---

### 2.3 SharePoint Search Configuration (If Needed)

**Who:** SharePoint Administrator or Site Collection Administrator

**Required Actions:**
1. Verify site is visible to search
2. Trigger search reindex (if needed)
3. Verify library permissions for search

**Permissions Required:**

| Permission Level | Purpose | Required |
|-----------------|---------|----------|
| **SharePoint Administrator** | Can manage search settings | ✅ **If needed** |
| **Site Collection Administrator** | Can trigger reindex | ✅ **If needed** |

**Steps:**
1. SharePoint Site → Site Settings → Search Settings
2. Verify "Allow this site to appear in search results" is enabled
3. If needed, trigger reindex:
   - Site Settings → Search and offline availability
   - Click "Reindex site" or "Reindex library"
4. Wait 24-48 hours for indexing (or use Search API to verify)

**Note:** Usually not needed - SharePoint libraries are indexed by default.

---

## 3. Runtime Permissions

### 3.1 Function App Runtime Permissions

**Who:** Azure AD App (Service Principal)

**What It Does:**
- Downloads files from source folder
- Processes documents
- Uploads enriched documents to destination folder
- Updates metadata on source documents

**Permissions Required:**

| Permission | Type | Purpose | Required |
|------------|------|---------|----------|
| `Sites.ReadWrite.All` | Application | Read/write SharePoint files and metadata | ✅ **Yes** |
| `Files.ReadWrite.All` | Application | Read/write files | ✅ **Yes** |

**Specific Access Needed:**
- ✅ **Source Folder:** Read access (to download files)
- ✅ **Source Folder:** Write access (to update metadata)
- ✅ **Destination Folder:** Write access (to upload enriched documents)
- ✅ **Templates Library:** Read access (to read template file)
- ✅ **SMEPilotConfig List:** Read access (to read configuration)

**Note:** These permissions are granted at tenant level (all sites). The app only accesses configured folders.

---

### 3.2 SharePoint Library/Folder Permissions

**Who:** SharePoint Site Administrator (one-time setup)

**Required Actions:**
1. Set folder-level permissions
2. Ensure Function App has access

**Permissions Required:**

| Folder/Library | Function App Access | User Access |
|----------------|---------------------|-------------|
| **Source Folder** | Read, Write | Contribute (for uploads) |
| **Destination Folder** | Write, Create | Read (for viewing) |
| **Templates Library** | Read | Read (optional) |
| **SMEPilotConfig List** | Read | Write (Admin only) |

**Steps:**
1. Navigate to each folder/library
2. Click "..." → "Manage access"
3. Add Function App service principal:
   - Search for app name (e.g., "SMEPilot Function App")
   - Grant appropriate permissions
4. Set user permissions:
   - Source Folder: Grant "Contribute" to users who upload
   - Destination Folder: Grant "Read" to users who query

**Note:** If Azure AD app has `Sites.ReadWrite.All`, it automatically has access. Folder-level permissions are for user access control.

---

## 4. User Permissions

### 4.1 Document Upload Users

**Who:** Business users who upload documents

**Required Actions:**
- Upload `.docx` files to source folder

**Permissions Required:**

| Permission Level | Purpose | Required |
|-----------------|---------|----------|
| **Contribute** | Upload documents to source folder | ✅ **Yes** |

**Specific Access Needed:**
- ✅ Source Folder: Contribute permission
- ✅ Can upload files
- ✅ Can view uploaded files

**Note:** No SharePoint admin permissions needed. Standard user permissions only.

---

### 4.2 Copilot Query Users

**Who:** End users who query enriched documents via Copilot

**Required Actions:**
- Query Copilot Agent in Teams/Web/O365
- View enriched documents

**Permissions Required:**

| Permission Level | Purpose | Required |
|-----------------|---------|----------|
| **Read** | Read enriched documents | ✅ **Yes** |
| **M365 Copilot License** | Use Copilot features | ✅ **If using O365 Copilot** |

**Specific Access Needed:**
- ✅ Destination Folder ("SMEPilot Enriched Docs"): Read permission
- ✅ M365 Copilot license (if using O365 Copilot integration)
- ✅ Teams access (if using Teams channel)

**Note:** 
- No SharePoint admin permissions needed
- Uses existing M365 Copilot license (no additional licenses)
- Security trimming applies (users only see documents they have access to)

---

### 4.3 Configuration Administrators

**Who:** Administrators who update configuration

**Required Actions:**
- Update SMEPilotConfig list
- Modify configuration settings

**Permissions Required:**

| Permission Level | Purpose | Required |
|-----------------|---------|----------|
| **Site Collection Administrator** | Update configuration | ✅ **Yes** |
| **Site Owner** | Update configuration (if granted) | ⚠️ **Alternative** |

**Specific Access Needed:**
- ✅ SMEPilotConfig List: Write permission
- ✅ Can modify configuration entries
- ✅ Can update folder paths, template settings, etc.

**Note:** Configuration updates don't require code changes - just update SharePoint list.

---

## 5. Permission Summary Matrix

### Quick Reference Table

| Level | Role | Permissions Required | When Needed |
|-------|------|---------------------|-------------|
| **Azure AD Setup** | Global Admin | Grant `Sites.ReadWrite.All`, `Files.ReadWrite.All`, `Webhooks.ReadWrite.All` | One-time during app registration |
| **SharePoint Setup** | Site Collection Admin | Create libraries, lists, columns | One-time during installation |
| **Function App Setup** | Subscription Owner | Create/manage Function App | One-time during deployment |
| **Copilot Studio** | Global Admin / Copilot Admin | Create Copilot Agent, configure data source | One-time during Copilot setup |
| **Runtime** | Azure AD App | `Sites.ReadWrite.All`, `Files.ReadWrite.All` | Ongoing (automatic) |
| **Users (Upload)** | Business Users | Contribute to source folder | Ongoing |
| **Users (Query)** | End Users | Read destination folder, M365 Copilot license | Ongoing |
| **Config Admin** | Site Collection Admin | Write to SMEPilotConfig list | As needed |

---

### Permission Flow Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                    APP SETUP PHASE                           │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  1. Azure AD Global Admin                                   │
│     └─> Grant App Permissions (Sites.ReadWrite.All, etc.)  │
│                                                              │
│  2. SharePoint Site Collection Admin                       │
│     └─> Create Libraries, Lists, Columns                   │
│                                                              │
│  3. Azure Subscription Owner                                │
│     └─> Create Function App, Configure Settings             │
│                                                              │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│              COPILOT CONFIGURATION PHASE                     │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  1. M365 Global Admin / Copilot Admin                      │
│     └─> Create Copilot Agent in Copilot Studio            │
│     └─> Configure Data Source (SharePoint)                 │
│     └─> Set Custom Instructions                            │
│     └─> Deploy to Teams                                    │
│                                                              │
│  2. SharePoint Admin (if needed)                           │
│     └─> Verify Search Indexing                             │
│                                                              │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│                    RUNTIME PHASE                             │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  1. Azure AD App (Automatic)                              │
│     └─> Processes documents using app permissions          │
│                                                              │
│  2. Business Users                                         │
│     └─> Upload documents (Contribute permission)            │
│                                                              │
│  3. End Users                                              │
│     └─> Query Copilot (Read permission + Copilot license)  │
│                                                              │
│  4. Config Admin (As Needed)                               │
│     └─> Update configuration (Site Collection Admin)       │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## 6. Important Notes

### 6.1 One-Time vs. Ongoing Permissions

**One-Time (Setup Only):**
- ✅ Azure AD Global Admin (grant app permissions)
- ✅ SharePoint Site Collection Admin (create libraries/lists)
- ✅ Azure Subscription Owner (create Function App)
- ✅ M365 Global Admin (configure Copilot Studio)

**Ongoing (Runtime):**
- ✅ Azure AD App (automatic - uses app permissions)
- ✅ Business Users (upload documents)
- ✅ End Users (query Copilot)
- ⚠️ Config Admin (only when updating configuration)

### 6.2 Minimal Permissions Principle

- ✅ **App Permissions:** Only SharePoint-related (`Sites.ReadWrite.All`, `Files.ReadWrite.All`, `Webhooks.ReadWrite.All`)
- ✅ **User Permissions:** Only what's needed (Contribute for upload, Read for query)
- ✅ **Admin Permissions:** Only during setup (no ongoing admin access needed)

### 6.3 Security Considerations

- ✅ App uses **app-only authentication** (no user credentials stored)
- ✅ Users only see documents they have **read access** to (security trimming)
- ✅ Copilot respects **SharePoint permissions** (users can't see restricted documents)
- ✅ All permissions are **audited** (Azure AD and SharePoint audit logs)

---

## 7. Troubleshooting Permission Issues

### Common Issues

#### Issue: "Permission denied" during document processing
**Solution:**
- Verify Azure AD app has `Sites.ReadWrite.All` with admin consent
- Check folder-level permissions
- Verify app can access source and destination folders

#### Issue: "Cannot create Copilot Agent"
**Solution:**
- Verify user has Global Admin or Copilot Studio Admin role
- Check M365 Copilot license availability
- Verify access to Copilot Studio

#### Issue: "Copilot can't find documents"
**Solution:**
- Verify users have Read access to "SMEPilot Enriched Docs" library
- Check SharePoint search indexing (may take 24-48 hours)
- Verify library is included in search index
- Check Copilot Agent data source configuration

#### Issue: "Cannot update configuration"
**Solution:**
- Verify user has Site Collection Admin or Site Owner permissions
- Check SMEPilotConfig list permissions
- Verify user has Write access to list

---

## 8. Quick Checklist

### App Setup Checklist
- [ ] Azure AD Global Admin granted app permissions
- [ ] SharePoint Site Collection Admin created libraries/lists
- [ ] Azure Subscription Owner created Function App
- [ ] Function App configured with app credentials
- [ ] Webhook subscription created

### Copilot Configuration Checklist
- [ ] M365 Global Admin accessed Copilot Studio
- [ ] Created Copilot Agent
- [ ] Configured data source (SharePoint - "SMEPilot Enriched Docs")
- [ ] Set custom instructions
- [ ] Configured access points (Teams/Web/O365)
- [ ] Deployed to Teams
- [ ] Tested Copilot Agent

### Runtime Checklist
- [ ] Users have Contribute access to source folder
- [ ] Users have Read access to destination folder
- [ ] Users have M365 Copilot license (if using O365 Copilot)
- [ ] Function App has access to all required folders
- [ ] Configuration is saved in SMEPilotConfig list

---

**This document provides complete permissions information for all levels of SMEPilot setup and configuration.**

