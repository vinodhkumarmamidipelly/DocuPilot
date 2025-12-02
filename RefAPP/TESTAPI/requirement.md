# Document Merge API – Current Specification (December 2025)

This document reflects the *actual* behavior of the Document Merge Web API that now ships in this repository. Older references to unit-test projects, legacy snippets, or manual code pastes have been removed. Use this guide to understand the architecture, build/run steps, metadata handling, and troubleshooting playbook for the newest merge flow.

---

## 1. Product Overview

**Objective**  
Accept a branded template DOCX plus a raw DOCX, replace `[TOKEN]` placeholders with either numbered sections or metadata extracted from the raw document, append any unmatched content, refresh document tables (Version History, Change Log), insert a Table of Contents (TOC) with auto page numbering, and return the merged DOCX with a meaningful file name.

**Value propositions**
- Preserves template styling via AltChunk insertion and inline replacements.
- Supports metadata-driven tokens (e.g., `[PROJECT_NAME]`, `[AUTHOR_NAME]`) when no numbered section exists.
- Generates a deterministic output file name such as `FitNex Fitness App - v1.0.docx`.
- Cleans up temp files and validates the merged DOCX before returning it.

---

## 2. Repository Layout

```
DocumentMergeApi.sln
Directory.Build.props
requirement.md               <-- this document
src/
  DocumentMergeApi/
    Controllers/
    Models/
    Services/
    Swagger/
    Program.cs
```

> **Note:** The former `tests/DocumentMergeApi.Tests` project was deleted. Any automated validation must be added back explicitly if required later.

---

## 3. Build & Run

1. Ensure **.NET 8 SDK** is installed.
2. Restore and build:
   ```bash
   dotnet restore
   dotnet build
   ```
3. Launch the API:
   ```bash
   dotnet run --project src/DocumentMergeApi
   ```
4. Open Swagger at `https://localhost:{port}/swagger`. Use `POST /api/merge` to upload:
   - `template` – DOCX with `[TOKEN]` placeholders.
   - `raw` – DOCX containing numbered sections plus metadata rows.
   - `author` – optional string for table updates.

The response is a DOCX stream. The controller now uses the dynamic name produced by the merge service rather than the old hard-coded `merged.docx`.

---

## 4. HTTP API Contract

`POST /api/merge`
- **Consumes:** `multipart/form-data`
- **Returns:** `application/vnd.openxmlformats-officedocument.wordprocessingml.document`
- **Errors:**  
  - `400` when either file is missing.  
  - `500` when OpenXML merge fails (message logged to console).

Swagger configuration (`FileUploadOperationFilter`) maps `IFormFile` parameters to `binary` schemas so the UI renders upload inputs correctly.

---

## 5. Merge Pipeline (Runtime Behavior)

1. **Token Detection (`TokenDetector`)**
   - Scans every paragraph descendant in the template (including tables).
   - Records whether the token is inline (surrounded by other text) or standalone (entire paragraph).  
   - Multiple tokens in the same paragraph are supported.

2. **Metadata Extraction (`MetadataExtractor`)**
   - Reads the raw document from the beginning until the first numbered heading (`^\s*(\d+)\.`).
   - Splits each paragraph into fragments (line breaks, bullet characters) and matches `Key: Value` pairs.
   - Normalizes keys (uppercased alphanumeric with underscores) and applies aliases:
     - `Author(s)` → `AUTHOR_NAME`
     - `Version` → `VERSION_NUMBER`
     - etc.

3. **Section Extraction (`RawSectionExtractor`)**
   - Uses the heading regex `^\s*(\d+)\.\s*(.+)$`.
   - Each section captures all OpenXml elements until the next heading.

4. **Matching (`MatcherEngine`)**
   - Strategy order:
     1. **Numeric key** – `[3]` matches heading `3.` exactly.
     2. **Normalized heading equality** – compares lowercase, punctuation-stripped strings (e.g., `[PROJECT_OVERVIEW]` ↔ “Project Overview”).
     3. **Substring match** – heading contains the normalized token text.
     4. **Keyword scoring** – splits both token and heading into word sets and picks the highest overlap score (≥ 1).
   - The first rule that yields a section wins; unmatched tokens fall back to metadata replacement.

5. **Insertion**
   - Matched sections are inserted where the placeholder paragraph lived using `AltChunkInserter` (fallback to inline cloning if AltChunk fails).  
   - Placeholder paragraphs are removed once their content is inserted.
   - Each matched section is marked as “used,” so it will **not** appear again when the leftover raw content is appended.
   - After all template tokens are satisfied, only the *unmatched* raw sections are appended in order. This keeps template-requested blocks (e.g., `HR`) confined to the branded area while the trailing “Remaining Raw Document” contains only unused content.

6. **Metadata Replacement**
   - Tokens with no section match are replaced from the metadata dictionary.
   - Inline tokens substitute only the `[TOKEN]` substring.
   - Standalone tokens keep their `ParagraphProperties` but swap the run text, preserving heading styles/numbering.

