# SMEPilot SPFx SharePoint App

**Status:** In Development  
**Purpose:** Sellable SharePoint App for document enrichment and management

## ⚠️ Prerequisites

**Required:** Node.js v18.x or v20.x LTS  
Your current Node.js version may need upgrade for SPFx latest version.

To check: `node --version`  
To upgrade: Download from https://nodejs.org/

## Quick Start

```bash
# Install dependencies
npm install

# Build solution
gulp build

# Bundle and package
gulp bundle --ship
gulp package-solution --ship

# Output: sharepoint/solution/sme-pilot.sppkg
```

## Project Structure

```
SMEPilot.SPFx/
├── config/
│   ├── package-solution.json  ✅ Created
│   └── serve.json              ✅ Created
├── src/
│   ├── webparts/
│   │   └── documentUploader/   ✅ Created (basic structure)
│   └── services/
│       └── FunctionAppService.ts ✅ Created
├── package.json                ✅ Created
├── tsconfig.json               ✅ Created
└── gulpfile.js                 ✅ Created
```

## Web Parts

1. **DocumentUploader** - Main web part for uploading scratch documents ✅ Structure created
2. **AdminPanel** - Admin interface (TODO - create structure)

## Implementation Status

- ✅ Project structure created
- ✅ Package configuration (package-solution.json)
- ✅ FunctionAppService for API calls
- ✅ DocumentUploader component structure
- ⬜ Complete DocumentUploader implementation
- ⬜ AdminPanel web part
- ⬜ Build and package as .sppkg

---

**Note:** Full SPFx development requires:
1. Node.js v18+ installed
2. Run `npm install` to install dependencies
3. Complete React component implementations
4. Build with `gulp bundle --ship`
