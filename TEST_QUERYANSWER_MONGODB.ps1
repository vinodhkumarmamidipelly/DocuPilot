# Test QueryAnswer Endpoint with MongoDB
# This tests semantic search using stored embeddings in MongoDB

param(
    [string]$FunctionAppUrl = "http://localhost:7071",
    [string]$TenantId = "default"
)

Write-Host "`nTesting QueryAnswer Endpoint (MongoDB)`n" -ForegroundColor Cyan
Write-Host "Function App URL: $FunctionAppUrl" -ForegroundColor Gray
Write-Host "Tenant ID: $TenantId`n" -ForegroundColor Gray

# Test 1: Basic question about alerts
Write-Host ("=" * 60) -ForegroundColor DarkGray
Write-Host "Test 1: Basic Question" -ForegroundColor Yellow
$query1 = @{
    tenantId = $TenantId
    question = "What are the types of alerts?"
} | ConvertTo-Json

Write-Host "Question: What are the types of alerts?" -ForegroundColor White
Write-Host "Sending request to $FunctionAppUrl/api/QueryAnswer..." -ForegroundColor Gray

try {
    $response1 = Invoke-RestMethod -Uri "$FunctionAppUrl/api/QueryAnswer" -Method Post -Body $query1 -ContentType "application/json"
    Write-Host "`n✅ Response Received!" -ForegroundColor Green
    Write-Host "`nAnswer:" -ForegroundColor Cyan
    Write-Host $response1.answer -ForegroundColor White
    Write-Host "`nSources Found: $($response1.sources.Count)" -ForegroundColor Cyan
    $response1.sources | ForEach-Object {
        Write-Host "  - $($_.heading)" -ForegroundColor Yellow
        Write-Host "     Score: $([math]::Round($_.score, 3))" -ForegroundColor Gray
        Write-Host "     URL: $($_.fileUrl)" -ForegroundColor DarkGray
        Write-Host ""
    }
} catch {
    Write-Host "`n❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody" -ForegroundColor Red
    }
    Write-Host "`nTip: Make sure:" -ForegroundColor Yellow
    Write-Host "   1. Function App is running" -ForegroundColor Gray
    Write-Host "   2. MongoDB has embeddings stored (process a document first)" -ForegroundColor Gray
    Write-Host "   3. Tenant ID matches the one used when processing documents" -ForegroundColor Gray
}

Write-Host "`n" + ("=" * 60) + "`n" -ForegroundColor DarkGray

# Test 2: More specific question
Write-Host "Test 2: Specific Question" -ForegroundColor Yellow
$query2 = @{
    tenantId = $TenantId
    question = "How are alerts triggered?"
} | ConvertTo-Json

Write-Host "Question: How are alerts triggered?" -ForegroundColor White
Write-Host "Sending request..." -ForegroundColor Gray

try {
    $response2 = Invoke-RestMethod -Uri "$FunctionAppUrl/api/QueryAnswer" -Method Post -Body $query2 -ContentType "application/json"
    Write-Host "`n✅ Response Received!" -ForegroundColor Green
    Write-Host "`nAnswer:" -ForegroundColor Cyan
    Write-Host $response2.answer -ForegroundColor White
    Write-Host "`nSources Found: $($response2.sources.Count)" -ForegroundColor Cyan
    $response2.sources | ForEach-Object {
        Write-Host "  📄 $($_.heading) (Score: $([math]::Round($_.score, 3)))" -ForegroundColor Yellow
    }
} catch {
    Write-Host "`n❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n" + ("=" * 60) + "`n" -ForegroundColor DarkGray

# Test 3: Question about SignalR
Write-Host "Test 3: SignalR Question" -ForegroundColor Yellow
$query3 = @{
    tenantId = $TenantId
    question = "What is SignalR used for?"
} | ConvertTo-Json

Write-Host "Question: What is SignalR used for?" -ForegroundColor White
Write-Host "Sending request..." -ForegroundColor Gray

try {
    $response3 = Invoke-RestMethod -Uri "$FunctionAppUrl/api/QueryAnswer" -Method Post -Body $query3 -ContentType "application/json"
    Write-Host "`n✅ Response Received!" -ForegroundColor Green
    Write-Host "`nAnswer:" -ForegroundColor Cyan
    Write-Host $response3.answer -ForegroundColor White
    Write-Host "`nSources Found: $($response3.sources.Count)" -ForegroundColor Cyan
    $response3.sources | ForEach-Object {
        Write-Host "  📄 $($_.heading) (Score: $([math]::Round($_.score, 3)))" -ForegroundColor Yellow
    }
} catch {
    Write-Host "`n❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n✅ Testing Complete!`n" -ForegroundColor Green

Write-Host "Tip: To test with ngrok URL, use:" -ForegroundColor Cyan
Write-Host "   .\TEST_QUERYANSWER_MONGODB.ps1 -FunctionAppUrl https://your-ngrok-url.ngrok-free.app" -ForegroundColor Gray
Write-Host ""

