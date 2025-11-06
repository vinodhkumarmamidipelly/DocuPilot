# SPFx Setup & Implementation Instructions

## Prerequisites Check

Before scaffolding SPFx, verify you have:

1. **Node.js** - v18.x or v20.x LTS
   ```bash
   node --version
   ```
   If not installed: Download from https://nodejs.org/

2. **Yeoman** - SharePoint generator
   ```bash
   npm install -g yo
   ```

3. **SPFx Generator**
   ```bash
   npm install -g @microsoft/generator-sharepoint
   ```

4. **Gulp CLI**
   ```bash
   npm install -g gulp-cli
   ```

---

## Step-by-Step SPFx Implementation

### Step 1: Install Prerequisites (if missing)
```bash
npm install -g yo
npm install -g @microsoft/generator-sharepoint
npm install -g gulp-cli
```

### Step 2: Scaffold SPFx Solution

**From project root (D:\CodeBase\DocuPilot):**
```bash
yo @microsoft/sharepoint
```

**Answer prompts:**
- Solution name: `smepilot`
- Target environment: SharePoint Online only (latest)
- Deployment option: **N** (No, not right now)
- Permissions: Will have full control
- Type of client-side component: **WebPart**
- Web part name: `DocumentUploader`
- Description: `SMEPilot document upload and enrichment`
- Framework: **React**

### Step 3: Create Second Web Part (AdminPanel)

After first web part is created:
```bash
cd smepilot
yo @microsoft/sharepoint
```

**For AdminPanel:**
- Add component to existing solution: **Y** (Yes)
- Type: **WebPart**
- Web part name: `AdminPanel`
- Description: `SMEPilot admin management interface`
- Framework: **React**

### Step 4: Project Structure

After scaffolding, you should have:
```
smepilot/
├── config/
│   ├── package-solution.json
│   └── serve.json
├── src/
│   ├── webparts/
│   │   ├── documentUploader/
│   │   └── adminPanel/
│   └── services/
└── package.json
```

---

## Implementation Checklist

- [ ] Prerequisites installed
- [ ] SPFx solution scaffolded
- [ ] DocumentUploader web part created
- [ ] AdminPanel web part created
- [ ] Build successfully (`gulp build`)
- [ ] Package solution (`gulp bundle --ship && gulp package-solution --ship`)

---

## Next: Implement Web Parts

After scaffolding, we'll implement:
1. DocumentUploader web part (upload interface)
2. AdminPanel web part (management interface)
3. Service layer for Function App API calls
4. App package configuration


