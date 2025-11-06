# Step-by-Step: Get SharePoint IDs Using Graph Explorer

## 🎯 Goal
Get **Site ID** and **Drive ID** from your SharePoint site using Graph Explorer.

---

## Step 1: Open Graph Explorer

1. **Open browser** → Go to: **https://developer.microsoft.com/graph/graph-explorer**
2. **Click "Sign in to Graph Explorer"** (top right corner)
3. **Sign in** with: `vinodkumar@rigaps.com`
4. **Grant permissions** if prompted (usually doesn't need admin consent)

---

## Step 2: Get Site ID

### 2.1: In the query box (top), paste this:
```
https://graph.microsoft.com/v1.0/sites/onblick.sharepoint.com:/sites/DocEnricher-PoC
```

### 2.2: Click **"Run query"** button (or press Enter)

### 2.3: Look at the response (JSON on the right side)

**Find this part:**
```json
{
  "id": "onblick.sharepoint.com,12345678-1234-1234-1234-123456789012,abc123...",
  "name": "DocEnricher-PoC",
  ...
}
```

### 2.4: Copy the entire `id` value
- It looks like: `onblick.sharepoint.com,12345678-1234-1234-1234-123456789012,abc123...`
- This is your **Site ID** ✅

---

## Step 3: Get Drive ID

### 3.1: Replace `{SITE-ID}` in this query with your Site ID:

**Template:**
```
https://graph.microsoft.com/v1.0/sites/{SITE-ID}/drives
```

**Example** (replace with your actual Site ID):
```
https://graph.microsoft.com/v1.0/sites/onblick.sharepoint.com,12345678-1234-1234-1234-123456789012,abc123.../drives
```

### 3.2: Paste into query box and click **"Run query"**

### 3.3: Look at the response

**You'll see a list of drives:**
```json
{
  "value": [
    {
      "name": "Documents",
      "id": "b!xyz123...",
      "driveType": "documentLibrary",
      ...
    },
    {
      "name": "Shared Documents",
      "id": "b!abc456...",
      ...
    }
  ]
}
```

### 3.4: Find the drive named **"Documents"**

### 3.5: Copy the `id` value from that drive
- It looks like: `b!xyz123...`
- This is your **Drive ID** ✅

---

## Step 4: Extract Tenant ID (Optional)

**From your Site ID:**
- Format: `domain,TENANT-ID,SITE-ID`
- **Tenant ID** is the **middle part** (between the commas)

**Example:**
- Site ID: `onblick.sharepoint.com,12345678-1234-1234-1234-123456789012,abc123...`
- Tenant ID: `12345678-1234-1234-1234-123456789012`

---

## ✅ What You Should Have Now

After these steps, copy these values:

1. **Site ID:** `onblick.sharepoint.com,12345678-...,abc123...`
2. **Drive ID:** `b!xyz123...`
3. **Tenant ID:** `12345678-1234-1234-1234-123456789012` (optional)

---

## 🚀 Next Step

**Once you have the IDs, share them with me and I'll help you:**
- Create the Graph subscription (Step 3)
- Set up automatic triggers
- Test the complete flow

---

## 📸 Screenshot Reference

**Graph Explorer looks like this:**
```
┌─────────────────────────────────────────┐
│ [Sign in]  Graph Explorer               │
├─────────────────────────────────────────┤
│ Query: [https://graph.microsoft.com/...]│
│         [Run query]                     │
├─────────────────────────────────────────┤
│ Response:                               │
│ {                                       │
│   "id": "...",                          │
│   ...                                   │
│ }                                       │
└─────────────────────────────────────────┘
```

---

**Ready? Follow the steps above and share the IDs you get!** 🎯

