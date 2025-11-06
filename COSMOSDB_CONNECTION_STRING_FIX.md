# CosmosDB Connection String Fix

## Issue
The CosmosDB client is failing to initialize with error:
```
System.FormatException: The input is not a valid Base-64 string
```

## Root Cause
The AccountKey in the connection string contains special characters (`+`, `/`) that might be getting corrupted during JSON parsing or environment variable reading.

## Solution

### Step 1: Check the Logs
After restarting your Function App, look for these logs in the output window:
```
[COSMOS] Initializing CosmosDB connection...
   Connection String: AccountEndpoint=https://localhost:8081/;AccountKey=***MASKED***
   Connection String Length: [should be around 150-200]
   First 50 chars: [should show AccountEndpoint=https://localhost:8081/;AccountKey=C]
   Last 50 chars: [should show ...J3w3AAABACOG41Io==]
   AccountKey Length: [should be 88]
   AccountKey (first 20 chars): C2y6yDjf5/R+ob0N8A7Cgv
```

### Step 2: Verify Connection String Format
Your `local.settings.json` should have:
```json
{
  "Values": {
    "Cosmos_ConnectionString": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jr3g=="
  }
}
```

**Important:** Make sure there are:
- ✅ No extra spaces before or after the connection string
- ✅ No line breaks in the connection string
- ✅ The AccountKey starts with `C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jr3g==`
- ✅ The connection string is all on one line

### Step 3: Alternative Format (If Still Failing)
If the connection string still fails, try URL-encoding the AccountKey:

**Current (might have issues):**
```
AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jr3g==
```

**Try this (URL-encoded):**
```
AccountKey=C2y6yDjf5%2FR%2Bob0N8A7Cgv30VRDJIWEHLM%2B4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw%2FJr3g%3D%3D
```

Where:
- `/` becomes `%2F`
- `+` becomes `%2B`
- `=` becomes `%3D`

**Full connection string with URL-encoded key:**
```json
{
  "Values": {
    "Cosmos_ConnectionString": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5%2FR%2Bob0N8A7Cgv30VRDJIWEHLM%2B4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw%2FJr3g%3D%3D"
  }
}
```

### Step 4: Verify CosmosDB Emulator is Running
1. Check Windows System Tray - CosmosDB Emulator icon should be visible
2. Open browser: `https://localhost:8081/_explorer/index.html`
3. If you see the Data Explorer, the emulator is running

### Step 5: Restart Function App
After updating `local.settings.json`:
1. **Stop** the Function App (Shift+F5 in Visual Studio)
2. **Start** it again (F5)
3. **Check logs** for `[COSMOS]` messages

### Expected Output (Success)
```
[COSMOS] Initializing CosmosDB connection...
   Connection String: AccountEndpoint=https://localhost:8081/;AccountKey=***MASKED***
   Database: SMEPilotDB
   Container: Embeddings
   Connection String Length: 150
   First 50 chars: AccountEndpoint=https://localhost:8081/;AccountKey=C
   Last 50 chars: Qy67XIw/Jr3g==
   AccountKey Length: 88
   AccountKey (first 20 chars): C2y6yDjf5/R+ob0N8A7Cgv
✅ [COSMOS] CosmosDB client initialized successfully
```

### If Still Failing
The code will now fall back to **mock mode** automatically, so your Function App won't crash. You'll see:
```
❌ [COSMOS] Failed to initialize CosmosDB client: FormatException: ...
   Falling back to mock mode (no CosmosDB operations will be performed)
```

**In mock mode:**
- Document enrichment will still work ✅
- Embeddings will be generated ✅
- But embeddings will NOT be stored in CosmosDB ⚠️
- QueryAnswer will return "No relevant documents found" ⚠️

## Next Steps
1. Check the logs to see what's actually being received
2. Try the URL-encoded format if needed
3. Verify CosmosDB Emulator is running
4. Restart Function App and test again

