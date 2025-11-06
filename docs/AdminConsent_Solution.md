# Solution: Admin Consent Required for Microsoft Graph

## 🔐 Problem
The script requires **admin consent** for `Sites.Read.All` permission.

## ✅ Solutions (Choose One)

### Option 1: Use Admin Account (Recommended for Production)
1. Click **"Have an admin account? Sign in with that account"**
2. Sign in with your admin account
3. Grant consent
4. Script will continue

### Option 2: Use Graph Explorer (Easiest - No Admin Consent Needed)
Graph Explorer uses your existing permissions. Let's use it instead!

#### Step 1: Go to Graph Explorer
https://developer.microsoft.com/graph/graph-explorer

#### Step 2: Sign In
Click "Sign in to Graph Explorer" → Use your work account

#### Step 3: Get Site ID
**Query:**
```
GET https://graph.microsoft.com/v1.0/sites/onblick.sharepoint.com:/sites/DocEnricher-PoC
```

**Copy the `id` value** → This is your **Site ID**

#### Step 4: Get Drive ID
**Query:**
```
GET https://graph.microsoft.com/v1.0/sites/{SITE-ID}/drives
```

**Replace `{SITE-ID}`** with Site ID from Step 3

**Find the "Documents" drive** → Copy the `id` value → This is your **Drive ID**

### Option 3: Ask IT Admin to Grant Consent
Share this with your IT admin:
- **App Name:** Microsoft Graph Command Line Tools
- **Permission:** Sites.Read.All
- **Admin Consent URL:** https://login.microsoftonline.com/{tenant-id}/adminconsent?client_id=14d82eec-204b-4c2f-b7e8-296a70dab67e

### Option 4: Use Different Permissions (Limited Access)
We can modify the script to use permissions that don't require admin consent, but you'll only see sites you have direct access to.

---

## 🚀 Quick Action: Use Graph Explorer (5 minutes)

**I'll create a simple guide for you to get the IDs manually using Graph Explorer.**

This is the fastest way if you don't have admin access right now.

---

**Which option do you prefer?**
1. Use admin account (if you have one)
2. Use Graph Explorer (recommended - no admin needed)
3. Ask IT to grant consent
4. Modify script for limited permissions

