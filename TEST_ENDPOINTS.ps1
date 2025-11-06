# Test SMEPilot Azure Functions
# Keep Visual Studio running while executing this script

Write-Host "Testing ProcessSharePointFile endpoint..." -ForegroundColor Green

$body = @{
    siteId = "local-test"
    driveId = "local-test"
    itemId = "local-test"
    fileName = "test.docx"
    uploaderEmail = "test@example.com"
    tenantId = "default"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "http://localhost:7071/api/ProcessSharePointFile" `
        -Method Post `
        -Body $body `
        -ContentType "application/json"
    
    Write-Host "✅ ProcessSharePointFile SUCCESS!" -ForegroundColor Green
    Write-Host $response | ConvertTo-Json -Depth 10
} catch {
    Write-Host "❌ ProcessSharePointFile ERROR:" -ForegroundColor Red
    Write-Host $_.Exception.Message
}

Write-Host "`nTesting QueryAnswer endpoint..." -ForegroundColor Green

$queryBody = @{
    tenantId = "default"
    question = "What is this document about?"
} | ConvertTo-Json

try {
    $queryResponse = Invoke-RestMethod -Uri "http://localhost:7071/api/QueryAnswer" `
        -Method Post `
        -Body $queryBody `
        -ContentType "application/json"
    
    Write-Host "✅ QueryAnswer SUCCESS!" -ForegroundColor Green
    Write-Host $queryResponse | ConvertTo-Json -Depth 10
} catch {
    Write-Host "❌ QueryAnswer ERROR:" -ForegroundColor Red
    Write-Host $_.Exception.Message
}

Write-Host "`n✅ Testing complete! Check Visual Studio Output window for function logs." -ForegroundColor Cyan

