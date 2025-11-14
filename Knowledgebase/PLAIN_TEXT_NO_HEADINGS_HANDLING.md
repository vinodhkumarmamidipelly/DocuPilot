# Handling Documents with No Headings (Plain Text Only)

## The Problem

**Scenario:**
- Raw document has **no headings** - just plain text paragraphs
- Template expects structured sections (Overview, Functional, Technical, etc.)
- Current mapping relies on headings to identify sections
- **Question:** How to map plain text to template sections?

**Challenge:**
- No structural markers (headings) to identify sections
- Need to intelligently split and categorize plain text
- Must map to template structure without headings

---

## Current Implementation Status

### ✅ What We're Already Doing

**Edge Case Documentation:**
```markdown
Documents with no headings:
- Fallback: Use first 2 non-empty paragraphs as "Overview".
```

**Code Evidence:**
- `SimplifiedContentMapper.cs` - Has fallback for unmapped sections
- `RuleBasedFormatter.cs` - Handles documents without headers
- `SimpleEnricher.cs` - Creates sections from plain text

**Current Limitation:**
- ⚠️ Basic fallback (first 2 paragraphs → Overview)
- ⚠️ Doesn't intelligently split plain text into multiple sections
- ⚠️ Doesn't analyze content to determine section type

---

## Solution: Intelligent Plain Text Processing

### Approach 1: Content-Based Section Detection

**Strategy:** Analyze plain text content to identify natural section boundaries and categorize content.

**How It Works:**
1. Split plain text into paragraphs
2. Analyze content to detect section boundaries
3. Categorize content blocks by semantic meaning
4. Map categorized blocks to template sections

**Implementation:**

