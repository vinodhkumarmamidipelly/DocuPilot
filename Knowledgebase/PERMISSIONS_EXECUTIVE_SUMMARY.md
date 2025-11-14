# SMEPilot Permissions - Executive Summary

> **Quick Reference:** Who needs what permissions, when, and why.

---

## 📋 At a Glance

| Phase | Who | What Permission | When |
|-------|-----|----------------|------|
| **App Setup** | Azure AD Global Admin | Grant app permissions | One-time |
| **App Setup** | SharePoint Site Admin | Create libraries/lists | One-time |
| **App Setup** | Azure Subscription Owner | Create Function App | One-time |
| **Copilot Setup** | M365 Global Admin | Configure Copilot Studio | One-time |
| **Runtime** | Business Users | Upload documents | Ongoing |
| **Runtime** | End Users | Query Copilot | Ongoing |

---

## 🔐 1. App Setup Permissions (One-Time)

### Azure AD App Registration
**Who:** Azure AD Global Administrator  
**What:** Grant permissions to the app  
**Permissions:**
- `Sites.ReadWrite.All` - Read/write SharePoint files
- `Files.ReadWrite.All` - Read/write files
- `Webhooks.ReadWrite.All` - Manage webhooks

**Time:** 5 minutes  
**Frequency:** One-time per tenant

---

### SharePoint Site Setup
**Who:** SharePoint Site Collection Administrator  
**What:** Create libraries, lists, and columns  
**Permissions:**
- Site Collection Admin or Site Owner

**Actions:**
- Create "SMEPilot Enriched Docs" library
- Create source folder
- Create SMEPilotConfig list
- Create metadata columns

**Time:** 15-30 minutes  
**Frequency:** One-time per site

---

### Azure Function App Setup
**Who:** Azure Subscription Owner  
**What:** Create and configure Function App  
**Permissions:**
- Subscription Owner or Contributor

**Actions:**
- Create Function App
- Configure app settings
- Deploy code

**Time:** 30-60 minutes  
**Frequency:** One-time per deployment

---

## 🤖 2. Copilot Configuration Permissions

### Standard Copilot (Automatic)
**Who:** No one - automatic  
**What:** Documents automatically indexed by Microsoft Search  
**Permissions:** None needed  
**Note:** Works automatically if documents are in SharePoint library

---

### Copilot Studio Configuration (Manual)
**Who:** Microsoft 365 Global Administrator or Copilot Studio Administrator  
**What:** Create and configure Copilot Agent  
**Permissions:**
- Global Admin or Copilot Studio Admin
- Teams Admin (if deploying to Teams)

**Actions:**
1. Create Copilot Agent in Copilot Studio
2. Set data source to "SMEPilot Enriched Docs"
3. Add custom instructions
4. Deploy to Teams/Web

**Time:** 30-60 minutes  
**Frequency:** One-time per Copilot Agent

**Steps:**
- Access Copilot Studio (admin.microsoft.com → Copilot Studio)
- Create new Copilot Agent
- Configure SharePoint data source
- Set system prompt with manager's instructions
- Deploy to Teams

---

## ⚙️ 3. Runtime Permissions (Ongoing)

### Function App (Automatic)
**Who:** Azure AD App (automatic)  
**What:** Process documents automatically  
**Permissions:** Already granted during setup  
**Note:** No ongoing admin action needed

---

### Business Users (Upload Documents)
**Who:** Users who upload documents  
**What:** Upload `.docx` files to source folder  
**Permissions:**
- Contribute access to source folder

**Note:** Standard user permissions only - no admin rights needed

---

### End Users (Query Copilot)
**Who:** Users who query enriched documents  
**What:** Ask questions via Copilot  
**Permissions:**
- Read access to "SMEPilot Enriched Docs" library
- M365 Copilot license (if using O365 Copilot)

**Note:** Uses existing M365 Copilot license - no additional licenses needed

---

### Configuration Administrators
**Who:** Admins who update settings  
**What:** Update SMEPilotConfig list  
**Permissions:**
- Site Collection Administrator

**Note:** Only needed when updating configuration - not for daily operations

