# Delete Old Subscriptions

You have **TWO active subscriptions** causing duplicate notifications:

1. **Old Subscription:** `8798609a-c12a-4a07-88f4-ea66c93f6776` (expires: 2025-11-08)
2. **New Subscription:** `8b2d5c0d-fdc1-4094-924a-b8dda375b0a2` (expires: 2025-11-08)

## Quick Fix: Delete Old Subscription

### Option 1: Using Graph Explorer (Easiest)

1. **Go to:** https://developer.microsoft.com/graph/graph-explorer
2. **Sign in** with your work account
3. **Query to list subscriptions:**
   ```
   GET https://graph.microsoft.com/v1.0/subscriptions
   ```
4. **Find subscription ID:** `8798609a-c12a-4a07-88f4-ea66c93f6776`
5. **Delete it:**
   ```
   DELETE https://graph.microsoft.com/v1.0/subscriptions/8798609a-c12a-4a07-88f4-ea66c93f6776
   ```

### Option 2: Using PowerShell

```powershell
# Connect to Graph
Connect-MgGraph -Scopes "Subscription.ReadWrite.All"

# List subscriptions
Get-MgSubscription

# Delete old subscription
Remove-MgSubscription -SubscriptionId "8798609a-c12a-4a07-88f4-ea66c93f6776"
```

### Option 3: Wait for Expiration

The old subscription will expire automatically on **2025-11-08**, but that's 3 days away.

---

## After Deleting Old Subscription

- ✅ Only ONE subscription will be active
- ✅ Duplicate notifications will stop
- ✅ File processing will work correctly

---

## Why This Happens

When you create a new subscription, the old one doesn't automatically delete. Graph API allows multiple subscriptions for the same resource. Each metadata update triggers notifications from BOTH subscriptions, causing duplicate processing.

