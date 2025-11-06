# ⚡ Quick Start: Configure Azure Services

## Step-by-Step Configuration Guide

Follow these steps in order to configure all Azure services needed for SMEPilot.

---

## 🔵 Step 1: Azure OpenAI (15 minutes)

### Create Resource
1. Go to [Azure Portal](https://portal.azure.com)
2. **Create** → Search "Azure OpenAI" → Create
3. Fill in:
   - Name: `sme-pilot-openai`
   - Region: East US or West Europe
   - Pricing: Standard S0
4. **Wait for deployment** (5-10 min)

### Deploy Models
1. Go to your OpenAI resource → **Model deployments**
2. **Create** deployment:
   - Model: `gpt-4` or `gpt-4o`
   - Deployment name: `gpt-4`
3. **Create** another deployment:
   - Model: `text-embedding-ada-002`
   - Deployment name: `text-embedding-ada-002`

### Get Credentials
1. Go to **Keys and Endpoint**
2. Copy **KEY 1** → `AzureOpenAI_Key`
3. Copy **Endpoint** → `AzureOpenAI_Endpoint`

---

## 🔵 Step 2: Microsoft Graph API (10 minutes)

### Create App Registration
1. Azure Portal → **Azure Active Directory** (or **Microsoft Entra ID**)
2. **App registrations** → **New registration**
3. Name: `SMEPilot` → Register

### Add Permissions
1. **API permissions** → **Add a permission**
2. **Microsoft Graph** → **Application permissions**
3. Add:
   - `Sites.ReadWrite.All`
   - `Files.ReadWrite.All`
   - `User.Read.All`
4. **Grant admin consent** ✅

### Create Secret
1. **Certificates & secrets** → **New client secret**
2. Copy **Value** immediately → `Graph_ClientSecret`

### Get IDs
1. **Overview** tab:
   - Copy **Application (client) ID** → `Graph_ClientId`
   - Copy **Directory (tenant) ID** → `Graph_TenantId`

---

## 🔵 Step 3: Azure Cosmos DB (10 minutes)

### Create Account
1. Azure Portal → **Create** → Search "Azure Cosmos DB"
2. Fill in:
   - API: **Core (SQL)**
   - Name: `sme-pilot-cosmos` (unique)
   - Region: Your choice
3. **Create** → Wait (5-10 min)

### Get Connection String
1. Go to Cosmos DB account → **Keys**
2. Copy **Primary Connection String** → `Cosmos_ConnectionString`

---

## 🔵 Step 4: Update local.settings.json

Open `SMEPilot.FunctionApp/local.settings.json` and replace values:

```json
{
  "Values": {
    "Graph_TenantId": "YOUR_TENANT_ID_FROM_STEP_2",
    "Graph_ClientId": "YOUR_CLIENT_ID_FROM_STEP_2",
    "Graph_ClientSecret": "YOUR_CLIENT_SECRET_FROM_STEP_2",
    
    "AzureOpenAI_Endpoint": "https://sme-pilot-openai.openai.azure.com/",
    "AzureOpenAI_Key": "YOUR_KEY_FROM_STEP_1",
    "AzureOpenAI_Deployment_GPT": "gpt-4",
    "AzureOpenAI_Embedding_Deployment": "text-embedding-ada-002",
    
    "Cosmos_ConnectionString": "AccountEndpoint=https://sme-pilot-cosmos.documents.azure.com:443/;AccountKey=YOUR_KEY==;"
  }
}
```

---

## 🔵 Step 5: Restart & Test

1. **Stop** Function App in Visual Studio
2. **Press F5** to restart
3. Functions now use **real Azure services**!

---

## ✅ Verification

### Test ProcessSharePointFile
Upload a real SharePoint document and process it:
- Should use real OpenAI for enrichment
- Should store embeddings in CosmosDB
- Should save to SharePoint ProcessedDocs folder

### Test QueryAnswer
After processing documents:
- Should find documents from CosmosDB
- Should return real answers based on content

---

## 📚 Detailed Guides

- **Full Azure Configuration**: `docs/ConfigureAzureServices.md`
- **Get SharePoint File IDs**: `docs/GetSharePointFileDetails.md`

---

## ⚠️ Important Notes

1. **Never commit** `local.settings.json` to Git
2. Secrets expire - set reminders to rotate
3. Check Azure costs - some services have usage charges
4. Test in development first before production

---

## 🆘 Troubleshooting

### "OpenAI authentication failed"
- Check endpoint URL ends with `/`
- Verify deployment names match
- Ensure API key is correct

### "Graph API failed"
- Verify admin consent granted
- Check permissions are correct
- Ensure tenant ID is correct

### "Cosmos DB connection failed"
- Verify connection string is complete
- Check account name in connection string
- Ensure account is deployed

---

## 🎯 Next Steps

After configuration:
1. ✅ Test with real SharePoint document
2. ✅ Set up Graph webhook for automatic triggers
3. ✅ Deploy to Azure Function App
4. ✅ Configure Microsoft Search Connector for Copilot

---

**Ready? Start with Step 1: Azure OpenAI!** 🚀

