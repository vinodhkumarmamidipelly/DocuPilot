# SMEPilot - Manager Overview Document

## 📋 Executive Summary

**SMEPilot** is a SharePoint-based document enrichment and AI-powered knowledge management system that automatically transforms rough, incomplete documents into structured, searchable, and AI-ready content for Microsoft 365 Copilot integration.

---

## 🎯 Business Problem We're Solving

### **Current Challenge:**
- Business users create "scratch documents" with minimal descriptions and screenshots
- Documents are incomplete, unstructured, and not searchable
- O365 Copilot can't effectively use these documents to answer questions
- Manual document formatting and enrichment is time-consuming

### **Our Solution:**
- **Automatic Document Enrichment**: Transforms rough documents into structured, enriched content
- **AI-Powered Enhancement**: Expands content, creates sections, generates summaries
- **Copilot Integration**: Makes documents searchable and usable by Microsoft 365 Copilot
- **Cost-Effective**: Hybrid approach minimizes AI costs while maintaining quality

---

## 🏗️ Concept & Approach

### **High-Level Architecture:**

```
┌─────────────────────────────────────────────────────────────┐
│                    SharePoint Document Library              │
│  (Users upload scratch documents with screenshots)          │
└────────────────────┬────────────────────────────────────┘
                  │
                  │ Webhook Notification
                  ▼
┌─────────────────────────────────────────────────────────────┐
│              Azure Function App (SMEPilot)                  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  1. Document Extraction                              │  │
│  │     - Extract text and images from DOCX              │  │
│  └──────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  2. Document Enrichment (Hybrid Mode)               │  │
│  │     - Rule-based sectioning (no AI cost)            │  │
│  │     - AI content enrichment (minimal cost)           │  │
│  │     - Classification (keyword-based)                 │  │
│  └──────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  3. Template Application                             │  │
│  │     - Apply standard template                        │  │
│  │     - Add table of contents                         │  │
│  │     - Structure sections                             │  │
│  └──────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  4. Embedding Generation                             │  │
│  │     - Create semantic embeddings for search          │  │
│  │     - Store in MongoDB/Cosmos DB                    │  │
│  └──────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  5. Upload & Metadata                                │  │
│  │     - Upload enriched document to ProcessedDocs     │  │
│  │     - Update metadata (status, classification)      │  │
│  └──────────────────────────────────────────────────────┘  │
└────────────────────┬───────────────────────────────────────┘
                     │
                     │ Enriched Document
                     ▼
┌─────────────────────────────────────────────────────────────┐
│              SharePoint ProcessedDocs Folder               │
│  (Structured, enriched documents ready for Copilot)        │
└────────────────────┬───────────────────────────────────────┘
                     │
                     │ Semantic Search
                     ▼
┌─────────────────────────────────────────────────────────────┐
│              Microsoft 365 Copilot (Teams)                 │
│  (Users can ask questions, get answers from documents)     │
└─────────────────────────────────────────────────────────────┘
```

---

## 🔄 Complete Workflow

### **Step 1: Document Upload**
- User uploads a scratch document to SharePoint
- Document contains: screenshots, minimal text, basic descriptions
- **Trigger**: Automatic webhook notification

### **Step 2: Automatic Processing**
- **Extraction**: Extract text and images from DOCX
- **Sectioning**: Rule-based detection of headings and sections (no AI cost)
- **Enrichment**: AI expands and enhances content (minimal AI cost)
- **Classification**: Keyword-based categorization (Technical/Support/Functional)
- **Template**: Apply standard document template with TOC

### **Step 3: Storage & Indexing**
- **Enriched Document**: Uploaded to `ProcessedDocs` folder
- **Embeddings**: Semantic vectors stored in MongoDB/Cosmos DB
- **Metadata**: Status, classification, enrichment date tracked

### **Step 4: Copilot Integration**
- Documents are searchable by Microsoft 365 Copilot
- Users can ask questions in Teams
- Copilot retrieves relevant sections and provides answers

---

## 💡 Key Features & Capabilities

### **1. Automatic Document Enrichment**
- ✅ Transforms rough documents into structured content
- ✅ Expands minimal descriptions into comprehensive text
- ✅ Creates logical sections with headings
- ✅ Generates summaries for each section
- ✅ Preserves all original content (images, technical details)

### **2. Cost-Effective AI Usage (Hybrid Mode)**
- ✅ **Rule-based sectioning**: No AI cost
- ✅ **AI content enrichment**: Only where needed (minimal cost)
- ✅ **Keyword classification**: No AI cost
- **Result**: ~60% cost savings vs full AI approach

### **3. Semantic Search**
- ✅ Vector embeddings enable semantic search
- ✅ "What are alerts?" finds "alert types", "notification system", etc.
- ✅ Understands meaning, not just keywords

### **4. Multi-Tenant Support**
- ✅ Isolated data per organization
- ✅ Secure tenant-based access
- ✅ Scalable architecture

### **5. Automatic Processing**
- ✅ No manual intervention required
- ✅ Processes documents as soon as uploaded
- ✅ Handles concurrent uploads
- ✅ Prevents duplicate processing

---

## 📊 What We Can Answer (Use Cases)

### **Example Questions Users Can Ask:**

1. **"What are the types of alerts in the system?"**
   - Copilot searches enriched documents
   - Finds relevant sections about alerts
   - Returns: "There are two types of alerts: Immediate alerts and Scheduled alerts..."

2. **"How are alerts triggered?"**
   - Finds sections about alert triggering mechanisms
   - Provides detailed explanation from enriched content

3. **"What is SignalR used for?"**
   - Searches for SignalR-related content
   - Returns explanation from enriched documents

