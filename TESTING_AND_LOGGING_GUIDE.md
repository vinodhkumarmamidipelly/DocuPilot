# Testing and Logging Guide

## 📋 Overview

This guide explains:
1. **How to test** the application
2. **Where to find logs** (UI and API)
3. **How to debug issues** using logs

---

## 🔍 Logging Architecture

### Function App (API) Logging

#### 1. **File Logs (Serilog)**
- **Location:** `{FunctionAppRoot}/Logs/sme-pilot-{date}.log`
- **Format:** JSON (CompactJsonFormatter)
- **Level:** Information and above
- **Retention:** 30 days
- **Rotation:** Daily or 10MB per file
- **Console:** ❌ **DISABLED** (all logs go to files only)

**Access Methods:**
- **Local:** `D:\CodeBase\DocuPilot\SMEPilot.FunctionApp\Logs\`
- **Azure:** 
  - Function App → **Log stream** (real-time)
  - Function App → **Advanced Tools (Kudu)** → `LogFiles/Application/Functions/Function/`
  - Or download via Azure Portal → **Logs** → **Download**

**Log Levels:**
- `LogInformation` - General info, processing steps
- `LogWarning` - Warnings, skipped items
- `LogError` - Errors, failures
- `LogDebug` - Detailed debugging (filtered in production)

**Example Log Entry:**
```json
{
  "@t": "2025-01-XXT10:30:45.123Z",
  "@l": "Information",
  "@mt": "✅ [ENRICHMENT] Document enriched successfully: {FileName}",
  "FileName": "test-document.docx",
  "SourceContext": "SMEPilot.FunctionApp.Functions.ProcessSharePointFile"
}
```

#### 2. **Application Insights (Telemetry)**
- **Location:** Azure Portal → Application Insights
- **Format:** Structured telemetry
- **Includes:**
  - Custom events (DocumentProcessed, ProcessingFailed)
  - Dependencies (Graph API calls)
  - Exceptions
  - Performance metrics
  - Custom metrics

**Access:**
1. Go to Azure Portal → Application Insights resource
2. **Logs** → Query telemetry
3. **Live Metrics** → Real-time monitoring
4. **Failures** → Error tracking
5. **Performance** → Response times

**Example Queries:**
```kusto
// All document processing events
customEvents
| where name == "DocumentProcessed"
| project timestamp, customDimensions.FileName, customDimensions.Status

// Processing failures
exceptions
| where customDimensions.FileName != ""
| project timestamp, outerMessage, customDimensions.FileName

// Graph API dependency calls
dependencies
| where type == "HTTP"
| where name contains "graph.microsoft.com"
| project timestamp, name, duration, success
```

#### 3. **Azure Functions Logs**
- **Location:** Azure Portal → Function App → **Log stream**
- **Format:** Text (console-like)
- **Real-time:** ✅ Yes
- **Filter:** By function name, log level

**Access:**
1. Azure Portal → Function App
2. **Log stream** (left menu)
3. Select function: `ProcessSharePointFile`, `SetupSubscription`, `WebhookRenewal`

---

### SPFx (UI) Logging

#### 1. **Browser Console**
- **Location:** Browser Developer Tools (F12) → **Console** tab
- **Format:** Text
- **Levels:**
  - `console.log()` - Info
  - `console.warn()` - Warnings
  - `console.error()` - Errors

**Access:**
1. Open SharePoint page with SPFx web part
2. Press **F12** (or Right-click → Inspect)
3. Go to **Console** tab
4. Filter by log level or search

**Example Logs:**
```javascript
// Info
"SMEPilotConfig list created successfully"

// Warning
"Column SMEPilot_Enriched might already exist: Column already exists"