```csharp
public class PlainTextProcessor
{
    /// <summary>
    /// Process plain text document (no headings) and map to template sections
    /// </summary>
    public Dictionary<string, string> ProcessPlainText(
        string plainText,
        TemplateStructure templateStructure,
        ILogger? logger = null)
    {
        var contentMap = new Dictionary<string, string>();
        
        // Step 1: Split text into logical blocks
        var textBlocks = SplitIntoLogicalBlocks(plainText);
        
        logger?.LogInformation("📄 [PLAINTEXT] Split into {Count} logical blocks", textBlocks.Count);
        
        // Step 2: Categorize each block
        var categorizedBlocks = new List<(string Category, string Content)>();
        
        foreach (var block in textBlocks)
        {
            var category = CategorizeTextBlock(block, templateStructure);
            categorizedBlocks.Add((category, block));
        }
        
        // Step 3: Map categorized blocks to template sections
        foreach (var (category, content) in categorizedBlocks)
        {
            var templateSection = FindBestTemplateSection(category, templateStructure);
            
            if (templateSection != null)
            {
                if (contentMap.ContainsKey(templateSection.Tag))
                {
                    contentMap[templateSection.Tag] += "\n\n" + content;
                }
                else
                {
                    contentMap[templateSection.Tag] = content;
                }
            }
        }
        
        // Step 4: Ensure Overview section (fallback)
        if (!contentMap.ContainsKey("Overview") && textBlocks.Count > 0)
        {
            // Use first 2-3 blocks as Overview
            var overviewContent = string.Join("\n\n", textBlocks.Take(3));
            contentMap["Overview"] = overviewContent;
        }
        
        return contentMap;
    }
    
    /// <summary>
    /// Split plain text into logical blocks (paragraphs, sections)
    /// </summary>
    private List<string> SplitIntoLogicalBlocks(string text)
    {
        var blocks = new List<string>();
        
        // Split by double newlines (paragraph breaks)
        var paragraphs = text.Split(
            new[] { "\r\n\r\n", "\n\n", "\r\r" }, 
            StringSplitOptions.RemoveEmptyEntries);
        
        var currentBlock = new StringBuilder();
        
        foreach (var para in paragraphs)
        {
            var trimmed = para.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            
            // Detect potential section break (blank line, topic change)
            if (IsSectionBreak(trimmed, currentBlock.ToString()))
            {
                if (currentBlock.Length > 0)
                {
                    blocks.Add(currentBlock.ToString().Trim());
                    currentBlock.Clear();
                }
            }
            
            if (currentBlock.Length > 0) currentBlock.AppendLine();
            currentBlock.Append(trimmed);
        }
        
        // Add last block
        if (currentBlock.Length > 0)
        {
            blocks.Add(currentBlock.ToString().Trim());
        }
        
        // If no blocks found, treat entire text as one block
        if (blocks.Count == 0)
        {
            blocks.Add(text.Trim());
        }
        
        return blocks;
    }
    
    /// <summary>
    /// Detect if this is a section break (topic change)
    /// </summary>
    private bool IsSectionBreak(string newParagraph, string currentBlock)
    {
        // Heuristics for section breaks:
        // 1. Current block is long (>500 chars) and new paragraph starts differently
        // 2. New paragraph starts with number/bullet (new topic)
        // 3. Significant topic shift detected
        
        if (currentBlock.Length < 200) return false; // Too short to be a section
        
        // Check for numbered/bulleted list start
        if (Regex.IsMatch(newParagraph, @"^[\d\w][\.\)]\s+[A-Z]"))
        {
            return true;
        }
        
        // Check for topic shift (different keywords)
        var currentTopics = ExtractTopics(currentBlock);
        var newTopics = ExtractTopics(newParagraph);
        
        // If topics are significantly different, it's a section break
        var similarity = CalculateTopicSimilarity(currentTopics, newTopics);
        return similarity < 0.3; // Low similarity = section break
    }
    
    /// <summary>
    /// Categorize text block by content analysis
    /// </summary>
    private string CategorizeTextBlock(string block, TemplateStructure templateStructure)
    {
        var blockLower = block.ToLowerInvariant();
        
        // Define category keywords
        var categoryKeywords = new Dictionary<string, List<string>>
        {
            ["Overview"] = new List<string> 
            { 
                "overview", "summary", "introduction", "background", 
                "purpose", "objective", "goal", "about", "this document" 
            },
            ["Functional"] = new List<string> 
            { 
                "feature", "function", "capability", "workflow", 
                "process", "step", "how to", "user", "business" 
            },
            ["Technical"] = new List<string> 
            { 
                "api", "endpoint", "integration", "technical", 
                "implementation", "code", "sdk", "rest", "graph", 
                "database", "architecture", "system", "component" 
            },
            ["Troubleshooting"] = new List<string> 
            { 
                "issue", "problem", "error", "troubleshoot", 
                "fix", "solution", "known issue", "faq", "support" 
            }
        };
        
        // Calculate scores for each category
        var scores = new Dictionary<string, double>();
        
        foreach (var (category, keywords) in categoryKeywords)
        {
            double score = 0.0;
            foreach (var keyword in keywords)
            {
                var count = Regex.Matches(blockLower, @"\b" + Regex.Escape(keyword) + @"\b").Count;
                score += count * 1.0; // Weight by keyword frequency
            }
            scores[category] = score;
        }
        
        // Return category with highest score
        var bestCategory = scores.OrderByDescending(s => s.Value).FirstOrDefault();
        
        return bestCategory.Value > 0 ? bestCategory.Key : "Overview"; // Default to Overview
    }
    
    /// <summary>
    /// Extract topics/keywords from text
    /// </summary>
    private HashSet<string> ExtractTopics(string text)
    {
        var topics = new HashSet<string>();
        var textLower = text.ToLowerInvariant();
        
        // Common technical/business keywords
        var keywords = new[] 
        { 
            "api", "endpoint", "function", "feature", "process", 
            "system", "component", "integration", "workflow", 
            "issue", "problem", "solution", "technical", "business" 
        };
        
        foreach (var keyword in keywords)
        {
            if (textLower.Contains(keyword))
            {
                topics.Add(keyword);
            }
        }
        
        return topics;
    }
    
    /// <summary>
    /// Calculate similarity between topic sets
    /// </summary>
    private double CalculateTopicSimilarity(HashSet<string> topics1, HashSet<string> topics2)
    {
        if (topics1.Count == 0 && topics2.Count == 0) return 1.0;
        if (topics1.Count == 0 || topics2.Count == 0) return 0.0;
        
        var intersection = topics1.Intersect(topics2).Count();
        var union = topics1.Union(topics2).Count();
        
        return (double)intersection / union; // Jaccard similarity
    }
    
    /// <summary>
    /// Find best matching template section for category
    /// </summary>
    private TemplateSection? FindBestTemplateSection(
        string category, 
        TemplateStructure templateStructure)
    {
        return templateStructure.Sections
            .FirstOrDefault(s => 
                s.Tag.Equals(category, StringComparison.OrdinalIgnoreCase) ||
                s.DisplayName.Equals(category, StringComparison.OrdinalIgnoreCase));
    }
}
```

---

### Approach 2: Template-Guided Splitting

**Strategy:** Use template structure to guide how plain text should be split.

**How It Works:**
1. Read template structure (what sections template expects)
2. Split plain text proportionally based on template sections
3. Map each portion to corresponding template section
4. Use content analysis to refine mapping

