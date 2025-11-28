# What Makes a "Proper" Raw File and Template

## ✅ **Proper Raw File Structure**

### **Required Elements:**

1. **Metadata Header (First 2000 characters):**
   ```
   **Project Name**: Your Project Name
   **Version**: 1.0
   **Date**: December 2024
   **Status**: Draft for Review
   ```

2. **Numbered Sections:**
   ```
   ## 1. PROJECT OVERVIEW
   ### 1.1 Project Information
   ### 1.2 Target Users
   ## 2. FUNCTIONAL REQUIREMENTS
   ### 2.1 Core Features
   ```

3. **Numbered Features:**
   ```
   #### Feature 1: Feature Name
   #### Feature 2: Another Feature
   ```

4. **Numbered Personas:**
   ```
   -   **User Personas**:
       -   **Trainer Persona**:
       -   **Client Persona**:
   ```

5. **Numbered Workflows:**
   ```
   #### 2.2.1 Trainer Workflow: Workflow Name
   #### 2.2.2 Trainer Workflow: Another Workflow
   ```

---

## ✅ **Proper Template Structure**

### **Required Elements:**

1. **Metadata Placeholders:**
   ```
   [PROJECT_NAME]
   [VERSION_NUMBER]
   [DATE]
   [AUTHOR_NAME(S)]
   [REVIEWER_NAME(S)]
   [APPROVER_NAME(S)]
   [STATUS]
   ```

2. **Content Placeholders:**
   ```
   [Persona Name]
   [Feature Name]
   [Workflow Name]
   [Business Rule Name]
   [Entity Name]
   [Integration Name]
   [Epic Name]
   [Story Title]
   [Endpoint Name]
   ```

---

## 📋 **Example Files Created**

I've created:
- `example_proper_raw.md` - Example raw file with proper structure
- `example_proper_template.md` - Example template with placeholders

**These files demonstrate the structure that works with the current extraction logic.**

---

## ⚠️ **Important Notes**

1. **Raw file must have:**
   - Clear metadata in header (Project Name:, Version:, Date:)
   - Numbered sections (1., 2., 3.)
   - Numbered subsections (1.1, 1.2, 2.1)
   - Numbered features/personas/workflows (Feature 1:, Persona 1:, etc.)

2. **Template must have:**
   - Placeholders in brackets: `[PLACEHOLDER_NAME]`
   - Clear placeholder names that match what we're extracting

3. **What will work:**
   - ✅ Metadata extraction (if patterns match)
   - ✅ Section organization (if headings are clear)
   - ✅ Numbered lists (if format is consistent)

4. **What won't work:**
   - ❌ Extracting "Persona Name" from prose paragraphs
   - ❌ Extracting "Feature Name" from unstructured text
   - ❌ Filling placeholders when data doesn't exist in document

---

## 🎯 **The Reality**

**Even with "proper" structure:**
- Some placeholders will be filled ✅
- Some placeholders will be empty ⚠️
- Template formatting will be applied ✅
- Content will be organized ✅

**This is the best we can achieve with rule-based extraction.**


