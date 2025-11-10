# Migration Guide: Console.WriteLine → ILogger

## 📋 Overview

This guide helps migrate from `Console.WriteLine` to `ILogger` for proper file logging.

---

## 🔄 Migration Pattern

### **Pattern 1: Simple Info Messages**

**Before:**
```csharp
Console.WriteLine($"Processing file: {fileName}");
```

**After:**
```csharp
_logger.LogInformation("Processing file: {FileName}", fileName);
```

---

### **Pattern 2: Warning Messages**

**Before:**
```csharp
Console.WriteLine($"⚠️ File {fileName} is locked");
```

**After:**
```csharp
_logger.LogWarning("⚠️ File {FileName} is locked", fileName);
```

---

### **Pattern 3: Error Messages**

**Before:**
```csharp
Console.WriteLine($"❌ Error processing {fileName}: {ex.Message}");
```

**After:**
```csharp
_logger.LogError(ex, "❌ Error processing {FileName}: {Error}", fileName, ex.Message);
```

---

### **Pattern 4: Debug Messages**

**Before:**
```csharp
Console.WriteLine($"Debug: Metadata = {JsonConvert.SerializeObject(metadata)}");
```

**After:**
```csharp
_logger.LogDebug("Debug: Metadata = {Metadata}", JsonConvert.SerializeObject(metadata));
```

---

### **Pattern 5: Multiple Parameters**

**Before:**
```csharp
Console.WriteLine($"Processing file {fileName} (ID: {itemId}) in Drive {driveId}");
```

**After:**
```csharp
_logger.LogInformation("Processing file {FileName} (ID: {ItemId}) in Drive {DriveId}", 
    fileName, itemId, driveId);
```

---

## 📝 Quick Reference

| Console.WriteLine | ILogger Equivalent |
|------------------|-------------------|
| `Console.WriteLine("Info")` | `_logger.LogInformation("Info")` |
| `Console.WriteLine($"Value: {x}")` | `_logger.LogInformation("Value: {X}", x)` |
| `Console.WriteLine("Warning")` | `_logger.LogWarning("Warning")` |
| `Console.WriteLine("Error")` | `_logger.LogError("Error")` |
| `Console.WriteLine($"Error: {ex}")` | `_logger.LogError(ex, "Error: {Message}", ex.Message)` |

---

## ✅ Benefits of Structured Logging

1. **Searchable:** Easy to search logs by parameter values
2. **Structured:** JSON format for parsing
3. **Context:** Automatic context (class name, timestamp)
4. **Levels:** Filter by log level (Info, Warning, Error)
5. **Performance:** Async file writing

---

## 🚀 Migration Steps

1. **Add ILogger to constructor** (already done for ProcessSharePointFile)
2. **Replace Console.WriteLine** with appropriate Log method
3. **Use structured parameters** (e.g., `{FileName}` instead of string interpolation)
4. **Test logging** - verify logs appear in files

---

## 📁 Log File Location

- **Local:** `%TEMP%\Logs\sme-pilot-{Date}.log`
- **Azure:** `D:\home\LogFiles\sme-pilot-{Date}.log`

---

## 🔍 Viewing Logs

```powershell
# View latest log
Get-Content "$env:TEMP\Logs\sme-pilot-$(Get-Date -Format 'yyyy-MM-dd').log" -Tail 50

# Search for errors
Select-String -Path "$env:TEMP\Logs\sme-pilot-*.log" -Pattern "Error"
```

---

**Status:** ✅ Serilog configured, ready for migration

