# Quick Start — Copilot & SMEPilot

> **Note:** No custom Azure Function code is needed for Copilot queries. Copilot pulls from SharePoint Search Index automatically.

## Goal

Make enriched docs discoverable by Microsoft 365 Copilot.

## Steps (Concise)

1. **Ensure enriched docs are saved to a SharePoint library that is included in the tenant's search index.**
   - Default: `SMEPilot Enriched Docs` (created by installer or configured during installation).

2. **Verify Search crawl & visibility**
   - Site settings → Search → Ensure site is visible to enterprise search.
   - Verify library permissions (should be readable by intended users).

3. **Copilot readiness**
   - If your org uses M365 Copilot, Copilot will utilize SharePoint search results automatically.
   - If you require a "Copilot Agent" (custom behavior or action), configure in Copilot Studio:
     - Create SMEPilot Agent
     - Set data source to "SMEPilot Enriched Docs" library
     - Add manager's custom instructions as system prompt:
       > "You are SMEPilot — the organization's knowledge assistant. Use only documents from the 'SMEPilot Enriched Docs' SharePoint library as the primary evidence. Provide a short summary (2-3 lines), then give a numbered step list for procedures or troubleshooting. Always cite the document title and link. If uncertain, say 'I couldn't find a definitive answer in SMEPilot docs.' Do not invent facts beyond the indexed docs."
     - Deploy to Teams

## Troubleshooting

- **If Copilot can't find a document:**
  - Check library permissions (should be readable by intended users).
  - Check search schema (content type mapping).
  - Wait for search crawl or trigger reindex on library (24-48 hours typically).
  - Verify documents are in "SMEPilot Enriched Docs" library.
  - Check that site is visible to enterprise search.

**Note:** For standard Copilot usage, no additional configuration needed beyond ensuring documents are in searchable library. For custom Copilot Agent behavior, use Copilot Studio configuration above.

**Testing:** After Copilot Studio setup, test by asking 'What is SMEPilot?' to verify context loading.
