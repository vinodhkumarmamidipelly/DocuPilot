# Best Free AI/ML Tools for Document Processing

## Your Updated Requirements

**Requirements:**
1. ✅ Context-aware heading mapping (semantic understanding)
2. ✅ Plain text document handling (no headings)
3. ✅ Content categorization and section detection
4. ✅ **CAN USE AI/ML** - No restrictions!
5. ✅ **Free tools only**

**Goal:** Find the **best free AI/ML tools** for document understanding and content mapping.

---

## Top Free AI/ML Tools for Your Use Case

### 1. ML.NET (Microsoft) ✅ **BEST FOR .NET**

**Cost:** ✅ **100% Free** - Open-source

**What It Does:**
- ✅ Text classification (categorize content)
- ✅ Document structure detection
- ✅ Content categorization
- ✅ Context-aware mapping

**Capabilities:**
- ✅ Train custom models for your document types
- ✅ Text classification (Overview, Functional, Technical, etc.)
- ✅ Document structure analysis
- ✅ Content categorization

**Pros:**
- ✅ **Free and open-source**
- ✅ **.NET Native** - Perfect for Azure Functions
- ✅ **No API calls** - Runs locally
- ✅ **Can train on your documents**
- ✅ **Microsoft-supported**

**Cons:**
- ⚠️ Requires training data (but can use rule-based initially)
- ⚠️ Training time needed

**Integration:**
```csharp
// Install: dotnet add package Microsoft.ML
// Install: dotnet add package Microsoft.ML.TextAnalytics

using Microsoft.ML;
using Microsoft.ML.Data;

// Train model for content classification
var mlContext = new MLContext();
var model = TrainContentClassifier(mlContext, trainingData);

// Classify content
var prediction = model.Predict(new ContentInput { Text = content });
// Returns: Category (Overview, Functional, Technical, etc.)
```

**Verdict:** ✅ **BEST OPTION** - Perfect for .NET, free, powerful

---

### 2. Azure Cognitive Services - Text Analytics ✅

**Cost:** ✅ **Free Tier Available** - $0 for first 5,000 transactions/month

**What It Does:**
- ✅ Text classification
- ✅ Key phrase extraction
- ✅ Sentiment analysis
- ✅ Language detection
- ✅ Named entity recognition

**Capabilities:**
- ✅ Categorize content automatically
- ✅ Extract key phrases (for section detection)
- ✅ Understand document structure
- ✅ Context-aware analysis

**Pros:**
- ✅ **Free tier** (5,000 transactions/month)
- ✅ **.NET SDK** - Easy integration
- ✅ **No training needed** - Pre-trained models
- ✅ **High accuracy**
- ✅ **Microsoft-supported**

**Cons:**
- ⚠️ API calls (requires internet)
- ⚠️ Free tier has limits

**Integration:**
```csharp
// Install: dotnet add package Azure.AI.TextAnalytics

using Azure;
using Azure.AI.TextAnalytics;

var client = new TextAnalyticsClient(
    new Uri("https://your-endpoint.cognitiveservices.azure.com/"),
    new AzureKeyCredential("your-key"));

// Classify content
var response = await client.AnalyzeSentimentAsync(content);
var keyPhrases = await client.ExtractKeyPhrasesAsync(content);
```

**Verdict:** ✅ **EXCELLENT** - Pre-trained, easy to use, free tier

---

### 3. Azure OpenAI Service ⚠️

**Cost:** ⚠️ **Free Tier Limited** - Pay-per-use after free credits

**What It Does:**
- ✅ Advanced text understanding
- ✅ Content categorization
- ✅ Semantic analysis
- ✅ Context-aware mapping

**Capabilities:**
- ✅ Understand document structure
- ✅ Categorize content semantically
- ✅ Map content to sections
- ✅ Handle plain text intelligently

**Pros:**
- ✅ **Most powerful** - Best understanding
- ✅ **.NET SDK** - Easy integration
- ✅ **Context-aware** - Understands meaning
- ✅ **Handles plain text** - Very well

**Cons:**
- ⚠️ **Costs money** after free credits
- ⚠️ API calls (requires internet)
- ⚠️ Rate limits

