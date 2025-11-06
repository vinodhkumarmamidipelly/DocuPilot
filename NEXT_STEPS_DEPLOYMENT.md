# 🚀 SMEPilot - Next Steps for Deployment

## ✅ Current Status

**Completed:**
- ✅ Backend Azure Functions (local development)
- ✅ SPFx Frontend built and packaged
- ✅ Document enrichment working
- ✅ QueryAnswer API working
- ✅ CosmosDB integration working
- ✅ Graph API subscriptions configured

**Package Ready:**
- 📦 `SMEPilot.SPFx/sharepoint/solution/sme-pilot.sppkg` (617 KB)

---

## 📋 Next Steps - Deployment Checklist

### **Phase 1: Deploy Backend to Azure** (Priority: HIGH)

#### **Step 1.1: Deploy Function App to Azure**

**Option A: Visual Studio Deployment**
1. Open `SMEPilot.FunctionApp` in Visual Studio
2. Right-click project → **Publish**
3. Select **Azure** → **Azure Function App (Windows)**
4. Create new or select existing Function App
5. Configure:
   - **App Service Plan**: Consumption or Premium
   - **Storage Account**: Create new or use existing
   - **Application Insights**: Enable (recommended)
6. Click **Publish**

**Option B: Azure CLI**
```bash
# Login to Azure
az login

# Create Resource Group (if needed)
az group create --name SMEPilot-RG --location eastus

# Create Storage Account
az storage account create --name smepilotstorage --resource-group SMEPilot-RG --location eastus --sku Standard_LRS

# Create Function App
az functionapp create --resource-group SMEPilot-RG --consumption-plan-location eastus --runtime dotnet-isolated --functions-version 4 --name SMEPilot-FunctionApp --storage-account smepilotstorage

# Deploy
cd SMEPilot.FunctionApp
func azure functionapp publish SMEPilot-FunctionApp
```

#### **Step 1.2: Configure Application Settings**

In Azure Portal → Function App → Configuration → Application Settings:

**Required Settings:**
```
FUNCTIONS_WORKER_RUNTIME = dotnet-isolated
Graph_TenantId = <your-tenant-id>
Graph_ClientId = <your-client-id>
Graph_ClientSecret = <your-client-secret>
AzureOpenAI_Endpoint = <your-openai-endpoint>
AzureOpenAI_Key = <your-openai-key>
AzureOpenAI_Deployment_GPT = gpt-4o-mini
AzureOpenAI_Embedding_Deployment = text-embedding-ada-002
Cosmos_ConnectionString = <your-cosmos-connection-string>
Cosmos_Database = SMEPilotDB
Cosmos_Container = Embeddings
EnrichedFolderRelativePath = /Shared Documents/ProcessedDocs
```

#### **Step 1.3: Update Graph API Subscription**

Update the notification URL in your Graph subscription to point to Azure:

```powershell
# Get your Function App URL
# Format: https://<function-app-name>.azurewebsites.net/api/ProcessSharePointFile

# Update subscription (use SetupSubscription.ps1 with new URL)
.\SetupSubscription.ps1 `
  -SiteId "<your-site-id>" `
  -DriveId "<your-drive-id>" `
  -NotificationUrl "https://<function-app-name>.azurewebsites.net/api/ProcessSharePointFile"
```

---

### **Phase 2: Deploy SPFx Package to SharePoint** (Priority: HIGH)

#### **Step 2.1: Upload to App Catalog**

1. **Go to SharePoint Admin Center:**
   - Navigate to: https://admin.microsoft.com/Adminportal/Home#/SharePoint
   - Go to **More features** → **Apps** → **App Catalog**

2. **Upload Package:**
   - Click **Distribute apps for SharePoint**
   - Click **New** → **App for SharePoint**
   - Upload: `SMEPilot.SPFx/sharepoint/solution/sme-pilot.sppkg`
   - Click **Deploy**

3. **Approve API Permissions:**
   - Go to **API Management** → **Pending requests**
   - Approve:
     - `Sites.ReadWrite.All` (Microsoft Graph)
     - `Files.ReadWrite` (Microsoft Graph)

#### **Step 2.2: Add App to SharePoint Site**

1. **Go to your SharePoint site:**
   - Navigate to: `https://onblick.sharepoint.com/sites/DocEnricher-PoC`

2. **Add App:**
   - Click **Settings** (gear icon) → **Add an app**
   - Find **SMEPilot** → Click **Add**

3. **Trust the app:**
   - Click **Trust It** when prompted

#### **Step 2.3: Add Web Parts to Pages**

1. **Create/Edit a Page:**
   - Go to **Pages** → Create new or edit existing

2. **Add DocumentUploader Web Part:**
   - Click **+** → Search for **"SMEPilot Document Upload"**
   - Add to page
   - Configure:
     - **Function App URL**: `https://<function-app-name>.azurewebsites.net`
     - **Scratch Docs Library**: `ScratchDocs` (or your library name)

