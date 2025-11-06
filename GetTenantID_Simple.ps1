# Simple PowerShell script to get Tenant ID
# Uses User.Read permission (available to all users)

Write-Host "🔍 Getting Tenant ID (No Admin Permissions Needed)" -ForegroundColor Cyan
Write-Host ""

# Check if module is installed
if (-not (Get-Module -ListAvailable -Name Microsoft.Graph)) {
    Write-Host "Installing Microsoft.Graph module..." -ForegroundColor Yellow
    Install-Module Microsoft.Graph -Scope CurrentUser -Force -AllowClobber
    Write-Host ""
}

Write-Host "Connecting to Microsoft Graph..." -ForegroundColor Yellow
Write-Host "Required permission: User.Read (you already have this)" -ForegroundColor Gray
Write-Host ""

try {
    # Connect with minimal permissions
    Connect-MgGraph -Scopes "User.Read" -ErrorAction Stop
    
    Write-Host "✅ Connected!" -ForegroundColor Green
    Write-Host ""
    
    # Get context (contains Tenant ID)
    $context = Get-MgContext
    
    if ($context -and $context.TenantId) {
        Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
        Write-Host "   ✅ TENANT ID FOUND" -ForegroundColor Green
        Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "   Tenant ID: $($context.TenantId)" -ForegroundColor White
        Write-Host ""
        Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
        Write-Host ""
        
        # Copy to clipboard
        try {
            $context.TenantId | Set-Clipboard
            Write-Host "✅ Tenant ID copied to clipboard!" -ForegroundColor Green
        }
        catch {
            Write-Host "⚠️  Could not copy to clipboard" -ForegroundColor Yellow
        }
        
        Write-Host ""
        Write-Host "📋 Use this in Power Automate Body:" -ForegroundColor Cyan
        Write-Host '   "tenantId": "' + $context.TenantId + '"' -ForegroundColor White
        Write-Host ""
    }
    else {
        Write-Host "❌ Could not get Tenant ID from context" -ForegroundColor Red
        Write-Host ""
        Write-Host "Trying alternative method..." -ForegroundColor Yellow
        
        # Alternative: Get from SharePoint site
        try {
            Write-Host "Getting SharePoint site information..." -ForegroundColor Yellow
            $site = Get-MgSite -SiteId "onblick.sharepoint.com" -ErrorAction SilentlyContinue
            
            if ($site -and $site.Id) {
                # Site ID format: "domain.sharepoint.com,TENANT-UUID,SITE-GUID"
                $parts = $site.Id -split ","
                if ($parts.Length -ge 2) {
                    $tenantId = $parts[1]
                    Write-Host ""
                    Write-Host "✅ Tenant ID extracted from SharePoint site:" -ForegroundColor Green
                    Write-Host "   $tenantId" -ForegroundColor White
                    Write-Host ""
                    $tenantId | Set-Clipboard
                    Write-Host "✅ Copied to clipboard!" -ForegroundColor Green
                }
            }
        }
        catch {
            Write-Host "Alternative method also failed" -ForegroundColor Yellow
        }
    }
}
catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Alternative: Ask your IT admin for the Tenant ID" -ForegroundColor Yellow
    Write-Host "They can get it from Azure Portal → Azure AD → Overview" -ForegroundColor Gray
}

finally {
    Disconnect-MgGraph -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

