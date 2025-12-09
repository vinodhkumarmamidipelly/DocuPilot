## SMEPilot – Admin Installation & Setup Guide

This guide is for **SharePoint / Tenant admins** who install and configure SMEPilot for a site.

---

### 1. Prerequisites

**Permissions**

- You should be:
  - A **SharePoint Site Owner** on the target site where you will install SMEPilot, and
  - Either a **Tenant admin** or have access to one for granting Azure AD app permissions.

**Environment**

- An Azure subscription with the **SMEPilot Function App** deployed and running.
- The **SMEPilot SPFx solution** deployed to the tenant app catalog and available to add to sites.
- A SharePoint site where:
  - Users will upload **raw documents** (source).
  - SMEPilot will store **enriched documents** (destination).

---

### 2. Minimum Required Permissions (Summary)

To install and run SMEPilot successfully, the following permissions are the **minimum** required:

- **Azure AD / Microsoft Graph application (SMEPilot Function App identity)**
  - **Application permissions** (granted once per tenant by a tenant admin):
    - `Sites.ReadWrite.All`  
      - Needed so the Function App can read raw documents, write enriched documents, and manage the `SMEPilotConfig` / `SMEPilotRuns` lists via Microsoft Graph.
  - No extra “organization profile” permissions (such as `Organization.Read.All`) are required.

- **SharePoint / SPFx Admin Panel**
  - On the target site, the admin configuring SMEPilot must have:
    - **Full Control / Site Owner** rights, so the SPFx web part can:
      - Create the `SMEPilotConfig` list and its columns,
      - Save configuration items,
      - Optionally create metadata columns on the source library.
  - At the tenant level:
    - Permission to deploy the SPFx solution to the **app catalog** (usually via a SharePoint admin).

- **Tenant Admin (one‑time)**
  - A Microsoft 365 / Azure AD tenant admin is required to:
    - Approve the SMEPilot app’s Graph permissions (admin consent),
    - Optionally review and accept future permission changes (if we ever extend the permission set).

If any of these minimum permissions are missing, SMEPilot may fail to create subscriptions, access documents, or save configuration.

---

### 3. High‑Level Steps

1. Verify the **Function App** is running.
2. Add the **SMEPilot Admin Panel** web part to a page on your target site.
3. Use the Admin Panel to:
   - Choose source and destination folders,
   - Upload/select a `.dotx` template,
   - Set processing settings,
   - Save configuration.
4. If prompted, **grant admin consent** for the SMEPilot Azure AD app.
5. Confirm webhook subscription and run a test upload.

---

### 4. Verify Function App

1. In Azure Portal, open your Function App (e.g., `SMEPilotfunction`).
2. Check:
   - **Status**: Running.
   - Under **Functions**, you should see:
     - `ProcessSharePointFile` (HTTP),
     - `SetupSubscription` (HTTP),
     - `WebhookRenewal` (Timer),
     - `TenantHealth` (HTTP),
     - `ConsentComplete` (HTTP).
3. If any are disabled, enable them before proceeding.

---

### 5. Add the Admin Panel Web Part

1. Go to the target SharePoint site (e.g., `https://yourtenant.sharepoint.com/sites/SMEPilot`).
2. Edit a page where admins can configure SMEPilot (Home page or a dedicated “SMEPilot Setup” page).
3. Click **+ Add web part** and search for **SMEPilot Admin Panel** (or the name you used when building the SPFx solution).
4. Add it to the page and **Publish** the page.

---

### 6. Configure SMEPilot on the Site

Open the page containing the Admin Panel. The UI is organized into three main sections.

#### 5.1 Document configuration (Part 1)

- **Source folder**
  - Choose the library/folder where users will upload **raw documents**.
  - Typical example: `/sites/SMEPilot/Shared Documents/Raw Documents`.
  - Config is saved as `SourceFolderPath` in `SMEPilotConfig`.

- **Destination folder**
  - Choose where SMEPilot will store **enriched documents**.
  - Typically another folder/library, e.g. `/sites/SMEPilot/Shared Documents/SMEPilot Enriched Docs`.
  - The Admin Panel enforces rules:
    - Destination cannot equal Source.
    - Destination cannot be a subfolder of Source (and vice versa).

- **Template file**
  - Select a `.dotx` file that defines your organization’s standard document format.
  - Either:
    - Upload a new template into a configured **Templates** folder, or
    - Select an existing `.dotx`.
  - The selected template URL is stored as `TemplateFileUrl`, and the path/name as `TemplateLibraryPath` / `TemplateFileName`.

When you choose these values and click **Save configuration**, the Admin Panel will:

1. Ensure `SMEPilotConfig` list exists on the site.
2. Save or update one configuration item with:
   - Source and destination folder paths.
   - Template URL/path/name.
   - Max file size, timeout, retry settings.
   - Copilot prompt and access point flags.
   - Enriched output type (DOCX/PDF/Both).

