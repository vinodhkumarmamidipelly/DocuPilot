# Switch to Node 18.20.4 for SPFx Build

## Instructions

1. **Switch Node version** (use a new PowerShell window or command prompt):
   ```powershell
   nvm use 18.20.4
   ```

2. **Verify Node version**:
   ```powershell
   node --version
   ```
   Should show: `v18.20.4`

3. **Navigate to SPFx project**:
   ```powershell
   cd D:\CodeBase\DocuPilot\SMEPilot.SPFx
   ```

4. **Clean and build**:
   ```powershell
   npx gulp clean
   npx gulp bundle --ship
   npx gulp package-solution --ship
   ```

5. **Expected output**: `solution/sme-pilot.sppkg` file created

## If config.json errors occur:
The config.json has been created with the proper structure. If you still get format errors, we may need to adjust the bundle structure.