// Error
"Error creating webhook subscription: CORS error: Cannot connect to Function App"
```

#### 2. **Network Tab (API Calls)**
- **Location:** Browser Developer Tools → **Network** tab
- **Shows:**
  - All HTTP requests (SPFx → Function App)
  - Request/Response headers
  - Response body
  - Status codes
  - Timing

**How to Use:**
1. Open **Network** tab
2. Filter by **Fetch/XHR**
3. Look for requests to Function App URL
4. Check:
   - **Status:** 200 (success), 400/500 (error)
   - **Headers:** CORS headers present?
   - **Response:** Error message?

**Example:**
```
Request URL: https://your-function-app.azurewebsites.net/api/SetupSubscription
Method: POST
Status: 200 OK
Response Headers:
  Access-Control-Allow-Origin: *
  Content-Type: application/json
Response Body:
  {
    "subscriptionId": "abc123",
    "expirationDateTime": "2025-01-XXT10:30:00Z",
    "success": true
  }
```

#### 3. **SPFx Workbench Console**
- **Location:** SPFx Workbench (local development)
- **Format:** Browser console
- **Use:** For local development testing

---

## 🧪 Testing Guide

### Phase 1: Pre-Deployment Testing

#### Test 1.1: Function App Startup
**Goal:** Verify Function App starts without errors

**Steps:**
1. Deploy Function App to Azure
2. Check **Log stream** in Azure Portal
3. Look for: `🚀 SMEPilot Function App starting...`
4. Verify no exceptions in logs

**Expected Logs:**
```
🚀 SMEPilot Function App starting...
📁 Log files location: D:\home\LogFiles\...
🔧 [CONFIG] Template formatting mode - Rule-based sectioning only
📋 [CONFIG] Configuration validated successfully
```

**If Errors:**
- Check `Logs/sme-pilot-{date}.log` file
- Check Application Insights → **Failures**
- Verify environment variables are set

---

#### Test 1.2: SPFx Deployment
**Goal:** Verify SPFx web part loads

**Steps:**
1. Deploy SPFx solution to App Catalog
2. Add web part to SharePoint page
3. Open browser console (F12)
4. Check for errors

**Expected:**
- No console errors
- Web part renders
- Configuration form visible

**If Errors:**
- Check browser console for error messages
- Check Network tab for failed requests
- Verify Function App URL is correct

---

### Phase 2: Configuration Testing

#### Test 2.1: Save Configuration
**Goal:** Verify configuration saves successfully

**Steps:**
1. Fill configuration form
2. Click "Save Configuration"
3. Open browser console (F12)
4. Check logs

**Expected Console Logs:**
```
SMEPilotConfig list created successfully
Metadata columns created successfully
Configuration saved successfully
```

**Expected Function App Logs:**
```
✅ Validation token returned to Graph API
✅ Webhook subscription created: {subscriptionId}
```

**If Errors:**
- **CORS Error:** Check Function App CORS settings
- **Network Error:** Check Function App URL
- **Permission Error:** Check SharePoint permissions

---

#### Test 2.2: Webhook Subscription
**Goal:** Verify webhook subscription is created

**Steps:**
1. Save configuration
2. Check Application Insights → **Logs**
3. Query: `customEvents | where name == "WebhookSubscription"`

**Expected:**
- Subscription created successfully
- Subscription ID stored in SMEPilotConfig
- No errors in logs

**If Errors:**
- Check Graph API permissions
- Check Function App URL is accessible
- Check validation token handshake logs

---

### Phase 3: Document Processing Testing

#### Test 3.1: Basic Document Upload
**Goal:** Verify document processing works

**Steps:**
1. Upload a .docx file to Source Folder
2. Wait 30-60 seconds
3. Check logs

**Expected Function App Logs:**
```
📥 [PROCESS] Processing file: test-document.docx
📋 [ENRICHMENT] Using DocumentEnricherService for .docx file...
✅ [ENRICHMENT] Document enriched successfully
📤 [UPLOAD] Uploading enriched document...
✅ [METADATA] Metadata updated successfully
```

**Expected Application Insights:**
- Event: `DocumentProcessed`
- Status: `Succeeded`
- ProcessingTime: < 60 seconds

**If Errors:**
- Check file logs for error details
- Check Application Insights → **Failures**
- Verify template file exists
- Verify mapping.json exists

---

#### Test 3.2: File Size Limit
**Goal:** Verify 50MB limit is enforced

**Steps:**
1. Upload file > 50MB
2. Check logs

**Expected Logs:**
```
⚠️ [VALIDATION] File size {SizeMB}MB exceeds limit of 50MB
❌ [PROCESS] File rejected: File size exceeds maximum allowed size
```

**Expected Metadata:**
- `SMEPilot_Status` = "Failed"
- `SMEPilot_ErrorMessage` = "File size exceeds maximum allowed size"

---

#### Test 3.3: Duplicate Detection
**Goal:** Verify duplicate files are skipped

**Steps:**
1. Upload same file twice (unchanged)
2. Check logs

**Expected Logs (First Upload):**
```
📥 [PROCESS] Processing file: test-document.docx
✅ [PROCESS] File processed successfully
```

**Expected Logs (Second Upload):**
```
📥 [PROCESS] Processing file: test-document.docx
⏭️ [IDEMPOTENCY] File already processed, skipping
```

---

#### Test 3.4: Version Detection
**Goal:** Verify updated files are reprocessed

**Steps:**
1. Upload file
2. Wait for processing
3. Modify file
4. Upload again
5. Check logs

**Expected Logs (Second Upload):**
```
📥 [PROCESS] Processing file: test-document.docx
🔄 [VERSION] File modified after last enrichment, reprocessing...
✅ [PROCESS] File processed successfully
```

---

### Phase 4: Error Scenario Testing

#### Test 4.1: CORS Error
**Goal:** Verify CORS errors are logged

**Steps:**
1. Try to call Function App from SPFx
2. Check browser console

**Expected Console Error:**
```
CORS error: Cannot connect to Function App. Please ensure:
1. Function App is running locally
2. Ngrok tunnel is active (if using ngrok)
3. Visit {url} in a new tab and bypass ngrok warning (if shown)
4. CORS headers are configured in Function App
```

**Fix:**
- Configure CORS in Azure Functions portal
- Add SharePoint domain to allowed origins
- Remove `*` wildcard in production

---

#### Test 4.2: Template File Missing
**Goal:** Verify error when template file is missing

**Steps:**
1. Remove template file from deployment
2. Upload document
3. Check logs

**Expected Logs:**
```
❌ [ENRICHMENT] Template file not found: {TemplatePath}
❌ [PROCESS] Document enrichment failed: Template file not found
```

**Fix:**
- Ensure `Templates/**/*` is included in deployment
- Verify file path in logs
- Check deployment package

---

#### Test 4.3: Graph API Error
**Goal:** Verify Graph API errors are logged

**Steps:**
1. Simulate Graph API failure (wrong permissions)
2. Check logs

**Expected Logs:**
```
❌ [GRAPH] Failed to download file: {Error}
❌ [PROCESS] Failed to process file: {Error}
```

**Expected Application Insights:**
- Dependency failure: Graph API call
- Exception: Graph API error details

---

## 🔧 Debugging Common Issues

### Issue 1: Function App Not Starting

**Symptoms:**
- Function App shows "Error" status
- No logs in Log stream

**Debug Steps:**
1. Check **Log stream** for startup errors
2. Check `Logs/sme-pilot-{date}.log` file
3. Verify environment variables:
   - `Graph_TenantId`
   - `Graph_ClientId`
   - `Graph_ClientSecret`
4. Check Application Insights → **Failures**

**Common Causes:**
- Missing environment variables
- Invalid Graph API credentials
- DI container exception

---

### Issue 2: SPFx Can't Connect to Function App

**Symptoms:**
- Browser console shows CORS error
- Network tab shows failed request

**Debug Steps:**
1. Check browser console for error message
2. Check Network tab → Request/Response headers
3. Verify Function App URL is correct
4. Check Function App CORS settings

**Common Causes:**
- CORS not configured in Azure Functions portal
- Function App URL incorrect
- Network/firewall blocking

---

### Issue 3: Document Not Processing

**Symptoms:**
- File uploaded but not processed
- No enriched document created

**Debug Steps:**
1. Check Function App logs for webhook notification
2. Check Application Insights → **Dependencies** → Graph API calls
3. Verify webhook subscription is active
4. Check metadata: `SMEPilot_Status`

**Common Causes:**
- Webhook subscription expired
- Graph API permissions insufficient
- Template file missing
- Processing failed silently

---

### Issue 4: Template File Not Found

**Symptoms:**
- Logs show "Template file not found"
- Processing fails

**Debug Steps:**
1. Check logs for exact file path
2. Verify `Templates/**/*` in deployment package
3. Check `AppDomain.CurrentDomain.BaseDirectory` path
4. Verify file exists in Azure

**Common Causes:**
- Files not included in deployment
- Path resolution incorrect
- Files in wrong location

---

## 📊 Log Analysis Tips

### Finding Errors Quickly

**Function App:**
```bash
# Search for errors in log file
grep -i "error\|exception\|failed" Logs/sme-pilot-*.log

