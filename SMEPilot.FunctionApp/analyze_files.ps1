# Quick analysis script for template and document files
# Run this from the SMEPilot.FunctionApp directory

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "FILE ANALYSIS" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$templatePath = "Templates\Template_C.dotx"
$rawPath = "Templates\raw.docx"
$enrichedPath = "Enriched\raw_enriched.docx"

# Check if files exist
Write-Host "Checking files..." -ForegroundColor Yellow
if (Test-Path $templatePath) { Write-Host "✅ Template found: $templatePath" -ForegroundColor Green }
else { Write-Host "❌ Template not found: $templatePath" -ForegroundColor Red }

if (Test-Path $rawPath) { Write-Host "✅ Raw document found: $rawPath" -ForegroundColor Green }
else { Write-Host "❌ Raw document not found: $rawPath" -ForegroundColor Red }

if (Test-Path $enrichedPath) { Write-Host "✅ Enriched document found: $enrichedPath" -ForegroundColor Green }
else { Write-Host "❌ Enriched document not found: $enrichedPath" -ForegroundColor Red }

Write-Host "`nTo analyze the files programmatically, use the TemplateInspector class." -ForegroundColor Yellow
Write-Host "The template needs Content Controls (SDT) or plain text placeholders." -ForegroundColor Yellow

