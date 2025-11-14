# Edge Cases & Permissions

## Edge Cases the Enrichment Logic Handles

### Document Structure Edge Cases

#### Documents with no headings:
- **Fallback:** Use first 2 non-empty paragraphs as "Overview".
- **Handling:** Content mapper identifies content without explicit headings and maps to Overview section.

#### Documents with odd spacing / multiple consecutive line breaks:
- **Normalized:** Converted to single paragraph breaks.
- **Handling:** OpenXML processing normalizes whitespace and line breaks.

#### Documents with trailing empty sections/section breaks:
- **Removed:** Trailing empty paragraphs removed, keep last SectionProperties only.
- **Handling:** Template filler sanitizes document structure before final save.

#### Partially filled revision history:
- **Revision table builder:** Uses available rows (no empty rows inserted).
- **Handling:** Revision history expansion only adds rows with actual data.

#### Documents containing only images:
- **Fallback:** First image caption used in Overview section.
- **Handling:** Images preserved in enriched doc, captions extracted for content mapping.

#### Documents with embedded objects (OLE):
- **Status:** Not fully supported in v1.
- **Handling:** Embedded OLE objects preserved as-is when possible.

### 1. File Processing Edge Cases

#### 1.1 Large Files (>10MB)
**Scenario:** User uploads file larger than configured limit

**Handling:**
- Check file size before processing
- If too large, return error message
- Update metadata: `SMEPilot_Status = "FileTooLarge"`
- Log warning with file size
- Option: Process asynchronously with Durable Functions

**Error Message:**
```
File "{fileName}" is too large ({size}MB). Maximum size: {maxSize}MB.
Please split the file or contact administrator.
```

#### 1.2 Unsupported File Formats
**Scenario:** User uploads file with unsupported extension

**Handling:**
- Validate file extension against supported list
- Return error immediately
- Update metadata: `SMEPilot_Status = "UnsupportedFormat"`
- Log error with file extension

**Supported Formats:**
- `.docx` - Word documents
- `.pptx` - PowerPoint presentations
- `.xlsx` - Excel spreadsheets
- `.pdf` - PDF documents
- `.png`, `.jpg`, `.jpeg`, `.gif`, `.bmp`, `.tiff` - Images

**Error Message:**
```
File "{fileName}" has unsupported format "{extension}".
Supported formats: DOCX, PPTX, XLSX, PDF, Images (PNG, JPG, etc.)
```

#### 1.3 Corrupted Files
**Scenario:** File is corrupted and cannot be opened

**Handling:**
- Try to open file
- Catch exception during extraction
- Update metadata: `SMEPilot_Status = "Corrupted"`
- Log error with exception details
- Do not retry (permanent failure)

**Error Message:**
```
File "{fileName}" appears to be corrupted and cannot be processed.
Please re-upload the file.
```

#### 1.4 Duplicate Uploads
**Scenario:** Same file uploaded multiple times

**Handling:**
- Check metadata: `SMEPilot_Enriched = True`
- If already processed, skip processing
- Log info message
- Return success (idempotent operation)

**Log Message:**
```
File "{fileName}" already processed. Skipping duplicate processing.
```

#### 1.5 Concurrent Processing
**Scenario:** Multiple webhooks received for same file

**Handling:**
- Use semaphore lock per file ID
- First request acquires lock
- Subsequent requests wait or skip
- Update metadata: `SMEPilot_Status = "Processing"` immediately

**Implementation:**
```csharp
private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

var semaphore = _locks.GetOrAdd(fileId, _ => new SemaphoreSlim(1, 1));
await semaphore.WaitAsync();
try
{
    // Process file
}
finally
{
    semaphore.Release();
}
```

#### 1.6 File Deletion During Processing
**Scenario:** File deleted while being processed

**Handling:**
- Catch `ODataError` with code "itemNotFound"
- Log warning
- Update metadata: `SMEPilot_Status = "Deleted"`
- Clean up any partial processing