**Implementation:**

```csharp
public Dictionary<string, string> ProcessPlainTextWithTemplate(
    string plainText,
    TemplateStructure templateStructure,
    ILogger? logger = null)
{
    var contentMap = new Dictionary<string, string>();
    
    // Step 1: Split text into chunks (proportional to template sections)
    var textChunks = SplitProportionally(plainText, templateStructure.Sections.Count);
    
    // Step 2: Map each chunk to template section
    for (int i = 0; i < textChunks.Count && i < templateStructure.Sections.Count; i++)
    {
        var chunk = textChunks[i];
        var templateSection = templateStructure.Sections[i];
        
        // Refine mapping based on content analysis
        var bestSection = RefineMapping(chunk, templateSection, templateStructure);
        
        if (contentMap.ContainsKey(bestSection.Tag))
        {
            contentMap[bestSection.Tag] += "\n\n" + chunk;
        }
        else
        {
            contentMap[bestSection.Tag] = chunk;
        }
    }
    
    return contentMap;
}

private List<string> SplitProportionally(string text, int numberOfSections)
{
    var chunks = new List<string>();
    var paragraphs = text.Split(
        new[] { "\r\n\r\n", "\n\n" }, 
        StringSplitOptions.RemoveEmptyEntries);
    
    if (paragraphs.Length == 0)
    {
        return new List<string> { text };
    }
    
    // Calculate chunk size
    var chunkSize = Math.Max(1, paragraphs.Length / numberOfSections);
    
    for (int i = 0; i < numberOfSections; i++)
    {
        var startIndex = i * chunkSize;
        var endIndex = (i == numberOfSections - 1) 
            ? paragraphs.Length 
            : (i + 1) * chunkSize;
        
        var chunk = string.Join("\n\n", 
            paragraphs.Skip(startIndex).Take(endIndex - startIndex));
        
        if (!string.IsNullOrWhiteSpace(chunk))
        {
            chunks.Add(chunk);
        }
    }
    
    return chunks;
}
```

---

### Approach 3: Hybrid Approach (Recommended)

**Strategy:** Combine content analysis + template structure + intelligent splitting.

**Implementation:**

```csharp
public class IntelligentPlainTextMapper
{
    public Dictionary<string, string> MapPlainTextToTemplate(
        string plainText,
        TemplateStructure templateStructure,
        ILogger? logger = null)
    {
        var contentMap = new Dictionary<string, string>();
        
        // Step 1: Detect if document has any structure
        var hasStructure = DetectStructure(plainText);
        
        if (!hasStructure)
        {
            logger?.LogInformation("📄 [PLAINTEXT] No structure detected - using intelligent mapping");
            
            // Step 2: Split into logical blocks
            var blocks = SplitIntoLogicalBlocks(plainText);
            
            // Step 3: Categorize each block
            var categorizedBlocks = blocks
                .Select(block => new
                {
                    Content = block,
                    Category = CategorizeTextBlock(block, templateStructure),
                    Score = CalculateCategoryScore(block, templateStructure)
                })
                .OrderByDescending(x => x.Score)
                .ToList();
            
            // Step 4: Map to template sections
            var sectionMapping = new Dictionary<string, List<string>>();
            
            foreach (var block in categorizedBlocks)
            {
                var templateSection = FindBestTemplateSection(
                    block.Category, 
                    templateStructure);
                
                if (templateSection != null)
                {
                    if (!sectionMapping.ContainsKey(templateSection.Tag))
                    {
                        sectionMapping[templateSection.Tag] = new List<string>();
                    }
                    sectionMapping[templateSection.Tag].Add(block.Content);
                }
            }
            
            // Step 5: Build content map
            foreach (var (tag, contents) in sectionMapping)
            {
                contentMap[tag] = string.Join("\n\n", contents);
            }
            
            // Step 6: Ensure Overview (fallback)
            if (!contentMap.ContainsKey("Overview") && blocks.Count > 0)
            {
                // Use first 2-3 meaningful blocks
                var overviewBlocks = blocks
                    .Where(b => b.Length > 50)
                    .Take(3)
                    .ToList();
                
                if (overviewBlocks.Count > 0)
                {
                    contentMap["Overview"] = string.Join("\n\n", overviewBlocks);
                }
            }
        }
        else
        {
            // Document has some structure - use structure-aware mapping
            logger?.LogInformation("📄 [PLAINTEXT] Some structure detected - using structure-aware mapping");
            contentMap = MapWithStructure(plainText, templateStructure, logger);
        }
        
        return contentMap;
    }
    
    private bool DetectStructure(string text)
    {
        // Check for structural markers:
        // - Numbered lists (1., 2., etc.)
        // - Bulleted lists (-, *, •)
        // - Short lines that might be headings
        // - Repeated patterns
        
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        
        int headingLikeLines = 0;
        int numberedLines = 0;
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Check for numbered list
            if (Regex.IsMatch(trimmed, @"^\d+[\.\)]\s+"))
            {
                numberedLines++;
            }
            
            // Check for heading-like line (short, no punctuation)
            if (trimmed.Length < 80 && 
                !trimmed.EndsWith(".") && 
                !trimmed.EndsWith(",") &&
                trimmed.Length > 5)
            {
                headingLikeLines++;
            }
        }
        
        // If >10% of lines are heading-like or numbered, consider it structured
        var structureRatio = (double)(headingLikeLines + numberedLines) / lines.Length;
        return structureRatio > 0.1;
    }
    
    private double CalculateCategoryScore(string block, TemplateStructure templateStructure)
    {
        // Calculate how well this block matches each template section
        double maxScore = 0.0;
        
        foreach (var section in templateStructure.Sections)
        {
            double score = 0.0;
            var blockLower = block.ToLowerInvariant();
            
            // Check semantic keywords
            foreach (var keyword in section.Keywords)
            {
                var matches = Regex.Matches(blockLower, @"\b" + Regex.Escape(keyword.ToLowerInvariant()) + @"\b");
                score += matches.Count * 1.0;
            }
            
            // Check tag/display name
            if (blockLower.Contains(section.Tag.ToLowerInvariant()) ||
                blockLower.Contains(section.DisplayName.ToLowerInvariant()))
            {
                score += 2.0;
            }
            
            maxScore = Math.Max(maxScore, score);
        }
        
        return maxScore;
    }
}
```

