# Test QueryAnswer Endpoint
# This tests semantic search using stored embeddings in CosmosDB

Write-Host "`n🔍 Testing QueryAnswer Endpoint`n" -ForegroundColor Cyan

# Test 1: Basic question
Write-Host "Test 1: Basic Question" -ForegroundColor Yellow
$query1 = @{
    tenantId = "default"
    question = "What are the types of alerts?"
} | ConvertTo-Json

Write-Host "Question: What are the types of alerts?" -ForegroundColor Gray
Write-Host "Sending request..." -ForegroundColor Gray

try {
    $response1 = Invoke-RestMethod -Uri "http://localhost:7071/api/QueryAnswer" -Method Post -Body $query1 -ContentType "application/json"
    Write-Host "`n✅ Response:" -ForegroundColor Green
    Write-Host "Answer: $($response1.answer)" -ForegroundColor White
    Write-Host "`nSources:" -ForegroundColor Cyan
    $response1.sources | ForEach-Object {
        Write-Host "  - $($_.heading) (Score: $($_.score))" -ForegroundColor Gray
        Write-Host "    URL: $($_.fileUrl)" -ForegroundColor DarkGray
    }
} catch {
    Write-Host "`n❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response: $responseBody" -ForegroundColor Red
    }
}

Write-Host "`n" + ("="*60) + "`n" -ForegroundColor DarkGray

# Test 2: More specific question
Write-Host "Test 2: Specific Question" -ForegroundColor Yellow
$query2 = @{
    tenantId = "default"
    question = "How are alerts triggered?"
} | ConvertTo-Json

Write-Host "Question: How are alerts triggered?" -ForegroundColor Gray
Write-Host "Sending request..." -ForegroundColor Gray

try {
    $response2 = Invoke-RestMethod -Uri "http://localhost:7071/api/QueryAnswer" -Method Post -Body $query2 -ContentType "application/json"
    Write-Host "`n✅ Response:" -ForegroundColor Green
    Write-Host "Answer: $($response2.answer)" -ForegroundColor White
    Write-Host "`nSources:" -ForegroundColor Cyan
    $response2.sources | ForEach-Object {
        Write-Host "  - $($_.heading) (Score: $($_.score))" -ForegroundColor Gray
        Write-Host "    URL: $($_.fileUrl)" -ForegroundColor DarkGray
    }
} catch {
    Write-Host "`n❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n" + ("="*60) + "`n" -ForegroundColor DarkGray

# Test 3: Question about SignalR
Write-Host "Test 3: SignalR Question" -ForegroundColor Yellow
$query3 = @{
    tenantId = "default"
    question = "What is SignalR used for?"
} | ConvertTo-Json

Write-Host "Question: What is SignalR used for?" -ForegroundColor Gray
Write-Host "Sending request..." -ForegroundColor Gray

try {
    $response3 = Invoke-RestMethod -Uri "http://localhost:7071/api/QueryAnswer" -Method Post -Body $query3 -ContentType "application/json"
    Write-Host "`n✅ Response:" -ForegroundColor Green
    Write-Host "Answer: $($response3.answer)" -ForegroundColor White
    Write-Host "`nSources:" -ForegroundColor Cyan
    $response3.sources | ForEach-Object {
        Write-Host "  - $($_.heading) (Score: $($_.score))" -ForegroundColor Gray
        Write-Host "    URL: $($_.fileUrl)" -ForegroundColor DarkGray
    }
} catch {
    Write-Host "`n❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n✅ Testing Complete!`n" -ForegroundColor Green

