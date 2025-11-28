# PowerShell script to generate example Word documents using the existing project
# This script uses the built DLLs from the project

$ErrorActionPreference = "Stop"

# Get paths
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir
$binDir = Join-Path $projectDir "bin\Debug\net8.0"
$dllPath = Join-Path $binDir "DocumentFormat.OpenXml.dll"

Write-Host "Checking for OpenXML DLL..."
if (-not (Test-Path $dllPath)) {
    Write-Host "DLL not found. Building project..."
    Push-Location $projectDir
    dotnet build
    Pop-Location
    if (-not (Test-Path $dllPath)) {
        Write-Host "ERROR: Could not find DocumentFormat.OpenXml.dll after build."
        Write-Host "Please ensure the project builds successfully."
        exit 1
    }
}

Write-Host "Found DLL at: $dllPath"
Write-Host ""
Write-Host "To generate the Word files, you have two options:"
Write-Host ""
Write-Host "Option 1: Use the markdown files as reference and create Word documents manually"
Write-Host "  - Open example_proper_raw.md in a text editor"
Write-Host "  - Copy content to Word"
Write-Host "  - Save as example_proper_raw.docx"
Write-Host "  - Repeat for example_proper_template.md -> example_proper_template.dotx"
Write-Host ""
Write-Host "Option 2: Use a C# compiler (if available)"
Write-Host "  - Install .NET SDK if not already installed"
Write-Host "  - Compile CreateWordFiles.cs with:"
Write-Host "    dotnet script CreateWordFiles.cs"
Write-Host ""
Write-Host "Option 3: Add CreateWordFiles.cs to the main project temporarily"
Write-Host "  - Add it as a console app entry point"
Write-Host "  - Run it to generate the files"
Write-Host ""
Write-Host "The markdown files (example_proper_raw.md and example_proper_template.md)"
Write-Host "contain the exact content that should be in the Word documents."
Write-Host "You can use them as a reference to create the .docx and .dotx files manually."