**Integration:**
```csharp
// Install: dotnet add package Azure.AI.OpenAI

using Azure.AI.OpenAI;

var client = new OpenAIClient(
    new Uri("https://your-endpoint.openai.azure.com/"),
    new AzureKeyCredential("your-key"));

// Classify and map content
var response = await client.GetChatCompletionsAsync(
    new ChatCompletionsOptions
    {
        Messages = 
        {
            new ChatMessage(ChatRole.System, 
                "Categorize this content into: Overview, Functional, Technical, or Troubleshooting"),
            new ChatMessage(ChatRole.User, content)
        }
    });
```

**Verdict:** ⚠️ **BEST BUT COSTS** - Most powerful, but not fully free

---

### 4. Hugging Face Transformers (.NET) ✅

**Cost:** ✅ **Free** - Open-source models

**What It Does:**
- ✅ Text classification
- ✅ Document understanding
- ✅ Content categorization
- ✅ Pre-trained models

**Capabilities:**
- ✅ Use pre-trained models
- ✅ Text classification
- ✅ Zero-shot classification (no training needed)
- ✅ Document structure detection

**Pros:**
- ✅ **Free** - Open-source
- ✅ **Pre-trained models** - No training needed
- ✅ **.NET support** - Via ML.NET or ONNX
- ✅ **Many models available**

**Cons:**
- ⚠️ Integration complexity
- ⚠️ Model size (memory usage)
- ⚠️ May need Python bridge

**Integration:**
```csharp
// Via ML.NET ONNX Runtime
// Install: dotnet add package Microsoft.ML.OnnxRuntime

using Microsoft.ML.OnnxRuntime;

// Load pre-trained model
var session = new InferenceSession("text-classification-model.onnx");

// Classify content
var inputs = new List<NamedOnnxValue>
{
    NamedOnnxValue.CreateFromTensor("input", tensor)
};
var results = session.Run(inputs);
```

**Verdict:** ✅ **GOOD** - Free, powerful, but complex integration

---

### 5. OpenAI API (Direct) ⚠️

**Cost:** ⚠️ **Pay-per-use** - Not free (but very cheap)

**What It Does:**
- ✅ Best text understanding
- ✅ Content categorization
- ✅ Semantic analysis
- ✅ Context-aware mapping

**Pros:**
- ✅ **Most powerful** - Best results
- ✅ **Easy integration**
- ✅ **Handles all cases** - Headings, plain text, etc.

**Cons:**
- ❌ **Not free** - Pay-per-use
- ⚠️ API calls required

**Verdict:** ⚠️ **BEST BUT NOT FREE** - Excellent but costs money

---

## Comparison Matrix

| Tool | Free | .NET | Context-Aware | Plain Text | Training | Recommendation |
|------|------|------|---------------|------------|----------|----------------|
| **ML.NET** | ✅ | ✅ | ✅ | ✅ | ⚠️ Needed | ✅ **BEST FOR .NET** |
| **Azure Text Analytics** | ✅ Tier | ✅ | ✅ | ✅ | ❌ No | ✅ **EXCELLENT** |
| **Azure OpenAI** | ⚠️ Limited | ✅ | ✅✅ | ✅✅ | ❌ No | ⚠️ Best but costs |
| **Hugging Face** | ✅ | ⚠️ Complex | ✅ | ✅ | ❌ No | ⚠️ Complex |
| **OpenAI API** | ❌ | ✅ | ✅✅ | ✅✅ | ❌ No | ❌ Not free |

---

## Recommended Solution: Hybrid Approach

### Best Free Combination

**1. ML.NET (Primary)**
- ✅ Train custom classifier for your document types
- ✅ Free, .NET native, runs locally
- ✅ Handles content categorization

**2. Azure Text Analytics (Enhancement)**
- ✅ Use for key phrase extraction
- ✅ Free tier: 5,000 transactions/month
- ✅ Enhances ML.NET results

**3. OpenXML (Document Processing)**
- ✅ Keep for document manipulation
- ✅ Works with AI/ML results

---

## Implementation: ML.NET for Content Classification

### Step 1: Install ML.NET

