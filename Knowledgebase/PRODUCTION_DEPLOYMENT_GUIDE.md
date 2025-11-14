# Production Deployment Guide

## Overview

This guide provides step-by-step instructions for deploying SMEPilot to production with all production-grade features enabled.

---

## Prerequisites

### 1. Azure Resources

- [ ] Azure Function App (Consumption or Premium plan)
- [ ] Application Insights resource
- [ ] Azure Communication Services (for email notifications) - Optional but recommended
- [ ] Azure AD App Registration with required permissions

### 2. Required Permissions

- [ ] Azure AD Global Admin (for app registration consent)
- [ ] SharePoint Site Collection Admin (for SPFx deployment)
- [ ] Azure Subscription Contributor (for Function App deployment)

### 3. Configuration Values

Prepare the following configuration values:

- Application Insights Connection String
- Azure Communication Services Connection String (for email)
- Admin Email Address
- Graph API credentials (TenantId, ClientId, ClientSecret)

---

## Step 1: Configure Application Insights

### 1.1 Create Application Insights Resource

1. Go to Azure Portal → Create Resource → Application Insights
2. Fill in:
   - **Name**: `sme-pilot-insights` (or your preferred name)
   - **Resource Group**: Same as Function App
   - **Region**: Same as Function App
3. Click **Create**

### 1.2 Get Connection String

1. Navigate to your Application Insights resource
2. Go to **Overview** → **Connection String**
3. Copy the connection string (format: `InstrumentationKey=xxx;IngestionEndpoint=xxx`)

### 1.3 Configure Function App

Add to Function App **Configuration** → **Application settings**:

```
APPLICATIONINSIGHTS_CONNECTION_STRING=<your-connection-string>
```

---

## Step 2: Configure Email Notifications

### 2.1 Create Azure Communication Services (Optional but Recommended)

1. Go to Azure Portal → Create Resource → Communication Services
2. Fill in:
   - **Name**: `sme-pilot-email` (or your preferred name)
   - **Resource Group**: Same as Function App
3. Click **Create**

### 2.2 Get Connection String

1. Navigate to your Communication Services resource
2. Go to **Keys** → Copy **Connection String**

### 2.3 Configure Function App

Add to Function App **Configuration** → **Application settings**:

```
AzureCommunicationServices_ConnectionString=<your-connection-string>
AdminEmail=<admin@yourcompany.com>
```

**Note:** If Azure Communication Services is not configured, email notifications will be disabled but the system will continue to work.

---

## Step 3: Configure Rate Limiting

Add to Function App **Configuration** → **Application settings**:

```
RateLimit_MaxPerMinute=60
RateLimit_MaxPerHour=1000
```

**Default values:**
- Max requests per minute: 60
- Max requests per hour: 1000

Adjust based on your expected load.

---

## Step 4: Deploy Function App

### 4.1 Build the Solution

```bash
cd SMEPilot.FunctionApp
dotnet build --configuration Release
```

### 4.2 Publish to Azure

**Option A: Using Visual Studio**
1. Right-click on `SMEPilot.FunctionApp` project
2. Select **Publish**
3. Choose **Azure** → **Azure Function App (Windows)**
4. Select your Function App
5. Click **Publish**

**Option B: Using Azure CLI**

```bash
# Login to Azure
az login

# Set variables
RESOURCE_GROUP="your-resource-group"
FUNCTION_APP_NAME="your-function-app-name"

# Publish
func azure functionapp publish $FUNCTION_APP_NAME
```

**Option C: Using VS Code**
1. Install Azure Functions extension
2. Right-click on Function App folder
3. Select **Deploy to Function App**

### 4.3 Verify Deployment

1. Go to Azure Portal → Your Function App
2. Navigate to **Functions** → Verify all functions are listed:
   - `ProcessSharePointFile`
   - `SetupSubscription`
   - `WebhookRenewal`

---

## Step 5: Configure Environment Variables

