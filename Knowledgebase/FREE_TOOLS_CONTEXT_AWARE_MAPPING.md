# Free Tools for Context-Aware Content Mapping & Plain Text Handling

## Your Specific Need

**Requirements:**
1. ✅ Context-aware heading mapping (semantic understanding)
2. ✅ Plain text document handling (no headings)
3. ✅ Content categorization and section detection
4. ❌ **NO AI** - Must be rule-based (critical constraint)

**Question:** Are there free tools that can handle these cases better than OpenXML?

**Answer:** ⚠️ **Limited options** - Most advanced tools use AI, but there are some rule-based alternatives.

---

## Critical Constraint: NO AI

**Your Requirement:**
- ❌ **NO AI** - All logic must be rule-based
- ✅ Rule-based processing only

**Impact:**
- ❌ Cannot use AI-powered tools (most advanced tools)
- ✅ Must use rule-based libraries
- ✅ Can use keyword matching, pattern recognition, heuristics

---

## Free Rule-Based Tools Analysis

### 1. ML.NET (Microsoft Machine Learning) ⚠️

**Cost:** ✅ **Free** - Open-source

**Capabilities:**
- ✅ Text classification (can be rule-based)
- ✅ Document structure detection
- ✅ Content categorization
- ⚠️ Primarily ML-based (but can use rule-based models)

**Pros:**
- ✅ Free and open-source
- ✅ .NET native
- ✅ Can create rule-based models
- ✅ Good for text classification

**Cons:**
- ⚠️ **Requires Training** - Need to train models (even rule-based)
- ⚠️ **Complex Setup** - More complex than simple keyword matching
- ⚠️ **May Violate "No AI"** - Even rule-based models might be considered ML

**Verdict:** ⚠️ **Questionable** - Might violate "no AI" requirement

---

### 2. Natural Language Toolkit (NLTK) - Python ⚠️

**Cost:** ✅ **Free** - Open-source

**Capabilities:**
- ✅ Text processing
- ✅ Keyword extraction
- ✅ Pattern matching
- ✅ Rule-based classification

**Pros:**
- ✅ Free
- ✅ Good for text processing
- ✅ Rule-based features available

**Cons:**
- ❌ **Python, Not .NET** - Would need Python integration
- ⚠️ **Complex** - More than needed for simple use case
- ⚠️ **Integration Overhead** - Need to call from .NET

**Verdict:** ⚠️ **Not Suitable** - Wrong language, complex integration

---

### 3. Stanford CoreNLP - Java ⚠️

**Cost:** ✅ **Free** - Open-source

**Capabilities:**
- ✅ Text processing
- ✅ Structure detection
- ✅ Rule-based parsing

**Pros:**
- ✅ Free
- ✅ Good NLP capabilities

**Cons:**
- ❌ **Java, Not .NET** - Wrong language
- ⚠️ **Complex** - Overkill for simple use case
- ⚠️ **Integration Overhead** - Need to call from .NET

**Verdict:** ❌ **Not Suitable** - Wrong language

---

### 4. Regex + Custom Logic (Current Approach) ✅

**Cost:** ✅ **Free** - Built into .NET

**Capabilities:**
- ✅ Pattern matching
- ✅ Keyword detection
- ✅ Rule-based classification
- ✅ Custom logic

**Pros:**
- ✅ Free (built-in)
- ✅ .NET native
- ✅ Full control
- ✅ Rule-based (no AI)

**Cons:**
- ⚠️ **Manual Implementation** - Need to write logic
- ⚠️ **Limited** - Basic pattern matching

**Verdict:** ✅ **Current Approach** - Can be enhanced

---

### 5. Spacy (Python) ⚠️

**Cost:** ✅ **Free** - Open-source

**Capabilities:**
- ✅ Text processing
- ✅ Rule-based matching
- ✅ Pattern recognition

**Pros:**
- ✅ Free
- ✅ Good rule-based features

**Cons:**
- ❌ **Python, Not .NET** - Wrong language
- ⚠️ **Integration Overhead**

**Verdict:** ❌ **Not Suitable** - Wrong language

---

### 6. Custom Rule Engine ✅

**Cost:** ✅ **Free** - Build yourself

**Capabilities:**
- ✅ Full control
- ✅ Rule-based only
- ✅ Custom logic

**Pros:**
- ✅ Free
- ✅ .NET native
- ✅ Rule-based (no AI)
- ✅ Tailored to your needs

**Cons:**
- ⚠️ **Development Effort** - Need to build
- ⚠️ **Maintenance** - Need to maintain

**Verdict:** ✅ **Best Option** - Build custom rule engine

---

## Comparison Matrix

