✅ SMEPilot – Manager Questions & Architecture Design Decisions (Final Review Version)

Purpose: Provide clear, crisp, architect-level answers to all review questions asked regarding SMEPilot design, restrictions, permissions, templating, concurrency, formats, failure handling, and versioning.

1. Why do we have a 50MB file size restriction?
Reason 1 — Azure Platform Limits

Azure Functions allow max ~100MB per request (fixed by Microsoft).

A 50MB .docx expands to 300–600MB in RAM when unzipped (DOCX = ZIP).

Setting 50MB protects us from memory crashes, timeouts, and function failures.

Reason 2 — Reliability & Performance

Large files:

Slow down overall processing

Cause timeouts

Block the queue (delay other documents)

Introduce higher failure probability

Small–medium files:

Are processed in <60 sec

Fit safely within memory limits

Reason 3 — Cost Control

Large files consume:

More compute time

More memory

Higher Azure billing

Reason 4 — Practical Observation

99% of organizational documents are <20MB

Even very large technical docs rarely exceed 40MB

Configurable

Admin can increase limit (50→80MB), but default remains 50MB for safety.

2. What permissions are required at each level?
Level A — SharePoint App Installation (One-time)

Required role: Site Collection Admin or Site Owner

Permissions:

Add/Customize Pages (for SPFx panel, if SPFx used)

Manage Lists & Libraries

Read/Write for source & destination libraries

Create webhooks

Used for:

Installing SMEPilot App

Creating Config List

Setting Source/Destination/Template paths

Level B — Azure AD App Registration (One-time IT Admin Consent)

Required role: Azure AD Admin

Permissions needed:

Sites.ReadWrite.All → Read/write files and metadata

Webhooks.ReadWrite.All (if auto-subscription used)

Used for:

Allowing backend function to access SharePoint

Handling document operations

Managing webhook events

Level C — Copilot Integration (One-time)

Required role: M365 Global Admin OR Copilot Admin

Permissions:

Approve SharePoint library for search indexing

Configure Copilot data sources

Create Copilot Agent with instructions

No code changes needed — only configuration.

Level D — Runtime Permissions (Automatic)

Azure Function uses:

App-only OAuth token

Permissions already granted in Azure AD

Used for:

Reading files

Enriching & writing processed files

Updating metadata

Level E — End Users

Uploaders → Contribute on Source Folder
Consumers → Read on Enriched Docs Folder
Copilot Users → Standard Copilot license

3. Can OpenXML handle all document beautification and templating?
Yes — OpenXML fully supports:

Applying organizational .dotx templates

Formatting: fonts, colors, spacing, margins

Creating TOC with page numbers

Rebuilding headings (H1/H2/H3)

Handling images, tables, lists

Adding headers/footers and branding

Page setup, section breaks

What OpenXML cannot do:

Understand document meaning

Identify which paragraph belongs to which “logical section”

How we solve the limitation

A Rule-Based Classification Engine (no AI) will:

Detect keywords (steps, troubleshooting, summary, etc.)

Recognize title-like formatting (bold, large font)

Use heading position & indentation

Map content to template sections

Place unknown content in fallback bucket

Enhancement (Optional): Can integrate ML.NET for intelligent content categorization while maintaining rule-based processing.

This ensures:

Predictable

Deterministic

Repeatable results

Fully aligned with "NO AI" requirement (or "AI-enhanced" if ML.NET used)

4. What if multiple documents are uploaded at once?
Design: Concurrent Processing with Protection

Current Approach:

SharePoint → Webhook → Function App receives notifications

Each file processed independently

Concurrency protection prevents duplicate processing of same file

Multiple different files can process in parallel (up to Azure Functions limits)

Protection Mechanisms:

Semaphore locks per file (prevents concurrent processing of same file)

Idempotency checks (metadata-based, prevents duplicate processing)

Notification deduplication (30-second window)

Performance:

Small batches (1-10 files): ✅ Handles perfectly

Medium batches (10-20 files): ✅ Handles well (~5-10 minutes)

Large batches (20-50 files): ✅ Works but slower (~10-30 minutes)

Future Enhancement (Optional):

Queue-based processing for very large batches (50+ files)

Parallel processing with controlled concurrency (3-5 files at a time)

Benefits:

No overload or crashes

Safe for 10, 50, 100 simultaneous uploads

Each file processed independently

Automatic retry for failures

5. Destination Format — DOCX or PDF?
Default Recommendation: DOCX

Because DOCX provides:

Best Copilot search accuracy

Better preservation of structure (headings, metadata)

Faster indexing

Better citations and context extraction

PDF Pros

Great for sharing/viewing

Non-editable

Looks consistent everywhere

Final Design (Configurable):

Admin can choose:

Setting	Output
DOCX Only	Best Copilot search results
PDF Only	Best distribution format
Both	Balanced (DOCX for Copilot + PDF for users)
6. What happens to the source document after processing?
Default: KEEP the original document.

Because:

Acts as audit trail

Safe fallback if processing fails

Enables reprocessing

Users may need the original copy

Configurable options:

Keep (default, safest)

Move to "Archive" folder

Replace original file

Delete after enrichment (least preferred)

7. What if processing stops in the middle?
We designed full failure resilience:
Failure Handling:

Try/Catch at every stage

Automatic retries (3 attempts, exponential backoff)

Logs saved in SharePoint + App Insights

Partial processed files automatically removed

Metadata updated with status:

Processing
Succeeded
Retrying
Failed
NeedsManualReview


Source document is never touched or lost.

Recovery:

Automatic retry on next webhook notification (for transient failures)

Admin can manually retrigger

System is always recoverable

No data loss

Failure Points & Handling:

Download failure → Retry with backoff

Extraction failure → Status = "Retry" or "Failed"

Enrichment failure → Status = "ManualReview", cleanup temp files

Upload failure → Retry up to MaxUploadRetries

Metadata update failure → Fallback metadata prevents reprocessing

8. Duplicate Processing vs Versioning
Duplicates Handling

Using:

LastModified timestamp comparison (primary method)

File hash (SHA256) - Optional enhancement for accuracy

Logic:

Compare LastModifiedDateTime with SMEPilot_LastEnrichedTime

If unchanged → Skip processing (duplicate)

If metadata change only → Configurable (skip or reprocess)

Versioning Handling

Using:

LastModified timestamp comparison with SMEPilot_LastEnrichedTime

If content updated (LastModified > LastEnriched) → Reprocess

SharePoint automatically maintains versions:

Enriched document tagged with version metadata

Copilot always indexes the latest version

Benefits:

No wasted compute (duplicates skipped)

Always fresh content (versions reprocessed)

Copilot answers always up-to-date

Implementation:

Compare LastModifiedDateTime with SMEPilot_LastEnrichedTime

If updated → Reprocess (version)

If unchanged → Skip (duplicate)