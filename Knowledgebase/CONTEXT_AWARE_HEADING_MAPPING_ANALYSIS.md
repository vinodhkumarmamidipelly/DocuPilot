# Context-Aware Heading Mapping Analysis

## The Problem You Identified

**Your Requirement:**
1. ✅ Pick text according to **template headings** (not hardcoded keywords)
2. ✅ **Context awareness** - understand which content belongs to which template section
3. ⚠️ **Currently Failing** - mapping is not template-aware

**Current Issue:**
- Content mapping uses **hardcoded keywords** (contains "overview", "functional", etc.)
- Does **NOT read template structure** to understand what headings exist
- Does **NOT have context awareness** - just simple string matching
- **Fails** when raw document headings don't match hardcoded keywords

---

## Current Implementation (Why It's Failing)

### What We're Currently Doing

**SimplifiedContentMapper.cs:**
```csharp
// Hardcoded keyword matching - NOT template-aware
if (headingLower.Contains("overview") || headingLower.Contains("summary"))
{
    targetTag = "Overview";
}
else if (headingLower.Contains("functional"))
{
    targetTag = "Functional";
}
// ... more hardcoded keywords
```

**Problems:**
1. ❌ **Not Template-Aware:** Doesn't read template to see what headings exist
2. ❌ **Hardcoded Keywords:** Uses fixed keyword list, not template structure
3. ❌ **No Context:** Simple string matching, no understanding of document structure
4. ❌ **Fails When:** Raw document uses different heading names

**Example Failure:**
- Template has: "Executive Summary", "Business Requirements", "System Architecture"
- Raw document has: "Summary", "Requirements", "Architecture"
- Current code: ❌ **Fails** - keywords don't match exactly
- Needed: ✅ **Context-aware mapping** based on template structure

---

## What Context-Aware Mapping Requires

### 1. Read Template Structure

**Need to:**
- ✅ Read template (.dotx) file
- ✅ Extract all headings/content controls from template
- ✅ Understand template's section structure
- ✅ Map raw document content to template sections

**Example:**
```
Template Structure:
- Overview (content control)
- Functional Details (content control)
- Technical Details (content control)
- Troubleshooting (content control)

Raw Document:
- Introduction (heading)
- Features (heading)
- Implementation (heading)
- Issues (heading)

Context-Aware Mapping:
- "Introduction" → "Overview" (semantic match)
- "Features" → "Functional Details" (semantic match)
- "Implementation" → "Technical Details" (semantic match)
- "Issues" → "Troubleshooting" (semantic match)
```

---

### 2. Semantic Understanding

**Need to:**
- ✅ Understand meaning of headings (not just exact match)
- ✅ Map semantically similar headings
- ✅ Consider document context
- ✅ Handle variations in heading names

**Example:**
```
Template: "Overview"
Raw Document Variations:
- "Introduction" → Should map to "Overview"
- "Summary" → Should map to "Overview"
- "Executive Summary" → Should map to "Overview"
- "Background" → Should map to "Overview"

Current Code: ❌ Only matches if contains "overview"
Context-Aware: ✅ Understands semantic similarity
```

---

### 3. Template Structure Awareness

**Need to:**
- ✅ Know what sections template expects
- ✅ Map content to correct template sections
- ✅ Handle missing sections gracefully
- ✅ Preserve template structure

---

## Can OpenXML Do This?

### ✅ Yes, But Requires Better Logic

**What OpenXML Provides:**
1. ✅ **Read Template Structure:**
   - Can read template file
   - Can extract content controls
   - Can identify template sections

2. ✅ **Read Raw Document Structure:**
   - Can extract headings
   - Can identify document sections
   - Can read heading hierarchy

3. ✅ **Map Content:**
   - Can match content to template sections
   - Can fill template content controls

**What's Missing:**
- ⚠️ **Semantic Matching Logic** - Need to implement
- ⚠️ **Context Awareness** - Need to implement
- ⚠️ **Template-Aware Mapping** - Need to implement

**Conclusion:** OpenXML provides the tools, but we need better mapping logic.

---

## Solution: Template-Aware Context Mapping

### Approach 1: Template Structure Reading

**Step 1: Read Template Structure**
```csharp
public class TemplateStructure
{
    public List<TemplateSection> Sections { get; set; }
}

public class TemplateSection
{
    public string Tag { get; set; }          // Content control tag
    public string DisplayName { get; set; }  // Display name in template
    public List<string> Keywords { get; set; } // Semantic keywords
}

// Read template structure
public TemplateStructure ReadTemplateStructure(string templatePath)
{
    var structure = new TemplateStructure();
    
    using (var doc = WordprocessingDocument.Open(templatePath, false))
    {
        var body = doc.MainDocumentPart.Document.Body;
        var contentControls = body.Descendants<SdtElement>().ToList();
        
        foreach (var sdt in contentControls)
        {
            var tag = GetContentControlTag(sdt);
            var displayName = GetContentControlDisplayName(sdt);
            var keywords = GetSemanticKeywords(tag, displayName);
            
            structure.Sections.Add(new TemplateSection
            {
                Tag = tag,
                DisplayName = displayName,
                Keywords = keywords
            });
        }
    }
    
    return structure;
}
```

