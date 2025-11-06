# Configure Azure Services for SMEPilot

## Prerequisites

Before configuring, ensure you have:
- ✅ Azure subscription
- ✅ Access to Azure Portal (https://portal.azure.com)
- ✅ Permission to create resources

---

## Step 1: Configure Azure OpenAI

### 1.1 Create Azure OpenAI Resource

1. Go to [Azure Portal](https://portal.azure.com)
2. Click **"Create a resource"**
3. Search for **"Azure OpenAI"**
4. Click **"Create"**

**Fill in:**
- **Subscription**: Your subscription
- **Resource Group**: Create new or use existing
- **Region**: Choose region with OpenAI access (e.g., East US, West Europe)
- **Name**: `sme-pilot-openai` (or your preferred name)
- **Pricing tier**: Standard S0 (or higher)
- Click **"Create"**

**Wait for deployment to complete** (5-10 minutes)

### 1.2 Deploy Models

1. Go to your Azure OpenAI resource
2. Click **"Model deployments"** (left menu)
3. Click **"+ Create"**

**Deploy GPT Model:**
- **Model name**: `gpt-4` or `gpt-4o` or `gpt-35-turbo` (choose one)
- **Deployment name**: `gpt-4` (use this name in settings)
- Click **"Create"**

**Deploy Embedding Model:**
- Click **"+ Create"** again
- **Model name**: `text-embedding-ada-002` or `text-embedding-3-small`
- **Deployment name**: `text-embedding-ada-002` (use this name in settings)
- Click **"Create"**

### 1.3 Get API Key and Endpoint

1. In Azure OpenAI resource, go to **"Keys and Endpoint"** (left menu)
2. Copy:
   - **KEY 1** (or KEY 2) → This is your `AzureOpenAI_Key`
   - **Endpoint** → This is your `AzureOpenAI_Endpoint`

**Example:**
```
Endpoint: https://your-resource-name.openai.azure.com/
Key: abc123def456...
```

---

## Step 2: Configure Microsoft Graph API

### 2.1 Create Azure AD App Registration

1. Go to [Azure Portal](https://portal.azure.com)
2. Search for **"Azure Active Directory"** (or **"Microsoft Entra ID"**)
3. Click **"App registrations"** (left menu)
4. Click **"+ New registration"**

**Fill in:**
- **Name**: `SMEPilot` (or your preferred name)
- **Supported account types**: Accounts in this organizational directory only
- **Redirect URI**: Leave blank for now
- Click **"Register"**

### 2.2 Configure API Permissions

1. In your App Registration, go to **"API permissions"**
2. Click **"+ Add a permission"**
3. Select **"Microsoft Graph"**
4. Select **"Application permissions"**
5. Add these permissions:
   - `Sites.ReadWrite.All` (Read and write all site collections)
   - `Files.ReadWrite.All` (Have full access to all files)
   - `User.Read.All` (Read all users' profiles)
6. Click **"Add permissions"**

**Important:** After adding permissions:
1. Click **"Grant admin consent for [Your Organization]"**
2. Confirm the consent

### 2.3 Create Client Secret

1. In App Registration, go to **"Certificates & secrets"**
2. Click **"+ New client secret"**
3. **Description**: `SMEPilot Secret`
4. **Expires**: Choose expiration (e.g., 24 months)
5. Click **"Add"**
6. **IMPORTANT:** Copy the **Value** immediately (you won't see it again!)
   - This is your `Graph_ClientSecret`

### 2.4 Get Client ID and Tenant ID

1. In App Registration, go to **"Overview"**
2. Copy:
   - **Application (client) ID** → This is your `Graph_ClientId`
   - **Directory (tenant) ID** → This is your `Graph_TenantId`

---

## Step 3: Configure Azure Cosmos DB

### 3.1 Create Cosmos DB Account

1. Go to [Azure Portal](https://portal.azure.com)
2. Click **"Create a resource"**
3. Search for **"Azure Cosmos DB"**
4. Click **"Create"**

**Fill in:**
- **Subscription**: Your subscription
- **Resource Group**: Create new or use existing
- **Account Name**: `sme-pilot-cosmos` (must be globally unique)
- **API**: **Core (SQL)**
- **Location**: Choose region closest to you
- **Capacity mode**: **Provisioned throughput** (Free tier available)
- Click **"Review + Create"**, then **"Create"**

**Wait for deployment** (5-10 minutes)

### 3.2 Get Connection String

1. Go to your Cosmos DB account
2. Click **"Keys"** (left menu)
3. Copy **"Primary Connection String"**
   - This is your `Cosmos_ConnectionString`

**Example:**
```
AccountEndpoint=https://sme-pilot-cosmos.documents.azure.com:443/;AccountKey=abc123...==;
```

**Note:** Database `SMEPilotDB` and container `Embeddings` will be created automatically when first document is inserted.

---

## Step 4: Update local.settings.json

### 4.1 Edit the File

1. Open `SMEPilot.FunctionApp/local.settings.json` in Visual Studio
2. Replace empty strings with your actual values:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "FUNCTIONS_INPROC_NET8_ENABLED": "1",
    
    "Graph_TenantId": "YOUR_TENANT_ID_HERE",
    "Graph_ClientId": "YOUR_CLIENT_ID_HERE",
    "Graph_ClientSecret": "YOUR_CLIENT_SECRET_HERE",
    
    "AzureOpenAI_Endpoint": "https://your-resource-name.openai.azure.com/",
    "AzureOpenAI_Key": "YOUR_OPENAI_KEY_HERE",
    "AzureOpenAI_Deployment_GPT": "gpt-4",
    "AzureOpenAI_Embedding_Deployment": "text-embedding-ada-002",
    
    "Cosmos_ConnectionString": "AccountEndpoint=https://your-cosmos.documents.azure.com:443/;AccountKey=YOUR_KEY==;",
    "Cosmos_Database": "SMEPilotDB",
    "Cosmos_Container": "Embeddings",
    
    "EnrichedFolderRelativePath": "/Shared Documents/ProcessedDocs"
  }
}
```

### 4.2 Replace Values

**Example (with real-looking values):**
```json
{
  "Graph_TenantId": "12345678-1234-1234-1234-123456789012",
  "Graph_ClientId": "87654321-4321-4321-4321-210987654321",
  "Graph_ClientSecret": "abc~DEF123ghi456JKL789",
  
  "AzureOpenAI_Endpoint": "https://sme-pilot-openai.openai.azure.com/",
  "AzureOpenAI_Key": "a1b2c3d4e5f6g7h8i9j0",
  "AzureOpenAI_Deployment_GPT": "gpt-4",
  "AzureOpenAI_Embedding_Deployment": "text-embedding-ada-002",
  
  "Cosmos_ConnectionString": "AccountEndpoint=https://sme-pilot-cosmos.documents.azure.com:443/;AccountKey=a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0==;"
}
```

---

## Step 5: Restart Function App

1. **Stop** the function app in Visual Studio (if running)
2. **Press F5** to restart
3. Functions will now use **real Azure services** instead of mock mode

---

## Step 6: Test with Real Services

### 6.1 Test ProcessSharePointFile

You'll need a **real SharePoint document**:

1. Upload a `.docx` file to SharePoint
2. Get the file details:
   - **Site ID**: From SharePoint URL or Graph API
   - **Drive ID**: From SharePoint URL or Graph API
   - **Item ID**: From SharePoint file properties

3. Call the endpoint with real values:
```powershell
$body = @{
    siteId = "REAL_SITE_ID"
    driveId = "REAL_DRIVE_ID"
    itemId = "REAL_ITEM_ID"
    fileName = "your-document.docx"
    uploaderEmail = "your-email@domain.com"
    tenantId = "YOUR_TENANT_ID"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:7071/api/ProcessSharePointFile" -Method Post -Body $body -ContentType "application/json"
```

### 6.2 Test QueryAnswer

After processing documents, embeddings will be stored in CosmosDB. Then query:

```powershell
$queryBody = @{
    tenantId = "YOUR_TENANT_ID"
    question = "What is this document about?"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:7071/api/QueryAnswer" -Method Post -Body $queryBody -ContentType "application/json"
```

---

## Troubleshooting

### "OpenAI returned invalid JSON"
- Check deployment names match in `local.settings.json`
- Verify models are deployed in Azure OpenAI
- Check API key is correct

### "Graph API authentication failed"
- Verify Tenant ID, Client ID, and Client Secret are correct
- Check API permissions are granted and consented
- Ensure app registration is in same tenant as SharePoint

### "Cosmos DB connection failed"
- Verify connection string is complete (includes AccountKey)
- Check account name is correct
- Ensure Cosmos DB account is deployed and running

### "No embeddings found"
- Process documents first (embeddings are created during processing)
- Check CosmosDB has data
- Verify tenant ID matches

---

## Security Notes

⚠️ **Important:**
- `local.settings.json` is **NOT encrypted** by default
- **Never commit** this file to Git (it should be in `.gitignore`)
- For production, use Azure Key Vault or Function App Configuration
- Rotate secrets regularly

---

## Next Steps

After configuring:
1. ✅ Test with real SharePoint document
2. ✅ Verify enriched document is created
3. ✅ Test QueryAnswer with processed documents
4. ✅ Set up Graph webhook for automatic triggers
5. ✅ Deploy to Azure Function App (for production)

---

## Quick Reference

| Service | Where to Get |
|---------|--------------|
| **Azure OpenAI Key** | Azure OpenAI → Keys and Endpoint |
| **Azure OpenAI Endpoint** | Azure OpenAI → Keys and Endpoint |
| **Graph Tenant ID** | Azure AD → App Registrations → Overview |
| **Graph Client ID** | Azure AD → App Registrations → Overview |
| **Graph Client Secret** | Azure AD → App Registrations → Certificates & secrets |
| **Cosmos Connection String** | Cosmos DB → Keys → Primary Connection String |

---

**Ready? Start with Step 1: Azure OpenAI!** 🚀

