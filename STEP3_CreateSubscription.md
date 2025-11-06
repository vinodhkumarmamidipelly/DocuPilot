# Step 3: Create Graph Subscription

## ✅ You Have:
- **Site ID:** `onblick.sharepoint.com,4b82e6b5-e79c-4787-8aa8-8c4aad49e9e0,af7fd8eb-328c-45b1-b7a7-8b79d27dd516`
- **Drive ID:** `b!teaCS5znh0eKqIxKrUnp4OvYf6-MMrFFt6eLedJ91Ra5fXoii7EjRK7WFJxSDDf3`
- **Tenant ID:** `4b82e6b5-e79c-4787-8aa8-8c4aad49e9e0`

---

## ⚠️ Important Requirement

**Graph API requires a PUBLIC HTTPS URL** for webhooks. Your local `http://localhost:7071` won't work.

### Option 1: Use ngrok (Quick Testing)

**For local testing, expose your Function App with ngrok:**

1. **Download ngrok:** https://ngrok.com/download
2. **Extract and run:**
   ```powershell
   ngrok http 7071
   ```
3. **Copy the HTTPS URL** (e.g., `https://abc123.ngrok.io`)
4. **Use this as notification URL:** `https://abc123.ngrok.io/api/ProcessSharePointFile`

### Option 2: Deploy to Azure (Production)

**Deploy Function App to Azure** → Get HTTPS URL automatically.

---

## 🚀 Create Subscription

### Step 1: Ensure Function App is Running

**In Visual Studio:**
- Press **F5** to run Function App
- Should show: `Functions: http://localhost:7071/api/...`

### Step 2: Get Public URL

**If using ngrok:**
- Run `ngrok http 7071` in separate terminal
- Copy HTTPS URL (e.g., `https://abc123.ngrok.io`)

**If deployed to Azure:**
- Use: `https://your-function-app.azurewebsites.net`

### Step 3: Run SetupSubscription Script

**In PowerShell (D:\CodeBase\DocuPilot):**

```powershell
.\SetupSubscription.ps1 `
  -SiteId "onblick.sharepoint.com,4b82e6b5-e79c-4787-8aa8-8c4aad49e9e0,af7fd8eb-328c-45b1-b7a7-8b79d27dd516" `
  -DriveId "b!teaCS5znh0eKqIxKrUnp4OvYf6-MMrFFt6eLedJ91Ra5fXoii7EjRK7WFJxSDDf3" `
  -NotificationUrl "https://YOUR-PUBLIC-URL/api/ProcessSharePointFile"
```

**Replace `YOUR-PUBLIC-URL`** with:
- ngrok URL: `https://abc123.ngrok.io`
- OR Azure URL: `https://your-function-app.azurewebsites.net`

---

## 📋 Expected Result

```
✅ Subscription created successfully!

Subscription Details:
{
  "subscriptionId": "abc123...",
  "resource": "/sites/.../drives/.../root/children",
  "expiration": "2025-11-06T...",
  ...
}

⚠️  IMPORTANT: Subscription expires in 3 days!
```

---

## 🧪 Test It

1. **Upload a document** to SharePoint Documents library
2. **Check Function App logs** → Should see notification received
3. **Verify enriched document** in ProcessedDocs folder

---

## ❓ Do You Have ngrok Installed?

**If NO:**
1. Download: https://ngrok.com/download
2. Extract to folder
3. Run: `ngrok http 7071`
4. Copy HTTPS URL

**If YES:**
- Just run `ngrok http 7071` and use the URL

---

**Ready? Let me know if you want to:**
1. Use ngrok for testing (quick)
2. Deploy to Azure (production)

Then we'll create the subscription!