---

## 📊 Permission Summary Matrix

### Setup Phase (One-Time)

| Role | Permission Level | Purpose | Duration |
|------|-----------------|---------|----------|
| Azure AD Global Admin | Global Admin | Grant app permissions | 5 min |
| SharePoint Site Admin | Site Collection Admin | Create libraries/lists | 15-30 min |
| Azure Subscription Owner | Subscription Owner | Create Function App | 30-60 min |
| M365 Global Admin | Global Admin | Configure Copilot Studio | 30-60 min |

**Total Setup Time:** ~2-3 hours (one-time)

---

### Runtime Phase (Ongoing)

| Role | Permission Level | Purpose | Frequency |
|------|-----------------|---------|-----------|
| Azure AD App | App Permissions | Process documents | Automatic |
| Business Users | Contribute | Upload documents | As needed |
| End Users | Read + Copilot License | Query Copilot | As needed |
| Config Admin | Site Collection Admin | Update configuration | Rarely |

**Note:** No ongoing admin permissions needed for normal operations

---

## 🎯 Key Points for Management

### ✅ Minimal Permissions
- **App Permissions:** Only SharePoint-related (no access to other systems)
- **User Permissions:** Standard read/contribute (no admin rights)
- **Admin Permissions:** Only during setup (one-time)

### ✅ Security
- App uses app-only authentication (no user credentials)
- Users only see documents they have access to (security trimming)
- All permissions are audited

### ✅ Cost
- No additional licenses needed (uses existing M365 Copilot)
- No ongoing admin overhead (setup is one-time)

### ✅ Scalability
- Once set up, scales automatically
- No additional permissions needed for new users
- New sites can reuse same app permissions

---

## 📝 Quick Checklist

### App Setup (One-Time)
- [ ] Azure AD Global Admin granted app permissions
- [ ] SharePoint Site Admin created libraries/lists
- [ ] Azure Subscription Owner created Function App
- [ ] Function App configured and deployed

### Copilot Setup (One-Time)
- [ ] M365 Global Admin accessed Copilot Studio
- [ ] Created Copilot Agent
- [ ] Configured data source (SharePoint)
- [ ] Set custom instructions
- [ ] Deployed to Teams

### User Access (Ongoing)
- [ ] Business users have Contribute access to source folder
- [ ] End users have Read access to destination folder
- [ ] End users have M365 Copilot license (if using O365 Copilot)

---

## 🔍 Common Questions

### Q: Do users need admin permissions?
**A:** No. Business users only need Contribute (upload), and end users only need Read (query). No admin permissions required.

### Q: Do we need additional licenses?
**A:** No. Uses existing M365 Copilot license. No additional licenses needed.

### Q: How often do we need admin permissions?
**A:** Only during initial setup (one-time). After that, no ongoing admin permissions needed.

### Q: Can we reuse permissions across sites?
**A:** Yes. Azure AD app permissions are tenant-wide. Each site just needs libraries/lists created (Site Admin, one-time per site).

### Q: What if we need to change configuration?
**A:** Site Collection Admin can update SMEPilotConfig list. No code changes or redeployment needed.

### Q: Who can configure Copilot?
**A:** M365 Global Admin or Copilot Studio Admin. One-time setup, then works automatically.

---

## 📚 Detailed Documentation

For complete technical details, see:
- **Full Permissions Guide:** `PERMISSIONS_BY_LEVEL.md`
- **Installation Guide:** `INSTALLATION_AND_CONFIGURATION.md`
- **Copilot Setup:** `QUICK_START_COPILOT.md`

---

## 🎯 Summary

**Setup Phase (One-Time):**
- 4 admin roles needed (Azure AD, SharePoint, Azure, M365)
- ~2-3 hours total setup time
- No ongoing admin overhead

**Runtime Phase (Ongoing):**
- Standard user permissions only
- No admin permissions needed
- Automatic processing

**Key Benefit:** Minimal permissions, maximum security, one-time setup.

---

**This executive summary provides quick reference for management. For detailed technical steps, see `PERMISSIONS_BY_LEVEL.md`.**