**Step 2: Semantic Keyword Mapping**
```csharp
// Define semantic keywords for each template section
private Dictionary<string, List<string>> GetSemanticKeywords(string tag, string displayName)
{
    var keywordMap = new Dictionary<string, List<string>>();
    
    // Overview section
    keywordMap["Overview"] = new List<string> 
    { 
        "overview", "summary", "introduction", "background", 
        "executive summary", "abstract", "preface" 
    };
    
    // Functional section
    keywordMap["Functional"] = new List<string> 
    { 
        "functional", "features", "requirements", "business", 
        "capabilities", "functionality", "use cases" 
    };
    
    // Technical section
    keywordMap["Technical"] = new List<string> 
    { 
        "technical", "implementation", "architecture", "design", 
        "system", "components", "api", "endpoints" 
    };
    
    // Troubleshooting section
    keywordMap["Troubleshooting"] = new List<string> 
    { 
        "troubleshooting", "issues", "problems", "errors", 
        "known issues", "faq", "support" 
    };
    
    return keywordMap;
}
```

---

### Approach 2: Context-Aware Content Mapping

**Enhanced Mapper:**
```csharp
public class ContextAwareContentMapper
{
    private readonly TemplateStructure _templateStructure;
    private readonly Dictionary<string, List<string>> _semanticKeywords;
    
    public Dictionary<string, string> MapContentToTemplate(
        DocumentModel rawDocument,
        TemplateStructure templateStructure)
    {
        var contentMap = new Dictionary<string, string>();
        var templateSections = templateStructure.Sections;
        
        // For each section in raw document
        foreach (var rawSection in rawDocument.Sections)
        {
            var rawHeading = rawSection.Heading ?? "";
            var rawContent = rawSection.Body ?? "";
            
            // Find best matching template section
            var bestMatch = FindBestTemplateSection(
                rawHeading, 
                rawContent, 
                templateSections);
            
            if (bestMatch != null)
            {
                // Map content to template section
                if (contentMap.ContainsKey(bestMatch.Tag))
                {
                    contentMap[bestMatch.Tag] += "\n\n" + rawContent;
                }
                else
                {
                    contentMap[bestMatch.Tag] = rawContent;
                }
            }
        }
        
        return contentMap;
    }
    
    private TemplateSection FindBestTemplateSection(
        string rawHeading,
        string rawContent,
        List<TemplateSection> templateSections)
    {
        var bestMatch = templateSections
            .Select(section => new
            {
                Section = section,
                Score = CalculateMatchScore(rawHeading, rawContent, section)
            })
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();
        
        // Only return if score is above threshold
        return bestMatch?.Score > 0.5 ? bestMatch.Section : null;
    }
    
    private double CalculateMatchScore(
        string rawHeading,
        string rawContent,
        TemplateSection templateSection)
    {
        double score = 0.0;
        var headingLower = rawHeading.ToLowerInvariant();
        var contentLower = rawContent.ToLowerInvariant();
        
        // Check exact match with template display name
        if (headingLower.Contains(templateSection.DisplayName.ToLowerInvariant()))
        {
            score += 1.0;
        }
        
        // Check semantic keyword matches
        foreach (var keyword in templateSection.Keywords)
        {
            if (headingLower.Contains(keyword.ToLowerInvariant()))
            {
                score += 0.8;
            }
            if (contentLower.Contains(keyword.ToLowerInvariant()))
            {
                score += 0.5;
            }
        }
        
        // Check content control tag match
        if (headingLower.Contains(templateSection.Tag.ToLowerInvariant()))
        {
            score += 0.9;
        }
        
        return score;
    }
}
```

---

### Approach 3: Template Inspection Integration

**Enhanced TemplateFiller:**
```csharp
public static class TemplateFiller
{
    // Already has InspectTemplate - enhance it
    public static TemplateStructure InspectTemplateStructure(
        string templatePath, 
        ILogger? logger = null)
    {
        var structure = new TemplateStructure();
        
        var tags = InspectTemplate(templatePath, logger);
        
        using (var doc = WordprocessingDocument.Open(templatePath, false))
        {
            var body = doc.MainDocumentPart.Document.Body;
            var contentControls = body.Descendants<SdtElement>().ToList();
            
            foreach (var sdt in contentControls)
            {
                var tag = GetTag(sdt);
                var displayName = GetDisplayName(sdt);
                var keywords = GetSemanticKeywordsForTag(tag);
                
                structure.Sections.Add(new TemplateSection
                {
                    Tag = tag,
                    DisplayName = displayName,
                    Keywords = keywords
                });
            }
        }
        
        return structure;
    }
}
```

