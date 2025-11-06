# Fix: Use Correct SharePoint Query

## ❌ What You Did
You ran a user query (`/users` or `/me`) which returned your user profile.

## ✅ What You Need to Do

### In Graph Explorer:

1. **Clear the query box** (or type new query)

2. **Paste this EXACT query:**
```
https://graph.microsoft.com/v1.0/sites/onblick.sharepoint.com:/sites/DocEnricher-PoC
```

3. **Click "Run query"** button

4. **You should see response like this:**
```json
{
  "@odata.context": "https://graph.microsoft.com/v1.0/$metadata#sites/$entity",
  "id": "onblick.sharepoint.com,12345678-1234-1234-1234-123456789012,abc123...",
  "name": "DocEnricher-PoC",
  "displayName": "DocEnricher-PoC",
  "webUrl": "https://onblick.sharepoint.com/sites/DocEnricher-PoC",
  ...
}
```

5. **Copy the `id` value** → This is your **Site ID** ✅

---

## 📋 Quick Reference

**Query 1 (Get Site ID):**
```
https://graph.microsoft.com/v1.0/sites/onblick.sharepoint.com:/sites/DocEnricher-PoC
```

**Query 2 (Get Drive ID - AFTER you have Site ID):**
```
https://graph.microsoft.com/v1.0/sites/{YOUR-SITE-ID}/drives
```

---

## 🎯 Expected Response Format

**For Site Query:**
- Should have `"id"` field starting with `onblick.sharepoint.com,`
- Should have `"name"` field with `"DocEnricher-PoC"`
- Should have `"webUrl"` field

**NOT:**
- User profile info
- `"displayName"` with your name
- `"mail"` field

---

**Try again with the SharePoint site query above!** 🚀