3. **Add AdminPanel Web Part (Optional):**
   - Add to admin page
   - Configure Function App URL

---

### **Phase 3: Configure CosmosDB (Production)** (Priority: MEDIUM)

#### **Step 3.1: Create CosmosDB Account**

1. **Azure Portal:**
   - Create **Azure Cosmos DB** account
   - API: **Core (SQL)**
   - Location: Same as Function App

2. **Create Database and Container:**
   - Database: `SMEPilotDB`
   - Container: `Embeddings`
   - Partition Key: `/TenantId`

3. **Get Connection String:**
   - Go to **Keys** → Copy **Primary Connection String**
   - Update Function App settings

---

### **Phase 4: End-to-End Testing** (Priority: HIGH)

#### **Step 4.1: Test Document Upload**

1. **Upload a test document:**
   - Use DocumentUploader web part
   - Upload a `.docx` file with images

2. **Verify Processing:**
   - Check Function App logs in Azure Portal
   - Verify enriched document appears in `ProcessedDocs` folder
   - Check CosmosDB for stored embeddings

#### **Step 4.2: Test QueryAnswer API**

```powershell
# Test QueryAnswer endpoint
$body = @{
    tenantId = "<your-tenant-id>"
    question = "What are the types of alerts?"
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://<function-app-name>.azurewebsites.net/api/QueryAnswer" `
  -Method Post -Body $body -ContentType "application/json"
```

#### **Step 4.3: Test Copilot Integration**

1. **Verify SharePoint Indexing:**
   - Go to Microsoft Search Admin Center
   - Verify site is indexed

2. **Test in Teams:**
   - Open Microsoft Teams
   - Ask Copilot: "Show me documents in DocEnricher-PoC site"
   - Ask specific questions about your documents

---

### **Phase 5: Production Hardening** (Priority: LOW)

#### **Step 5.1: Security**

- ✅ Use Key Vault for secrets (instead of App Settings)
- ✅ Enable Managed Identity for Function App
- ✅ Configure CORS for Function App
- ✅ Review and restrict API permissions

#### **Step 5.2: Monitoring**

- ✅ Set up Application Insights alerts
- ✅ Configure Function App monitoring
- ✅ Set up CosmosDB metrics
- ✅ Create dashboards in Azure Portal

#### **Step 5.3: Performance**

- ✅ Optimize Function App scaling
- ✅ Configure CosmosDB throughput
- ✅ Review and optimize AI prompts
- ✅ Cache frequently accessed data

---

## 🎯 Quick Start - Minimum Viable Deployment

**For quick testing, do these 3 steps:**

1. **Deploy Function App to Azure** (30 minutes)
   - Use Visual Studio Publish
   - Configure App Settings
   - Update Graph subscription URL

2. **Deploy SPFx Package** (15 minutes)
   - Upload to App Catalog
   - Approve permissions
   - Add to site

3. **Test End-to-End** (15 minutes)
   - Upload document
   - Verify enrichment
   - Test QueryAnswer

**Total Time: ~1 hour**

---

## 📝 Deployment Checklist

- [ ] Function App deployed to Azure
- [ ] Application Settings configured
- [ ] Graph API subscription updated
- [ ] SPFx package uploaded to App Catalog
- [ ] API permissions approved
- [ ] App added to SharePoint site
- [ ] Web parts configured
- [ ] CosmosDB production instance created
- [ ] Document upload tested
- [ ] Enrichment verified
- [ ] QueryAnswer API tested
- [ ] Copilot integration verified

---

## 🆘 Troubleshooting

### **Function App Issues:**
- Check Application Insights logs
- Verify all App Settings are set
- Check Function App status in Azure Portal

### **SPFx Deployment Issues:**
- Verify API permissions are approved
- Check browser console for errors
- Verify Function App URL is correct

### **Graph API Issues:**
- Verify subscription is active
- Check notification URL is accessible
- Review Function App logs for validation errors

---

## 📚 Documentation References

- **Function App Deployment**: `docs/Instructions.md`
- **SPFx Deployment**: `SMEPilot.SPFx/UPGRADE_SUCCESS.md`
- **Graph API Setup**: `docs/GraphAPI_Production_Setup.md`
- **Copilot Integration**: `STEP2_COPILOT_INTEGRATION.md`

---

## 🎉 Success Criteria

**Deployment is successful when:**
- ✅ Documents upload successfully
- ✅ Enrichment happens automatically
- ✅ Enriched documents appear in ProcessedDocs folder
- ✅ QueryAnswer API returns relevant results
- ✅ Copilot can access and answer questions about documents

---

**Ready to deploy?** Start with **Phase 1: Deploy Backend to Azure**! 🚀

