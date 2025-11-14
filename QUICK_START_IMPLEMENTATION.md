# 🚀 Quick Start - Implementation

> **Ready to start coding?** Follow these steps to begin implementation.

---

## ✅ Prerequisites Check

Before starting, ensure you have:

- [ ] Node.js v18+ or v20+ installed (`node --version`)
- [ ] SPFx project structure exists (`SMEPilot.SPFx/`)
- [ ] Function App project exists (`SMEPilot.FunctionApp/`)
- [ ] Access to SharePoint site (for testing)
- [ ] Azure Function App created (or local development setup)

---

## 🎯 Step 1: Start with SPFx Admin Panel (Phase 1, Task 1.1)

### 1.1 Navigate to SPFx Project

```bash
cd SMEPilot.SPFx
```

### 1.2 Install Dependencies (if not done)

```bash
npm install
```

### 1.3 Open Admin Panel Component

**File to edit:** `SMEPilot.SPFx/src/webparts/adminPanel/components/AdminPanel.tsx`

**Current state:** Basic structure exists, needs full implementation

### 1.4 What to Build

Create a configuration form with these 6 fields:

1. **Source Folder** (User Selected)
   - Use SharePoint folder picker
   - Component: `@pnp/spfx-controls-react` or Fluent UI

2. **Destination Folder** (User Selected)
   - Use SharePoint folder picker
   - Validate or create folder

3. **Template File**
   - Use SharePoint file picker
   - Filter: `.dotx` files only

4. **Processing Settings**
   - Max File Size: Number input (default: 50)
   - Timeout: Number input (default: 60)
   - Retry Count: Number input (default: 3)

5. **Copilot Agent Prompt**
   - Multi-line text area
   - Default text: (from documentation)

6. **Access Points**
   - Checkboxes: Teams, Web, O365
   - Default: All checked

### 1.5 Recommended Approach

1. **Start with UI only** (no backend calls yet)
2. **Use Fluent UI components:**
   - `TextField` for text inputs
   - `Dropdown` for selections
   - `Checkbox` for access points
   - `PrimaryButton` for Save button
   - `MessageBar` for errors/success

3. **Add form state management:**
   ```typescript
   interface IConfigurationState {
     sourceFolder: string;
     destinationFolder: string;
     templateFile: string;
     maxFileSize: number;
     timeout: number;
     retries: number;
     copilotPrompt: string;
     accessTeams: boolean;
     accessWeb: boolean;
     accessO365: boolean;
   }
   ```

4. **Add validation:**
   - Required fields check
   - Number validation (positive numbers)
   - File format validation (.dotx)

5. **Test locally:**
   ```bash
   gulp serve
   ```

---

## 📋 Step 2: Create SharePoint Service (Phase 1, Task 1.2)

### 2.1 Create New Service File

**File to create:** `SMEPilot.SPFx/src/services/SharePointService.ts`

### 2.2 Implement Basic Structure

```typescript
import { SPHttpClient, SPHttpClientResponse } from '@microsoft/sp-http';
import { WebPartContext } from '@microsoft/sp-webpart-base';

export interface IConfiguration {
  sourceFolderPath: string;
  destinationFolderPath: string;
  templateFileUrl: string;
  maxFileSizeMB: number;
  processingTimeoutSeconds: number;
  maxRetries: number;
  copilotPrompt: string;
  accessTeams: boolean;
  accessWeb: boolean;
  accessO365: boolean;
  subscriptionId?: string;
  lastUpdated?: Date;
}

export class SharePointService {
  private context: WebPartContext;
  private httpClient: SPHttpClient;

  constructor(context: WebPartContext) {
    this.context = context;
    this.httpClient = context.spHttpClient;
  }

  // Methods to implement:
  // - createSMEPilotConfigList()
  // - saveConfiguration(config: IConfiguration)
  // - getConfiguration()
  // - validateConfiguration()
}
```

### 2.3 Use SharePoint REST API

Example for creating list:
```typescript
const url = `${this.context.pageContext.web.absoluteUrl}/_api/web/lists`;
const body = {
  __metadata: { type: 'SP.List' },
  Title: 'SMEPilotConfig',
  Description: 'SMEPilot Configuration',
  BaseTemplate: 100 // Custom List
};
```

---

## 🔧 Step 3: Integrate Services (Phase 1, Task 1.6)

### 3.1 Update AdminPanel Component

Connect the form to SharePointService:

```typescript
private async handleSaveConfiguration = async () => {
  // 1. Validate form
  // 2. Create SMEPilotConfig list (if doesn't exist)
  // 3. Save configuration
  // 4. Create metadata columns
  // 5. Create error folders
  // 6. Create webhook subscription (via Function App)
  // 7. Show success message
}
```

---

## 🧪 Step 4: Test Locally

### 4.1 Build and Serve

```bash
gulp build
gulp serve
```

### 4.2 Test in SharePoint Workbench

1. Open SharePoint Workbench (local or hosted)
2. Add Admin Panel web part
3. Fill configuration form
4. Click "Save Configuration"
5. Verify SMEPilotConfig list created
6. Verify configuration saved

---

## 📚 Reference Documents

- **Full Implementation Plan:** `IMPLEMENTATION_PLAN.md`
- **Task Checklist:** `TASK_CHECKLIST.md`
- **Complete Documentation:** `SMEPILOT_COMPLETE_DOCUMENTATION.md`
- **Configuration Details:** `Knowledgebase/INSTALLATION_TIME_CONFIGURATION.md`

---

## 🆘 Need Help?

### Common Issues

1. **SPFx build errors:**
   - Check Node.js version (v18+ or v20+)
   - Run `npm install` again
   - Clear `node_modules` and reinstall

2. **SharePoint API errors:**
   - Check permissions (Site Collection Admin)
   - Verify site URL is correct
   - Check browser console for errors

3. **Folder/File picker not working:**
   - Install `@pnp/spfx-controls-react` if needed
   - Check Fluent UI documentation
   - Use SharePoint REST API as fallback

---

## ✅ Next Steps After Phase 1

Once Phase 1 is complete:

1. **Move to Phase 2:** Update Function App to read from SharePoint
2. **Test Integration:** End-to-end installation flow
3. **Configure Copilot:** Set up O365 Copilot Agent
4. **Documentation:** Create user guides

---

## 🎯 Current Focus

**Start Here:** Phase 1, Task 1.1 - Admin Panel UI - Configuration Form

**File:** `SMEPilot.SPFx/src/webparts/adminPanel/components/AdminPanel.tsx`

**Goal:** Create a working configuration form with all 6 fields.

---

**Ready? Let's code!** 🚀

