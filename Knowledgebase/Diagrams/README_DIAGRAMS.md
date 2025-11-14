# 📊 SMEPilot Draw.io Diagrams

All diagrams are in draw.io XML format and can be opened directly in draw.io web.

**⚠️ UPDATED:** All diagrams have been redesigned for clarity and professionalism. They now feature:
- Clear step-by-step numbering
- Better visual hierarchy
- Professional color coding
- Detailed explanations
- Proper spacing and layout

## 📁 Available Diagrams

### 1. **SMEPilot_Architecture_Diagram.drawio**
**Purpose:** Complete system architecture overview

**Shows:**
- SharePoint Online Tenant structure
- Source Folder, Templates Library, SMEPilot Enriched Docs library
- SMEPilotConfig list
- Azure Function App components
- Azure AD App Registration
- O365 Copilot integration
- Microsoft Search
- All connections and data flows

**Use:** High-level architecture presentation to stakeholders

---

### 2. **SMEPilot_Enrichment_Flow.drawio**
**Purpose:** Document enrichment process flow

**Shows:**
- User uploads document
- Webhook notification
- Idempotency check
- File download and validation
- Configuration reading
- Template application
- Enrichment process
- Upload to destination
- Metadata update
- Error handling paths

**Use:** Understanding the enrichment workflow step-by-step

---

### 3. **SMEPilot_Copilot_Flow.drawio**
**Purpose:** Copilot query flow

**Shows:**
- User asks question
- O365 Copilot processes query
- Microsoft Search queries SharePoint
- SMEPilot Enriched Docs library
- Search results
- Formatted response with citations
- Note: NO Function App involvement

**Use:** Understanding how Copilot queries work

---

### 4. **SMEPilot_System_Components.drawio**
**Purpose:** System components by layer

**Shows:**
- SharePoint Layer (libraries, lists, metadata)
- Azure Function App Layer (services, processors)
- Azure Services Layer (AD, Insights, Graph API)
- O365 Copilot Layer (Studio, Instructions, Search, Teams)

**Use:** Understanding system components and their relationships

---

### 5. **SMEPilot_Configuration_Diagram.drawio** ⭐ **NEW**
**Purpose:** Complete configuration architecture

**Shows:**
- SMEPilotConfig List structure
- All configuration parameters (Source, Destination, Template, Notification, Copilot, Processing)
- Azure Function App environment variables
- Configuration validation rules
- Important notes and requirements

**Use:** Understanding all configuration settings and where they are stored

---

### 6. **SMEPilot_Installation_Setup_Flow.drawio** ⭐ **NEW**
**Purpose:** Complete installation and setup process flow

**Shows:**
- Step 1: Azure AD App Registration (permissions, client secret)
- Step 2: Azure Function App Configuration (settings, Application Insights)
- Step 3: SPFx App Installation (deploy, admin panel)
- Step 4: Configuration (6 items: folders, template, settings, prompt, access points)
- Step 5: SharePoint Configuration (SMEPilotConfig list, metadata columns, error folders)
- Step 6: Webhook Subscription Creation (Graph API, validation, storage)
- Step 7: Copilot Agent Configuration (Copilot Studio, prompt, deployment)
- Success confirmation

**Use:** Step-by-step installation guide for administrators

---

### 7. **SMEPilot_Webhook_Subscription_Flow.drawio** ⭐ **NEW**
**Purpose:** Detailed webhook subscription lifecycle

**Shows:**
- Webhook Subscription Creation (Graph API POST, parameters, response)
- Webhook Validation (validation token, 10-second response requirement)
- Automatic Renewal Process (timer trigger, expiration check, new subscription creation)
- Decision flow for renewal (expires within 24 hours?)
- Key technical details (expiration limits, validation requirements)

**Use:** Understanding webhook subscription creation, validation, and renewal process

---

## 🚀 How to Use

### Option 1: Draw.io Web
1. Go to https://app.diagrams.net (or https://draw.io)
2. Click "Open Existing Diagram"
3. Select the `.drawio` file you want to open
4. Edit, customize, or export as needed

### Option 2: Draw.io Desktop
1. Download Draw.io desktop app
2. Open the `.drawio` file
3. Edit and save

### Option 3: VS Code Extension
1. Install "Draw.io Integration" extension in VS Code
2. Open the `.drawio` file
3. Edit directly in VS Code

---

## 📤 Export Options

Once opened in draw.io, you can export as:
- PNG (for presentations)
- PDF (for documentation)
- SVG (for web)
- JPEG (for images)
- XML (original format)

---

## 🎨 Customization

All diagrams use:
- Color coding:
  - 🔵 Blue: Azure services
  - 🟢 Green: SharePoint/Storage
  - 🟡 Yellow: Processing/Validation
  - 🔴 Red: Enriched Docs/Copilot
  - 🟣 Purple: Configuration/Metadata

- Icons:
  - 🔍 = Indexed by Copilot/Search
  - 📄 = Documents
  - 📋 = Templates
  - ⚙️ = Configuration

---

## 📝 Notes

- All diagrams are editable in draw.io
- Diagrams match the architecture described in `REQUIREMENTS_AND_ARCHITECTURE.md`
- Diagrams can be customized for presentations
- All components are labeled and connected

---

**Ready to use!** Open any `.drawio` file in draw.io web to view and edit.

