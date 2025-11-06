# How to Add SharePoint Site to Microsoft Search Indexing

## 🎯 Goal

If your SharePoint site is **not automatically indexed** by Microsoft Search, you can manually add it as a content source.

---

## ✅ Step-by-Step Instructions

### **Step 1: Access Microsoft Search Admin Center**

1. **Go to:** https://admin.microsoft.com/Adminportal/Home#/MicrosoftSearch
   - Or: Admin Center → Settings → Search & intelligence → Microsoft Search

2. **Sign in** with admin credentials (Global Admin or Search Administrator)

---

### **Step 2: Navigate to Content Sources**

1. In Microsoft Search Admin Center, click **"Content sources"** in the left navigation
2. You'll see a list of existing content sources (SharePoint, OneDrive, etc.)

---

### **Step 3: Add SharePoint Site**

**Option A: SharePoint is Already Listed (Most Common)**

If you see **"SharePoint"** in the content sources list:

1. **Click on "SharePoint"** to open its settings
2. **Check "Content sources"** or **"Sites"** tab
3. **Look for your site:** `https://onblick.sharepoint.com/sites/DocEnricher-PoC`
4. **If NOT listed:**
   - Click **"Add site"** or **"Add content source"**
   - Enter your site URL: `https://onblick.sharepoint.com/sites/DocEnricher-PoC`
   - Click **"Add"** or **"Save"**

**Option B: SharePoint is NOT Listed (Rare)**

If SharePoint is not in the content sources list at all:

1. Click **"Add content source"** (top right)
2. Select **"SharePoint"** from the list
3. **Configure:**
   - **Name:** SharePoint Sites
   - **Source type:** SharePoint
   - **Site URL:** `https://onblick.sharepoint.com/sites/DocEnricher-PoC`
   - **Scope:** Entire site or specific library
4. Click **"Save"**

---

### **Step 4: Verify Site is Added**

1. **Go back to Content sources**
2. **Click on SharePoint**
3. **Verify** your site appears in the list:
   - Site: `https://onblick.sharepoint.com/sites/DocEnricher-PoC`
   - Status: Should show "Active" or "Indexing"

---

### **Step 5: Trigger Indexing (If Needed)**

**Automatic Indexing:**
- SharePoint sites are usually indexed automatically
- Wait 24-48 hours for initial indexing

**Manual Refresh (If Available):**
1. In Content sources → SharePoint
2. Find your site
3. Click **"Refresh"** or **"Re-index"** button (if available)
4. This forces immediate re-indexing

---

## 🔍 Troubleshooting

### **Issue: "Add site" button is grayed out or missing**

**Solution:**
- You need **Global Admin** or **Search Administrator** role
- Contact your IT admin to add the site
- Or request Search Administrator permissions

---

### **Issue: Site added but not indexing**

**Check:**
1. **Site permissions:** Site must be accessible to the search service account
2. **Site settings:** Go to Site Settings → Search and offline availability
   - Ensure "Allow this site to appear in search results" is **Yes**
3. **Wait time:** Initial indexing can take 24-48 hours

---

### **Issue: Can't find "Content sources" option**

**Solution:**
- You might not have admin access
- Request **Search Administrator** role from Global Admin
- Or ask your IT admin to add the site

---

## 📋 Quick Checklist

- [ ] Access Microsoft Search Admin Center
- [ ] Navigate to Content sources
- [ ] Check if SharePoint is listed
- [ ] If SharePoint exists, check if your site is included
- [ ] If site not included, click "Add site"
- [ ] Enter site URL: `https://onblick.sharepoint.com/sites/DocEnricher-PoC`
- [ ] Save changes
- [ ] Verify site appears in list
- [ ] Wait 24-48 hours for indexing (or trigger refresh if available)

---

## 🎯 Expected Result

After adding your site:

1. **Site appears** in Content sources → SharePoint → Sites list
2. **Status shows** "Active" or "Indexing"
3. **Within 24-48 hours**, documents become searchable
4. **Copilot can find** your documents in Teams

---

## ⚡ Quick Reference

**Your Site URL:**
```
https://onblick.sharepoint.com/sites/DocEnricher-PoC
```

**ProcessedDocs Folder:**
```
https://onblick.sharepoint.com/sites/DocEnricher-PoC/Shared Documents/ProcessedDocs
```

**Admin Center:**
```
https://admin.microsoft.com/Adminportal/Home#/MicrosoftSearch
```

---

## 🚀 After Adding Site

1. **Wait 24-48 hours** for initial indexing
2. **Test in Teams:** Ask Copilot "Show me documents in DocEnricher-PoC site"
3. **Verify documents appear** in search results
4. **Test QueryAnswer:** Run `.\VERIFY_COPILOT_SIMPLE.ps1` (with Function App running)

---

**That's it! Follow these steps to add your SharePoint site to Microsoft Search indexing.**

