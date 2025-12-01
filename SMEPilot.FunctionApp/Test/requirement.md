✅ FINAL REQUIREMENT (SIMPLE, CLEAR, CORRECT)

Below is the complete requirement you approved, rewritten in plain English, without any noise or complexity.

1. Input

You always provide:

One raw document (DOCX / PDF text layer)

One template DOCX

Template contains:

Branding (logo, header, footer)

Version History table

Change Log table

Template headings (sections to extract)

2. Output

System must generate one final branded DOCX with:

Branding

TOC

Filled template sections

Remaining raw content (after removing extracted parts)

Version History row

Change Log row

No JSON.
No external files.
Just the final DOCX.

3. Template Section Extraction

If the template has headings like:

HR
Scope
Architecture


Then for each heading:

✔️ Step 1 — Find the same section in raw document

Matching rules:

Exact match

Case-insensitive

Normalized text

Fuzzy match (small typos allowed)

✔️ Step 2 — Extract the entire block

This includes:

Heading

Paragraphs

Bullet points

Numbered lists

Bold/italic colors

Tables

Images

Diagrams

Anything inside that section

✔️ Step 3 — Insert into the template slot

Formatting MUST remain identical to raw.

✔️ Step 4 — Remove that section from raw

So the extracted part does not appear again in the remaining content.

No duplication.

4. Remaining Raw Content

After extracting all template-required sections:

Whatever content is left in the raw document

With ALL formatting preserved

Must be inserted under Remaining Document Content (or the appropriate heading in your template)

This includes all:

Text

Bullets

Tables

Images

Styling

Colors

Alignment

Only the extracted sections should be missing.

5. Version History (Template Table)

System automatically adds one new row at bottom:

Example entry:

Version: auto-increment (1.0 → 1.1)

Date: today

Author: from user input or system user

Summary: e.g., Generated new enriched document, filled sections: HR

Approved By: left blank

6. Change Log (Template Table)

System automatically adds one new line with:

Change number (increment)

Date

Section name(s) extracted

Description like:
HR section extracted and updated. Content moved from raw document to template section.

Author name

This gives clear traceability.

7. Formatting Preservation (Very Important)

Everything must be preserved EXACTLY as-is:

Bold

Italic

Underline

Colors

Tables

Nested tables

Bullets

Numbering

Images

Image position

Hyperlinks

Alignment

Paragraph spacing

No plain text conversion.
No loss of styling.
No breaking images.

8. Edge Cases
✔ If a heading appears multiple times

Use the first one (default).

✔ If the heading is missing

Follow template rule:

leave empty or

use fallback text

✔ If lists/tables/images are inside the extracted block

They must move with the block.

✔ If images exist inside remaining content

Leave them untouched.

✔ If PDF formatting is imperfect

Do best possible extraction but keep structure.

9. What the final document looks like
[Template Branding]
[TOC]

--- Template Sections (filled) ---
HR:
   <Extracted content with full formatting>

Scope:
   <Extracted content>

...

--- Remaining Raw Document ---
<Raw content minus extracted sections>
<Formatting preserved>

--- Version History (new row added) ---

--- Change Log (new row added) ---