```bash
dotnet add package Microsoft.ML
dotnet add package Microsoft.ML.TextAnalytics
```

### Step 2: Define Data Models

```csharp
public class ContentInput
{
    [LoadColumn(0)]
    public string Text { get; set; }
    
    [LoadColumn(1)]
    public string Category { get; set; } // Overview, Functional, Technical, etc.
}

public class ContentPrediction
{
    [ColumnName("PredictedLabel")]
    public string Category { get; set; }
    
    public float[] Score { get; set; }
}
```

### Step 3: Train Model

```csharp
public ITransformer TrainContentClassifier(MLContext mlContext, string trainingDataPath)
{
    // Load training data
    var dataView = mlContext.Data.LoadFromTextFile<ContentInput>(
        trainingDataPath,
        separatorChar: '\t',
        hasHeader: true);
    
    // Define pipeline
    var pipeline = mlContext.Transforms.Text
        .FeaturizeText("Features", nameof(ContentInput.Text))
        .Append(mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(ContentInput.Category)))
        .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
        .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));
    
    // Train
    var model = pipeline.Fit(dataView);
    return model;
}
```

### Step 4: Use for Classification

```csharp
public class IntelligentContentClassifier
{
    private readonly PredictionEngine<ContentInput, ContentPrediction> _predictor;
    
    public IntelligentContentClassifier(string modelPath)
    {
        var mlContext = new MLContext();
        var model = mlContext.Model.Load(modelPath, out var schema);
        _predictor = mlContext.Model.CreatePredictionEngine<ContentInput, ContentPrediction>(model);
    }
    
    public string ClassifyContent(string text)
    {
        var input = new ContentInput { Text = text };
        var prediction = _predictor.Predict(input);
        return prediction.Category;
    }
    
    public Dictionary<string, string> MapContentToSections(
        DocumentModel docModel,
        TemplateStructure templateStructure)
    {
        var contentMap = new Dictionary<string, string>();
        
        foreach (var section in docModel.Sections)
        {
            var content = section.Body ?? "";
            if (string.IsNullOrWhiteSpace(content)) continue;
            
            // Classify content
            var category = ClassifyContent(content);
            
            // Map to template section
            var templateSection = templateStructure.Sections
                .FirstOrDefault(s => s.Tag.Equals(category, StringComparison.OrdinalIgnoreCase));
            
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
        
        return contentMap;
    }
}
```

---

## Implementation: Azure Text Analytics Integration

### For Enhanced Key Phrase Extraction

```csharp
public class EnhancedContentMapper
{
    private readonly TextAnalyticsClient _textAnalyticsClient;
    private readonly IntelligentContentClassifier _classifier;
    
    public async Task<Dictionary<string, string>> MapContentIntelligently(
        DocumentModel docModel,
        TemplateStructure templateStructure)
    {
        var contentMap = new Dictionary<string, string>();
        
        foreach (var section in docModel.Sections)
        {
            var content = section.Body ?? "";
            if (string.IsNullOrWhiteSpace(content)) continue;
            
            // Use ML.NET for classification
            var category = _classifier.ClassifyContent(content);
            
            // Use Azure Text Analytics for key phrases (enhancement)
            var keyPhrases = await _textAnalyticsClient.ExtractKeyPhrasesAsync(content);
            
            // Refine category based on key phrases
            var refinedCategory = RefineCategory(category, keyPhrases.Value, templateStructure);
            
            // Map to template
            var templateSection = FindTemplateSection(refinedCategory, templateStructure);
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
        
        return contentMap;
    }
    
    private string RefineCategory(
        string initialCategory,
        IEnumerable<string> keyPhrases,
        TemplateStructure templateStructure)
    {
        // Use key phrases to refine classification
        var phraseLower = string.Join(" ", keyPhrases).ToLowerInvariant();
        
        // Check if key phrases suggest different category
        foreach (var section in templateStructure.Sections)
        {
            foreach (var keyword in section.Keywords)
            {
                if (phraseLower.Contains(keyword.ToLowerInvariant()))
                {
                    return section.Tag; // Override with more specific category
                }
            }
        }
        
        return initialCategory;
    }
}
```

---

## Best Free Solution: ML.NET + Azure Text Analytics

