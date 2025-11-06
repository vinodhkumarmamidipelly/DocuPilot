# Quick Guide: Get Tenant ID (No Azure Portal Needed)

## ⚡ Fastest Method: Graph Explorer

### Step 1: Go to Graph Explorer
https://developer.microsoft.com/graph/graph-explorer

### Step 2: Sign In
- Click "Sign in with Microsoft"
- Use your work account (`@rigaps.com` or similar)
- Grant permissions when prompted

### Step 3: Run Query
In the query box, enter:
```
https://graph.microsoft.com/v1.0/organization
```

### Step 4: Click "Run query"

### Step 5: Get Tenant ID
Look at the response - you'll see:
```json
{
  "value": [{
    "id": "12345678-1234-1234-1234-123456789012"  ← This is your Tenant ID
  }]
}
```

**Copy the `id` value** - That's your Tenant ID! ✅

---

## Alternative: PowerShell Script

I've created `GetTenantID.ps1` for you:

1. **Open PowerShell**
2. **Navigate to project folder**
3. **Run**: `.\GetTenantID.ps1`
4. **Sign in** when prompted
5. **Tenant ID** will be displayed and copied to clipboard

**No admin access needed!** Just your work account.

---

## If You Can't Access Either

**Ask your IT administrator:**
> "I need the Azure AD Tenant ID for SMEPilot configuration. Can you provide it?"

They can get it in 30 seconds from Azure Portal.

---

## For Testing (Temporary)

If you just need to test the flow right now:
- Use `"default"` as tenantId in Power Automate
- SMEPilot will work (in test mode)
- Get real Tenant ID later

```json
{
  "tenantId": "default"
}
```

---

**Recommended: Use Graph Explorer - Takes 2 minutes!** 🚀