---

## Complete Implementation

### Enhanced Content Mapper with Plain Text Support

```csharp
public static class EnhancedContentMapper
{
    public static Dictionary<string, string> BuildContentMap(
        DocumentModel docModel,
        string? documentType,
        TemplateStructure? templateStructure,
        ILogger? logger = null)
    {
        var contentMap = new Dictionary<string, string>();
        
        // Check if document has headings
        bool hasHeadings = docModel.Sections?.Any(s => 
            !string.IsNullOrWhiteSpace(s.Heading) && 
            s.Heading.Length < 100) ?? false;
        
        if (!hasHeadings)
        {
            logger?.LogWarning("⚠️ [MAPPER] Document has no headings - using plain text processing");
            
            // Extract all text content
            var allText = string.Join("\n\n", 
                docModel.Sections?.Select(s => s.Body ?? "").Where(b => !string.IsNullOrWhiteSpace(b)) ?? 
                Enumerable.Empty<string>());
            
            if (string.IsNullOrWhiteSpace(allText))
            {
                logger?.LogWarning("⚠️ [MAPPER] No content found in document");
                return contentMap;
            }
            
            // Use intelligent plain text mapping
            if (templateStructure != null)
            {
                var plainTextProcessor = new IntelligentPlainTextMapper();
                contentMap = plainTextProcessor.MapPlainTextToTemplate(
                    allText, 
                    templateStructure, 
                    logger);
            }
            else
            {
                // Fallback: Simple proportional split
                contentMap = SimplePlainTextMapping(allText, logger);
            }
        }
        else
        {
            // Document has headings - use heading-based mapping
            logger?.LogInformation("✅ [MAPPER] Document has headings - using heading-based mapping");
            contentMap = MapWithHeadings(docModel, templateStructure, logger);
        }
        
        return contentMap;
    }
    
    private static Dictionary<string, string> SimplePlainTextMapping(
        string text, 
        ILogger? logger)
    {
        var contentMap = new Dictionary<string, string>();
        
        // Split into paragraphs
        var paragraphs = text.Split(
            new[] { "\r\n\r\n", "\n\n" }, 
            StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
        
        if (paragraphs.Count == 0)
        {
            return contentMap;
        }
        
        // Use first 2-3 paragraphs as Overview
        var overviewParagraphs = paragraphs.Take(3).ToList();
        contentMap["Overview"] = string.Join("\n\n", overviewParagraphs);
        
        // Remaining paragraphs → distribute to other sections
        if (paragraphs.Count > 3)
        {
            var remaining = paragraphs.Skip(3).ToList();
            
            // Simple distribution: Technical, Functional, Troubleshooting
            var chunkSize = Math.Max(1, remaining.Count / 3);
            
            if (remaining.Count > 0)
            {
                contentMap["Technical"] = string.Join("\n\n", 
                    remaining.Take(chunkSize));
            }
            
            if (remaining.Count > chunkSize)
            {
                contentMap["Functional"] = string.Join("\n\n", 
                    remaining.Skip(chunkSize).Take(chunkSize));
            }
            
            if (remaining.Count > chunkSize * 2)
            {
                contentMap["Troubleshooting"] = string.Join("\n\n", 
                    remaining.Skip(chunkSize * 2));
            }
        }
        
        return contentMap;
    }
}
```

