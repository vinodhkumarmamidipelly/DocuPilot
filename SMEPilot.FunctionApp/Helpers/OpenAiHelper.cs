using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Newtonsoft.Json;
using SMEPilot.FunctionApp.Models;
using System.IO;

namespace SMEPilot.FunctionApp.Helpers
{
    public class OpenAiHelper
    {
        private readonly OpenAIClient? _client;
        private readonly Config _cfg;
        private readonly string? _gptDeployment; // Store deployment name at initialization
        private readonly string? _embeddingDeployment; // Store embedding deployment name at initialization

        public OpenAiHelper(Config cfg)
        {
            _cfg = cfg;
            
            // Store deployment names at initialization (when env vars are available)
            _gptDeployment = cfg.AzureOpenAIDeployment;
            _embeddingDeployment = cfg.AzureOpenAIEmbeddingDeployment;
            
            // Debug: Log all config values at initialization
            Console.WriteLine($"🔍 [AI CONFIG] Initializing Azure OpenAI...");
            Console.WriteLine($"   Endpoint: '{cfg.AzureOpenAIEndpoint ?? "NULL"}'");
            Console.WriteLine($"   API Key: '{(string.IsNullOrWhiteSpace(cfg.AzureOpenAIKey) ? "NULL" : "SET")}'");
            Console.WriteLine($"   GPT Deployment: '{_gptDeployment ?? "NULL"}'");
            Console.WriteLine($"   Embedding Deployment: '{_embeddingDeployment ?? "NULL"}'");
            
            // Debug: Check environment variables directly
            var envEndpoint = System.Environment.GetEnvironmentVariable("AzureOpenAI_Endpoint");
            var envKey = System.Environment.GetEnvironmentVariable("AzureOpenAI_Key");
            var envGptDeployment = System.Environment.GetEnvironmentVariable("AzureOpenAI_Deployment_GPT");
            var envEmbeddingDeployment = System.Environment.GetEnvironmentVariable("AzureOpenAI_Embedding_Deployment");
            Console.WriteLine($"🔍 [AI CONFIG] Direct environment variables:");
            Console.WriteLine($"   AzureOpenAI_Endpoint: '{envEndpoint ?? "NULL"}'");
            Console.WriteLine($"   AzureOpenAI_Key: '{(string.IsNullOrWhiteSpace(envKey) ? "NULL" : "SET")}'");
            Console.WriteLine($"   AzureOpenAI_Deployment_GPT: '{envGptDeployment ?? "NULL"}'");
            Console.WriteLine($"   AzureOpenAI_Embedding_Deployment: '{envEmbeddingDeployment ?? "NULL"}'");
            
            if (!string.IsNullOrWhiteSpace(cfg.AzureOpenAIEndpoint) && !string.IsNullOrWhiteSpace(cfg.AzureOpenAIKey))
            {
                _client = new OpenAIClient(new Uri(cfg.AzureOpenAIEndpoint), new AzureKeyCredential(cfg.AzureOpenAIKey));
                Console.WriteLine($"✅ Azure OpenAI initialized successfully");
                Console.WriteLine($"   Endpoint: {cfg.AzureOpenAIEndpoint}");
                Console.WriteLine($"   GPT Deployment: {_gptDeployment ?? "NOT SET"}");
                Console.WriteLine($"   Embedding Deployment: {_embeddingDeployment ?? "NOT SET"}");
            }
            else
            {
                _client = null;
                Console.WriteLine($"⚠️ Azure OpenAI NOT configured - Using MOCK MODE");
                Console.WriteLine($"   Endpoint: {(string.IsNullOrWhiteSpace(cfg.AzureOpenAIEndpoint) ? "MISSING" : cfg.AzureOpenAIEndpoint)}");
                Console.WriteLine($"   API Key: {(string.IsNullOrWhiteSpace(cfg.AzureOpenAIKey) ? "MISSING" : "SET")}");
                Console.WriteLine($"   All AI responses will be mock data");
            }
        }

        public async Task<string> GenerateSectionsJsonAsync(string text, List<string> imageOcrs, string? fileId = null)
        {
            if (_client == null)
            {
                Console.WriteLine($"⚠️ [MOCK MODE] GenerateSectionsJson - Returning mock response (no Azure OpenAI configured)");
                var mockResponse = @"{
                    ""title"": ""Sample Document"",
                    ""sections"": [
                        {""id"": ""s1"", ""heading"": ""Introduction"", ""summary"": ""This is a sample document"", ""body"": ""This document was created in mock mode.""}
                    ],
                    ""images"": []
                }";
                return mockResponse;
            }