7. **TOC Handling (`TocManager`)**
   - If `[TOC]` exists, it is replaced with a TOC field.
    Otherwise:
      - If the raw doc lacks a TOC, a TOC paragraph is inserted at the top.
   - Sets `UpdateFieldsOnOpen` so Word refreshes page numbers automatically.

8. **Tables (`TableUpdater`)**
   - Appends a row to Version History and Change Log tables if their headers match expected column names.
   - Uses the `author` parameter or defaults to `System Generated`.

9. **Validation & Output**
   - Saves the modified DOCX to the temp file.
   - Reopens it read-only to ensure the package is valid.
   - Builds the download file name: `"{Project Name} - v{Version}.docx"` (falls back to `merged.docx`).

10. **Cleanup**
    - Deletes the temp file and disposes unmanaged streams even if errors occur.

---

## 6. Service Graph

```
MergeController
 └─ MergeService
     ├─ TokenDetector
     ├─ RawSectionExtractor
     ├─ MatcherEngine
     ├─ MetadataExtractor
     ├─ AltChunkInserter
     ├─ SectionInserter
     ├─ TocManager
     └─ TableUpdater
```

All services are registered as singletons in `Program.cs`. **Important:** keep `MetadataExtractor` registered; otherwise DI throws an AggregateException during startup.

---

## 7. Placeholder & Metadata Reference

| Placeholder        | Data source                         | Notes                                                |
|--------------------|-------------------------------------|------------------------------------------------------|
| `[PROJECT_NAME]`   | `Project Name:` row in raw doc      | Also used in output file name                        |
| `[VERSION_NUMBER]` | `Version:` row                      | Prefixed with `v` in file names                      |
| `[DATE]`           | `Date:` row                         | Fallback for naming when version missing             |
| `[AUTHOR_NAME]`    | `Author(s):` row                    | Used in Version History when no `author` provided    |
| `[REVIEWER_NAME]`  | `Reviewer(s):` row                  | Inline replacement only                              |
| `[APPROVER_NAME]`  | `Approver(s):` row                  | Inline replacement only                              |
| `[TOC]`            | Template placeholder for TOC        | Removed after TOC field insertion                    |
| `[1]`, `[2]`, etc. | Numbered sections in raw document   | Inserted via AltChunk                                |

Add new aliases inside `MetadataExtractor.KeyAliases` when templates introduce new labels.

---

## 8. File Naming Rules

1. Start with `Project Name` (if present).  
2. Append `- v{Version}`. If version missing, append `- {Date}`.  
3. Replace invalid filename characters with `_`.  
4. Append `.docx`.  

Example: `FitNex Fitness App - v1.0.docx`

---

## 9. Operational Guidance

- **Logging:** The service writes progress to stdout (token/section counts, insertion issues). Redirect STDOUT to your logging solution when hosting.
- **Temp files:** All merges happen via a temp DOCX under `%TEMP%`. Ensure the hosting process has permission to create/delete files there.
- **Long-running merges:** For large documents, consider wrapping `MergeService.Merge` in a background worker or queue.
- **Fonts:** Install template fonts on the server to avoid layout shifts when Word reflows the merged output.

---

## 10. Troubleshooting

| Symptom | Cause | Fix |
| --- | --- | --- |
| `AggregateException` complaining about `MetadataExtractor` | Service not registered | Ensure `Program.cs` calls `builder.Services.AddSingleton<MetadataExtractor>();` |
| Word prompt “file in use” during validation | Merge tried to reopen DOCX while still streaming | Already fixed by scoping `WordprocessingDocument.Open` inside `using` blocks |
| Heading styles disappear | Standalone tokens replaced incorrectly | Fixed by `ReplaceStandaloneToken` preserving paragraph properties |
| Word asks to recover unreadable content | Metadata rows contained multiple `Key: Value` pairs in one paragraph | Metadata extractor now splits fragments; if seen again verify raw DOC formatting |
| Output file named `merged.docx` | Missing `Project Name` and `Version/Date` metadata | Add those rows to raw document or update `MetadataExtractor` to recognize new labels |

---

## 11. Future Enhancements (Backlog)

- Add an integration test harness that runs template/raw fixtures through the merge pipeline and validates the resulting DOCX structure.
- Persist detailed diagnostics (per-token match info) to help authors adjust templates.
- Optional PDF conversion step using LibreOffice or cloud service once DOCX is finalized.
- Configurable table headers for templates with localized wording.
- Streaming/async file copy to lower memory pressure on very large inputs.

---

**Document history**
- *Dec 2025*: Comprehensive rewrite to align with the current codebase (metadata-driven merge, no test project, dynamic output naming).

This requirement file should now stay in sync with the implementation. Update it whenever service registrations, metadata rules, or merge steps change.***