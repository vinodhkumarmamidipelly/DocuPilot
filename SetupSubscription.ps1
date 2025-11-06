# PowerShell script to set up Graph webhook subscription for automatic SharePoint upload triggers
# Usage: .\SetupSubscription.ps1 -SiteId "abc123" -DriveId "b!xyz" -NotificationUrl "https://your-function.azurewebsites.net/api/ProcessSharePointFile"

param(
    [Parameter(Mandatory=$true)]
    [string]$SiteId,
    
    [Parameter(Mandatory=$true)]
    [string]$DriveId,
    
    [Parameter(Mandatory=$true)]
    [string]$NotificationUrl,
    
    [string]$FunctionAppUrl = "http://localhost:7071"
)

Write-Host "Setting up Graph Webhook Subscription for SMEPilot" -ForegroundColor Cyan
Write-Host ""

Write-Host "Parameters:" -ForegroundColor Yellow
Write-Host "  Site ID: $SiteId"
Write-Host "  Drive ID: $DriveId"
Write-Host "  Notification URL: $NotificationUrl"
Write-Host "  Function App URL: $FunctionAppUrl"
Write-Host ""

# URL encode the notification URL using PowerShell's built-in method
$encodedUrl = [System.Uri]::EscapeDataString($NotificationUrl)
# Build URL with proper escaping for ampersands
$setupUrl = "$FunctionAppUrl/api/SetupSubscription?siteId=$SiteId" + "&driveId=$DriveId" + "&notificationUrl=$encodedUrl"

Write-Host "Creating subscription..." -ForegroundColor Green
Write-Host "URL: $setupUrl" -ForegroundColor Gray
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri $setupUrl -Method Get
    
    Write-Host "Subscription created successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Subscription Details:" -ForegroundColor Yellow
    Write-Host ($response | ConvertTo-Json -Depth 10)
    Write-Host ""
    Write-Host "IMPORTANT: Subscription expires in 3 days!" -ForegroundColor Yellow
    Write-Host "   Set up auto-renewal or run this script again before expiration." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Cyan
    Write-Host "1. Upload a document to SharePoint" -ForegroundColor White
    Write-Host "2. Check Function App logs for automatic processing" -ForegroundColor White
    Write-Host "3. Verify enriched document appears in ProcessedDocs folder" -ForegroundColor White
}
catch {
    Write-Host "Error creating subscription:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "1. Ensure Function App is running" -ForegroundColor White
    Write-Host "2. Verify Graph API credentials are configured" -ForegroundColor White
    Write-Host "3. Check Site ID and Drive ID are correct" -ForegroundColor White
    Write-Host "4. Ensure notification URL is publicly accessible (HTTPS)" -ForegroundColor White
}