| Tool | Free | .NET | Rule-Based | Context-Aware | Plain Text | Recommendation |
|------|------|------|------------|---------------|------------|----------------|
| **ML.NET** | ✅ | ✅ | ⚠️ Maybe | ✅ | ✅ | ⚠️ Questionable (may violate no AI) |
| **NLTK** | ✅ | ❌ | ✅ | ✅ | ✅ | ❌ Wrong language |
| **CoreNLP** | ✅ | ❌ | ✅ | ✅ | ✅ | ❌ Wrong language |
| **Regex + Custom** | ✅ | ✅ | ✅ | ⚠️ Basic | ⚠️ Basic | ✅ **Current - Can Enhance** |
| **Spacy** | ✅ | ❌ | ✅ | ✅ | ✅ | ❌ Wrong language |
| **Custom Rule Engine** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ **Best Option** |

---

## Recommendation: Enhanced Custom Rule Engine

### Why Custom Rule Engine is Best

1. ✅ **Meets Requirements:**
   - Rule-based (no AI)
   - .NET native
   - Full control
   - Tailored to your needs

2. ✅ **Free:**
   - No licensing costs
   - No dependencies
   - Built with .NET standard libraries

3. ✅ **Flexible:**
   - Can implement any logic
   - Easy to customize
   - Can enhance over time

4. ✅ **Maintainable:**
   - Understandable code
   - Easy to debug
   - Can modify as needed

---

## Implementation: Enhanced Rule Engine

### What to Build

**1. Semantic Keyword Dictionary:**
```csharp
public class SemanticKeywordDictionary
{
    private readonly Dictionary<string, List<string>> _keywords;
    
    public SemanticKeywordDictionary()
    {
        _keywords = new Dictionary<string, List<string>>
        {
            ["Overview"] = new List<string> 
            { 
                "overview", "summary", "introduction", "background", 
                "abstract", "preface", "executive summary", "about",
                "purpose", "objective", "goal"
            },
            ["Functional"] = new List<string> 
            { 
                "functional", "features", "requirements", "business", 
                "capabilities", "functionality", "use cases", "workflow",
                "process", "steps", "how to", "user"
            },
            ["Technical"] = new List<string> 
            { 
                "technical", "implementation", "architecture", "design", 
                "system", "components", "api", "endpoints", "integration",
                "code", "sdk", "rest", "graph", "database"
            },
            ["Troubleshooting"] = new List<string> 
            { 
                "troubleshooting", "issues", "problems", "errors", 
                "known issues", "faq", "support", "debugging", "fix",
                "solution", "resolve"
            }
        };
    }
    
    public string? FindCategory(string text)
    {
        var textLower = text.ToLowerInvariant();
        var scores = new Dictionary<string, double>();
        
        foreach (var (category, keywords) in _keywords)
        {
            double score = 0.0;
            foreach (var keyword in keywords)
            {
                var count = Regex.Matches(textLower, @"\b" + Regex.Escape(keyword) + @"\b").Count;
                score += count * 1.0;
            }
            scores[category] = score;
        }
        
        var bestMatch = scores.OrderByDescending(s => s.Value).FirstOrDefault();
        return bestMatch.Value > 0 ? bestMatch.Key : null;
    }
}
```

**2. Content Structure Analyzer:**
```csharp
public class ContentStructureAnalyzer
{
    public DocumentStructure AnalyzeStructure(string text)
    {
        var structure = new DocumentStructure();
        
        // Detect headings (short lines, no punctuation)
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var headings = new List<string>();
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (IsLikelyHeading(trimmed))
            {
                headings.Add(trimmed);
            }
        }
        
        structure.HasHeadings = headings.Count > 0;
        structure.HeadingCount = headings.Count;
        structure.IsPlainText = headings.Count == 0;
        
        return structure;
    }
    
    private bool IsLikelyHeading(string line)
    {
        // Rule-based heading detection
        if (line.Length > 100) return false;
        if (line.Length < 3) return false;
        if (line.EndsWith(".") || line.EndsWith(",")) return false;
        if (Regex.IsMatch(line, @"^\d+[\.\)]\s+")) return true; // Numbered
        if (line.All(c => char.IsUpper(c) || char.IsWhiteSpace(c))) return true; // All caps
        
        return false;
    }
}
```

