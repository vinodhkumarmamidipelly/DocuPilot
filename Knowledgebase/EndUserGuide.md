## SMEPilot – End User Guide

This guide is for **everyday users** who upload and read documents, not for developers or admins.

---

### 1. What SMEPilot Does for You

- When you upload a **raw document** (e.g., a functional spec) into the configured **Source folder**, SMEPilot:
  - Automatically converts it into a **standard, branded template**.
  - Adds consistent headings, tables (e.g., version history), and a Table of Contents when the template defines them.
  - Optionally creates a **PDF** version that looks good in browser preview and Copilot.
- The finished document appears in the configured **Destination folder** without you having to run anything manually.

You can think of SMEPilot as an “auto‑formatter” that makes sure all important documents follow your company’s standard layout.

---

### 2. Where to Upload and Where to Look

Your admin will tell you the exact folders, but the pattern is:

- **Source folder (upload here)**  
  - Example: `Shared Documents/Raw Documents`  
  - This is where you drop your original files.

- **Destination folder (read here)**  
  - Example: `Shared Documents/SMEPilot Enriched Docs`  
  - This is where SMEPilot writes the formatted/enriched copies and PDFs.

Ask your site admin if you’re not sure which folders your team is using for SMEPilot.

---

### 3. How to Use SMEPilot in Your Day‑to‑Day Work

**A. Upload a new document**

1. Open the SharePoint **Source folder** in your site.
2. Upload your document (drag & drop or **Upload → Files**).
3. Keep the browser tab open for a bit; SMEPilot runs in the background.
4. After a short delay (usually seconds to a couple of minutes), open the **Destination folder**:
   - Look for a new enriched `.docx` with the same name (or a slightly adjusted name),
   - And, if configured, a `.pdf` version too.

**B. Update an existing document**

1. Edit your document in the Source folder as usual (Word Online/desktop).
2. Save the updated version.
3. SMEPilot will detect the change, and:
   - Create a new enriched copy with the updated content, and
   - Update the PDF if relevant.

**C. Reading and sharing**

- Open the enriched version from the **Destination folder**:
  - Use this version for sharing with customers/managers.
  - It will have more consistent structure and often a TOC and revision tables.
- If a PDF is available, it’s usually the best version to:
  - Attach to emails,
  - Embed in portals,
  - Or reference in Copilot scenarios.

---

### 4. Supported Documents & Limitations

- **Supported file types** (exact list may vary by version, but typically):
  - `.docx` (Word) – best results.
  - `.pptx`, `.xlsx`, `.pdf` – text is extracted and summarized into the template where possible.
  - Images (`.png`, `.jpg`, etc.) – may have limited text extraction depending on OCR configuration.
- **Not supported / not recommended**:
  - Very old formats like `.doc`, `.ppt`, `.xls`.
  - Extremely large files beyond the configured size limit.

If you upload an unsupported or too‑large file, SMEPilot will **skip** processing and your admin will see a warning in the logs.

---

### 5. How to Know If Something Went Wrong

Possible symptoms:

- No enriched document appears in the Destination folder for a long time.
- Only some documents are enriched but not others.
- Enriched document appears but looks clearly wrong (wrong content, broken template, etc.).

What you can do:

1. **Check with your site admin**
   - Confirm that:
     - The folder you used is actually the configured **Source folder**.
     - The system is currently enabled for your site.
2. **Check for error folders or status columns**
   - Your admin may have set up error folders/columns for documents that failed processing.
3. **Contact support or IT**
   - Provide:
     - The site URL,
     - Source folder path,
     - File name,
     - Approximate upload time.

You don’t need to understand the backend; just give enough detail so an admin/dev can look up logs.

---

### 6. Tips for Better Results

- Use **clear headings** in your raw document (e.g., `1. PROJECT OVERVIEW`, `1.1 Project Information`), especially for `.docx` files.
- Keep one document per major spec instead of many unrelated topics in one file.
- Avoid using unusual or experimental formatting features that might not survive template mapping.

---

### 7. FAQ (For End Users)

- **Q: Do I have to click a button to run SMEPilot?**  
  **A:** No. Once your admin has configured SMEPilot, it runs automatically when you upload or update documents in the Source folder.

- **Q: Can I edit the enriched document?**  
  **A:** Yes, but remember:
  - If you later change the raw document and SMEPilot re‑enriches it, a new version may overwrite or replace the previous enriched copy.
  - For long‑term edits, coordinate with your team on whether the raw or enriched version is the “master” document.

- **Q: Where do I go if I have issues?**  
  **A:** First ask your site owner/admin. If they cannot resolve it, they will escalate to IT or the SMEPilot support/development team.

---

Use this guide as a quick reference. For anything that requires changing configuration or templates, contact your **Admin**; for code‑level issues, developers will refer to the Developer and KT guides.