#### 5.2 Copilot configuration (Part 2)

- **Copilot Agent Prompt**
  - Free‑text instructions guiding how the Copilot agent should answer questions based on enriched documents.

- **Access Points**
  - Checkboxes for:
    - Microsoft Teams,
    - Web interface (SharePoint),
    - O365 Copilot (Word/Excel/PPT).
  - These flags are mainly for your governance and UX; they don’t grant permissions by themselves.

#### 5.3 Processing settings (Part 3)

- **Max File Size (MB)**
  - Maximum allowed size per document. Larger files are rejected to avoid long processing times.

- **Timeout (seconds)**
  - Maximum allowed processing time per document before the Function stops.

- **Max Retries**
  - Number of times SMEPilot retries after a transient failure (e.g., file locks).

- **Enriched Output Type**
  - `Both (DOCX + PDF)` – default; keeps the enriched DOCX and also generates a PDF via Graph.
  - `DOCX only` – no PDF is generated.
  - `PDF only` – attempt to create PDF and delete the DOCX (best‑effort).

---

### 6. Granting Permissions (Admin Consent)

If tenant‑wide permissions have not been granted yet:

1. In the Admin Panel, click **“Grant permissions (Admin only)”**.
2. You’ll be redirected to the Microsoft 365 admin consent page for the SMEPilot app.
3. As a tenant admin:
   - Review the requested permissions (Graph scopes).
   - Click **Accept** to grant them to the tenant.
4. After consent, Azure AD will redirect to the `ConsentComplete` function, showing a simple confirmation page.
5. Return to the SMEPilot Admin Panel and click **Save configuration** again to complete setup.

If this step is not completed, SMEPilot will not be able to:
- Access SharePoint content via Graph,
- Create webhook subscriptions,
- Or process documents.

---

### 7. Webhook Subscription Setup

When you save configuration, the Admin Panel calls the backend to create or update the Graph webhook subscription.

Under the hood:

1. `FunctionAppService.createWebhookSubscription` sends siteId/driveId/sourceFolderPath to the backend.
2. `SetupSubscription` function:
   - Resolves resource path and creates a `subscriptions` entry in Graph pointing to `/api/ProcessSharePointFile`.
   - Generates a `ClientStateSecret` (random GUID) and sets it on the subscription.
   - Writes `SubscriptionId`, `SubscriptionExpiration`, and `ClientStateSecret` to `SMEPilotConfig`.

**You do not have to manage subscriptions manually**; the `WebhookRenewal` function automatically renews them as long as:
- The Function App is running, and
- The app still has valid permissions.

---

### 8. Verifying Installation

After saving configuration and consent:

1. **Check SMEPilotConfig**
   - Go to **Site contents → SMEPilotConfig**.
   - You should see:
     - Source and destination paths.
     - Template URL.
     - `SubscriptionId` and `SubscriptionExpiration`.
     - `ClientStateSecret`.

2. **Upload a test document**
   - Upload a supported file (DOCX/PPTX/PDF/etc.) into the **Source folder**.
   - Wait a short time (webhook + processing).
   - Check the **Destination folder** for:
     - An enriched `.docx` (and `.pdf` if configured).

3. **Check logs if it fails**
   - In Azure Portal:
     - Open the Function App → **Monitor** → `ProcessSharePointFile`.
   - Look for warnings/errors:
     - ClientState mismatch,
     - Not in Source folder,
     - File too large/unsupported,
     - Template not found.

---

### 9. Common Issues & Fixes

- **No enriched document appears**
  - Confirm:
    - `SubscriptionId` and `ClientStateSecret` are populated in `SMEPilotConfig`.
    - `WebhookRenewal` is enabled and logging successful renewals.
    - The uploaded file is in the configured **Source folder** (not another library).
  - Check the Azure Function logs for `ProcessSharePointFile` errors.

- **Template upload errors**
  - Error about “Server relative urls must start with SPWeb.ServerRelativeUrl”:
    - Ensure template folder paths in Admin Panel are full server‑relative URLs (e.g., `/sites/SMEPilot/Shared Documents/Templates`).

- **Admin consent problems**
  - If you see permission errors in the logs:
    - Confirm the tenant admin completed the consent flow successfully.
    - Check the Azure AD Enterprise Application for SMEPilot and its granted permissions.

---

### 10. Responsibilities Summary

As the **Admin** you are responsible for:

- Choosing appropriate Source/Destination folders and templates.
- Ensuring necessary permissions are granted and maintained.
- Monitoring basic health (subscriptions, processing errors).
- Communicating to document authors:
  - Where to upload raw documents,
  - Where enriched documents will appear,
  - Any limitations (max size, supported formats).

Once this initial setup is done, SMEPilot runs automatically in the background; admins will typically only revisit the Admin Panel to:
- Change template or folders,
- Adjust processing limits,
- Or decommission SMEPilot from a site.


