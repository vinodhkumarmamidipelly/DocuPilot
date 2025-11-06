# SMEPilot Requirements

## Business Objective
Build a **sellable SharePoint App** that helps organizations manage their documentation easily - create, update, and make it available across the organization. The app will be distributed through SharePoint App Catalog and available for purchase/subscription.

## User Workflow (Actual Requirement)
1. **Business users create scratch documents** about products or applications with screenshots and minimal descriptions
2. **Upload to SharePoint** - Documents uploaded to designated SharePoint library
3. **SharePoint Document Enricher triggers automatically** when document is uploaded
4. **Enrichment process:**
   - Splits document into Images, Text, Sections
   - Puts content into a Standard Template (with proper indexing, sections, images & text)
   - Enhances and structures content using Azure OpenAI
5. **Push enriched document back to SharePoint** folder (ProcessedDocs)
6. **O365 Copilot integration** - Enriched documents are indexed and searchable
7. **Org-wide access** - All employees can query enriched documents through Teams/O365 Copilot (called SMEPilot)

## Key Features

### Core Features (MVP - Required for Selling)
- ✅ **SharePoint App Package (SPFx)** - Installable app via App Catalog
- ✅ Upload rough documents (with screenshots + minimal descriptions) through SPFx web part
- ✅ Automatically enrich and standardize content using Azure OpenAI
- ✅ Generate professional, indexed Word documents with standard template
- ✅ Store embeddings in CosmosDB for semantic search
- ✅ **O365 Copilot Integration** - Enable users to query content via Teams / O365 Copilot
- ✅ **Org-wide employee access** - Available to all organization employees through Teams
- ✅ Automatic trigger when documents uploaded to SharePoint

### Architecture Overview
- **Frontend:** SharePoint App (SPFx) - **REQUIRED FOR MVP** (must be sellable)
- **Backend:** Azure Function App (.NET 8)
- **AI Processing:** Azure OpenAI (GPT + Embeddings)
- **Database:** CosmosDB (free-tier or emulator)
- **Storage:** SharePoint Document Library
- **Integration:** 
  - Microsoft Graph API
  - Microsoft Search Connector (for Copilot indexing) - **REQUIRED FOR MVP**
  - Teams Bot or Copilot Extension - **REQUIRED FOR MVP**
- **Trigger:** Graph subscription/webhook (preferred) or Power Automate (fallback)

## Output
- Enriched document (.docx) in SharePoint ProcessedDocs folder
- Indexed metadata in SharePoint
- Embedding vectors for Copilot Q&A stored in CosmosDB
- Documents searchable via native O365 Copilot in Teams

## MVP Scope (Revised - Aligned with Business Requirement)

### MUST HAVE (Critical for Selling)
1. **SPFx SharePoint App Package** - Complete solution packageable as `.sppkg`
   - Main web part for document upload and monitoring
   - Admin UI for managing enrichment jobs
   - App manifest and permissions configuration

2. **O365 Copilot Integration** - Users query from Teams/Copilot
   - Microsoft Graph Connector for Microsoft Search (native Copilot integration)
   - OR Teams Bot wrapper around QueryAnswer API
   - Automatic user context detection (no manual tenantId)

3. **Core Enrichment Pipeline**
   - Support `.docx` upload
   - Automatic sectioning, summarization, and enrichment
   - Standard template generation (with indexing, sections, images & text)
   - Azure OpenAI integration
   - CosmosDB vector storage

4. **Org-wide Access**
   - Automatic tenant/user context extraction
   - RBAC for multi-tenant support
   - Available to all employees without technical knowledge

### NICE TO HAVE (Phase 2)
- OCR for images and PDF support
- Advanced template customization
- Notification system
- Human-in-the-loop review portal

## Distribution & Sales Model
- **Distribution:** SharePoint App Catalog (`.sppkg` file)
- **Installation:** Tenant admins deploy from App Catalog
- **Access:** All org employees via Teams/O365 Copilot
- **Licensing:** Per-tenant or per-user subscription model (future)

## Success Criteria
✅ App can be installed from SharePoint App Catalog  
✅ Users can upload documents through SPFx web part  
✅ Enriched documents appear in SharePoint automatically  
✅ Employees can query enriched docs from Teams/Copilot without technical setup  
✅ Works for all employees in the organization