Add all required environment variables to Function App **Configuration** → **Application settings**:

### Required Settings

```
Graph_TenantId=<your-tenant-id>
Graph_ClientId=<your-client-id>
Graph_ClientSecret=<your-client-secret>
APPLICATIONINSIGHTS_CONNECTION_STRING=<your-connection-string>
```

### Optional Settings

```
AdminEmail=<admin@yourcompany.com>
AzureCommunicationServices_ConnectionString=<your-connection-string>
RateLimit_MaxPerMinute=60
RateLimit_MaxPerHour=1000
MaxFileSizeBytes=52428800
MaxRetryAttempts=3
RetryWaitMinutes=5
NotificationDedupWindowSeconds=30
```

### Spire License (if using PDF features)

```
SpirePDFLicense=<your-license-key>
SpireDOCLicense=<your-license-key>
```

---

## Step 6: Deploy SPFx Solution

### 6.1 Build SPFx Solution

```bash
cd SMEPilot.SPFx
npm install
gulp build --ship
gulp bundle --ship
gulp package-solution --ship
```

### 6.2 Deploy to App Catalog

1. Go to SharePoint Admin Center → **More features** → **Apps**
2. Upload the `.sppkg` file from `SMEPilot.SPFx/sharepoint/solution/`
3. Check **"Make this solution available to all sites"** (if desired)
4. Click **Deploy**

### 6.3 Add to Site

1. Go to your SharePoint site
2. Click **Settings** → **Add an app**
3. Find **SMEPilot Admin Panel**
4. Click **Add**

---

## Step 7: Configure SharePoint

### 7.1 Run Installation via SPFx Admin Panel

1. Navigate to your SharePoint site
2. Go to **SMEPilot Admin Panel** web part
3. Fill in configuration:
   - **Source Folder**: Where documents are uploaded
   - **Destination Folder**: `SMEPilot Enriched Docs` (required for Copilot)
   - **Template Library**: Where template files are stored
   - **Template File**: Template file name
4. Click **Save Configuration**

This will:
- Create `SMEPilotConfig` list
- Create metadata columns
- Create error folders (`RejectedDocs`, `FailedDocs`)
- Create webhook subscription

### 7.2 Verify Configuration

1. Check that `SMEPilotConfig` list exists
2. Verify metadata columns are created on source library
3. Check webhook subscription status in Admin Panel

---

## Step 8: Configure Copilot Agent (REQUIRED)

### 8.1 Access Copilot Studio

