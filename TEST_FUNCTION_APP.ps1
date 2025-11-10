# Test Function App Endpoints
# Run this script to test the Function App locally

Write-Host "`n=== Testing Function App ===`n" -ForegroundColor Cyan

# Test 1: Health Check
Write-Host "Test 1: Health Check (SetupSubscription)" -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:7071/api/SetupSubscription" -Method GET -TimeoutSec 10
    Write-Host "✅ SUCCESS - Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "   Response: $($response.Content)" -ForegroundColor Gray
} catch {
    Write-Host "❌ FAILED - Function App may not be running" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "`n   Make sure Function App is running:" -ForegroundColor Yellow
    Write-Host "   cd SMEPilot.FunctionApp" -ForegroundColor White
    Write-Host "   func start`n" -ForegroundColor White
    exit
}

Write-Host "`nTest 2: ProcessSharePointFile (Manual Test)" -ForegroundColor Yellow
Write-Host "To test document processing, you need:" -ForegroundColor Cyan
Write-Host "  1. A file uploaded to SharePoint" -ForegroundColor White
Write-Host "  2. Site ID, Drive ID, and Item ID from SharePoint" -ForegroundColor White
Write-Host "`n  Use GetSharePointIDs.ps1 to get the IDs:" -ForegroundColor Yellow
Write-Host "  .\GetSharePointIDs.ps1 -SiteUrl 'https://your-tenant.sharepoint.com/sites/your-site'" -ForegroundColor White
Write-Host "`n  Then call ProcessSharePointFile with:" -ForegroundColor Yellow
Write-Host @"
  `$body = @{
      siteId = 'YOUR-SITE-ID'
      driveId = 'YOUR-DRIVE-ID'
      itemId = 'YOUR-ITEM-ID'
      fileName = 'test.docx'
      uploaderEmail = 'your-email@domain.com'
      tenantId = 'default'
  } | ConvertTo-Json

  Invoke-WebRequest -Uri 'http://localhost:7071/api/ProcessSharePointFile' `
      -Method POST `
      -ContentType 'application/json' `
      -Body `$body
"@ -ForegroundColor White

Write-Host "`n=== Test Complete ===`n" -ForegroundColor Cyan
Write-Host "For detailed testing guide, see: START_TESTING.md" -ForegroundColor Gray

