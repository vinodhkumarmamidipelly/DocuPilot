# SMEPilot - Knowledge Base Application

## 📋 Overview

SMEPilot is a knowledge base application for functional and technical documents with two main features:

1. **Document Enrichment** - Template-based formatting of uploaded documents
2. **Copilot Agent** - O365 Copilot configured to assist users with questions

---

## 📚 Documentation

All documentation is in the **`Knowledgebase/`** folder.

### Essential Documents (Read These)

1. **`Knowledgebase/REQUIREMENTS_AND_ARCHITECTURE.md`** ⭐ **START HERE**
   - Complete requirements, architecture, and configuration
   - All you need to understand the system

2. **`Knowledgebase/QUICK_START_COPILOT.md`** 
   - Step-by-step guide for Copilot Agent setup
   - O365 Copilot Studio configuration

3. **`Knowledgebase/ARCHITECTURE_DIAGRAM.md`**
   - System architecture and component diagrams
   - Visual representation of the system

### Reference Documentation

- **`Knowledgebase/EDGE_CASES_AND_PERMISSIONS.md`** - Edge cases handling and permissions (reference when needed)
- **`Knowledgebase/INSTALLATION_AND_CONFIGURATION.md`** - Installation and configuration guide
- **`Knowledgebase/QA_CHECKLIST.md`** - QA checklist for testing
- **`Knowledgebase/ConfigureAzureServices.md`** - ⚠️ **OUTDATED** - Azure OpenAI/CosmosDB setup (NOT USED in current implementation - kept for reference only)

---

## 🚀 Quick Start

### For Developers
1. Read `Knowledgebase/REQUIREMENTS_AND_ARCHITECTURE.md` for complete understanding
2. Review `Knowledgebase/ARCHITECTURE_DIAGRAM.md` for system design
3. Follow implementation in code

### For Configuration
1. Read `Knowledgebase/QUICK_START_COPILOT.md` for Copilot setup
2. Follow step-by-step instructions

---

## 🎯 Current Status

- ✅ **Document Enrichment:** Fully implemented
- ⚠️ **Copilot Agent:** Needs configuration (see `Knowledgebase/QUICK_START_COPILOT.md`)

---

## 📁 Project Structure

```
SMEPilot.FunctionApp/
├── Functions/          # Azure Functions
├── Helpers/            # Helper classes
├── Services/           # Service classes
├── Models/             # Data models
└── Templates/          # Word templates
```

---

## 🔧 Key Requirements

- **Source Folder:** Where documents are uploaded (configurable)
- **Destination Folder:** "SMEPilot Enriched Docs" (required for Copilot)
- **Template:** UniversalOrgTemplate.dotx
- **Copilot:** O365 Copilot with custom instructions

---

**Last Updated:** Based on current implementation requirements
