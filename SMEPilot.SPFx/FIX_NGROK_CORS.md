# Fix Ngrok CORS Error

## ⚠️ The Problem

When using ngrok free tier, you may encounter CORS errors because:

1. **Ngrok Browser Warning Page**: Free tier shows a warning page that blocks automated requests
2. **CORS Headers**: Need to be properly configured in Function App
3. **Preflight Requests**: Browser sends OPTIONS request first

## ✅ Solutions

### Solution 1: Bypass Ngrok Warning (Quick Fix)

1. **Open ngrok URL in a new browser tab:**
   ```
   https://a5fb7edc07fe.ngrok-free.app
   ```

2. **Click "Visit Site" button** on the ngrok warning page

3. **This bypasses the warning** for your browser session

4. **Try upload again** in SharePoint

### Solution 2: Use Ngrok Paid Tier

Ngrok paid tier doesn't show the browser warning page:
- Sign up at: https://ngrok.com/pricing
- Use authtoken: `ngrok config add-authtoken YOUR_TOKEN`
- No browser warning = no CORS issues

### Solution 3: Add Ngrok Header (If Using Paid Tier)

If you have ngrok paid tier, you can bypass warning with header:

```typescript
headers: {
  'Content-Type': 'application/json',
  'ngrok-skip-browser-warning': 'true'  // Bypass ngrok warning
}
```

### Solution 4: Use Azure Function App (Production)

Instead of ngrok, deploy to Azure:
1. Deploy Function App to Azure
2. Update web part URL to: `https://your-app.azurewebsites.net`
3. No CORS issues (Azure handles it)

## 🔍 Verify CORS Headers

Check that Function App returns CORS headers:

1. **Test endpoint directly:**
   ```powershell
   curl -X OPTIONS https://a5fb7edc07fe.ngrok-free.app/api/ProcessSharePointFile -H "Origin: https://onblick.sharepoint.com" -v
   ```

2. **Should see:**
   ```
   Access-Control-Allow-Origin: https://onblick.sharepoint.com
   Access-Control-Allow-Methods: GET, POST, OPTIONS
   Access-Control-Allow-Headers: Content-Type, Authorization
   ```

## 📝 Current Status

✅ CORS headers are configured in `ProcessSharePointFile.cs`  
✅ OPTIONS preflight is handled  
⚠️ Ngrok browser warning may still block requests  

## 🎯 Recommended Action

**Right now:**
1. Visit `https://a5fb7edc07fe.ngrok-free.app` in a new tab
2. Click "Visit Site" to bypass warning
3. Try upload again

**For production:**
- Deploy Function App to Azure
- Use Azure URL instead of ngrok

