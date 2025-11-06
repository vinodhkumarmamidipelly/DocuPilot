# Quick Guide: Get SharePoint IDs Using Graph Explorer

## Step-by-Step Instructions

### Step 1: Open Graph Explorer
Go to: **https://developer.microsoft.com/graph/graph-explorer**

### Step 2: Sign In
1. Click **"Sign in to Graph Explorer"** (top right)
2. Sign in with: `vinodkumar@rigaps.com`
3. Grant permissions (if prompted)

### Step 3: Get Site ID

**In the query box, paste:**
```
https://graph.microsoft.com/v1.0/sites/onblick.sharepoint.com:/sites/DocEnricher-PoC
```

**Click "Run query"**

**You'll see JSON response. Find:**
```json
{
  "id": "onblick.sharepoint.com,12345678-1234-1234-1234-123456789012,abc123...",
  ...
}
```

**Copy the entire `id` value** → This is your **Site ID**

---

### Step 4: Get Drive ID

**Replace `{SITE-ID}` in this query with your Site ID:**
```
https://graph.microsoft.com/v1.0/sites/{SITE-ID}/drives
```

**Example:**
```
https://graph.microsoft.com/v1.0/sites/onblick.sharepoint.com,12345678-1234-1234-1234-123456789012,abc123.../drives
```

**Click "Run query"**

**You'll see a list of drives. Find the one named "Documents":**
```json
{
  "value": [
    {
      "name": "Documents",
      "id": "b!xyz123...",
      ...
    }
  ]
}
```

**Copy the `id` value** → This is your **Drive ID**

---

### Step 5: Extract Tenant ID (Optional)

**From Site ID:** `onblick.sharepoint.com,TENANT-ID,SITE-ID`

**Tenant ID is the middle part** (between the commas)

---

## 📋 What You'll Get

After these steps, you'll have:
- ✅ **Site ID:** `onblick.sharepoint.com,12345678-1234-1234-1234-123456789012,abc123...`
- ✅ **Drive ID:** `b!xyz123...`
- ✅ **Tenant ID:** `12345678-1234-1234-1234-123456789012`

---

## 🚀 Next Step

Once you have these IDs, we'll use them in **Step 3** to create the Graph subscription.

**Share the IDs you get, and I'll help you create the subscription!**