### Why This Combination

**ML.NET:**
- ✅ Free, .NET native
- ✅ Train on your documents
- ✅ Runs locally (no API calls)
- ✅ Handles content classification

**Azure Text Analytics:**
- ✅ Free tier (5,000/month)
- ✅ Key phrase extraction
- ✅ Enhances ML.NET results
- ✅ Easy .NET integration

**OpenXML:**
- ✅ Keep for document processing
- ✅ Works with AI results

---

## Implementation Plan

### Phase 1: ML.NET Setup (Week 1)
1. Install ML.NET packages
2. Create training data (sample documents)
3. Train content classifier
4. Test classification accuracy

### Phase 2: Integration (Week 2)
1. Integrate ML.NET into content mapper
2. Replace hardcoded keywords with ML classification
3. Test with various documents

### Phase 3: Enhancement (Week 3)
1. Add Azure Text Analytics (optional)
2. Enhance with key phrase extraction
3. Fine-tune model

---

## Training Data Example

### Sample Training Data (CSV)

```csv
Text	Category
This document provides an overview of the system.	Overview
The system allows users to upload files and process them.	Functional
API endpoints are available at /api/upload.	Technical
If you encounter errors, check the logs.	Troubleshooting
The feature enables automatic processing.	Functional
REST API integration requires authentication.	Technical
Common issues include timeout errors.	Troubleshooting
```

**Training:**
- Start with 50-100 sample documents
- Label content by category
- Train ML.NET model
- Improve over time

---

## Cost Analysis

### ML.NET
- ✅ **$0** - Completely free
- ✅ No API calls
- ✅ Runs locally

### Azure Text Analytics
- ✅ **$0** - Free tier (5,000 transactions/month)
- ⚠️ **$1 per 1,000** - After free tier
- ✅ Very affordable

### Total Cost
- **Free tier:** $0/month (if under 5,000 documents)
- **After free tier:** ~$0.001 per document

---

## Performance Comparison

| Tool | Accuracy | Speed | Cost | .NET Integration |
|------|----------|-------|------|------------------|
| **ML.NET** | ✅ High (with training) | ✅ Fast (local) | ✅ Free | ✅ Native |
| **Azure Text Analytics** | ✅ High | ✅ Fast | ✅ Free tier | ✅ Easy |
| **Azure OpenAI** | ✅✅ Very High | ⚠️ Moderate | ⚠️ Costs | ✅ Easy |
| **Hugging Face** | ✅ High | ⚠️ Moderate | ✅ Free | ⚠️ Complex |

---

## Recommendation

### ✅ Best Free Solution: ML.NET

**Why:**
1. ✅ **100% Free** - No costs
2. ✅ **.NET Native** - Perfect for Azure Functions
3. ✅ **High Accuracy** - With proper training
4. ✅ **No API Calls** - Runs locally
5. ✅ **Context-Aware** - Understands content meaning
6. ✅ **Handles Plain Text** - Classifies without headings

**Enhancement:**
- Add Azure Text Analytics for key phrases (free tier)
- Improves accuracy without significant cost

---

## Quick Start

### 1. Install ML.NET
```bash
dotnet add package Microsoft.ML
```

### 2. Create Training Data
- Collect 50-100 sample documents
- Label content by category
- Save as CSV

### 3. Train Model
```csharp
var mlContext = new MLContext();
var model = TrainContentClassifier(mlContext, "training-data.csv");
mlContext.Model.Save(model, dataView.Schema, "content-classifier.zip");
```

### 4. Use in Content Mapper
```csharp
var classifier = new IntelligentContentClassifier("content-classifier.zip");
var category = classifier.ClassifyContent(content);
```

---

## Conclusion

### ✅ Best Free Tool: ML.NET

**Why:**
- ✅ 100% free
- ✅ .NET native
- ✅ High accuracy
- ✅ Context-aware
- ✅ Handles plain text
- ✅ No API dependencies

**Result:**
- Better than hardcoded keywords
- Understands content semantically
- Handles documents with/without headings
- Free and maintainable

---

**ML.NET is the best free AI/ML tool for your use case - it's free, .NET native, and provides the context-aware mapping you need.**

