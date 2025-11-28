using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using SMEPilot.FunctionApp.Helpers;

namespace SMEPilot.FunctionApp.Services
{
    /// <summary>
    /// Service for Azure OpenAI operations - enhances document enrichment with AI
    /// </summary>
    public class AzureOpenAIService
    {
        private readonly OpenAIClient? _client;
        private readonly string? _gptDeployment;
        private readonly string? _embeddingDeployment;
        private readonly ILogger<AzureOpenAIService>? _logger;
        private readonly bool _isConfigured;

        public AzureOpenAIService(Config config, ILogger<AzureOpenAIService>? logger = null)
        {
            _logger = logger;
            _isConfigured = config.IsAzureOpenAIConfigured;

            if (!_isConfigured)
            {
                if (!config.AzureOpenAIEnabled)
                {
                    _logger?.LogInformation("ℹ️ [OPENAI] Azure OpenAI explicitly disabled via configuration (AzureOpenAI_Enabled=false)");
                }
                else
                {
                    _logger?.LogWarning("⚠️ [OPENAI] Azure OpenAI not configured - AI features will be disabled");
                }
                return;
            }

            try
            {
                var endpoint = new Uri(config.AzureOpenAIEndpoint);
                var credential = new AzureKeyCredential(config.AzureOpenAIKey);
                _client = new OpenAIClient(endpoint, credential);
                _gptDeployment = config.AzureOpenAIDeploymentGPT;
                _embeddingDeployment = config.AzureOpenAIEmbeddingDeployment;
                _logger?.LogInformation("✅ [OPENAI] Azure OpenAI client initialized. GPT: {GPT}, Embedding: {Embedding}", _gptDeployment, _embeddingDeployment);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ [OPENAI] Failed to initialize Azure OpenAI client: {Error}", ex.Message);
                _isConfigured = false;
            }
        }

        public bool IsConfigured => _isConfigured && _client != null;

        /// <summary>
        /// Uses AI to intelligently match placeholder content from document sections
        /// </summary>
        public async Task<string?> FindContentForPlaceholderAsync(string placeholderName, List<string> documentSections, string? documentType = null)
        {
            if (!IsConfigured || documentSections == null || !documentSections.Any())
                return null;

            try
            {
                var prompt = BuildPlaceholderMatchingPrompt(placeholderName, documentSections, documentType);
                
                var chatCompletionsOptions = new ChatCompletionsOptions
                {
                    DeploymentName = _gptDeployment,
                    Messages =
                    {
                        new ChatRequestSystemMessage("You are a document analysis assistant. Extract the most relevant content for the given placeholder from the document sections. Return ONLY the extracted content, nothing else. If no relevant content is found, return 'NOT_FOUND'."),
                        new ChatRequestUserMessage(prompt)
                    },
                    Temperature = 0.3f, // Lower temperature for more consistent extraction
                    MaxTokens = 500 // Limit response length
                };

                var response = await _client!.GetChatCompletionsAsync(chatCompletionsOptions);
                var content = response.Value.Choices[0].Message.Content?.Trim();

                if (string.IsNullOrWhiteSpace(content) || content.Equals("NOT_FOUND", StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.LogDebug("🔍 [OPENAI] No content found for placeholder '{Placeholder}'", placeholderName);
                    return null;
                }

                _logger?.LogDebug("✅ [OPENAI] Found content for placeholder '{Placeholder}': {Content}", placeholderName, content.Substring(0, Math.Min(100, content.Length)));
                return content;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "⚠️ [OPENAI] Error finding content for placeholder '{Placeholder}': {Error}", placeholderName, ex.Message);
                return null; // Fall back to rule-based matching
            }
        }

