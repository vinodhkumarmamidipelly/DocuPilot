### SMEPilot – New Tenant Onboarding Runbook (Multi-tenant Function App)

This checklist is for admins/support when adding a **new customer tenant** to the shared SMEPilot Function App.
It assumes the Azure AD app is multi-tenant and SMEPilot is already running for at least one tenant.

---

### 1. Collect tenant details

- **Required**
  - **Tenant display name** (e.g., Contoso Ltd)
  - **Tenant ID (GUID)** – from Azure AD admin center or `tenant.onmicrosoft.com`
  - **Primary SharePoint site URL** for SMEPilot (e.g., `https://contoso.sharepoint.com/sites/DocEnricher`)

Write these into your internal tracker (e.g. Excel, SharePoint list, or ticket).

---

### 2. Grant admin consent for SMEPilot app

1. Share the **Admin consent URL** with the customer tenant admin (this is what the Admin Panel “Grant permissions (Admin only)” button opens).
2. Admin must log in with an account that has **Tenant admin / Global admin** rights in their own tenant.
3. Admin reviews required permissions and clicks **Accept**.
4. Confirm in the tenant’s Azure AD that:
   - The SMEPilot application appears under **Enterprise applications**.

If consent is missing or fails, SMEPilot will not be able to read/write SharePoint or create webhooks for that tenant.

---

### 3. Configure SMEPilot in the tenant’s SharePoint (SMEPilotConfig)

In the customer’s SharePoint (their tenant):

1. Ensure the **SMEPilotConfig** list exists on the chosen site (the Admin Panel normally creates/updates this).
2. In the Admin Panel (SPFx) opened on their site, fill:
   - **Source library / folder** – where raw documents arrive.
   - **Destination (enriched) library / folder** – where SMEPilot stores enriched files.
   - **Template file** – `.dotx` used for layout.
   - **Processing settings** – max file size, timeout, retries.
   - **Copilot configuration** – ensure values are sensible for this tenant.
3. Click **Save configuration** and confirm success in the UI.

This step ensures per‑tenant settings live in the tenant’s own SMEPilotConfig list.

---

### 4. Create webhook subscription for the tenant

From the SMEPilot Admin Panel running on that tenant:

1. Click **Test / Setup subscription** (wording may vary slightly – use the “Create subscription / Test configuration” action).
2. The SPFx web part calls the **SetupSubscription** Function in the shared Function App, passing the tenant’s **siteId**, **sourceFolderPath** and, if present, **tenantId**.
3. The Function App:
   - Creates a **Graph webhook subscription** for the tenant’s drive/folder.
   - Stores the **SubscriptionId** and **SubscriptionExpiration** back into the tenant’s **SMEPilotConfig**.
4. In the Admin Panel, verify that webhook status shows as **Active**.

If SetupSubscription reports that **admin consent is required**, return to Step 2 and complete consent in that tenant, then retry.

---

### 5. Register tenant for renewal (MultiTenant_TenantIds)

To allow **automatic renewal** of that tenant’s subscriptions by the shared Function App:

1. Open the Function App configuration (Azure Portal → Function App → **Configuration**).
2. Locate (or create) the app setting **`MultiTenant_TenantIds`**.
3. Add the new tenant’s ID (GUID) to the list, comma or semicolon separated, for example:

   - `MultiTenant_TenantIds = 11111111-1111-1111-1111-111111111111, 22222222-2222-2222-2222-222222222222`

4. Save and let the Function App restart if needed.

The **WebhookRenewal** timer function will now periodically renew subscriptions for each tenant ID listed here.

---

### 6. Validate tenant health via TenantHealth endpoint

Use the internal health endpoint (Function App):

- **All tenants**
  - `GET https://<your-function-app>.azurewebsites.net/api/health/tenants?code=<function-key>`
- **Single tenant**
  - `GET https://<your-function-app>.azurewebsites.net/api/health/tenants?tenantId=<TENANT_ID>&code=<function-key>`

Check that for the new tenant:

- There is at least **one active subscription**.
- The **expiration** is in the future and **not expiring soon** (or will be renewed by WebhookRenewal).

---

### 7. Smoke test document processing

In the new tenant’s SharePoint:

1. Upload a test document into the **source folder** configured in SMEPilotConfig.
2. Wait for processing (a few seconds to a few minutes depending on size).
3. Verify that an **enriched document** appears in the **destination folder** with expected formatting.
4. Optionally, check the tenant’s **SMEPilotRuns** list (if created) for processing history.

If processing fails, check:

- Function App logs for errors.
- TenantHealth endpoint for subscription status.
- Admin Panel status messages for configuration issues.

---

### 8. Handover notes for Support

For each onboarded tenant, track at least:

- Tenant name + Tenant ID.
- SMEPilot **site URL**.
- Source and destination folders.
- Date onboarded / last reviewed.
- Whether tenant is present in **MultiTenant_TenantIds**.

This runbook keeps onboarding repeatable and makes it clear which steps to verify when a new SMEPilot customer goes live.


