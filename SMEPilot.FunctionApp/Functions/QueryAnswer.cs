using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Newtonsoft.Json;
using SMEPilot.FunctionApp.Helpers;
using SMEPilot.FunctionApp.Models;
using System.Collections.Generic;
using Azure.AI.OpenAI;

namespace SMEPilot.FunctionApp.Functions
{
    public class QueryAnswer
    {
        private readonly OpenAiHelper _openai;
        private readonly IEmbeddingStore _embeddingStore;

        public QueryAnswer(OpenAiHelper openai, IEmbeddingStore embeddingStore)
        {
            _openai = openai;
            _embeddingStore = embeddingStore;
        }

        [Function("QueryAnswer")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic? data = JsonConvert.DeserializeObject(body);
                string? question = data?.question?.ToString();

                if (string.IsNullOrWhiteSpace(question))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("question is required");
                    return bad;
                }

                // Auto-detect tenant from user context (for org-wide access)
                string tenantId;
                if (req.Headers.TryGetValues("Authorization", out var authHeaders))
                {
                    var token = authHeaders.FirstOrDefault()?.Replace("Bearer ", "") ?? "";
                    tenantId = await UserContextHelper.GetTenantIdFromTokenAsync(token) ?? "default";
                }
                else if (data?.tenantId != null)
                {
                    tenantId = data.tenantId.ToString() ?? "default"; // Fallback for programmatic calls
                }
                else
                {
                    var bad = req.CreateResponse(HttpStatusCode.Unauthorized);
                    await bad.WriteStringAsync("Authorization required or tenantId must be provided");
                    return bad;
                }

                // 1. Get embedding for question
                var qEmb = await _openai.GetEmbeddingAsync(question);

                // 2. Fetch candidate docs for tenant (auto-detected from user context)
                var candidates = await _embeddingStore.GetEmbeddingsForTenantAsync(tenantId);

                // 3. Compute cosine similarity
                var ranked = candidates.Select(c => new
                {
                    Doc = c,
                    Score = CosineSimilarity(qEmb, c.Embedding ?? Array.Empty<float>())
                }).OrderByDescending(r => r.Score).Take(3).ToList();

                // 4. Build context and call OpenAI for synthesis
                if (ranked.Count == 0)
                {
                    var noResults = req.CreateResponse(HttpStatusCode.OK);
                    await noResults.WriteStringAsync(JsonConvert.SerializeObject(new
                    {
                        answer = "No relevant documents found for your question.",
                        sources = new List<object>()
                    }));
                    return noResults;
                }

                var context = string.Join("\n\n", ranked.Select(r => $"Heading: {r.Doc.Heading}\nSummary: {r.Doc.Summary}\nURL: {r.Doc.FileUrl}\n"));
                var system = "You are SMEPilot assistant. Synthesize a concise answer then list Sources with fileUrl and heading.";
                var user = $"Question: {question}\n\nSources:\n{context}";

                var client = _openai.GetClient();
                if (client == null)
                {
                    // Mock response in development mode
                    var mockAnswer = req.CreateResponse(HttpStatusCode.OK);
                    await mockAnswer.WriteStringAsync(JsonConvert.SerializeObject(new
                    {
                        answer = "This is a mock answer. Configure Azure OpenAI to get real answers.",
                        sources = ranked.Select(r => new { r.Doc.FileUrl, r.Doc.Heading, score = r.Score })
                    }));
                    return mockAnswer;
                }

                var deployment = _openai.GetDeployment();
                if (string.IsNullOrWhiteSpace(deployment))
                {
                    var bad = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await bad.WriteStringAsync("Azure OpenAI deployment name not configured");
                    return bad;
                }

                var options = new ChatCompletionsOptions(
                    deploymentName: deployment,
                    messages: new ChatRequestMessage[]
                    {
                        new ChatRequestSystemMessage(system),
                        new ChatRequestUserMessage(user)
                    })
                {
                    Temperature = 0.2f,
                    MaxTokens = 400
                };

                var resp = await client.GetChatCompletionsAsync(options);
                var answer = resp.Value.Choices.First().Message.Content;

                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteStringAsync(JsonConvert.SerializeObject(new
                {
                    answer,
                    sources = ranked.Select(r => new { r.Doc.FileUrl, r.Doc.Heading, score = r.Score })
                }));
                return res;
            }
            catch (Exception ex)
            {
                var res = req.CreateResponse(HttpStatusCode.InternalServerError);
                await res.WriteStringAsync("Query failed: " + ex.Message);
                return res;
            }
        }

        private static double CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length == 0 || b.Length == 0) return 0.0;
            
            double dot = 0, na = 0, nb = 0;
            var len = Math.Min(a.Length, b.Length);
            for (int i = 0; i < len; i++)
            {
                dot += a[i] * b[i];
                na += a[i] * a[i];
                nb += b[i] * b[i];
            }
            return dot / (Math.Sqrt(na) * Math.Sqrt(nb) + 1e-8);
        }
    }
}