        /// <summary>
        /// Uses AI to improve section mapping and content organization
        /// </summary>
        public async Task<Dictionary<string, string>?> MapSectionsToTemplateAsync(List<string> documentSections, List<string> templateSections)
        {
            if (!IsConfigured || documentSections == null || !documentSections.Any() || templateSections == null || !templateSections.Any())
                return null;

            try
            {
                var prompt = $@"Analyze the document sections and map them to template sections.

Document Sections:
{string.Join("\n", documentSections.Select((s, i) => $"{i + 1}. {s}"))}

Template Sections:
{string.Join("\n", templateSections.Select((s, i) => $"{i + 1}. {s}"))}

Return a JSON object mapping document section numbers to template section names. Example: {{""1"": ""Overview"", ""2"": ""Technical Details""}}";

                var chatCompletionsOptions = new ChatCompletionsOptions
                {
                    DeploymentName = _gptDeployment,
                    Messages =
                    {
                        new ChatRequestSystemMessage("You are a document structure analysis assistant. Map document sections to template sections. Return only valid JSON."),
                        new ChatRequestUserMessage(prompt)
                    },
                    Temperature = 0.2f,
                    MaxTokens = 1000
                };

                var response = await _client!.GetChatCompletionsAsync(chatCompletionsOptions);
                var jsonContent = response.Value.Choices[0].Message.Content?.Trim();

                if (string.IsNullOrWhiteSpace(jsonContent))
                    return null;

                // Parse JSON response (simplified - in production, use proper JSON parsing)
                var mapping = new Dictionary<string, string>();
                // TODO: Add proper JSON parsing here
                
                _logger?.LogDebug("✅ [OPENAI] Mapped {Count} sections to template", mapping.Count);
                return mapping;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "⚠️ [OPENAI] Error mapping sections: {Error}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Uses embeddings to find semantically similar content
        /// </summary>
        public async Task<List<string>> FindSimilarSectionsAsync(string placeholderName, List<string> documentSections, int topK = 3)
        {
            if (!IsConfigured || documentSections == null || !documentSections.Any())
                return new List<string>();

            try
            {
                // Get embedding for placeholder
                var placeholderEmbedding = await GetEmbeddingAsync(placeholderName);
                if (placeholderEmbedding == null || !placeholderEmbedding.Any())
                    return new List<string>();

                // Get embeddings for all sections
                var sectionEmbeddings = new List<(string section, float[] embedding)>();
                foreach (var section in documentSections)
                {
                    var embedding = await GetEmbeddingAsync(section);
                    if (embedding != null && embedding.Any())
                    {
                        sectionEmbeddings.Add((section, embedding));
                    }
                }

                // Calculate cosine similarity and return top K
                var similarities = sectionEmbeddings
                    .Select(se => new
                    {
                        Section = se.section,
                        Similarity = CosineSimilarity(placeholderEmbedding, se.embedding)
                    })
                    .OrderByDescending(s => s.Similarity)
                    .Take(topK)
                    .Where(s => s.Similarity > 0.7f) // Threshold for relevance
                    .Select(s => s.Section)
                    .ToList();

                _logger?.LogDebug("✅ [OPENAI] Found {Count} similar sections for '{Placeholder}'", similarities.Count, placeholderName);
                return similarities;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "⚠️ [OPENAI] Error finding similar sections: {Error}", ex.Message);
                return new List<string>();
            }
        }

        private async Task<float[]?> GetEmbeddingAsync(string text)
        {
            if (!IsConfigured || string.IsNullOrWhiteSpace(text))
                return null;

            try
            {
                // NOTE: In the current Azure.AI.OpenAI SDK, EmbeddingsOptions.Input is read-only
                // and must be provided via the constructor. The available constructor takes
                // a primary input plus an optional collection of additional inputs.
                var embeddingsOptions = new EmbeddingsOptions(text, Array.Empty<string>())
                {
                    DeploymentName = _embeddingDeployment
                };

                var response = await _client!.GetEmbeddingsAsync(embeddingsOptions);
                return response.Value.Data[0].Embedding.ToArray();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "⚠️ [OPENAI] Error getting embedding: {Error}", ex.Message);
                return null;
            }
        }

        private static float CosineSimilarity(float[] vector1, float[] vector2)
        {
            if (vector1.Length != vector2.Length)
                return 0f;

            float dotProduct = 0f;
            float magnitude1 = 0f;
            float magnitude2 = 0f;

            for (int i = 0; i < vector1.Length; i++)
            {
                dotProduct += vector1[i] * vector2[i];
                magnitude1 += vector1[i] * vector1[i];
                magnitude2 += vector2[i] * vector2[i];
            }

            magnitude1 = (float)Math.Sqrt(magnitude1);
            magnitude2 = (float)Math.Sqrt(magnitude2);

            if (magnitude1 == 0f || magnitude2 == 0f)
                return 0f;

            return dotProduct / (magnitude1 * magnitude2);
        }

        private string BuildPlaceholderMatchingPrompt(string placeholderName, List<string> documentSections, string? documentType)
        {
            var sectionsText = string.Join("\n\n", documentSections.Select((s, i) => $"Section {i + 1}:\n{s}"));

            // Derive a human-readable description of the placeholder from its name
            var normalizedName = placeholderName
                .Trim('[', ']')
                .Replace("#", string.Empty)
                .Replace("/", string.Empty)
                .Replace("_", " ")
                .Trim();

            return $@"
You are an expert technical writer helping to populate a structured requirements / design template
using the content from the document below.

Placeholder name   : ""{placeholderName}""
Interpreted meaning: ""{normalizedName}""
Document type      : {documentType ?? "Unknown"}

Document sections:
{sectionsText}

Your task:
- Write the best possible content for this placeholder, using the information and intent of the document.
- If the document contains relevant text, reuse and lightly edit it so it reads well in the template.
- If the document does NOT explicitly contain enough detail, infer and GENERATE clear, professional content
  that fits the placeholder name and typical expectations for a business/technical specification.
- For placeholders that represent lists (e.g., names starting with '#' or plural nouns like ""SCOPE_ITEMS"",
  ""FEATURES"", ""REFERENCES""), return a concise bullet list or numbered list.
- For single-value placeholders (e.g., ""PROJECT_NAME"", ""EXCUTIVE_OVERVIEW"", ""OBJECTIVE_DESCRIPTION""),
  return a short, well-structured sentence or paragraph.
- Do NOT return JSON, explanations, or phrases like ""NOT_FOUND"" or ""no information"".
- Do NOT mention that information is missing; always provide the best possible content.
- Return ONLY the final text that should appear in the document (max ~500 words).";
        }
    }
}