**Log Message:**
```
File "{fileName}" was deleted during processing. Operation cancelled.
```

#### 1.7 Network Failures
**Scenario:** Network error during download/upload

**Handling:**
- Implement retry policy with exponential backoff
- Maximum 3 retry attempts
- If all retries fail, update metadata: `SMEPilot_Status = "NetworkError"`
- Log error with retry count

**Retry Policy:**
```csharp
var retryPolicy = Policy
    .Handle<HttpRequestException>()
    .Or<TaskCanceledException>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
    );
```

#### 1.8 Template Missing
**Scenario:** Template file not found

**Handling:**
- Check template exists during configuration
- If missing, use fallback template
- Log warning
- Update metadata: `SMEPilot_Status = "TemplateMissing"` (if fallback fails)

**Fallback:**
- Use `TemplateBuilder` to create document without template
- Log: "Template not found, using default formatting"

#### 1.9 Destination Folder Full
**Scenario:** Destination folder has no space

**Handling:**
- Check available space before upload
- If insufficient, log error
- Update metadata: `SMEPilot_Status = "DestinationFull"`
- Alert administrator

**Error Message:**
```
Destination folder is full. Cannot upload enriched document.
Please free up space and retry.
```

#### 1.10 Permission Denied
**Scenario:** App lacks required permissions

**Handling:**
- Validate permissions during configuration
- If denied during processing, log error
- Update metadata: `SMEPilot_Status = "PermissionDenied"`
- Require admin intervention

**Error Message:**
```
Permission denied. Please ensure app has required permissions:
- Read/Write access to source folder
- Read/Write access to destination folder
- Create metadata columns
```

### 2. Copilot Query Edge Cases

#### 2.1 No Enriched Documents
**Scenario:** User queries but no documents enriched yet

**Handling:**
- Check destination folder for enriched documents
- If empty, return friendly message
- Log info message

**Response:**
```
No enriched documents available yet. Please upload documents to the source folder.
```

#### 2.2 Query Timeout
**Scenario:** Query takes too long to process

**Handling:**
- Set timeout (30 seconds)
- If timeout, return partial results or timeout message
- Log warning

**Response:**
```
Query timeout. Please try a more specific question or contact administrator.
```

#### 2.3 Invalid Queries
**Scenario:** User query is malformed or empty

**Handling:**
- Validate query before processing
- Return clarification request
- Log warning

**Response:**
```
Please provide a valid question. For example: "What is the alert configuration?"
```

#### 2.4 Permission Denied for Query
**Scenario:** User lacks permission to query documents

**Handling:**
- Check user permissions on destination folder
- Return access denied message
- Log warning with user ID

**Response:**
```
Access denied. You do not have permission to query enriched documents.
Please contact your administrator.
```

#### 2.5 Service Unavailable
**Scenario:** Copilot service is down

**Handling:**
- Catch service exceptions
- Return service unavailable message
- Log error

**Response:**
```
Copilot service is temporarily unavailable. Please try again later.
```

### 3. Configuration Edge Cases

#### 3.1 Invalid Folder Paths
**Scenario:** User enters invalid folder path

**Handling:**
- Validate folder path format
- Check folder exists
- Show error in configuration UI
- Prevent saving invalid configuration

**Validation:**
```csharp
public bool ValidateFolderPath(string path)
{
    // Check format
    if (string.IsNullOrWhiteSpace(path))
        return false;
    
    // Check exists
    try
    {
        var folder = await _graph.GetFolderAsync(path);
        return folder != null;
    }
    catch
    {
        return false;
    }
}
```

#### 3.2 Missing Permissions
**Scenario:** App lacks required permissions

**Handling:**
- Check permissions during configuration
- Show missing permissions
- Provide link to grant permissions
- Prevent saving until permissions granted

**Error Display:**
```
Missing Permissions:
- Sites.ReadWrite.All
- Files.ReadWrite.All

Please grant these permissions in Azure AD App Registration.
```

#### 3.3 Template Not Found
**Scenario:** Template file doesn't exist

