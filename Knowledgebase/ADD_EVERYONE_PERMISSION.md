# How to Add "Everyone" Permission - Step by Step

## 🎯 What You're Seeing

**Current Groups:**
- ✅ DocEnricher-PoC Members - Edit
- ✅ DocEnricher-PoC Owners - Full Control  
- ✅ DocEnricher-PoC Visitors - Read

**Missing:** ❌ "Everyone" group (needed for Copilot)

---

## ✅ Exact Steps to Add "Everyone"

### **Step 1: Click "Grant Permissions"**

**On the page you're viewing:**

1. **Look at the ribbon/toolbar** at the top (below "PERMISSIONS" tab)
2. **Find "Grant Permissions"** button (has icon of person with plus sign)
3. **Click "Grant Permissions"**

---

### **Step 2: Add "Everyone"**

**A dialog box will open:**

1. **In the "Share" dialog:**
   - **Type:** `Everyone`
   - **Select:** "Everyone" from the dropdown (it should appear)
   - **Or type:** `All authenticated users` (alternative)

2. **Permission Level:**
   - **Select:** "Read" or "DocEnricher-PoC Visitors"
   - **This gives read-only access** (safe for Copilot)

3. **Uncheck "Send an email invitation"** (optional - not needed for Everyone)

4. **Click "Share"** or "OK"

---

### **Step 3: Verify "Everyone" Was Added**

**After clicking "Share":**

1. **You should see "Everyone"** in the permissions list
2. **Permission Level should show:** "Read"
3. **If it shows "No Access"**, click on it and change to "Read"

---

## 🔍 Alternative: Add to "Visitors" Group

**If "Everyone" doesn't work, add to Visitors group:**

1. **Click on "DocEnricher-PoC Visitors"** group
2. **Click "New"** or **"Add users"**
3. **Type:** `Everyone`
4. **Select:** "Everyone"
5. **Click "OK"**

**This adds "Everyone" to the Visitors group** (which has Read access)

---

## 📋 What You Should See After

**Permissions List Should Show:**
- ✅ DocEnricher-PoC Members - Edit
- ✅ DocEnricher-PoC Owners - Full Control
- ✅ DocEnricher-PoC Visitors - Read
- ✅ **Everyone - Read** ← NEW!

---

## 🎯 Quick Checklist

- [ ] Click "Grant Permissions" button
- [ ] Type "Everyone" in the dialog
- [ ] Select "Read" permission level
- [ ] Click "Share" or "OK"
- [ ] Verify "Everyone" appears in the list with "Read" permission

---

## ✅ After Adding "Everyone"

**Next Steps:**

1. ✅ **Check Document Permissions** (make sure documents are also shared with Everyone)
2. ✅ **Trigger Re-indexing** (go to Search settings → Click "Reindex site")
3. ⏳ **Wait 24-48 hours** for indexing
4. ✅ **Test Copilot** in Teams again

---

## 💡 Why This Matters

**Copilot needs to read documents:**
- Without "Everyone" permission → Copilot can't access documents
- With "Everyone" Read permission → Copilot can read documents
- "Read" permission is safe → Users can only read, not edit

---

## 🚀 Summary

**What to Do Right Now:**

1. **Click "Grant Permissions"** (top ribbon)
2. **Type "Everyone"** → Select "Read" → Click "Share"
3. **Verify "Everyone" appears** in the list

**That's it!** After this, Copilot should be able to access documents (after re-indexing).

---

**Click "Grant Permissions" now and add "Everyone" with Read access!** 🚀

