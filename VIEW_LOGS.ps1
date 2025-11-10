# PowerShell script to view logs in real-time
# Usage: .\VIEW_LOGS.ps1

$appFolder = if ($PSScriptRoot) { 
    Join-Path $PSScriptRoot "..\SMEPilot.FunctionApp\bin\Debug\net8.0" 
} else { 
    $env:USERPROFILE 
}

$logPath = Join-Path $appFolder "Logs"
$logFile = Get-ChildItem -Path $logPath -Filter "sme-pilot-*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if ($logFile) {
    Write-Host "`n=== Viewing Logs in Real-Time ===" -ForegroundColor Cyan
    Write-Host "File: $($logFile.FullName)" -ForegroundColor White
    Write-Host "Press Ctrl+C to stop`n" -ForegroundColor Yellow
    
    # Tail logs with -Wait flag (reads even locked files)
    Get-Content $logFile.FullName -Wait -Tail 50
} else {
    Write-Host "`n❌ No log files found in: $logPath" -ForegroundColor Red
    Write-Host "Make sure the app has been run at least once." -ForegroundColor Yellow
}

