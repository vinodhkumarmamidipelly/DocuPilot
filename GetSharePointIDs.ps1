# PowerShell script to get SharePoint Site ID and Drive ID using Microsoft Graph
# Prerequisites: Microsoft.Graph PowerShell module installed
# Install: Install-Module Microsoft.Graph -Scope CurrentUser

param(
    [Parameter(Mandatory=$true)]
    [string]$SiteUrl,
    
    [string]$DriveName = "Documents"  # Default document library name
)

Write-Host "Getting SharePoint Site and Drive IDs" -ForegroundColor Cyan
Write-Host ""

# Connect to Microsoft Graph
Write-Host "Connecting to Microsoft Graph..." -ForegroundColor Yellow
Write-Host "You'll be prompted to sign in and grant permissions." -ForegroundColor Gray
Write-Host ""

Connect-MgGraph -Scopes "Sites.Read.All"

try {
    # Extract site path from URL
    # Example: https://onblick.sharepoint.com/sites/OnBlick-Inc-Team-Site
    # Need: onblick.sharepoint.com:/sites/OnBlick-Inc-Team-Site
    
    $uri = [System.Uri]$SiteUrl
    $hostName = $uri.Host  # onblick.sharepoint.com
    $path = $uri.PathAndQuery.TrimStart('/')  # sites/OnBlick-Inc-Team-Site
    
    $sitePath = "$hostName`:/$path"
    
    Write-Host "Site Path: $sitePath" -ForegroundColor Gray
    Write-Host ""
    
    # Get Site
    Write-Host "Getting Site ID..." -ForegroundColor Yellow
    $site = Get-MgSite -SiteId $sitePath -ErrorAction Stop
    $siteId = $site.Id
    
    Write-Host "Site ID: $siteId" -ForegroundColor Green
    Write-Host ""
    
    # Get Drives
    Write-Host "Getting Drive IDs..." -ForegroundColor Yellow
    $drives = Get-MgSiteDrive -SiteId $siteId
    
    Write-Host "Available Drives:" -ForegroundColor Cyan
    $drives | ForEach-Object {
        Write-Host "  - $($_.Name): $($_.Id)" -ForegroundColor White
    }
    Write-Host ""
    
    # Find specific drive (if specified)
    $targetDrive = $drives | Where-Object { $_.Name -eq $DriveName } | Select-Object -First 1
    
    if ($targetDrive) {
        Write-Host "Target Drive Found:" -ForegroundColor Green
        Write-Host "   Name: $($targetDrive.Name)" -ForegroundColor White
        Write-Host "   Drive ID: $($targetDrive.Id)" -ForegroundColor White
        Write-Host ""
        
        Write-Host "Use these values for SetupSubscription:" -ForegroundColor Cyan
        Write-Host ""
        Write-Host ".\SetupSubscription.ps1 \" -ForegroundColor Yellow
        Write-Host "  -SiteId `"$siteId`" \" -ForegroundColor Yellow
        Write-Host "  -DriveId `"$($targetDrive.Id)`" \" -ForegroundColor Yellow
        Write-Host "  -NotificationUrl `"https://your-function.azurewebsites.net/api/ProcessSharePointFile`"" -ForegroundColor Yellow
        Write-Host ""
    }
    else {
        Write-Host "Drive '$DriveName' not found. Available drives listed above." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Use one of the Drive IDs above with SetupSubscription.ps1" -ForegroundColor White
    }
    
    Write-Host "Site ID: $siteId" -ForegroundColor Cyan
    
    # Extract Tenant ID from Site ID (format: domain,TENANT-ID,SITE-ID)
    $comma = [char]44
    $siteIdParts = $siteId -split $comma
    if ($siteIdParts.Length -ge 2) {
        $tenantId = $siteIdParts[1]
        Write-Host "Tenant ID: $tenantId" -ForegroundColor Cyan
    }
}
catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "1. Verify you have access to the site" -ForegroundColor White
    Write-Host "2. Check site URL is correct" -ForegroundColor White
    Write-Host "3. Ensure Microsoft.Graph module is installed: Install-Module Microsoft.Graph" -ForegroundColor White
}
finally {
    Write-Host ""
    Write-Host "Disconnecting from Microsoft Graph..." -ForegroundColor Gray
    Disconnect-MgGraph
}
