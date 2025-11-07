# MongoDB Authentication Troubleshooting

## 🔴 Current Issue

**Error:** `Unable to authenticate using sasl protocol mechanism SCRAM-SHA-1`

---

## ✅ What I've Fixed

1. **Updated Connection String** to use `SCRAM-SHA-256`:
   ```
   mongodb://SMEPilottuser1:J38Cwba23mLI1z@40.88.230.59:27017/SMEPilotDb?authSource=admin&authMechanism=SCRAM-SHA-256
   ```

2. **Added Connection Test** - Will test authentication during initialization

---

## 🔧 If Still Failing - Try These Solutions

### **Option 1: Try SCRAM-SHA-1 Explicitly**

If your MongoDB server uses SCRAM-SHA-1, update connection string:

```json
"Mongo_ConnectionString": "mongodb://SMEPilottuser1:J38Cwba23mLI1z@40.88.230.59:27017/SMEPilotDb?authSource=admin&authMechanism=SCRAM-SHA-1"
```

### **Option 2: Try Different authSource**

The user might be in a different database:

```json
"Mongo_ConnectionString": "mongodb://SMEPilottuser1:J38Cwba23mLI1z@40.88.230.59:27017/SMEPilotDb?authSource=SMEPilotDb&authMechanism=SCRAM-SHA-256"
```

### **Option 3: Verify User Exists**

**On MongoDB Server, run:**
```javascript
use admin
db.getUsers()
```

**Check if user `SMEPilottuser1` exists and has correct password.**

### **Option 4: Create/Update User**

**If user doesn't exist or password is wrong:**

```javascript
use admin
db.createUser({
  user: "SMEPilottuser1",
  pwd: "J38Cwba23mLI1z",
  roles: [
    { role: "readWrite", db: "SMEPilotDb" }
  ]
})
```

**Or update existing user:**
```javascript
use admin
db.updateUser("SMEPilottuser1", {
  pwd: "J38Cwba23mLI1z"
})
```

### **Option 5: Test Connection Manually**

**From your local machine, test connection:**

```bash
mongosh "mongodb://SMEPilottuser1:J38Cwba23mLI1z@40.88.230.59:27017/SMEPilotDb?authSource=admin&authMechanism=SCRAM-SHA-256"
```

**If this works, the connection string is correct.**

---

## 🔍 Check MongoDB Server Configuration

### **1. Check MongoDB Version**

```bash
mongosh --version
```

**Different MongoDB versions use different auth mechanisms:**
- MongoDB 3.0+: SCRAM-SHA-1
- MongoDB 4.0+: SCRAM-SHA-256 (preferred)

### **2. Check Authentication Settings**

**In MongoDB config file (`mongod.conf`):**
```yaml
security:
  authorization: enabled
```

### **3. Check Network Access**

**Verify MongoDB is accessible from Azure Functions:**
- Firewall allows port 27017
- MongoDB binds to correct IP (0.0.0.0 or specific IP)
- Network security groups allow connection

---

## 🎯 Quick Fixes to Try

### **Fix 1: Remove authMechanism (Let MongoDB Auto-Detect)**

```json
"Mongo_ConnectionString": "mongodb://SMEPilottuser1:J38Cwba23mLI1z@40.88.230.59:27017/SMEPilotDb?authSource=admin"
```

### **Fix 2: Try Without authSource**

```json
"Mongo_ConnectionString": "mongodb://SMEPilottuser1:J38Cwba23mLI1z@40.88.230.59:27017/SMEPilotDb"
```

### **Fix 3: URL Encode Special Characters**

If password has special characters, URL encode them:
- `@` becomes `%40`
- `:` becomes `%3A`
- etc.

---

## 📋 Verification Steps

1. **Test connection from MongoDB server itself:**
   ```bash
   mongosh -u SMEPilottuser1 -p J38Cwba23mLI1z --authenticationDatabase admin
   ```

2. **Test from Azure Function App location:**
   - Use `mongosh` or MongoDB Compass
   - Try connection string directly

3. **Check MongoDB logs:**
   ```bash
   # On MongoDB server
   tail -f /var/log/mongodb/mongod.log
   ```

---

## ✅ Expected Success Output

When authentication works, you should see:
```
🔍 [MONGO] Initializing MongoDB connection...
🔍 [MONGO] Testing connection...
✅ [MONGO] Connection test successful
✅ [MONGO] MongoDB client initialized successfully
✅ [MONGO] Indexes created/verified
```

---

## 🚨 Common Issues

1. **Wrong Password** - Verify password is correct
2. **User Doesn't Exist** - Create user in MongoDB
3. **Wrong authSource** - User might be in different database
4. **Firewall Blocking** - Check network access
5. **MongoDB Version Mismatch** - Different auth mechanisms

---

## 📞 Next Steps

1. **Restart Function App** with updated connection string
2. **Check logs** for connection test result
3. **If still failing**, try the alternative connection strings above
4. **Verify user exists** on MongoDB server
5. **Test connection manually** from command line

---

**Current Connection String (Updated):**
```
mongodb://SMEPilottuser1:J38Cwba23mLI1z@40.88.230.59:27017/SMEPilotDb?authSource=admin&authMechanism=SCRAM-SHA-256
```

Try restarting the Function App and check if authentication works now!

