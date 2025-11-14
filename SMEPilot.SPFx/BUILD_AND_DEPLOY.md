# SMEPilot SPFx - Build and Deployment Guide

## Prerequisites

- Node.js 16.x or 18.x
- npm or yarn
- SharePoint Framework CLI (`npm install -g @microsoft/generator-sharepoint`)
- Access to SharePoint site with Site Collection Admin permissions

## Build Steps

### 1. Install Dependencies

```bash
cd SMEPilot.SPFx
npm install
```

### 2. Build for Development

```bash
gulp build
```

### 3. Build for Production

```bash
gulp bundle --ship
gulp package-solution --ship
```

This will create a `.sppkg` file in the `sharepoint/solution/` folder.

## Deployment Steps

### 1. Upload Solution Package

1. Go to SharePoint Admin Center → Apps → App Catalog
2. Upload the `.sppkg` file from `sharepoint/solution/`
3. Click "Deploy" when prompted

### 2. Add Web Part to Page

1. Navigate to your SharePoint site
2. Edit any page
3. Click "+" to add a web part
4. Search for "SMEPilot Admin Panel"
5. Add the web part to the page

### 3. Configure Web Part

1. Click the web part settings (gear icon)
2. Enter your Function App URL (e.g., `https://your-function-app.azurewebsites.net`)
3. Click "Apply"

### 4. Run Installation

1. Fill in the configuration form
2. Click "Save Configuration"
3. Wait for installation to complete
4. Follow the success message instructions for Copilot Studio setup

## Verification

After deployment, verify:

- [ ] SMEPilotConfig list is created
- [ ] Metadata columns are added to source library
- [ ] Error folders (RejectedDocs, FailedDocs) are created
- [ ] Webhook subscription is created
- [ ] Configuration is saved in SMEPilotConfig list

## Troubleshooting

### Build Errors

- **Error: Cannot find module**: Run `npm install` again
- **Error: TypeScript errors**: Check `tsconfig.json` settings
- **Error: Gulp not found**: Install gulp globally: `npm install -g gulp-cli`

### Deployment Errors

- **Error: App not found**: Ensure app catalog is configured
- **Error: Permission denied**: Check Site Collection Admin permissions
- **Error: Function App not accessible**: Verify Function App URL and CORS settings

### Runtime Errors

- **Error: Configuration not loading**: Check browser console for API errors
- **Error: Webhook subscription failed**: Verify Function App is running and accessible
- **Error: Folder not found**: Ensure folder paths are correct and accessible

## Next Steps

After successful deployment:

1. Configure Copilot Agent in Copilot Studio (see `Knowledgebase/QUICK_START_COPILOT.md`)
2. Test document upload and enrichment
3. Test Copilot Agent queries

