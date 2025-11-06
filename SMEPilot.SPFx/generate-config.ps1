# PowerShell script to generate SPFx config.json using Node 24
# Then switch back to Node 18 for building

Write-Host "Step 1: Creating temporary SPFx project to extract config.json..." -ForegroundColor Green

# Create temp directory
$tempDir = "$PSScriptRoot\..\temp-spfx-generate"
if (Test-Path $tempDir) {
    Remove-Item -Recurse -Force $tempDir
}
New-Item -ItemType Directory -Path $tempDir | Out-Null
Set-Location $tempDir

# Generate SPFx project (you'll need to run yo interactively)
Write-Host "Please run: yo @microsoft/generator-sharepoint" -ForegroundColor Yellow
Write-Host "Select: WebPart -> React -> Use current folder -> Yes -> No framework" -ForegroundColor Yellow
Write-Host ""
Write-Host "After generation, the config.json will be at: $tempDir\config\config.json" -ForegroundColor Cyan

