# DocuPilot - Document Template Formatting for SharePoint

## 📋 Overview

DocuPilot is a SharePoint-integrated solution that automatically formats documents into organizational templates. It processes documents uploaded to SharePoint, applies template formatting, and makes them ready for O365 Copilot.

## ✅ Key Features

- ✅ **No Database Required** - Works without any database
- ✅ **No AI Required** - Rule-based template formatting only
- ✅ **Multi-Format Support** - DOCX, PPTX, XLSX, PDF, Images (with OCR)
- ✅ **Automatic Processing** - Webhook-triggered document processing
- ✅ **Template Application** - Applies organizational template with proper formatting
- ✅ **Copilot Ready** - Formatted documents available for O365 Copilot via SharePoint native search

## 🏗️ Architecture

### **Components:**

1. **Azure Function App** (`SMEPilot.FunctionApp`)
   - Processes SharePoint documents
   - Applies template formatting
   - Handles webhook subscriptions

2. **SPFx Web Parts** (`SMEPilot.SPFx`)
   - Document uploader
   - Admin panel for status tracking

## 📁 Project Structure

```
DocuPilot/
├── README.md                    # This file
├── Knowledgebase/              # All documentation
├── SMEPilot.FunctionApp/       # Azure Functions backend
│   ├── Functions/              # Azure Functions
│   ├── Helpers/               # Helper classes
│   └── Models/                # Data models
└── SMEPilot.SPFx/             # SharePoint Framework frontend
```

## 🚀 Quick Start

### **Prerequisites:**
- .NET 8.0 SDK
- Azure Functions Core Tools
- Node.js 18+ (for SPFx)
- SharePoint site with appropriate permissions

### **Configuration:**

1. **Function App Configuration** (`local.settings.json`):
   ```json
   {
     "Values": {
       "Graph_TenantId": "your-tenant-id",
       "Graph_ClientId": "your-client-id",
       "Graph_ClientSecret": "your-client-secret",
       "EnrichedFolderRelativePath": "/Shared Documents/ProcessedDocs",
       "AzureVision_Endpoint": "",  // Optional: For OCR
       "AzureVision_Key": ""         // Optional: For OCR
     }
   }
   ```

2. **Run Function App:**
   ```bash
   cd SMEPilot.FunctionApp
   func start
   ```

3. **Setup Webhook Subscription:**
   ```powershell
   .\SetupSubscription.ps1
   ```

## 📚 Documentation

All documentation is available in the `Knowledgebase/` folder:

- **Status & Requirements:** See `Knowledgebase/FINAL_COMPLETION_STATUS.md`
- **Implementation Guides:** See `Knowledgebase/MULTI_FORMAT_SUPPORT_IMPLEMENTED.md`
- **Technical Details:** See `Knowledgebase/` folder for all documentation

## 🎯 Current Status

**✅ 100% Complete** - All requirements implemented and verified.

- ✅ No database dependencies
- ✅ No AI dependencies
- ✅ Template formatting working
- ✅ Multi-format support complete
- ✅ SharePoint integration complete
- ✅ SPFx UI ready

## 📝 License

[Your License Here]

---

**For detailed documentation, see the `Knowledgebase/` folder.**
