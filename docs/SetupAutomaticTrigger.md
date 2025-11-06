# Setup Automatic SharePoint Upload Trigger

## Overview

Currently, `ProcessSharePointFile` requires **manual HTTP calls**. To make it **automatic** when documents are uploaded to SharePoint, we need to set up a **Microsoft Graph webhook subscription**.

---

## How It Works

```
1. User uploads document → SharePoint
2. Microsoft Graph detects upload → Sends notification to webhook URL
3. Azure Function receives notification → Processes document automatically
4. Enriched document created → Saved to ProcessedDocs folder
```

---

## Prerequisites

✅ **Azure Functions deployed** (or running locally with ngrok)  
✅ **Graph API credentials configured** (from Step 2 of Azure setup)  
✅ **Public URL for webhook** (required - Graph needs to call your function)

---

## Step 1: Get Public URL for Your Function

### Option A: Azure Functions (Production)

If deployed to Azure:
- Your function URL: `https://your-function-app.azurewebsites.net/api/ProcessSharePointFile`
- ✅ Already public - no extra steps needed

### Option B: Local Development (ngrok)

If testing locally, use **ngrok** to expose localhost:

1. **Install ngrok**: https://ngrok.com/download
2. **Run ngrok**:
   ```bash
   ngrok http 7071
   ```
3. **Copy the HTTPS URL**: `https://abc123.ngrok.io`
4. **Your webhook URL**: `https://abc123.ngrok.io/api/ProcessSharePointFile`

⚠️ **Note**: ngrok free tier changes URL on restart. Use paid tier for stable URLs, or redeploy to Azure.

---

## Step 2: Create Graph Subscription Function

We need a function to create the subscription. Create this file:

**File**: `SMEPilot.FunctionApp/Functions/SetupSubscription.cs`

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using SMEPilot.FunctionApp.Helpers;
using Microsoft.Graph.Models;
using System.Net;

namespace SMEPilot.FunctionApp.Functions
{
    public class SetupSubscription
    {
        private readonly GraphHelper _graph;
        private readonly Config _cfg;

        public SetupSubscription(GraphHelper graph, Config cfg)
        {
            _graph = graph;
            _cfg = cfg;
        }

        [Function("SetupSubscription")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            try
            {
                // Get parameters from query string or body
                var query = req.Url.Query;
                var queryParams = System.Web.HttpUtility.ParseQueryString(query);
                string? siteId = queryParams.Get("siteId");
                string? driveId = queryParams.Get("driveId");
                string? notificationUrl = queryParams.Get("notificationUrl");

                if (string.IsNullOrWhiteSpace(siteId) || string.IsNullOrWhiteSpace(driveId) || string.IsNullOrWhiteSpace(notificationUrl))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Required parameters: siteId, driveId, notificationUrl");
                    return bad;
                }

                // Resource format: /sites/{siteId}/drives/{driveId}/root/children
                // This subscribes to all file changes in the drive
                var resource = $"/sites/{siteId}/drives/{driveId}/root/children";

                // Subscription expires in 3 days (Graph maximum)
                var expiration = DateTimeOffset.UtcNow.AddDays(3);

                var subscription = await _graph.CreateSubscriptionAsync(resource, notificationUrl, expiration);

                var ok = req.CreateResponse(HttpStatusCode.OK);
                await ok.WriteStringAsync(System.Text.Json.JsonSerializer.Serialize(new
                {
                    subscriptionId = subscription.Id,
                    resource = subscription.Resource,
                    expiration = subscription.ExpirationDateTime,
                    notificationUrl = subscription.NotificationUrl,
                    message = "Subscription created successfully. It will expire in 3 days and needs renewal."
                }));
                return ok;
            }
            catch (Exception ex)
            {
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteStringAsync($"Error creating subscription: {ex.Message}");
                return err;
            }
        }
    }
}
```

**Note**: Replace `System.Web.HttpUtility` with proper query parsing for .NET 8:

```csharp
var query = req.Url.Query;
var queryParams = query.TrimStart('?').Split('&')
    .Select(p => p.Split('='))
    .ToDictionary(p => p[0], p => p.Length > 1 ? Uri.UnescapeDataString(p[1]) : "");
```

---

## Step 3: Register SetupSubscription Function

Add to `Program.cs` (if using DI):

```csharp
services.AddSingleton<SetupSubscription>();
```

Or just ensure it's in the Functions namespace - Azure Functions will discover it automatically.

---

## Step 4: Create Subscription

### Method 1: HTTP Call (Recommended)

Once function is deployed/running:

```powershell
# Replace with your values
$siteId = "YOUR_SITE_ID"
$driveId = "YOUR_DRIVE_ID"  # Document library drive ID
$notificationUrl = "https://your-function-app.azurewebsites.net/api/ProcessSharePointFile"
# OR for local: "https://abc123.ngrok.io/api/ProcessSharePointFile"

$url = "http://localhost:7071/api/SetupSubscription?siteId=$siteId&driveId=$driveId&notificationUrl=$notificationUrl"