**Handling:**
- Validate template exists
- Show error in configuration UI
- Provide option to upload template
- Prevent saving until template exists

#### 3.4 Configuration Save Failure
**Scenario:** Failed to save configuration

**Handling:**
- Retry save operation (3 attempts)
- Log error
- Show error message to user
- Keep configuration in memory for retry

**Error Message:**
```
Failed to save configuration. Please try again.
If problem persists, contact administrator.
```

### 4. Webhook Edge Cases

#### 4.1 Duplicate Webhooks
**Scenario:** Same notification received multiple times

**Handling:**
- Deduplicate by notification ID + timestamp
- 30-second deduplication window
- Skip duplicate notifications

**Implementation:**
```csharp
private static readonly ConcurrentDictionary<string, DateTime> _processedNotifications = new();

var notificationKey = $"{subscriptionId}:{resource}:{changeType}";
if (_processedNotifications.TryGetValue(notificationKey, out var lastProcessed))
{
    if (DateTime.UtcNow - lastProcessed < TimeSpan.FromSeconds(30))
    {
        // Skip duplicate
        return;
    }
}
_processedNotifications[notificationKey] = DateTime.UtcNow;
```

#### 4.2 Webhook Validation Failure
**Scenario:** Webhook validation token invalid

**Handling:**
- Return validation token immediately
- Log validation attempt
- Do not process notification

#### 4.3 Webhook Subscription Expired
**Scenario:** Webhook subscription expired

**Handling:**
- Detect expired subscription
- Recreate subscription automatically
- Log subscription renewal

**Renewal:**
```csharp
if (subscription.ExpirationDateTime < DateTime.UtcNow.AddHours(1))
{
    await RenewSubscriptionAsync(subscriptionId);
}
```

## Permissions Matrix (Minimum)

### App Registration (Azure AD app; admin consent required)

- `Sites.ReadWrite.All` (application permission)
  - **Purpose:** Read/write site content and metadata
  - **Required:** Yes
  - **Scope:** All sites in tenant

- `Sites.Manage.All` (optional)
  - **Purpose:** Only if installer must create libraries
  - **Required:** No (only if installer needs to create libraries)
  - **Scope:** All sites in tenant

### Installer (human running scripts)

- **Site Owner** or **Site Collection Admin**
  - **Purpose:** For PnP operations in installer
  - **Required:** Yes
  - **Scope:** Target site collection

### End-users

- **Contribute permission** to Source folder
  - **Purpose:** Upload documents to source folder
  - **Required:** Yes

- **Read permission** to Enriched Docs library
  - **Purpose:** View enriched documents
  - **Required:** Yes

## Permissions Required (Detailed)

### Azure AD App Registration Permissions

#### Application Permissions (App-only)
```
Sites.ReadWrite.All
- Purpose: Read and write documents in SharePoint
- Required: Yes
- Scope: All sites in tenant

Files.ReadWrite.All
- Purpose: Read and write files in SharePoint
- Required: Yes
- Scope: All files in tenant

Sites.Read.All
- Purpose: Read site information
- Required: Yes
- Scope: All sites in tenant

User.Read.All (Optional)
- Purpose: Read user information for Copilot queries
- Required: No (only if user context needed)
- Scope: All users in tenant
```

### SharePoint Site Permissions

#### Source Folder Permissions
```
Read
- Purpose: Read uploaded documents
- Required: Yes
- Granted to: App service principal

Write
- Purpose: Update metadata
- Required: Yes
- Granted to: App service principal

Create
- Purpose: Create files (if needed)
- Required: No
- Granted to: App service principal
```

#### Destination Folder Permissions
```
Read
- Purpose: Read enriched documents for Copilot
- Required: Yes
- Granted to: App service principal, Users

Write
- Purpose: Upload enriched documents
- Required: Yes
- Granted to: App service principal

Create
- Purpose: Create enriched documents
- Required: Yes
- Granted to: App service principal
```

