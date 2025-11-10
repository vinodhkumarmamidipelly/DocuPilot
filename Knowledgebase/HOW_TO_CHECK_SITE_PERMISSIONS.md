# How to Check Site Permissions - Step by Step

## 🎯 Exact Steps to Follow

---

## ✅ Method 1: Direct URL (Easiest!)

**Copy and paste this URL:**

```
https://onblick.sharepoint.com/sites/DocEnricher-PoC/_layouts/15/user.aspx
```

**This goes directly to Site Permissions page!**

---

## ✅ Method 2: Through Site Settings

**If "Site Permissions" is not visible, try this:**

1. **Look for "Users and Permissions" section** (might be collapsed)
2. **Or look for "People and groups"**
3. **Or try this URL:**
   ```
   https://onblick.sharepoint.com/sites/DocEnricher-PoC/_layouts/15/people.aspx
   ```

---

## ✅ Method 3: Check from Site Homepage

1. **Go to site homepage:**
   - `https://onblick.sharepoint.com/sites/DocEnricher-PoC`

2. **Look for gear icon (⚙️)** in top right corner
3. **Click gear icon** → **"Site permissions"**
4. **Or click gear icon** → **"Site settings"** → Look for "Users and Permissions"

---

## 🔍 What to Check Once You're There

### **Step 1: See Who Has Access**

**Look at the list of groups/users:**
- **"Site Owners"** - Full control
- **"Site Members"** - Edit access
- **"Site Visitors"** - Read access
- **"Everyone"** - Should be here for Copilot to work
- **"All authenticated users"** - Alternative to Everyone

---

### **Step 2: Check if "Everyone" Has Access**

**If "Everyone" is NOT in the list:**

1. **Click "Grant permissions"** button (usually at top)
2. **In the "Share" dialog:**
   - Type: **"Everyone"**
   - Select: **"Everyone"** from dropdown
   - Permission level: **"Read"** (or "Site Visitors")
   - Click **"Share"**

**If "Everyone" IS in the list:**
- ✅ Good! Check permission level is "Read" or higher
- If it's "No Access", remove and re-add with "Read" permission

---

### **Step 3: Verify Permission Level**

**For each group/user, check permission level:**
- **"Read"** or **"Site Visitors"** = ✅ Good (Copilot can read)
- **"Edit"** or **"Site Members"** = ✅ Good (includes read)
- **"Full Control"** or **"Site Owners"** = ✅ Good (includes read)
- **"No Access"** = ❌ Bad (Copilot can't access)

---

## 🎯 Alternative: Check Document-Level Permissions

**If site permissions look OK, check document permissions:**

### **Step 1: Go to Documents**

1. **Go to:** `https://onblick.sharepoint.com/sites/DocEnricher-PoC/Shared Documents/ProcessedDocs`
2. **Or click "Documents"** in left navigation

### **Step 2: Check a Document**

1. **Right-click on an enriched document** (e.g., "Alerts - Copy_enriched.docx")
2. **Click "Details"** or **"Manage access"**
3. **Check who has access:**
   - Should see **"Everyone"** or **"All authenticated users"**
   - Permission should be **"Read"** or higher

### **Step 3: Share Document if Needed**

**If document is restricted:**

1. **Right-click document** → **"Share"**
2. **Type:** **"Everyone"**
3. **Select:** **"Everyone"** from dropdown
4. **Permission:** **"Read"**
5. **Click "Share"**

---

## 📋 Quick Checklist

- [ ] **Access Site Permissions** (use direct URL: `/_layouts/15/user.aspx`)
- [ ] **Check if "Everyone" has Read access**
- [ ] **If not, grant "Everyone" Read permission**
- [ ] **Check document permissions** (right-click → Details)
- [ ] **Share documents with "Everyone" if needed**
- [ ] **Click "Reindex site"** (from Search settings)

---

## 🚀 Recommended: Do These 3 Things

### **1. Check Site Permissions (5 minutes)**

**Use direct URL:**
```
https://onblick.sharepoint.com/sites/DocEnricher-PoC/_layouts/15/user.aspx
```

**Verify:**
- "Everyone" has Read access
- If not, grant it

---

### **2. Check Document Permissions (5 minutes)**

**Go to:**
```
https://onblick.sharepoint.com/sites/DocEnricher-PoC/Shared Documents/ProcessedDocs
```

**For each enriched document:**
- Right-click → "Details" or "Manage access"
- Verify "Everyone" has Read access
- If not, share with "Everyone"

---

### **3. Trigger Re-indexing (1 minute)**

**Go back to Search settings:**
```
https://onblick.sharepoint.com/sites/DocEnricher-PoC/_layouts/15/srchvis.aspx
```

**Click "Reindex site" button**

**Wait 24-48 hours** for indexing to complete

---

## 💡 Important Notes

### **Why Permissions Matter:**

**Copilot needs to read documents:**
- If "Everyone" doesn't have access → Copilot can't read
- If documents are restricted → Copilot can't read
- Site search enabled + proper permissions = Copilot can access

### **Permission Levels:**

- **"Read"** = ✅ Copilot can read documents
- **"Edit"** = ✅ Copilot can read documents (includes read)
- **"Full Control"** = ✅ Copilot can read documents (includes read)
- **"No Access"** = ❌ Copilot cannot access

---

## ✅ Summary

**What to Do:**

1. ✅ **Check Site Permissions:**
   - URL: `/_layouts/15/user.aspx`
   - Verify "Everyone" has Read access
   - Grant if needed

2. ✅ **Check Document Permissions:**
   - Go to ProcessedDocs folder
   - Right-click documents → Details
   - Verify "Everyone" has Read access
   - Share if needed

3. ✅ **Trigger Re-indexing:**
   - Go to Search settings
   - Click "Reindex site"
   - Wait 24-48 hours

**After these steps, Copilot should be able to access documents!**

---

## 🎯 Quick Action Plan

**Right Now (10 minutes):**

1. **Check Site Permissions:** `/_layouts/15/user.aspx`
2. **Check Document Permissions:** Go to ProcessedDocs folder
3. **Trigger Re-indexing:** Click "Reindex site"

**Then Wait (24-48 hours):**

4. **Wait for indexing** to complete
5. **Test Copilot** in Teams again

**Alternative (Works Now!):**

6. **Use QueryAnswer API** - Works immediately!

---

**Start with the direct URL for Site Permissions - that's the easiest way!** 🚀