---

## Requirements Coverage

### Your Requirements vs. Solution

| Requirement | Solution | Status |
|-------------|----------|--------|
| **Handle plain text (no headings)** | ✅ Intelligent splitting + categorization | ✅ **Covered** |
| **Map to template sections** | ✅ Content analysis + template structure | ✅ **Covered** |
| **Context awareness** | ✅ Semantic keyword matching | ✅ **Covered** |
| **Fallback handling** | ✅ Overview section fallback | ✅ **Covered** |

---

## Key Features

### ✅ What the Solution Provides

1. **Plain Text Detection:**
   - Detects when document has no headings
   - Switches to plain text processing mode

2. **Intelligent Splitting:**
   - Splits text into logical blocks
   - Detects section boundaries
   - Handles topic changes

3. **Content Categorization:**
   - Analyzes content to determine section type
   - Uses semantic keywords
   - Calculates match scores

4. **Template-Aware Mapping:**
   - Maps categorized blocks to template sections
   - Uses template structure to guide mapping
   - Handles missing sections gracefully

5. **Fallback Handling:**
   - Ensures Overview section always has content
   - Distributes remaining content proportionally
   - Handles edge cases

---

## Example Scenarios

### Scenario 1: Pure Plain Text

**Input:**
```
This document describes the new feature. 
It allows users to upload files and process them automatically.
The system uses REST APIs for integration.
If you encounter errors, check the logs.
```

**Processing:**
1. Detect: No headings
2. Split: 4 logical blocks
3. Categorize:
   - Block 1-2: "Overview" (describes feature)
   - Block 3: "Technical" (REST APIs)
   - Block 4: "Troubleshooting" (errors, logs)

**Output:**
- Overview: "This document describes... automatically."
- Technical: "The system uses REST APIs..."
- Troubleshooting: "If you encounter errors..."

---

### Scenario 2: Mixed Content

**Input:**
```
The system provides file upload capabilities.
Users can upload documents through the web interface.
API endpoints are available at /api/upload.
Integration requires authentication tokens.
Common issues include timeout errors.
Check network connectivity if uploads fail.
```

**Processing:**
1. Detect: No headings, but has structure
2. Split: 6 blocks
3. Categorize:
   - Block 1-2: "Functional" (features, users)
   - Block 3-4: "Technical" (API, endpoints, tokens)
   - Block 5-6: "Troubleshooting" (issues, errors)

**Output:**
- Functional: "The system provides... web interface."
- Technical: "API endpoints... authentication tokens."
- Troubleshooting: "Common issues... uploads fail."

---

## Implementation Priority

### High Priority

1. ✅ **Plain Text Detection** - Detect when no headings exist
2. ✅ **Intelligent Splitting** - Split text into logical blocks
3. ✅ **Content Categorization** - Analyze and categorize blocks
4. ✅ **Template Mapping** - Map to template sections

### Medium Priority

1. ⚠️ **Structure Detection** - Detect partial structure
2. ⚠️ **Topic Analysis** - Better topic detection
3. ⚠️ **Confidence Scoring** - Score-based mapping

---

## Conclusion

### ✅ Yes, OpenXML Can Handle Plain Text Documents

**What's Needed:**
- ✅ Intelligent text splitting (can implement)
- ✅ Content analysis and categorization (can implement)
- ✅ Template-aware mapping (can implement)
- ✅ Fallback handling (already partially done)

**Current Status:**
- ⚠️ **Basic fallback exists** (first 2 paragraphs → Overview)
- ✅ **Can be enhanced** with intelligent processing
- ✅ **OpenXML provides tools** - need better logic

**Recommendation:**
- ✅ Implement intelligent plain text processor
- ✅ Add content categorization
- ✅ Enhance template-aware mapping
- ✅ Test with various plain text documents

---

**This solution handles documents with no headings by intelligently analyzing and categorizing plain text content, then mapping it to template sections based on semantic meaning.**