Invoke-RestMethod -Uri $url -Method Get
```

### Method 2: PowerShell Script

Create `SetupSubscription.ps1`:

```powershell
param(
    [Parameter(Mandatory=$true)]
    [string]$SiteId,
    
    [Parameter(Mandatory=$true)]
    [string]$DriveId,
    
    [Parameter(Mandatory=$true)]
    [string]$NotificationUrl,
    
    [string]$FunctionAppUrl = "http://localhost:7071"
)

$url = "$FunctionAppUrl/api/SetupSubscription?siteId=$SiteId&driveId=$DriveId&notificationUrl=$([System.Web.HttpUtility]::UrlEncode($NotificationUrl))"

Write-Host "Creating subscription..." -ForegroundColor Green
Write-Host "Site ID: $SiteId"
Write-Host "Drive ID: $DriveId"
Write-Host "Notification URL: $NotificationUrl"
Write-Host ""

$response = Invoke-RestMethod -Uri $url -Method Get

Write-Host "✅ Subscription created!" -ForegroundColor Green
Write-Host ($response | ConvertTo-Json -Depth 10)
Write-Host ""
Write-Host "⚠️  Remember: Subscription expires in 3 days. Set up auto-renewal!" -ForegroundColor Yellow
```

Run:
```powershell
.\SetupSubscription.ps1 -SiteId "abc123..." -DriveId "b!xyz..." -NotificationUrl "https://your-function.azurewebsites.net/api/ProcessSharePointFile"
```

---

## Step 5: Process Graph Notifications

The `ProcessSharePointFile` function already handles notifications (lines 37-52), but we need to parse the Graph notification format and extract file details.

**Update ProcessSharePointFile.cs** to handle Graph notifications:

```csharp
// After validation token check, check if this is a Graph notification
var notificationBody = await new StreamReader(req.Body).ReadToEndAsync();

try
{
    // Graph sends notifications as array
    var notifications = JsonConvert.DeserializeObject<GraphNotification[]>(notificationBody);
    if (notifications != null && notifications.Length > 0)
    {
        // Process each notification
        foreach (var notification in notifications)
        {
            if (notification.ResourceData != null)
            {
                // Extract file details from notification
                var driveId = notification.ResourceData.DriveId;
                var itemId = notification.ResourceData.Id;
                
                // Create SharePointEvent and process
                var evt = new SharePointEvent
                {
                    driveId = driveId,
                    itemId = itemId,
                    fileName = notification.ResourceData.Name,
                    // Extract other fields from notification
                };
                
                // Process the file
                // (You may want to queue this for async processing)
            }
        }
        
        return req.CreateResponse(HttpStatusCode.Accepted);
    }
}
catch
{
    // Not a Graph notification, try manual payload
}
```

**Graph Notification Model:**

```csharp
public class GraphNotification
{
    [JsonProperty("value")]
    public GraphNotificationItem[] Value { get; set; }
}

public class GraphNotificationItem
{
    [JsonProperty("subscriptionId")]
    public string SubscriptionId { get; set; }
    
    [JsonProperty("resource")]
    public string Resource { get; set; }
    
    [JsonProperty("resourceData")]
    public GraphResourceData ResourceData { get; set; }
}

public class GraphResourceData
{
    [JsonProperty("id")]
    public string Id { get; set; }
    
    [JsonProperty("name")]
    public string Name { get; set; }
    
    [JsonProperty("driveId")]
    public string DriveId { get; set; }
    
    [JsonProperty("webUrl")]
    public string WebUrl { get; set; }
}
```

---

## Step 6: Handle Subscription Renewal

Graph subscriptions expire after **3 days**. You need to renew them:

### Option A: Automatic Renewal Function

Create a timer function that runs daily:

```csharp
[Function("RenewSubscriptions")]
public async Task Run([TimerTrigger("0 0 0 * * *")] TimerInfo timer) // Daily at midnight
{
    // Get all active subscriptions
    // Renew each one before expiration
}
```

### Option B: Manual Renewal Script

Run the setup script again before expiration.

---

## Step 7: Test Automatic Trigger

1. **Set up subscription** (Step 4)
2. **Upload a document** to SharePoint (the subscribed drive)
3. **Check Function logs** - Should see automatic processing
4. **Verify enriched document** appears in ProcessedDocs folder

---

## Alternative: Power Automate (Easier for Testing)

If Graph webhooks are complex, use **Power Automate**:

1. **Trigger**: "When a file is created" (SharePoint)
2. **Action**: "Call HTTP endpoint" → Your Function App URL
3. ✅ **Simpler setup, but requires Power Automate license**

---

## Troubleshooting

### "Subscription validation failed"
- Ensure notification URL is publicly accessible (HTTPS)
- Check function is running
- Verify validation token is echoed correctly

### "Subscription expired"
- Renew subscription before 3 days
- Set up automatic renewal

### "Notifications not received"
- Verify subscription is active (check Graph API)
- Ensure notification URL is correct
- Check function logs for errors

---

## Next Steps

After setting up automatic triggers:
1. ✅ Test with real document upload
2. ✅ Verify automatic processing works
3. ✅ Set up subscription renewal
4. ✅ Configure for production deployment

---

**Ready? Start with Step 1: Get public URL for your function!** 🚀