            Console.WriteLine($"🤖 [AI] Calling Azure OpenAI GPT for document sectioning...");
            Console.WriteLine($"   Deployment from config: '{_cfg.AzureOpenAIDeployment ?? "NULL"}'");
            Console.WriteLine($"   Deployment from field: '{_gptDeployment ?? "NULL"}'");
            Console.WriteLine($"   Text length: {text?.Length ?? 0} characters");
            Console.WriteLine($"   Images with OCR: {imageOcrs?.Count ?? 0}");
            
            // Debug: Check environment variable directly
            var deploymentFromEnv = System.Environment.GetEnvironmentVariable("AzureOpenAI_Deployment_GPT");
            Console.WriteLine($"   Debug - Direct env var: '{deploymentFromEnv ?? "NULL"}'");

            // Use stored deployment name (from initialization) or fallback to config
            var deploymentToUse = _gptDeployment ?? _cfg.AzureOpenAIDeployment ?? deploymentFromEnv;
            
            // Validate deployment name
            if (string.IsNullOrWhiteSpace(deploymentToUse))
            {
                var errorMsg = $"Azure OpenAI GPT Deployment name is not configured. Check 'AzureOpenAI_Deployment_GPT' in local.settings.json. Config value: '{_cfg.AzureOpenAIDeployment ?? "NULL"}', Stored value: '{_gptDeployment ?? "NULL"}', Env var: '{deploymentFromEnv ?? "NULL"}'";
                Console.WriteLine($"❌ [AI] {errorMsg}");
                throw new ArgumentNullException("DeploymentName", errorMsg);
            }
            
            Console.WriteLine($"✅ Using deployment: '{deploymentToUse}'");

            string system = @"You are a document enricher. Your job is to PRESERVE and EXPAND content, not condense it.
Output ONLY valid JSON matching the schema:
{""title"":""..."",""sections"":[{""id"":""s1"",""heading"":""..."",""summary"":""..."",""body"":""...""}],""images"":[{""id"":""img1"",""alt"":""...""}]}

CRITICAL INSTRUCTIONS:
1. PRESERVE all original content - do not remove or condense information
2. EXPAND and ENRICH the body text - add detail, explanations, and context
3. Create sections that logically group related content (up to 15 sections if needed)
4. Each section summary should be 20-40 words
5. Body text should be DETAILED and COMPREHENSIVE - expand on the original content
6. PRESERVE technical details: API endpoints, code snippets, lists, module names, etc.
7. PRESERVE all lists and bullet points - expand them, don't remove them
8. Make the enriched document MORE detailed than the original, not less

Return valid JSON only.";

            string user = $"RawText:\n{text}\n\nImageOCRs:\n{string.Join('\n', imageOcrs)}\n\nEnrich and expand this content. Preserve all technical details, lists, and information. Make it more comprehensive and detailed.";

            // Calculate dynamic MaxTokens based on input text length
            // Rough estimate: output should be 2-3x input length for enrichment
            // GPT-4o-mini max output is 16384 tokens, GPT-4 max is 4096-16384 depending on model
            // Use a dynamic calculation: base 4000 + (input chars / 4) * 2 (assuming ~4 chars per token)
            var estimatedInputTokens = (text?.Length ?? 0) / 4; // Rough estimate
            var dynamicMaxTokens = Math.Min(Math.Max(4000, estimatedInputTokens * 2), 8000); // Between 4000-8000 tokens
            
            Console.WriteLine($"📊 [AI] Input text: {text?.Length ?? 0} chars (~{estimatedInputTokens} tokens), Setting MaxTokens: {dynamicMaxTokens}");

            var options = new ChatCompletionsOptions(
                deploymentName: deploymentToUse,
                messages: new ChatRequestMessage[]
                {
                    new ChatRequestSystemMessage(system),
                    new ChatRequestUserMessage(user)
                })
            {
                Temperature = 0.3f, // Slightly higher for more creative expansion
                MaxTokens = dynamicMaxTokens
            };

