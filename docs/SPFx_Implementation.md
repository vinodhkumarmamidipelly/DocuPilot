# SPFx Implementation Guide (REQUIRED FOR MVP)

This document contains the SPFx code and setup instructions for the sellable SharePoint App.

## SPFx Project Structure
```
SMEPilot.SPFx/
├── config/
│   ├── package-solution.json    # App package configuration
│   └── serve.json
├── src/
│   ├── webparts/
│   │   ├── documentUploader/    # Main web part for upload
│   │   └── adminPanel/           # Admin web part
│   └── services/
│       └── functionAppService.ts # API calls to Azure Functions
└── package.json
```

## package-solution.json (App Manifest)
```json
{
  "$schema": "https://developer.microsoft.com/json-schemas/spfx-build/package-solution.schema.json",
  "solution": {
    "name": "sme-pilot-client-side-solution",
    "id": "12345678-1234-1234-1234-123456789012",
    "version": "1.0.0.0",
    "includeClientSideAssets": true,
    "skipFeatureDeployment": true,
    "isDomainIsolated": false,
    "developer": {
      "name": "Your Company",
      "websiteUrl": "",
      "privacyUrl": "",
      "termsOfUseUrl": ""
    },
    "webApiPermissionRequests": [
      {
        "resource": "Microsoft Graph",
        "scope": "Sites.ReadWrite.All"
      },
      {
        "resource": "Microsoft Graph",
        "scope": "Files.ReadWrite"
      }
    ]
  },
  "paths": {
    "zippedPackage": "solution/sme-pilot.sppkg"
  }
}
```

## Building & Packaging SPFx Solution
```bash
# Install dependencies
npm install

# Build solution
gulp build

# Bundle and package
gulp bundle --ship
gulp package-solution --ship

# Output: sharepoint/solution/sme-pilot.sppkg (deploy to App Catalog)
```

**SPFx Deployment:**
1. Build SPFx solution: `gulp bundle --ship && gulp package-solution --ship`
2. Upload `.sppkg` to SharePoint App Catalog (Tenant Admin Center)
3. Deploy app to sites where users need access
4. Configure Microsoft Search Connector for Copilot integration (see Microsoft Search Admin Center)

See TechnicalDoc.md for detailed component implementations.

