# Configure Function App URL

## Why Ngrok Was There

The ngrok URL (`https://a5fb7edc07fe.ngrok-free.app`) is used for **local testing** purposes. When running the Azure Function App locally, ngrok exposes it to the internet so SharePoint can call it.

## ⚠️ Issue

**Ngrok URLs expire** and change frequently. The hardcoded URL is likely:
- Expired
- Not running
- Pointing to a different tunnel

This causes "Failed to fetch" errors.

## ✅ Solution: Configure Function App URL

### Option 1: Use Azure Function App (Production)

1. **Get your Azure Function App URL:**
   - Go to Azure Portal
   - Find your Function App
   - Copy the URL (e.g., `https://sme-pilot-func.azurewebsites.net`)

2. **Configure in SharePoint:**
   - Edit the web part
   - Enter the URL in "Function App URL" field
   - Save

### Option 2: Use Ngrok (Local Testing)

1. **Start ngrok:**
   ```powershell
   ngrok http 7071  # Or your local Function App port
   ```

2. **Copy the ngrok URL:**
   - Current: `https://a5fb7edc07fe.ngrok-free.app`
   - Example: `https://abc123.ngrok-free.app`

3. **Configure in SharePoint:**
   - Edit the web part
   - Enter the ngrok URL in "Function App URL" field
   - Save

### Option 3: Update Default in Code

If you want to change the default URL in code:

1. Edit: `SMEPilot.SPFx/src/webparts/documentUploader/DocumentUploaderWebPart.ts`
2. Change line 27:
   ```typescript
   'https://a5fb7edc07fe.ngrok-free.app' // Your actual URL
   ```
3. Rebuild: `npx gulp build`

## 🔍 How to Check Current URL

1. Open browser console (F12)
2. Look for: `⚠️ Function App URL not configured`
3. Or check Network tab for the actual URL being called

## 📝 Quick Fix

**Right now, to fix the error:**

1. **Edit the web part** in SharePoint
2. **Set "Function App URL"** to your actual Function App URL
3. **Save**

The error should be resolved!