            try
            {
                Console.WriteLine($"🔄 [AI] Sending request to GPT model '{deploymentToUse}'...");
                var response = await _client.GetChatCompletionsAsync(options);
                var content = response.Value.Choices[0].Message.Content;
                Console.WriteLine($"✅ [AI] GPT response received");
                Console.WriteLine($"   Response length: {content?.Length ?? 0} characters");
                Console.WriteLine($"   Tokens used: {response.Value.Usage?.TotalTokens ?? 0}");

                if (!TryParseToDocumentModel(content, out var docModel))
                {
                    Console.WriteLine($"⚠️ [AI] Initial response failed JSON validation, retrying with stricter prompt...");
                    string strictSystem = @"Return valid JSON only. No commentary. No code fences. Respond with raw JSON matching the schema exactly.";
                    var retryOpts = new ChatCompletionsOptions(
                        deploymentName: deploymentToUse,
                        messages: new ChatRequestMessage[]
                        {
                            new ChatRequestSystemMessage(strictSystem),
                            new ChatRequestUserMessage(user)
                        })
                    { 
                        Temperature = 0.3f, 
                        MaxTokens = dynamicMaxTokens
                    };
                    var retryResp = await _client.GetChatCompletionsAsync(retryOpts);
                    var retryContent = retryResp.Value.Choices[0].Message.Content;
                    Console.WriteLine($"🔄 [AI] Retry response received ({retryContent?.Length ?? 0} characters)");

                    if (!TryParseToDocumentModel(retryContent, out docModel))
                    {
                        Console.WriteLine($"❌ [AI] Retry also failed JSON validation - saving error for manual review");
                        await PersistLlmErrorAsync(fileId ?? Guid.NewGuid().ToString(), content, retryContent);
                        throw new InvalidDataException("LLM did not return valid JSON after retry. Manual review required.");
                    }
                    else
                    {
                        Console.WriteLine($"✅ [AI] Retry succeeded - JSON is valid");
                        return retryContent;
                    }
                }
                else
                {
                    Console.WriteLine($"✅ [AI] JSON validation passed - {docModel?.Sections?.Count ?? 0} sections created");
                    return content;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [AI] Error calling Azure OpenAI: {ex.Message}");
                Console.WriteLine($"   Exception type: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        private bool TryParseToDocumentModel(string json, out DocumentModel? model)
        {
            model = null;
            try
            {
                model = JsonConvert.DeserializeObject<DocumentModel>(json);
                if (model == null) return false;
                if (model.Sections == null || model.Sections.Count == 0) return false;
                foreach (var s in model.Sections)
                {
                    if (string.IsNullOrWhiteSpace(s.Heading) || string.IsNullOrWhiteSpace(s.Body))
                        return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task PersistLlmErrorAsync(string fileId, string original, string retry)
        {
            try
            {
                var outDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "samples", "output", "llm_errors");
                Directory.CreateDirectory(outDir);
                var path = Path.Combine(outDir, $"{fileId}_{DateTime.UtcNow:yyyyMMddHHmmss}.txt");
                var txt = $"ORIGINAL:\n{original}\n\nRETRY:\n{retry}";
                await File.WriteAllTextAsync(path, txt);
            }
            catch
            {
                // swallow to avoid masking the original exception
            }
        }

        public async Task<float[]> GetEmbeddingAsync(string input)
        {
            if (_client == null)
            {
                Console.WriteLine($"⚠️ [MOCK MODE] GetEmbedding - Returning mock embedding (no Azure OpenAI configured)");
                var mockEmbedding = new float[1536];
                Array.Fill(mockEmbedding, 0.1f);
                return mockEmbedding;
            }

            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine($"⚠️ [AI] GetEmbedding called with empty input - returning zero vector");
                return new float[1536];
            }

            // Validate embedding deployment name - use stored value or fallback to config
            var embeddingDeploymentToUse = _embeddingDeployment ?? _cfg.AzureOpenAIEmbeddingDeployment;
            
            if (string.IsNullOrWhiteSpace(embeddingDeploymentToUse))
            {
                var errorMsg = $"Azure OpenAI Embedding Deployment name is not configured. Check 'AzureOpenAI_Embedding_Deployment' in local.settings.json. Config value: '{_cfg.AzureOpenAIEmbeddingDeployment ?? "NULL"}', Stored value: '{_embeddingDeployment ?? "NULL"}'";
                Console.WriteLine($"❌ [AI] {errorMsg}");
                throw new ArgumentNullException("DeploymentName", errorMsg);
            }

            Console.WriteLine($"🤖 [AI] Calling Azure OpenAI for embeddings...");
            Console.WriteLine($"   Deployment: {embeddingDeploymentToUse}");
            Console.WriteLine($"   Input length: {input.Length} characters");

            try
            {
                var embeddingsOptions = new EmbeddingsOptions(embeddingDeploymentToUse, new[] { input });
                var resp = await _client.GetEmbeddingsAsync(embeddingsOptions);
                var embedding = resp.Value.Data[0].Embedding.ToArray();
                Console.WriteLine($"✅ [AI] Embedding created successfully");
                Console.WriteLine($"   Embedding dimensions: {embedding.Length}");
                Console.WriteLine($"   Tokens used: {resp.Value.Usage?.TotalTokens ?? 0}");
                return embedding;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [AI] Error creating embedding: {ex.Message}");
                Console.WriteLine($"   Exception type: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        public OpenAIClient? GetClient() => _client;
        public string GetDeployment() => _cfg.AzureOpenAIDeployment;
    }
}