**3. Intelligent Content Mapper:**
```csharp
public class IntelligentContentMapper
{
    private readonly SemanticKeywordDictionary _keywordDict;
    private readonly ContentStructureAnalyzer _structureAnalyzer;
    
    public Dictionary<string, string> MapContent(
        string text,
        TemplateStructure templateStructure)
    {
        var contentMap = new Dictionary<string, string>();
        
        // Analyze structure
        var structure = _structureAnalyzer.AnalyzeStructure(text);
        
        if (structure.IsPlainText)
        {
            // Plain text processing
            contentMap = ProcessPlainText(text, templateStructure);
        }
        else
        {
            // Heading-based processing
            contentMap = ProcessWithHeadings(text, templateStructure);
        }
        
        return contentMap;
    }
    
    private Dictionary<string, string> ProcessPlainText(
        string text,
        TemplateStructure templateStructure)
    {
        var contentMap = new Dictionary<string, string>();
        
        // Split into blocks
        var blocks = SplitIntoBlocks(text);
        
        // Categorize each block
        foreach (var block in blocks)
        {
            var category = _keywordDict.FindCategory(block);
            if (category != null)
            {
                var templateSection = FindTemplateSection(category, templateStructure);
                if (templateSection != null)
                {
                    if (contentMap.ContainsKey(templateSection.Tag))
                    {
                        contentMap[templateSection.Tag] += "\n\n" + block;
                    }
                    else
                    {
                        contentMap[templateSection.Tag] = block;
                    }
                }
            }
        }
        
        // Fallback: ensure Overview
        if (!contentMap.ContainsKey("Overview") && blocks.Count > 0)
        {
            contentMap["Overview"] = string.Join("\n\n", blocks.Take(3));
        }
        
        return contentMap;
    }
}
```

---

## Free .NET Libraries for Text Processing

### 1. System.Text.RegularExpressions ✅

**Cost:** ✅ **Free** - Built into .NET

**Use For:**
- Pattern matching
- Keyword detection
- Text extraction

**Example:**
```csharp
var matches = Regex.Matches(text, @"\b(api|endpoint|integration)\b", RegexOptions.IgnoreCase);
```

---

### 2. System.Linq ✅

**Cost:** ✅ **Free** - Built into .NET

**Use For:**
- Text filtering
- Content analysis
- Data manipulation

**Example:**
```csharp
var technicalParagraphs = paragraphs
    .Where(p => p.Contains("api") || p.Contains("endpoint"))
    .ToList();
```

---

### 3. StringComparison & String Operations ✅

**Cost:** ✅ **Free** - Built into .NET

**Use For:**
- Text comparison
- Keyword matching
- Content categorization

**Example:**
```csharp
if (text.Contains("overview", StringComparison.OrdinalIgnoreCase))
{
    category = "Overview";
}
```

---

## Best Approach: Enhanced OpenXML + Custom Rule Engine

### Recommended Solution

**Use:**
1. ✅ **OpenXML** - For document processing (already using)
2. ✅ **Custom Rule Engine** - For context-aware mapping
3. ✅ **Built-in .NET Libraries** - For text processing

**Why:**
- ✅ All free
- ✅ All rule-based (no AI)
- ✅ .NET native
- ✅ Full control
- ✅ Can enhance incrementally

**Implementation:**
- Build semantic keyword dictionary
- Implement content structure analyzer
- Create intelligent content mapper
- Enhance existing OpenXML code

---

## Comparison: Tools vs. Custom Solution

| Aspect | Free Tools | Custom Rule Engine |
|--------|------------|-------------------|
| **Cost** | ✅ Free | ✅ Free |
| **.NET Support** | ⚠️ Mixed | ✅ Native |
| **Rule-Based** | ⚠️ Some use AI | ✅ Pure rule-based |
| **Customization** | ⚠️ Limited | ✅ Full control |
| **Maintenance** | ⚠️ External | ✅ Internal |
| **Learning Curve** | ⚠️ High | ✅ Low (your code) |
| **Integration** | ⚠️ Complex | ✅ Simple |

**Winner:** ✅ **Custom Rule Engine** - Best for your requirements

---

## Conclusion

### ✅ Best Option: Enhanced Custom Rule Engine

**Why:**
1. ✅ **Meets All Requirements:**
   - Free
   - Rule-based (no AI)
   - .NET native
   - Context-aware
   - Handles plain text

2. ✅ **No External Dependencies:**
   - Uses built-in .NET libraries
   - No licensing issues
   - Full control

3. ✅ **Can Enhance Incrementally:**
   - Start with basic rules
   - Add more sophisticated logic over time
   - Tailor to your specific needs

**Alternative Tools:**
- ⚠️ Most free tools use AI (violates requirement)
- ⚠️ Non-.NET tools require complex integration
- ⚠️ ML.NET might violate "no AI" requirement

**Recommendation:**
- ✅ **Stick with OpenXML** (for document processing)
- ✅ **Build Custom Rule Engine** (for context-aware mapping)
- ✅ **Use Built-in .NET Libraries** (for text processing)

---

## Implementation Plan

### Phase 1: Basic Rule Engine
1. Create semantic keyword dictionary
2. Implement basic content categorization
3. Add plain text detection

### Phase 2: Enhanced Mapping
1. Add template structure reading
2. Implement context-aware matching
3. Add confidence scoring

### Phase 3: Advanced Features
1. Topic detection
2. Section boundary detection
3. Content refinement

---

**This approach gives you the best free, rule-based solution that meets all your requirements.**