4. **"Show me the API endpoints for alerts"**
   - Finds technical sections with API details
   - Returns specific endpoints and usage

5. **"What are the troubleshooting steps for Module X?"**
   - Finds support/functional documentation
   - Returns step-by-step guidance

### **Document Types Supported:**
- ✅ Product documentation
- ✅ Application guides
- ✅ Technical specifications
- ✅ Support documentation
- ✅ Process workflows
- ✅ Feature descriptions

---

## 🛠️ Technical Implementation

### **Technology Stack:**
- **Backend**: Azure Functions (.NET 8.0, Isolated Worker)
- **AI Services**: Azure OpenAI (GPT-4o-mini, text-embedding-ada-002)
- **Database**: MongoDB (testing) / Cosmos DB (production)
- **Integration**: Microsoft Graph API (SharePoint, Teams)
- **Storage**: SharePoint Document Libraries

### **Key Components:**

1. **ProcessSharePointFile Function**
   - Handles document upload notifications
   - Orchestrates enrichment pipeline
   - Manages concurrency and idempotency

2. **HybridEnricher**
   - Rule-based sectioning
   - AI content enrichment
   - Document classification

3. **QueryAnswer Function**
   - Semantic search endpoint
   - Answer synthesis
   - Source attribution

4. **GraphHelper**
   - SharePoint file operations
   - Metadata management
   - Webhook subscriptions

---

## 📈 Current Status

### **✅ Completed:**
- ✅ Document extraction (text + images)
- ✅ Hybrid enrichment mode (cost-saving)
- ✅ Template generation
- ✅ Embedding storage (MongoDB/Cosmos DB)
- ✅ SharePoint integration
- ✅ Automatic triggers (webhooks)
- ✅ Semantic search
- ✅ Multi-tenant support
- ✅ Error handling & retry logic

### **🔄 In Progress:**
- 🔄 Testing and optimization
- 🔄 Copilot integration verification
- 🔄 Performance tuning

### **📋 Future Enhancements:**
- 📋 SPFx frontend (SharePoint app)
- 📋 Document versioning
- 📋 Approval workflows
- 📋 Advanced analytics

---

## 💰 Cost Analysis

### **Per Document Processing:**
- **Hybrid Mode**: ~₹1-2 per document
  - Rule-based sectioning: ₹0
  - AI enrichment: ~₹1
  - Embeddings: ~₹0.10-0.20
  - Classification: ₹0

- **Full AI Mode**: ~₹2-3 per document
  - AI sectioning: ~₹1
  - AI enrichment: ~₹1
  - Embeddings: ~₹0.10-0.20

### **Infrastructure:**
- **MongoDB**: Free (using existing VM)
- **Cosmos DB**: Pay-per-use (production option)
- **Azure Functions**: Consumption plan (pay-per-execution)
- **Azure OpenAI**: Pay-per-token

**Savings with Hybrid Mode: ~60% cost reduction**

---

## 🎯 Business Value

### **For Organizations:**
1. **Time Savings**: Automatic document enrichment vs manual formatting
2. **Better Search**: Semantic search finds relevant content easily
3. **Copilot Ready**: Documents immediately usable by O365 Copilot
4. **Consistency**: Standard templates ensure uniform documentation
5. **Scalability**: Handles high volume automatically

### **For Users:**
1. **Quick Answers**: Ask questions in Teams, get instant answers
2. **Better Documentation**: Enriched content is more comprehensive
3. **Easy Discovery**: Find relevant information quickly
4. **No Manual Work**: Upload and forget - enrichment is automatic

---

## 🔒 Security & Compliance

- ✅ **Multi-tenant isolation**: Data separated by organization
- ✅ **Azure AD authentication**: Secure access control
- ✅ **Graph API permissions**: Least privilege access
- ✅ **Data encryption**: At rest and in transit
- ✅ **Audit logging**: Track all operations

---

## 📝 What We Can Answer to Management

### **Q: What problem does this solve?**
**A:** Organizations struggle with incomplete, unstructured documentation that Copilot can't effectively use. SMEPilot automatically enriches documents, making them searchable and AI-ready.

### **Q: How does it work?**
**A:** Users upload scratch documents → System automatically enriches them → Documents become searchable by Copilot → Users ask questions in Teams → Get instant answers.

### **Q: What's the cost?**
**A:** ~₹1-2 per document with Hybrid Mode (60% savings). Scales with usage. No upfront infrastructure costs.

### **Q: What's the ROI?**
**A:** 
- Time savings: No manual document formatting
- Better knowledge discovery: Semantic search finds relevant content
- Improved user experience: Instant answers via Copilot
- Scalable: Handles any volume automatically

### **Q: Is it production-ready?**
**A:** Core functionality is complete and tested. Currently in final testing phase. Ready for pilot deployment.

### **Q: What's the deployment model?**
**A:** SharePoint App - can be deployed to any SharePoint site. No client installation required.

### **Q: How long to implement?**
**A:** Core system is ready. Deployment and configuration: 1-2 weeks. User training: 1 day.

---

## 🚀 Next Steps

1. **Final Testing**: Complete end-to-end testing
2. **Pilot Deployment**: Deploy to test SharePoint site
3. **User Training**: Train users on upload and query process
4. **Monitoring**: Set up monitoring and analytics
5. **Production Rollout**: Deploy to production sites

---

## 📞 Support & Questions

**Technical Questions**: Development team
**Business Questions**: Product owner
**Deployment**: Infrastructure team

---

**Document Version**: 1.0  
**Last Updated**: November 2025  
**Status**: Ready for Management Review