---

## Complete Implementation

### Template-Aware Content Mapping

```csharp
public class TemplateAwareContentMapper
{
    public Dictionary<string, string> MapContent(
        DocumentModel rawDocument,
        string templatePath,
        ILogger? logger = null)
    {
        // Step 1: Read template structure
        var templateStructure = ReadTemplateStructure(templatePath, logger);
        
        // Step 2: Map content using template structure
        var contentMap = MapContentToTemplateSections(
            rawDocument, 
            templateStructure, 
            logger);
        
        return contentMap;
    }
    
    private TemplateStructure ReadTemplateStructure(
        string templatePath, 
        ILogger? logger)
    {
        var structure = new TemplateStructure { Sections = new List<TemplateSection>() };
        
        using (var doc = WordprocessingDocument.Open(templatePath, false))
        {
            var body = doc.MainDocumentPart.Document.Body;
            var contentControls = body.Descendants<SdtElement>().ToList();
            
            logger?.LogInformation("📋 [TEMPLATE] Found {Count} content controls in template", 
                contentControls.Count);
            
            foreach (var sdt in contentControls)
            {
                var tag = GetContentControlTag(sdt);
                var displayName = GetContentControlDisplayName(sdt);
                var keywords = GetSemanticKeywords(tag, displayName);
                
                structure.Sections.Add(new TemplateSection
                {
                    Tag = tag,
                    DisplayName = displayName,
                    Keywords = keywords
                });
                
                logger?.LogDebug("📌 [TEMPLATE] Section: {Tag} ({DisplayName}) - Keywords: {Keywords}",
                    tag, displayName, string.Join(", ", keywords));
            }
        }
        
        return structure;
    }
    
    private Dictionary<string, string> MapContentToTemplateSections(
        DocumentModel rawDocument,
        TemplateStructure templateStructure,
        ILogger? logger)
    {
        var contentMap = new Dictionary<string, string>();
        var usedSections = new HashSet<int>();
        
        // For each section in raw document
        for (int i = 0; i < rawDocument.Sections.Count; i++)
        {
            var rawSection = rawDocument.Sections[i];
            var rawHeading = rawSection.Heading ?? "";
            var rawContent = rawSection.Body ?? "";
            
            if (string.IsNullOrWhiteSpace(rawContent)) continue;
            
            // Find best matching template section
            var bestMatch = FindBestTemplateSectionMatch(
                rawHeading, 
                rawContent, 
                templateStructure.Sections);
            
            if (bestMatch != null && bestMatch.Score > 0.5)
            {
                // Map to template section
                if (contentMap.ContainsKey(bestMatch.Section.Tag))
                {
                    contentMap[bestMatch.Section.Tag] += "\n\n" + rawContent;
                }
                else
                {
                    contentMap[bestMatch.Section.Tag] = rawContent;
                }
                
                usedSections.Add(i);
                logger?.LogInformation("✅ [MAPPER] Mapped '{RawHeading}' → '{TemplateTag}' (Score: {Score})",
                    rawHeading, bestMatch.Section.Tag, bestMatch.Score);
            }
        }
        
        // Handle unmapped sections
        MapUnmappedSections(rawDocument, templateStructure, contentMap, usedSections, logger);
        
        return contentMap;
    }
    
    private (TemplateSection Section, double Score)? FindBestTemplateSectionMatch(
        string rawHeading,
        string rawContent,
        List<TemplateSection> templateSections)
    {
        var matches = templateSections
            .Select(section => new
            {
                Section = section,
                Score = CalculateMatchScore(rawHeading, rawContent, section)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();
        
        return matches != null 
            ? (matches.Section, matches.Score) 
            : null;
    }
    
    private double CalculateMatchScore(
        string rawHeading,
        string rawContent,
        TemplateSection templateSection)
    {
        double score = 0.0;
        var headingLower = rawHeading.ToLowerInvariant();
        var contentLower = rawContent.ToLowerInvariant();
        var tagLower = templateSection.Tag.ToLowerInvariant();
        var displayNameLower = templateSection.DisplayName.ToLowerInvariant();
        
        // Exact tag match (highest priority)
        if (headingLower == tagLower || headingLower.Contains(tagLower))
        {
            score += 1.0;
        }
        
        // Display name match
        if (headingLower.Contains(displayNameLower) || displayNameLower.Contains(headingLower))
        {
            score += 0.9;
        }
        
        // Semantic keyword matches in heading
        foreach (var keyword in templateSection.Keywords)
        {
            if (headingLower.Contains(keyword.ToLowerInvariant()))
            {
                score += 0.8;
            }
        }
        
        // Semantic keyword matches in content
        foreach (var keyword in templateSection.Keywords)
        {
            if (contentLower.Contains(keyword.ToLowerInvariant()))
            {
                score += 0.5;
            }
        }
        
        return score;
    }
    
    private List<string> GetSemanticKeywords(string tag, string displayName)
    {
        // Define semantic keywords based on tag and display name
        var keywordMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Overview"] = new List<string> { "overview", "summary", "introduction", "background", "abstract", "preface", "executive summary" },
            ["Functional"] = new List<string> { "functional", "features", "requirements", "business", "capabilities", "functionality", "use cases", "business requirements" },
            ["Technical"] = new List<string> { "technical", "implementation", "architecture", "design", "system", "components", "api", "endpoints", "technical details" },
            ["Troubleshooting"] = new List<string> { "troubleshooting", "issues", "problems", "errors", "known issues", "faq", "support", "debugging" },
            ["Findings"] = new List<string> { "findings", "results", "analysis", "observations", "conclusions" },
            ["References"] = new List<string> { "references", "links", "sources", "bibliography", "resources" }
        };
        
        // Try to find keywords for this tag
        if (keywordMap.ContainsKey(tag))
        {
            return keywordMap[tag];
        }
        
        // Fallback: generate keywords from display name
        var keywords = new List<string> { tag.ToLowerInvariant() };
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            keywords.Add(displayName.ToLowerInvariant());
            // Add individual words from display name
            keywords.AddRange(displayName.ToLowerInvariant()
                .Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries));
        }
        
        return keywords;
    }
}
```