# Search for specific file
grep "test-document.docx" Logs/sme-pilot-*.log
```

**Application Insights:**
```kusto
// All errors in last hour
exceptions
| where timestamp > ago(1h)
| project timestamp, outerMessage, customDimensions

// Failed document processing
customEvents
| where name == "ProcessingFailed"
| where timestamp > ago(1h)
| project timestamp, customDimensions.FileName, customDimensions.ErrorMessage
```

**Browser Console:**
- Filter by "error" or "Error"
- Search for specific function names
- Check Network tab for failed requests

---

### Performance Monitoring

**Application Insights:**
```kusto
// Average processing time
customEvents
| where name == "DocumentProcessed"
| summarize avg(customMeasurements.ProcessingTimeSeconds) by bin(timestamp, 1h)

// Processing time by file size
customEvents
| where name == "DocumentProcessed"
| project customMeasurements.FileSizeMB, customMeasurements.ProcessingTimeSeconds
```

---

## ✅ Testing Checklist

### Pre-Deployment
- [ ] Function App starts without errors
- [ ] All environment variables set
- [ ] Application Insights connected
- [ ] Template files deployed
- [ ] SPFx builds successfully

### Post-Deployment
- [ ] SPFx web part loads
- [ ] Configuration saves successfully
- [ ] Webhook subscription created
- [ ] Document processing works
- [ ] Metadata updates correctly
- [ ] Error handling works
- [ ] Logs are accessible

### Production Readiness
- [ ] All logs accessible
- [ ] Application Insights monitoring active
- [ ] Error notifications working
- [ ] Performance metrics tracked
- [ ] Log retention configured

---

## 📝 Summary

### Logging Available

| Component | Log Type | Location | Access Method |
|-----------|----------|----------|---------------|
| **Function App** | File Logs (Serilog) | `Logs/sme-pilot-*.log` | Azure Portal → Log stream, Kudu, Download |
| **Function App** | Application Insights | Azure Portal | Application Insights → Logs |
| **Function App** | Azure Functions Logs | Azure Portal | Function App → Log stream |
| **SPFx** | Browser Console | Browser DevTools | F12 → Console tab |
| **SPFx** | Network Logs | Browser DevTools | F12 → Network tab |

### Testing Priority

1. ✅ **Function App Startup** - Check logs immediately
2. ✅ **SPFx Deployment** - Check browser console
3. ✅ **Configuration Save** - Check both logs
4. ✅ **Document Processing** - Check Function App logs + Application Insights
5. ✅ **Error Scenarios** - Check all log sources

---

**Last Updated:** 2025-01-XX  
**Status:** Complete testing and logging guide

