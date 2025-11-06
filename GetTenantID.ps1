# PowerShell script to get Tenant ID without Azure Portal access
# Requires: Microsoft.Graph PowerShell module
# Install: Install-Module Microsoft.Graph -Scope CurrentUser

Write-Host "🔍 Getting Tenant ID from Microsoft Graph" -ForegroundColor Cyan
Write-Host ""

# Check if module is installed
if (-not (Get-Module -ListAvailable -Name Microsoft.Graph)) {
    Write-Host "Microsoft Graph module not found." -ForegroundColor Yellow
    Write-Host "Installing Microsoft.Graph module..." -ForegroundColor Yellow
    Write-Host ""
    
    try {
        Install-Module Microsoft.Graph -Scope CurrentUser -Force
        Write-Host "✅ Module installed successfully" -ForegroundColor Green
        Write-Host ""
    }
    catch {
        Write-Host "❌ Failed to install module: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host ""
        Write-Host "Please run manually: Install-Module Microsoft.Graph -Scope CurrentUser" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host "Connecting to Microsoft Graph..." -ForegroundColor Yellow
Write-Host "You'll be prompted to sign in with your work account." -ForegroundColor Gray
Write-Host "Required permission: User.Read (read your profile)" -ForegroundColor Gray
Write-Host ""

try {
    Connect-MgGraph -Scopes "User.Read" -ErrorAction Stop
    
    $context = Get-MgContext
    
    if ($context -and $context.TenantId) {
        Write-Host ""
        Write-Host "✅ Tenant Information Found:" -ForegroundColor Green
        Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
        Write-Host "   Tenant ID:   $($context.TenantId)" -ForegroundColor White
        Write-Host "   Domain:       $($context.TenantDomain)" -ForegroundColor White
        Write-Host "   Account:      $($context.Account)" -ForegroundColor White
        Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
        Write-Host ""
        
        # Try to copy to clipboard
        try {
            $context.TenantId | Set-Clipboard
            Write-Host "✅ Tenant ID copied to clipboard!" -ForegroundColor Green
            Write-Host ""
        }
        catch {
            Write-Host "⚠️  Could not copy to clipboard (not critical)" -ForegroundColor Yellow
            Write-Host ""
        }
        
        Write-Host "📋 Use this Tenant ID in Power Automate:" -ForegroundColor Cyan
        Write-Host "   tenantId: `"$($context.TenantId)`"" -ForegroundColor White
        Write-Host ""
    }
    else {
        Write-Host "❌ Could not retrieve tenant information from context" -ForegroundColor Red
        Write-Host ""
        Write-Host "Trying alternative: Extract from SharePoint site..." -ForegroundColor Yellow
        
        # Alternative: Get from SharePoint site
        try {
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
            Write-Host ""
            Write-Host "💡 Alternative: Graph Explorer" -ForegroundColor Cyan
            Write-Host "   Query: GET https://graph.microsoft.com/v1.0/sites/onblick.sharepoint.com" -ForegroundColor Gray
            Write-Host "   Extract UUID from Site ID (middle part)" -ForegroundColor Gray
            Write-Host ""
        }
    }
}
catch {
    Write-Host "❌ Error connecting to Graph: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "1. Ensure you're using a work/school account" -ForegroundColor White
    Write-Host "2. Check if you have internet connection" -ForegroundColor White
    Write-Host "3. Try alternative: Graph Explorer (browser-based)" -ForegroundColor White
    Write-Host ""
}
finally {
    if ($context) {
        Write-Host "Disconnecting from Microsoft Graph..." -ForegroundColor Gray
        Disconnect-MgGraph -ErrorAction SilentlyContinue
    }
}

Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