1. Go to [Microsoft Copilot Studio](https://copilotstudio.microsoft.com/)
2. Sign in with M365 Global Admin or Copilot Admin account

### 8.2 Create Copilot Agent

1. Click **Create** → **Copilot**
2. Name: `SMEPilot`
3. Description: `Knowledge assistant for SMEPilot enriched documents`

### 8.3 Configure Data Source

1. Go to **Knowledge** → **Add data source**
2. Select **SharePoint**
3. Choose **"SMEPilot Enriched Docs"** library
4. Click **Add**

### 8.4 Set System Prompt

1. Go to **Settings** → **System**
2. Add the following prompt:

```
You are SMEPilot — the organization's knowledge assistant. Use only documents from the 'SMEPilot Enriched Docs' SharePoint library as the primary evidence. Provide a short summary (2-3 lines), then give a numbered step list for procedures or troubleshooting. Always cite the document title and link. If uncertain, say 'I couldn't find a definitive answer in SMEPilot docs.' Do not invent facts beyond the indexed docs.
```

### 8.5 Publish

1. Click **Publish**
2. Choose **Teams** and/or **Web**
3. Click **Publish**

---

## Step 9: Verify Production Deployment

### 9.1 Test Document Processing

1. Upload a test document to the source folder
2. Check Application Insights for processing logs
3. Verify enriched document appears in destination folder
4. Check metadata is updated correctly

### 9.2 Test Error Notifications

1. Upload an unsupported file type (e.g., `.txt`)
2. Verify error email is sent (if configured)
3. Check Application Insights for error tracking

### 9.3 Test Copilot Agent

1. Open Teams or Copilot web interface
2. Ask: "What documents are available in SMEPilot?"
3. Verify Copilot responds with document information

### 9.4 Monitor Application Insights

1. Go to Application Insights → **Live Metrics**
2. Verify telemetry is flowing:
   - Document processing events
   - Dependency tracking (Graph API calls)
   - Error tracking
   - Performance metrics

---

## Step 10: Set Up Alerts

### 10.1 Create Alert Rules

Go to Application Insights → **Alerts** → **New alert rule**

#### Alert 1: High Error Rate

- **Metric**: Failed requests
- **Condition**: Count > 10 in 5 minutes
- **Action**: Email notification

#### Alert 2: Processing Failures

- **Metric**: Custom event "ProcessingFailed"
- **Condition**: Count > 5 in 10 minutes
- **Action**: Email notification

#### Alert 3: Webhook Expiration

- **Metric**: Custom event "WebhookSubscription" with action="Expiring"
- **Condition**: Count > 0
- **Action**: Email notification

---

## Step 11: Performance Tuning

### 11.1 Function App Settings

Configure in Function App **Configuration** → **General settings**:

- **Always On**: Enable (for Premium plan)
- **HTTP Version**: 2.0
- **Minimum TLS Version**: 1.2

### 11.2 Application Insights Sampling

Configure in `host.json`:

```json
{
  "logging": {
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": true,
        "maxTelemetryItemsPerSecond": 20
      }
    }
  }
}
```

---

## Step 12: Security Hardening

### 12.1 Enable Authentication (Optional)

For additional security, enable Function App authentication:

1. Go to Function App → **Authentication**
2. Click **Add identity provider**
3. Choose **Microsoft** (for Azure AD)
4. Configure allowed tenants

### 12.2 CORS Configuration

Update CORS in Function App **CORS** settings:

- Add your SharePoint domain(s)
- Remove `*` in production

### 12.3 Network Security

- Enable **Private Endpoints** (if using VNet)
- Configure **IP Restrictions** (if needed)

---

## Troubleshooting

### Issue: Application Insights not working

**Solution:**
1. Verify `APPLICATIONINSIGHTS_CONNECTION_STRING` is set
2. Check Function App logs for connection errors
3. Verify Application Insights resource exists

### Issue: Email notifications not sending

**Solution:**
1. Verify `AzureCommunicationServices_ConnectionString` is set
2. Verify `AdminEmail` is set
3. Check Communication Services resource is active
4. Check Function App logs for email errors

### Issue: Rate limiting too aggressive

**Solution:**
1. Increase `RateLimit_MaxPerMinute` and `RateLimit_MaxPerHour`
2. Monitor Application Insights for rate limit events
3. Adjust based on actual usage patterns

### Issue: Copilot not finding documents

**Solution:**
1. Verify documents are in "SMEPilot Enriched Docs" library
2. Wait 5-10 minutes for SharePoint indexing
3. Check Copilot data source configuration
4. Verify library permissions allow Copilot access

---

## Post-Deployment Checklist

- [ ] Application Insights configured and receiving data
- [ ] Email notifications working (if configured)
- [ ] Rate limiting configured appropriately
- [ ] SPFx solution deployed and accessible
- [ ] SharePoint configuration complete
- [ ] Webhook subscription active
- [ ] Copilot Agent configured and published
- [ ] Test document processed successfully
- [ ] Error notifications working
- [ ] Alerts configured
- [ ] Monitoring dashboards set up
- [ ] Documentation updated with production URLs

---

## Support

For issues or questions:
1. Check Application Insights logs
2. Review Function App logs
3. Check SharePoint ULS logs (if SPFx issues)
4. Review this guide's troubleshooting section

---

**Last Updated:** 2025-01-XX  
**Status:** Production Ready ✅

