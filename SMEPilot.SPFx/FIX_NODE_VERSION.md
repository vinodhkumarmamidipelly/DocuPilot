# Fix: Switch to Node 18.20.4

## Problem
You're still using Node 24.11.0, but SPFx 1.18.2 requires Node 18.17.1 - 18.x.

## Solution

### Option 1: Use nvm in Command Prompt (not PowerShell)
Open **Command Prompt** (not PowerShell) and run:
```cmd
nvm use 18.20.4
node --version
```
Should show: `v18.20.4`

Then navigate to project and build:
```cmd
cd D:\CodeBase\DocuPilot\SMEPilot.SPFx
npm run clean
npm run build
```

### Option 2: Use Local Gulp (Bypasses npx/npm issues)
If npm is having issues, use the locally installed gulp:
```powershell
.\node_modules\.bin\gulp clean
.\node_modules\.bin\gulp bundle --ship
.\node_modules\.bin\gulp package-solution --ship
```

### Option 3: Reinstall npm for Node 24
If you must use Node 24, reinstall npm:
```powershell
node -v
npm install -g npm@latest
```

But **SPFx 1.18.2 won't work with Node 24**, so you MUST switch to Node 18.20.4.