---

## Why Current Implementation Fails

### Current Problems

1. **Not Template-Aware:**
   ```csharp
   // Current: Hardcoded keywords
   if (headingLower.Contains("overview"))
   {
       targetTag = "Overview";
   }
   
   // Needed: Template-aware
   var templateSections = ReadTemplateStructure(templatePath);
   var bestMatch = FindBestMatch(rawHeading, templateSections);
   ```

2. **No Context Understanding:**
   ```csharp
   // Current: Simple string contains
   if (headingLower.Contains("functional"))
   
   // Needed: Semantic matching
   var score = CalculateSemanticMatch(rawHeading, templateSection);
   ```

3. **Doesn't Read Template:**
   ```csharp
   // Current: Doesn't read template structure
   // Needed: Read template content controls
   var templateStructure = InspectTemplateStructure(templatePath);
   ```

---

## Solution Summary

### ✅ What Needs to Be Done

1. **Read Template Structure:**
   - Extract content controls from template
   - Understand template section structure
   - Get semantic keywords for each section

2. **Implement Context-Aware Mapping:**
   - Match raw document headings to template sections
   - Use semantic matching (not just exact keywords)
   - Calculate match scores
   - Map to best matching template section

3. **Handle Edge Cases:**
   - Unmapped sections
   - Multiple matches
   - Low confidence matches

### ✅ OpenXML Can Do This

**OpenXML Provides:**
- ✅ Read template structure
- ✅ Extract content controls
- ✅ Read raw document headings
- ✅ Map content to template

**What We Need to Add:**
- ✅ Semantic matching logic
- ✅ Template structure reading
- ✅ Context-aware mapping algorithm

---

## Implementation Priority

### High Priority (Fixes Current Failure)

1. ✅ **Read Template Structure** - Inspect template content controls
2. ✅ **Semantic Keyword Mapping** - Map variations of heading names
3. ✅ **Context-Aware Matching** - Calculate match scores

### Medium Priority (Improvements)

1. ⚠️ **Confidence Thresholds** - Only map if confidence > threshold
2. ⚠️ **Fallback Mapping** - Handle unmapped sections
3. ⚠️ **Logging** - Better logging for debugging

---

## Conclusion

### ✅ Yes, OpenXML Can Handle Context-Aware Mapping

**What's Needed:**
- ✅ Read template structure (OpenXML can do this)
- ✅ Semantic matching logic (need to implement)
- ✅ Context-aware algorithm (need to implement)

**Current Status:**
- ❌ **Failing** - Not template-aware, uses hardcoded keywords
- ✅ **Fixable** - Can implement template-aware mapping with OpenXML

**Recommendation:**
- ✅ Implement template structure reading
- ✅ Implement semantic matching
- ✅ Replace hardcoded keywords with template-aware mapping

---

**This will fix the context-awareness issue and make content mapping work according to template headings.**

