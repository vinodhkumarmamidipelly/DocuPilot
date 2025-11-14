# Future Enhancements

This document tracks planned enhancements and improvements for SMEPilot.

---

## 🔮 Future Enhancements

### 1. Delete Configuration Feature
**Status:** Planned  
**Priority:** Medium  
**Estimated Effort:** 2-3 hours

**Description:**
Add a proper "Delete Configuration" function that allows users to completely remove their SMEPilot configuration and start fresh.

**Features:**
- Delete configuration item from SMEPilotConfig list
- Optionally delete webhook subscription (via Function App API)
- Clear UI state and reset to default values
- Confirmation dialog to prevent accidental deletion
- Option to keep or delete metadata columns on source library

**Current Workaround:**
Users can manually delete the configuration item from SharePoint Site Contents → SMEPilotConfig list. The system will detect the missing configuration and show the empty form for fresh setup.

**Implementation Notes:**
- Add `deleteConfiguration()` method to `SharePointService.ts`
- Add "Delete Configuration" button in Admin Panel (with confirmation)
- Optionally add Function App endpoint to delete webhook subscription
- Consider adding option to preserve/delete metadata columns

---

## 📝 Notes

- Future enhancements are not scheduled for immediate implementation
- Priority and effort estimates are subject to change
- Users can request features to be moved to active development