#### Document Library Permissions
```
Read
- Purpose: Read document metadata
- Required: Yes
- Granted to: App service principal

Write
- Purpose: Update metadata columns
- Required: Yes
- Granted to: App service principal

Create Columns
- Purpose: Create SMEPilot metadata columns
- Required: Yes
- Granted to: Site Collection Admin (one-time)
```

### User Permissions

#### For Document Upload
```
Contribute
- Purpose: Upload documents to source folder
- Required: Yes
- Granted to: All users who need to upload
```

#### For Copilot Queries
```
Read
- Purpose: Query enriched documents
- Required: Yes
- Granted to: All users who need to query
```

#### For Configuration
```
Site Collection Administrator
- Purpose: Configure app settings
- Required: Yes
- Granted to: Administrators only
```

### Permission Validation

#### During Installation
```csharp
public async Task<PermissionValidationResult> ValidatePermissionsAsync(
    string sourceFolderPath,
    string destinationFolderPath)
{
    var result = new PermissionValidationResult();
    
    // Check source folder permissions
    var canReadSource = await CanReadFolderAsync(sourceFolderPath);
    var canWriteSource = await CanWriteFolderAsync(sourceFolderPath);
    
    if (!canReadSource)
        result.Errors.Add("Cannot read source folder");
    if (!canWriteSource)
        result.Errors.Add("Cannot write to source folder");
    
    // Check destination folder permissions
    var canReadDest = await CanReadFolderAsync(destinationFolderPath);
    var canWriteDest = await CanWriteFolderAsync(destinationFolderPath);
    
    if (!canReadDest)
        result.Errors.Add("Cannot read destination folder");
    if (!canWriteDest)
        result.Errors.Add("Cannot write to destination folder");
    
    // Check metadata column creation
    var canCreateColumns = await CanCreateMetadataColumnsAsync();
    if (!canCreateColumns)
        result.Errors.Add("Cannot create metadata columns. Site Collection Admin required.");
    
    result.IsValid = result.Errors.Count == 0;
    return result;
}
```

#### During Runtime
```csharp
public async Task<bool> ValidateProcessingPermissionsAsync(string fileId)
{
    try
    {
        // Try to read file
        await _graph.GetFileAsync(fileId);
        
        // Try to update metadata
        await _graph.UpdateMetadataAsync(fileId, new Dictionary<string, object>());
        
        return true;
    }
    catch (ODataError ex) when (ex.Error?.Code == "accessDenied")
    {
        _logger.LogError("Permission denied for file {FileId}", fileId);
        return false;
    }
}
```

## Minimal Permissions Strategy

### Principle: Least Privilege
- Grant only necessary permissions
- Use app-only authentication where possible
- Separate read and write permissions
- Review permissions regularly

### Permission Matrix

| Operation | Permission Required | Scope |
|-----------|-------------------|-------|
| Read source folder | Sites.Read.All | Source folder only |
| Write source metadata | Sites.ReadWrite.All | Source folder only |
| Read destination folder | Sites.Read.All | Destination folder only |
| Write destination files | Files.ReadWrite.All | Destination folder only |
| Create metadata columns | Sites.FullControl.All | Document library |
| Copilot queries | Sites.Read.All | Destination folder only |

### Permission Request Flow

```
1. User installs app
2. App requests permissions
3. Admin grants permissions in Azure AD
4. App validates permissions
5. If valid, proceed with configuration
6. If invalid, show error and request again
```

## Security Considerations

### 1. Authentication
- Use app-only authentication for processing
- Use user authentication for Copilot queries
- Validate tokens on every request

### 2. Authorization
- Check permissions before each operation
- Log permission denials
- Alert on suspicious activity

### 3. Data Protection
- Encrypt sensitive data in transit (HTTPS)
- Encrypt sensitive data at rest (Azure Storage)
- Mask sensitive data in logs

### 4. Audit Logging
- Log all permission checks
- Log all file operations
- Log all configuration changes
- Retain logs for 90 days

