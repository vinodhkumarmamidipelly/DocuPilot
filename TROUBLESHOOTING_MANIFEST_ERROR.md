# 🔧 Troubleshooting: Manifest Loading Error

## Error Message
```
Error loading debug manifests.
Error: Script error for "https://localhost:4321/temp/build/manifests.js"
```

## Common Causes & Solutions

### 1. ✅ SSL Certificate Issue (Most Common)

**Problem:** Browser doesn't trust the self-signed certificate from localhost:4321

**Solution:**
1. Open `https://localhost:4321` in a new tab
2. You'll see a security warning
3. Click **"Advanced"** or **"Show Details"**
4. Click **"Proceed to localhost (unsafe)"** or **"Accept the Risk and Continue"**
5. Go back to SharePoint Workbench and refresh

### 2. ✅ Serve Process Not Running

**Problem:** `gulp serve` is not running

**Solution:**
```powershell
cd SMEPilot.SPFx
npx gulp serve
```

Wait for the build to complete, then try again.

### 3. ✅ Browser Cache

**Problem:** Browser cached old manifest file

**Solution:**
- Press `Ctrl + Shift + Delete`
- Clear "Cached images and files"
- Refresh the page with `Ctrl + F5`

### 4. ✅ Wrong Workbench URL

**Problem:** Using wrong workbench URL

**Solutions:**

**Option A: Local Workbench (Recommended for Testing)**
```
https://localhost:4321/temp/workbench.html
```

**Option B: Hosted Workbench**
Make sure the URL in `serve.json` matches your SharePoint site:
```
https://YOUR-SITE.sharepoint.com/sites/YOUR-SITE/_layouts/15/workbench.aspx
```

### 5. ✅ Firewall/Antivirus Blocking

**Problem:** Firewall or antivirus blocking localhost:4321

**Solution:**
- Temporarily disable firewall/antivirus
- Or add exception for localhost:4321

### 6. ✅ Port Already in Use

**Problem:** Another process using port 4321

**Solution:**
```powershell
# Check what's using port 4321
netstat -ano | findstr :4321

# Kill the process (replace PID with actual process ID)
taskkill /PID <PID> /F

# Then restart serve
npx gulp serve
```

### 7. ✅ CORS Issue

**Problem:** CORS blocking the manifest file

**Solution:**
- Make sure you're accessing from the same origin
- Check browser console for CORS errors
- Try a different browser (Chrome, Edge, Firefox)

## Step-by-Step Fix

1. **Stop any running serve process:**
   ```powershell
   # Press Ctrl+C in the terminal running gulp serve
   ```

2. **Clean and rebuild:**
   ```powershell
   cd SMEPilot.SPFx
   npm run clean
   npm run build
   ```

3. **Start serve:**
   ```powershell
   npx gulp serve
   ```

4. **Accept SSL certificate:**
   - Open `https://localhost:4321` in browser
   - Accept the security warning

5. **Open workbench:**
   - Use local workbench: `https://localhost:4321/temp/workbench.html`
   - Or use hosted workbench with correct URL

6. **Clear browser cache:**
   - `Ctrl + Shift + Delete`
   - Clear cache
   - Refresh page

## Verify Serve is Running

Check the terminal output. You should see:
```
[XX:XX:XX] Finished 'bundle' after XX s
[XX:XX:XX] ==================[ Finished ]==================
```

If you see errors, check:
- Node.js version (should be v18+ or v20+)
- All dependencies installed (`npm install`)
- No port conflicts

## Alternative: Use HTTP (Not Recommended for Production)

If SSL continues to be an issue, you can temporarily modify `serve.json`:

```json
{
  "port": 4321,
  "https": false,  // Change to false
  "hostname": "localhost"
}
```

**Note:** This is only for local development. Always use HTTPS in production.

## Still Having Issues?

1. Check browser console (F12) for detailed errors
2. Check terminal output from `gulp serve` for build errors
3. Verify `temp/build/manifests.js` exists
4. Try a different browser
5. Restart your computer (sometimes helps with port/SSL issues)

---

**Most Common Fix:** Accept the SSL certificate at `https://localhost:4321` first, then refresh the workbench page.

