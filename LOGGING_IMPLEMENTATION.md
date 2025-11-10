# File Logging Implementation Guide

## 📋 Overview

Implemented Serilog-based file logging to replace Console.WriteLine with proper structured logging that works in both development and production.

---

## ✅ What Was Implemented

### **1. Serilog Configuration**
- **Package:** Serilog with file sink
- **Location:** `Program.cs`
- **Features:**
  - File logging with daily rotation
  - JSON format (compact) for easy parsing
  - 30-day retention
  - 10 MB file size limit
  - Automatic cleanup

### **2. Log File Location**
- **Local Development:** `%TEMP%\Logs\sme-pilot-{Date}.log`
- **Azure Functions:** `D:\home\LogFiles\sme-pilot-{Date}.log`
- **Format:** `sme-pilot-2025-11-09.log`

### **3. Log Levels**
- **Information:** Normal operations, processing steps
- **Warning:** Non-critical issues, retries
- **Error:** Failures, exceptions
- **Fatal:** Application startup failures

### **4. Migration Strategy**
- Replace `Console.WriteLine` with `_logger.LogInformation/Warning/Error`
- Keep structured logging with context
- Maintain existing log messages (just change output method)

---

## 📁 Log File Structure

### **File Naming:**
```
sme-pilot-2025-11-09.log
sme-pilot-2025-11-10.log
sme-pilot-2025-11-11.log
```

### **Log Format (JSON):**
```json
{
  "@t": "2025-11-09T02:03:37.728Z",
  "@l": "Information",
  "@mt": "Received Graph notification with {Count} items",
  "Count": 1,
  "SourceContext": "SMEPilot.FunctionApp.Functions.ProcessSharePointFile"
}
```

---

## 🔧 Configuration

### **Log Retention:**
- **Days:** 30 days (configurable in `Program.cs`)
- **File Size:** 10 MB per file
- **Rotation:** Daily + on file size limit

### **Log Levels:**
- **Default:** Information
- **Microsoft/System:** Warning (reduces noise)

---

## 📝 Usage Examples

### **Before (Console.WriteLine):**
```csharp
Console.WriteLine($"Processing file: {fileName}");
```

### **After (ILogger):**
```csharp
_logger.LogInformation("Processing file: {FileName}", fileName);
```

### **With Context:**
```csharp
_logger.LogInformation("Processing Graph notification: File {FileName} (ID: {ItemId}) in Drive {DriveId}", 
    fileName, itemId, driveId);
```

### **Error Logging:**
```csharp
_logger.LogError(ex, "Failed to process file {FileName}: {Error}", fileName, ex.Message);
```

---

## 🚀 Benefits

1. **Production Ready:** Logs persist to files in production
2. **Structured:** JSON format for easy parsing/searching
3. **Rotated:** Automatic file rotation prevents disk space issues
4. **Retained:** 30-day retention for troubleshooting
5. **Searchable:** Easy to find issues in log files
6. **Performance:** Async file writing doesn't block execution

---

## 📂 Next Steps

1. **Replace Console.WriteLine:** Update all files to use ILogger
2. **Test Logging:** Verify logs are written correctly
3. **Monitor:** Check log files in production
4. **Troubleshoot:** Use logs to find issues easily

---

## 🔍 Finding Logs

### **Local Development:**
```powershell
# Windows
Get-ChildItem "$env:TEMP\Logs\sme-pilot-*.log"

# View latest log
Get-Content "$env:TEMP\Logs\sme-pilot-$(Get-Date -Format 'yyyy-MM-dd').log" -Tail 50
```

### **Azure Functions:**
- Logs are in: `D:\home\LogFiles\`
- Access via: Azure Portal → Function App → Log stream
- Or: Kudu console → `D:\home\LogFiles\`

---

## ⚙️ Customization

### **Change Log Location:**
Edit `Program.cs`:
```csharp
var logPath = "C:\\Custom\\Logs\\Path";
```

### **Change Retention:**
```csharp
retainedFileCountLimit: 60 // Keep 60 days
```

### **Change File Size:**
```csharp
fileSizeLimitBytes: 50 * 1024 * 1024 // 50 MB
```

---

**Status:** ✅ Serilog configured, ready to migrate Console.WriteLine calls

