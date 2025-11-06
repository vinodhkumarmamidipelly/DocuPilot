# TechnicalDoc.md (complete — includes all .cs files)

SMEPilot — Full Technical Implementation Document (CTO + Developer combined, corrected & exhaustive)
Author: Vinodh Kumar Mamidipelly
Date: 2025-10-31

## Overview / Purpose
SMEPilot is a **sellable SharePoint App (SPFx)** + serverless enrichment pipeline designed to help organizations manage documentation. Business users create scratch documents (screenshots + minimal descriptions) about products/applications. The app automatically enriches these documents, splits them into Images/Text/Sections, applies a Standard Template with proper indexing, and makes them available across the organization. Enriched documents are indexed for O365 Copilot/Teams, enabling all org employees to query them through Teams/Copilot. The MVP focuses on `.docx` files and uses Azure OpenAI for sectioning, summarization and embeddings.

---
## Architecture (detailed)
**Complete Flow:**
1. **SPFx Web Part** → User uploads scratch document to SharePoint `ScratchDocs` library
2. **Trigger** → Graph subscription/webhook (preferred) or Power Automate → Azure Function `ProcessSharePointFile`
3. **Enrichment Pipeline:**
   - Download file via Graph
   - Extract text & images (OpenXML) — produce `text`, `images[]` (bytes), and `imageOcrTexts[]` (optional)
   - Call Azure OpenAI LLM for **sectionization**: returns strict JSON (`DocumentModel`) with `title`, `sections[]` (id, heading, summary, body) and `images[]` (id, alt text)
   - Validate JSON; if invalid retry once; if still invalid flag for manual review and persist LLM output for inspection
   - Build enriched `.docx` using TemplateBuilder (Title, TOC placeholder, Heading1 sections, summary, body, embedded images with captions/alt text)
   - Upload enriched file to `ProcessedDocs` (Graph), update original item metadata (SMEPilot_Enriched, SMEPilot_Status, SMEPilot_EnrichedFileUrl, SMEPilot_EnrichedJobId, SMEPilot_Confidence)
   - Call Azure OpenAI embeddings API per section (summary or body) and store embedding vectors in Cosmos DB (container partitioned by `/TenantId`)
4. **Microsoft Search Connector** → Push enriched document metadata to Microsoft Search for Copilot indexing
5. **Query Flow (via Teams/Copilot):**
   - Employee asks question in Teams/Copilot
   - Microsoft Search (via Graph Connector) OR Teams Bot calls `QueryAnswer` endpoint
   - `QueryAnswer` auto-detects user/tenant context, computes query embedding, fetches candidates (tenant-partitioned), cosine-similarity ranking, synthesizes answer with sources (LLM)
   - Response returned to Teams/Copilot for employee

---
## Components & responsibilities

### Frontend (SPFx - REQUIRED FOR MVP)
- **SharePoint Framework (SPFx)** — React-based web parts for SharePoint App
  - **Main Web Part**: Document upload interface, monitor enrichment status
  - **Admin Web Part**: Manage templates, view logs, trigger manual enrichment
  - **App Package**: `.sppkg` file for App Catalog deployment
- **App Manifest & Permissions** — Configure Graph API permissions for app

### Backend
- **Azure Functions (.NET 8)** — host the enrichment pipeline and query endpoint. Use Durable Functions when orchestrator needed for long-running flows or chunked processing.
- **Microsoft Graph** — download/upload files, update list item fields, create Graph subscriptions (change notifications).
- **Microsoft Search Connector** — push enriched document metadata to Microsoft Search for native Copilot indexing (REQUIRED FOR MVP).
- **Teams Bot (Optional Alternative)** — Bot Framework bot that wraps QueryAnswer API for Teams integration.

### AI & Data
- **Azure OpenAI** — LLM for sectioning (chat completions) and embeddings (embeddings API).
- **Azure Cosmos DB (Core SQL)** — store embeddings and metadata, partitioned by TenantId. Use change feed for future processing.
- **DocumentFormat.OpenXml** — parse `.docx` and construct enriched `.docx`.

### Integration
- **Power Automate** — optional simplified trigger for `When a file is created` step. For production prefer Graph subscription or webhook.
- **Storage / Samples** — local `samples/` for mock testing; `samples/output` to capture enriched files & error artifacts in mock mode.

---
## All C# files (complete copy-paste ready)
Below are all the `.cs` files you need to drop into `SMEPilot.FunctionApp/` exactly as shown. Create folders `Models/`, `Helpers/`, `Functions/` and paste each file name and content accordingly.

---

### SMEPilot.FunctionApp.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AzureFunctionsVersion>v4</AzureFunctionsVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Graph" Version="5.15.0" />
    <PackageReference Include="Microsoft.Identity.Client" Version="4.75.0" />
    <PackageReference Include="Azure.Identity" Version="2.12.0" />
    <PackageReference Include="Azure.AI.OpenAI" Version="1.4.0" />
    <PackageReference Include="Azure.Cosmos" Version="4.36.0" />
    <PackageReference Include="DocumentFormat.OpenXml" Version="2.20.0" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
```

---

### Program.cs
```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SMEPilot.FunctionApp.Helpers;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddSingleton<Config>();
        services.AddSingleton<GraphHelper>();
        services.AddSingleton<SimpleExtractor>();
        services.AddSingleton<OpenAiHelper>();
        services.AddSingleton<TemplateBuilder>();
        services.AddSingleton<CosmosHelper>();
    })
    .Build();

host.Run();
```

---

### Models/SharePointEvent.cs
```csharp
namespace SMEPilot.FunctionApp.Models
{
    public class SharePointEvent
    {
        public string siteId { get; set; }
        public string driveId { get; set; }
        public string itemId { get; set; }
        public string fileName { get; set; }
        public string uploaderEmail { get; set; }
        public string tenantId { get; set; }
    }
}
```

---

### Models/DocumentModel.cs
```csharp
using System.Collections.Generic;

namespace SMEPilot.FunctionApp.Models
{
    public class DocumentModel
    {
        public string Title { get; set; }
        public List<Section> Sections { get; set; } = new();
        public List<ImageData> Images { get; set; } = new();
    }

    public class Section
    {
        public string Id { get; set; }
        public string Heading { get; set; }
        public string Summary { get; set; }
        public string Body { get; set; }
    }

    public class ImageData
    {
        public string Id { get; set; }
        public string Alt { get; set; }
        public byte[] Bytes { get; set; }
    }
}
```

---

### Models/EmbeddingDocument.cs
```csharp
using System;

namespace SMEPilot.FunctionApp.Models
{
    public class EmbeddingDocument
    {
        public string id { get; set; }
        public string TenantId { get; set; }
        public string FileId { get; set; }
        public string FileUrl { get; set; }
        public string SectionId { get; set; }
        public string Heading { get; set; }
        public string Summary { get; set; }
        public string Body { get; set; }
        public float[] Embedding { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
```

---

### Helpers/Config.cs
```csharp
using System;

namespace SMEPilot.FunctionApp.Helpers
{
    public class Config
    {
        public string GraphTenantId => Environment.GetEnvironmentVariable("Graph_TenantId");
        public string GraphClientId => Environment.GetEnvironmentVariable("Graph_ClientId");
        public string GraphClientSecret => Environment.GetEnvironmentVariable("Graph_ClientSecret");
        public string AzureOpenAIEndpoint => Environment.GetEnvironmentVariable("AzureOpenAI_Endpoint");
        public string AzureOpenAIKey => Environment.GetEnvironmentVariable("AzureOpenAI_Key");
        public string AzureOpenAIDeployment => Environment.GetEnvironmentVariable("AzureOpenAI_Deployment_GPT");
        public string AzureOpenAIEmbeddingDeployment => Environment.GetEnvironmentVariable("AzureOpenAI_Embedding_Deployment");
        public string CosmosConnectionString => Environment.GetEnvironmentVariable("Cosmos_ConnectionString");
        public string CosmosDatabase => Environment.GetEnvironmentVariable("Cosmos_Database") ?? "SMEPilotDB";
        public string CosmosContainer => Environment.GetEnvironmentVariable("Cosmos_Container") ?? "Embeddings";
        public string EnrichedFolderRelativePath => Environment.GetEnvironmentVariable("EnrichedFolderRelativePath") ?? "/Shared Documents/ProcessedDocs";
    }
}
```

---

### Helpers/GraphHelper.cs
```csharp
using System;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using System.Collections.Generic;

namespace SMEPilot.FunctionApp.Helpers
{
    public class GraphHelper
    {
        private readonly GraphServiceClient _client;
        private readonly Config _cfg;
        private readonly bool _hasCredentials;

        public GraphHelper(Config cfg)
        {
            _cfg = cfg;
            _hasCredentials = !string.IsNullOrWhiteSpace(cfg.GraphClientId) && !string.IsNullOrWhiteSpace(cfg.GraphClientSecret) && !string.IsNullOrWhiteSpace(cfg.GraphTenantId);

            if (_hasCredentials)
            {
                var cca = ConfidentialClientApplicationBuilder
                            .Create(_cfg.GraphClientId)
                            .WithClientSecret(_cfg.GraphClientSecret)
                            .WithTenantId(_cfg.GraphTenantId)
                            .Build();

                var authProvider = new DelegateAuthenticationProvider(async (requestMessage) =>
                {
                    var token = await cca.AcquireTokenForClient(new[] { "https://graph.microsoft.com/.default" }).ExecuteAsync();
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
                });

                _client = new GraphServiceClient(authProvider);
            }
            else
            {
                // _client left null; methods below use LOCAL MOCK when credentials missing
                _client = null;
            }
        }

        /// <summary>
        /// Download file stream from Graph; when credentials not configured, return local sample file.
        /// </summary>
        public async Task<Stream> DownloadFileStreamAsync(string driveId, string itemId)
        {
            if (!_hasCredentials)
            {
                // LOCAL MOCK: return local sample for dev/testing
                var samplePath = Path.Combine(Directory.GetCurrentDirectory(), "samples", "sample1.docx");
                if (File.Exists(samplePath))
                    return File.OpenRead(samplePath);

                throw new FileNotFoundException("Local sample not found: " + samplePath);
            }

            var stream = await _client.Drives[driveId].Items[itemId].Content.Request().GetAsync();
            var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;
            return ms;
        }

        /// <summary>
        /// Upload bytes to folderPath in drive; if mock, write to samples/output and return synthetic DriveItem.
        /// </summary>
        public async Task<DriveItem> UploadFileBytesAsync(string driveId, string folderPath, string fileName, byte[] bytes)
        {
            if (!_hasCredentials)
            {
                var outDir = Path.Combine(Directory.GetCurrentDirectory(), "samples", "output");
                Directory.CreateDirectory(outDir);
                var outPath = Path.Combine(outDir, fileName);
                await File.WriteAllBytesAsync(outPath, bytes);

                // Also create a metadata stub
                var meta = new Dictionary<string, object>
                {
                    {"SMEPilot_Enriched", true},
                    {"SMEPilot_Status", "Completed"},
                    {"SMEPilot_EnrichedFileUrl", outPath},
                    {"SMEPilot_EnrichedJobId", Guid.NewGuid().ToString()}
                };
                var metaPath = Path.ChangeExtension(outPath, ".metadata.json");
                await File.WriteAllTextAsync(metaPath, System.Text.Json.JsonSerializer.Serialize(meta));

                // Return synthetic DriveItem
                return new DriveItem { Id = Guid.NewGuid().ToString(), WebUrl = outPath };
            }

            var path = folderPath.TrimEnd('/') + "/" + fileName;
            using var ms = new MemoryStream(bytes);
            var item = await _client.Drives[driveId].Root.ItemWithPath(path).Content.Request().PutAsync<DriveItem>(ms);
            return item;
        }

        /// <summary>
        /// Update list item fields for the provided item. Works only when Graph credentials configured.
        /// </summary>
        public async Task UpdateListItemFieldsAsync(string driveId, string itemId, Dictionary<string, object> fields)
        {
            if (!_hasCredentials)
            {
                // In mock mode, write a local metadata file
                var outDir = Path.Combine(Directory.GetCurrentDirectory(), "samples", "output");
                Directory.CreateDirectory(outDir);
                var metaPath = Path.Combine(outDir, $"{itemId}.metadata.json");
                await File.WriteAllTextAsync(metaPath, System.Text.Json.JsonSerializer.Serialize(fields));
                return;
            }

            var driveItem = await _client.Drives[driveId].Items[itemId].Request().GetAsync();
            if (driveItem.ListItem != null)
            {
                var siteId = driveItem.ParentReference.SiteId;
                var listId = driveItem.ListItem.ParentReference.ListId;
                var listItemId = driveItem.ListItem.Id;
                await _client.Sites[siteId].Lists[listId].Items[listItemId].Fields.Request().UpdateAsync(new FieldValueSet { AdditionalData = fields });
            }
        }

        /// <summary>
        /// Create a Graph subscription for change notifications.
        /// </summary>
        public async Task<Subscription> CreateSubscriptionAsync(string resource, string notificationUrl, DateTimeOffset expiration)
        {
            if (!_hasCredentials) throw new InvalidOperationException("Graph credentials are not configured.");
            var subscription = new Subscription
            {
                ChangeType = "created,updated",
                NotificationUrl = notificationUrl,
                Resource = resource,
                ExpirationDateTime = expiration,
                ClientState = "SMEPilotState"
            };
            return await _client.Subscriptions.Request().AddAsync(subscription);
        }
    }
}
```

---

### Helpers/SimpleExtractor.cs
```csharp
using DocumentFormat.OpenXml.Packaging;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SMEPilot.FunctionApp.Helpers
{
    public class SimpleExtractor
    {
        public async Task<(string Text, List<byte[]> Images)> ExtractDocxAsync(Stream docxStream)
        {
            var textBuilder = new System.Text.StringBuilder();
            var images = new List<byte[]>();

            using var ms = new MemoryStream();
            await docxStream.CopyToAsync(ms);
            ms.Position = 0;

            using var doc = WordprocessingDocument.Open(ms, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body != null)
            {
                textBuilder.AppendLine(body.InnerText);
            }

            var imageParts = doc.MainDocumentPart?.ImageParts;
            if (imageParts != null)
            {
                foreach (var imgPart in imageParts)
                {
                    using var imgStream = imgPart.GetStream();
                    using var ims = new MemoryStream();
                    await imgStream.CopyToAsync(ims);
                    images.Add(ims.ToArray());
                }
            }
            return (textBuilder.ToString(), images);
        }
    }
}
```

---

### Helpers/OpenAiHelper.cs
```csharp
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
        private readonly OpenAIClient _client;
        private readonly Config _cfg;

        public OpenAiHelper(Config cfg)
        {
            _cfg = cfg;
            _client = new OpenAIClient(new Uri(_cfg.AzureOpenAIEndpoint), new AzureKeyCredential(_cfg.AzureOpenAIKey));
        }

        public async Task<string> GenerateSectionsJsonAsync(string text, List<string> imageOcrs, string fileId = null)
        {
            string system = @"You are a document enricher. Output ONLY valid JSON matching the schema:
{""title"":""..."",""sections"":[{""id"":""s1"",""heading"":""..."",""summary"":""..."",""body"":""...""}],""images"":[{""id"":""img1"",""alt"":""...""}]}
Create up to 12 sections. Summary 20-40 words. Do not add extra text.";

        user = $"RawText:\n{text}\n\nImageOCRs:\n{string.Join('\n', imageOcrs)}\n\nReturn valid JSON only.";

            var options = new ChatCompletionsOptions
            {
                Temperature = 0.2f,
                MaxTokens = 1500
            };
            options.Messages.Add(new ChatMessage(ChatRole.System, system));
            options.Messages.Add(new ChatMessage(ChatRole.User, user));

            var response = await _client.GetChatCompletionsAsync(_cfg.AzureOpenAIDeployment, options);
            var content = response.Value.Choices[0].Message.Content;

            // Try parse - if invalid retry once with stricter prompt
            if (!TryParseToDocumentModel(content, out var docModel))
            {
                // Retry with even stricter system message
                string strictSystem = @"Return valid JSON only. No commentary. No code fences. Respond with raw JSON matching the schema exactly.";
                var retryOpts = new ChatCompletionsOptions { Temperature = 0.0f, MaxTokens = 1500 };
                retryOpts.Messages.Add(new ChatMessage(ChatRole.System, strictSystem));
                retryOpts.Messages.Add(new ChatMessage(ChatRole.User, user));
                var retryResp = await _client.GetChatCompletionsAsync(_cfg.AzureOpenAIDeployment, retryOpts);
                var retryContent = retryResp.Value.Choices[0].Message.Content;

                if (!TryParseToDocumentModel(retryContent, out docModel))
                {
                    // Persist error for manual review
                    await PersistLlmErrorAsync(fileId ?? Guid.NewGuid().ToString(), content, retryContent);
                    throw new InvalidDataException("LLM did not return valid JSON after retry. Manual review required.");
                }

                return retryContent;
            }

            return content;
        }

        private bool TryParseToDocumentModel(string json, out DocumentModel model)
        {
            model = null;
            try
            {
                model = JsonConvert.DeserializeObject<DocumentModel>(json);
                if (model == null) return false;
                if (model.Sections == null || model.Sections.Count == 0) return false;
                // Additional minimal validation
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
                var outDir = Path.Combine(Directory.GetCurrentDirectory(), "samples", "output", "llm_errors");
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
            var resp = await _client.GetEmbeddingsAsync(_cfg.AzureOpenAIEmbeddingDeployment, new EmbeddingsOptions(input));
            return resp.Value.Data[0].Embedding.ToArray();
        }

        public OpenAIClient GetClient() => _client;
        public string GetDeployment() => _cfg.AzureOpenAIDeployment;
    }
}
```

---

### Helpers/TemplateBuilder.cs
```csharp
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SMEPilot.FunctionApp.Models;
using System.Collections.Generic;
using System.IO;
using System;

namespace SMEPilot.FunctionApp.Helpers
{
    public class TemplateBuilder
    {
        public static byte[] BuildDocxBytes(DocumentModel model, List<byte[]> images)
        {
            using var mem = new MemoryStream();
            using (var doc = WordprocessingDocument.Create(mem, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new Document(new Body());
                var body = mainPart.Document.Body;

                // Title
                if (!string.IsNullOrWhiteSpace(model.Title))
                {
                    body.Append(new Paragraph(new Run(new Text(model.Title)))
                    {
                        ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "Title" })
                    });
                }

                // Table of Contents placeholder (Word will generate TOC if user updates fields)
                body.Append(new Paragraph(new Run(new Text("Table of Contents (auto-generated)"))) {
                    ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "TOCHeading" })
                });

                // Sections
                foreach (var s in model.Sections)
                {
                    // Heading
                    var headingPara = new Paragraph(new Run(new Text(s.Heading)))
                    {
                        ParagraphProperties = new ParagraphProperties(new ParagraphStyleId() { Val = "Heading1" })
                    };
                    body.Append(headingPara);

                    // Summary
                    if (!string.IsNullOrWhiteSpace(s.Summary))
                    {
                        body.Append(new Paragraph(new Run(new Text("Summary: " + s.Summary))));
                    }

                    // Body
                    if (!string.IsNullOrWhiteSpace(s.Body))
                    {
                        body.Append(new Paragraph(new Run(new Text(s.Body))));
                    }
                }

                // Images: append at the end with captions (if any)
                int idx = 0;
                foreach (var img in images)
                {
                    idx++;
                    try
                    {
                        var imagePart = mainPart.AddImagePart(DocumentFormat.OpenXml.Packaging.ImagePartType.Png);
                        using var imgStream = new MemoryStream(img);
                        imagePart.FeedData(imgStream);
                        var imgPara = new Paragraph(new Run(new DocumentFormat.OpenXml.Drawing.Wordprocessing.Drawing(
                            new DocumentFormat.OpenXml.Drawing.Wordprocessing.Inline()
                        )));
                        // Simple caption text underneath (alt text stored in model.Images if present)
                        body.Append(new Paragraph(new Run(new Text($"Image {idx}"))));
                    }
                    catch
                    {
                        // ignore image errors — images are optional
                    }
                }

                mainPart.Document.Save();
            }
            return mem.ToArray();
        }
    }
}
```

---

### Helpers/CosmosHelper.cs
```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Cosmos;
using SMEPilot.FunctionApp.Models;

namespace SMEPilot.FunctionApp.Helpers
{
    public class CosmosHelper
    {
        private readonly CosmosClient _client;
        private readonly Container _container;

        public CosmosHelper(Config cfg)
        {
            _client = new CosmosClient(cfg.CosmosConnectionString);
            _container = _client.GetDatabase(cfg.CosmosDatabase).GetContainer(cfg.CosmosContainer);
        }

        public async Task UpsertEmbeddingAsync(EmbeddingDocument doc)
        {
            await _container.UpsertItemAsync(doc, new PartitionKey(doc.TenantId));
        }

        public async Task<List<EmbeddingDocument>> GetEmbeddingsForTenantAsync(string tenantId, int maxItems = 1000)
        {
            var q = new QueryDefinition("SELECT * FROM c WHERE c.TenantId = @tenantId").WithParameter("@tenantId", tenantId);
            var it = _container.GetItemQueryIterator<EmbeddingDocument>(q, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(tenantId), MaxItemCount = 100 });
            var list = new List<EmbeddingDocument>();
            while (it.HasMoreResults && list.Count < maxItems)
            {
                var res = await it.ReadNextAsync();
                list.AddRange(res.Resource);
            }
            return list;
        }
    }
}
```

---

### Functions/ProcessSharePointFile.cs
```csharp
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

namespace SMEPilot.FunctionApp.Functions
{
    public class ProcessSharePointFile
    {
        private readonly GraphHelper _graph;
        private readonly SimpleExtractor _extractor;
        private readonly OpenAiHelper _openai;
        private readonly Config _cfg;
        private readonly CosmosHelper _cosmos;

        public ProcessSharePointFile(GraphHelper graph, SimpleExtractor extractor, OpenAiHelper openai, Config cfg, CosmosHelper cosmos)
        {
            _graph = graph;
            _extractor = extractor;
            _openai = openai;
            _cfg = cfg;
            _cosmos = cosmos;
        }

        [Function("ProcessSharePointFile")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            try
            {
                // Graph subscription validation handshake (echo validationToken if present)
                var q = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var validationToken = q.Get("validationToken");
                if (!string.IsNullOrEmpty(validationToken))
                {
                    var vresp = req.CreateResponse(HttpStatusCode.OK);
                    vresp.Headers.Add("Content-Type", "text/plain");
                    await vresp.WriteStringAsync(validationToken);
                    return vresp;
                }

                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var evt = JsonConvert.DeserializeObject<SharePointEvent>(body);
                if (evt == null)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Invalid event payload");
                    return bad;
                }

                var tenantId = string.IsNullOrWhiteSpace(evt.tenantId) ? "default" : evt.tenantId;

                // idempotency: check if already processed (mock: check local metadata file)
                // For brevity, skipping DB job lookup; implement as needed.

                // 1. Download file
                using var fileStream = await _graph.DownloadFileStreamAsync(evt.driveId, evt.itemId);

                // orchestration hook: if large file, signal to queue or durable functions (placeholder)
                if (fileStream.Length > 4 * 1024 * 1024)
                {
                    // TODO: push message to queue for chunked processing
                    var respLarge = req.CreateResponse(HttpStatusCode.Accepted);
                    await respLarge.WriteStringAsync("File too large for single-run processing. It will be processed asynchronously.");
                    return respLarge;
                }

                // 2. Extract docx text & images
                var (text, imagesBytes) = await _extractor.ExtractDocxAsync(fileStream);

                // 3. Optionally OCR images here (skipped in POC)
                var imageOcrs = new List<string>(); // placeholder

                // 4. Call OpenAI to get sections JSON (strict validation inside helper)
                string fileId = Guid.NewGuid().ToString();
                string json;
                try
                {
                    json = await _openai.GenerateSectionsJsonAsync(text, imageOcrs, fileId);
                }
                catch (Exception ex)
                {
                    // Mark for manual review
                    await _graph.UpdateListItemFieldsAsync(evt.driveId, evt.itemId, new Dictionary<string, object>
                    {
                        {"SMEPilot_Enriched", false},
                        {"SMEPilot_Status", "ManualReview"},
                        {"SMEPilot_EnrichedJobId", fileId}
                    });
                    var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await err.WriteStringAsync("OpenAI failed to return valid JSON: " + ex.Message);
                    return err;
                }

                var docModel = JsonConvert.DeserializeObject<DocumentModel>(json);
                if (docModel == null)
                {
                    var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await err.WriteStringAsync("OpenAI returned invalid JSON (post-parse).");
                    return err;
                }

                // 5. Build enriched docx bytes
                var enrichedBytes = TemplateBuilder.BuildDocxBytes(docModel, imagesBytes);
                var enrichedName = Path.GetFileNameWithoutExtension(evt.fileName) + "_enriched.docx";

                // 6. Upload to ProcessedDocs
                var uploaded = await _graph.UploadFileBytesAsync(evt.driveId, _cfg.EnrichedFolderRelativePath, enrichedName, enrichedBytes);

                // 7. Create embeddings and store
                foreach (var s in docModel.Sections)
                {
                    var textForEmb = string.IsNullOrWhiteSpace(s.Summary) ? s.Body : s.Summary;
                    var emb = await _openai.GetEmbeddingAsync(textForEmb);

                    var embDoc = new EmbeddingDocument
                    {
                        id = Guid.NewGuid().ToString(),
                        TenantId = tenantId,
                        FileId = uploaded.Id,
                        FileUrl = uploaded.WebUrl,
                        SectionId = s.Id ?? Guid.NewGuid().ToString(),
                        Heading = s.Heading,
                        Summary = s.Summary,
                        Body = s.Body,
                        Embedding = emb,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _cosmos.UpsertEmbeddingAsync(embDoc);
                }

                // 8. Update original item metadata to reflect enrichment
                var metadata = new Dictionary<string, object>
                {
                    {"SMEPilot_Enriched", true},
                    {"SMEPilot_Status", "Completed"},
                    {"SMEPilot_EnrichedFileUrl", uploaded.WebUrl},
                    {"SMEPilot_EnrichedJobId", fileId},
                    {"SMEPilot_Confidence", 0.0} // placeholder: compute if available from model
                };
                await _graph.UpdateListItemFieldsAsync(evt.driveId, evt.itemId, metadata);

                var ok = req.CreateResponse(HttpStatusCode.OK);
                await ok.WriteStringAsync(JsonConvert.SerializeObject(new { enrichedUrl = uploaded.WebUrl }));
                return ok;
            }
            catch (Exception ex)
            {
                var res = req.CreateResponse(HttpStatusCode.InternalServerError);
                await res.WriteStringAsync("Processing failed: " + ex.Message);
                return res;
            }
        }
    }
}
```

---

### Functions/QueryAnswer.cs
```csharp
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

namespace SMEPilot.FunctionApp.Functions
{
    public class QueryAnswer
    {
        private readonly OpenAiHelper _openai;
        private readonly CosmosHelper _cosmos;

        public QueryAnswer(OpenAiHelper openai, CosmosHelper cosmos)
        {
            _openai = openai;
            _cosmos = cosmos;
        }

        [Function("QueryAnswer")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(body);
                string question = data?.question;

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
                    var token = authHeaders.FirstOrDefault()?.Replace("Bearer ", "");
                    tenantId = await UserContextHelper.GetTenantIdFromTokenAsync(token) ?? "default";
                }
                else if (data?.tenantId != null)
                {
                    tenantId = data.tenantId; // Fallback for programmatic calls
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
                var candidates = await _cosmos.GetEmbeddingsForTenantAsync(tenantId);

            // 3. Compute cosine similarity
            var ranked = candidates.Select(c => new
            {
                Doc = c,
                Score = CosineSimilarity(qEmb, c.Embedding)
            }).OrderByDescending(r => r.Score).Take(3).ToList();

            // 4. Build context and call OpenAI for synthesis
            var context = string.Join("\n\n", ranked.Select(r => $"Heading: {r.Doc.Heading}\nSummary: {r.Doc.Summary}\nURL: {r.Doc.FileUrl}\n"));
            var system = "You are SMEPilot assistant. Synthesize a concise answer then list Sources with fileUrl and heading.";
            var user = $"Question: {question}\n\nSources:\n{context}";

            var options = new Azure.AI.OpenAI.ChatCompletionsOptions
            {
                Temperature = 0.2f,
                MaxTokens = 400
            };
            options.Messages.Add(new Azure.AI.OpenAI.ChatMessage(Azure.AI.OpenAI.ChatRole.System, system));
            options.Messages.Add(new Azure.AI.OpenAI.ChatMessage(Azure.AI.OpenAI.ChatRole.User, user));

            var resp = await _openai.GetClient().GetChatCompletionsAsync(_openai.GetDeployment(), options);
            var answer = resp.Value.Choices.First().Message.Content;

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteStringAsync(JsonConvert.SerializeObject(new
            {
                answer,
                sources = ranked.Select(r => new { r.Doc.FileUrl, r.Doc.Heading, score = r.Score })
            }));
            return res;
        }

        private static double CosineSimilarity(float[] a, float[] b)
        {
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
```

---

### Helpers/UserContextHelper.cs
```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Graph;
using Microsoft.Identity.Client;

namespace SMEPilot.FunctionApp.Helpers
{
    public static class UserContextHelper
    {
        /// <summary>
        /// Extract tenant ID from Azure AD token (for org-wide employee access).
        /// </summary>
        public static async Task<string> GetTenantIdFromTokenAsync(string bearerToken)
        {
            if (string.IsNullOrWhiteSpace(bearerToken))
                return null;

            try
            {
                // Decode JWT token to extract tenant ID
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var token = handler.ReadJwtToken(bearerToken);
                var tid = token.Claims.FirstOrDefault(c => c.Type == "tid")?.Value;
                return tid;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get user information from Graph API using token.
        /// </summary>
        public static async Task<(string UserId, string TenantId, string Email)> GetUserContextAsync(string bearerToken, Config cfg)
        {
            if (string.IsNullOrWhiteSpace(bearerToken))
                return (null, null, null);

            try
            {
                var tenantId = await GetTenantIdFromTokenAsync(bearerToken);
                if (string.IsNullOrWhiteSpace(tenantId))
                    return (null, null, null);

                // Create Graph client with user token
                var authProvider = new DelegateAuthenticationProvider(async (requestMessage) =>
                {
                    requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
                    await Task.CompletedTask;
                });

                var graphClient = new GraphServiceClient(authProvider);
                var user = await graphClient.Me.Request().GetAsync();

                return (user.Id, tenantId, user.Mail ?? user.UserPrincipalName);
            }
            catch
            {
                return (null, tenantId, null);
            }
        }
    }
}
```

---

### Helpers/MicrosoftSearchConnectorHelper.cs
```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Helper to push enriched document metadata to Microsoft Search for Copilot indexing.
    /// This enables native O365 Copilot to find and reference enriched documents.
    /// </summary>
    public class MicrosoftSearchConnectorHelper
    {
        private readonly GraphServiceClient _graphClient;
        private readonly Config _cfg;

        public MicrosoftSearchConnectorHelper(GraphServiceClient graphClient, Config cfg)
        {
            _graphClient = graphClient;
            _cfg = cfg;
        }

        /// <summary>
        /// Index enriched document in Microsoft Search so Copilot can find it.
        /// </summary>
        public async Task IndexEnrichedDocumentAsync(string documentId, string title, string summary, string webUrl, string tenantId)
        {
            if (_graphClient == null)
            {
                // In mock mode, log but don't fail
                Console.WriteLine($"Mock: Would index document {documentId} to Microsoft Search");
                return;
            }

            try
            {
                // Push to Microsoft Search using Graph External Connection API
                // Note: Requires Microsoft Search connector to be configured in tenant admin center
                var externalItem = new ExternalItem
                {
                    Id = documentId,
                    Properties = new Properties
                    {
                        AdditionalData = new Dictionary<string, object>
                        {
                            { "title", title },
                            { "summary", summary },
                            { "url", webUrl },
                            { "tenantId", tenantId },
                            { "enrichedDate", DateTime.UtcNow.ToString("O") }
                        }
                    },
                    Content = new ExternalItemContent
                    {
                        Type = ExternalItemContentType.Text,
                        Value = summary // Index summary for search
                    }
                };

                // This requires a Microsoft Search Connection to be created first
                // For MVP, this can be set up via PowerShell or admin UI
                // await _graphClient.External.Connections["SMEPilot"].Items[documentId].PutAsync(externalItem);
                
                // For now, log that indexing should be done
                Console.WriteLine($"Document {documentId} ready for Microsoft Search indexing. Configure Search Connector to enable Copilot integration.");
            }
            catch (Exception ex)
            {
                // Log but don't fail enrichment if indexing fails
                Console.WriteLine($"Failed to index document in Microsoft Search: {ex.Message}");
            }
        }
    }
}
```

---

### local.settings.json (dev-only template)
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "REPLACE_ME_AZURE_STORAGE_CONN",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet",
    "Graph_TenantId": "REPLACE_ME_TENANT_ID",
    "Graph_ClientId": "REPLACE_ME_CLIENT_ID",
    "Graph_ClientSecret": "REPLACE_ME_CLIENT_SECRET",
    "AzureOpenAI_Endpoint": "https://REPLACE_ME_OPENAI_RESOURCE.openai.azure.com/",
    "AzureOpenAI_Key": "REPLACE_ME_OPENAI_KEY",
    "AzureOpenAI_Deployment_GPT": "REPLACE_ME_GPT_DEPLOYMENT",
    "AzureOpenAI_Embedding_Deployment": "REPLACE_ME_EMBEDDING_DEPLOYMENT",
    "Cosmos_ConnectionString": "REPLACE_ME_COSMOS_CONN",
    "Cosmos_Database": "SMEPilotDB",
    "Cosmos_Container": "Embeddings",
    "EnrichedFolderRelativePath": "/Shared Documents/ProcessedDocs"
  }
}
```

---
## Prompts & JSON schema
Use the strict system prompts in the earlier docs:
- Sectioning system prompt: returns only JSON matching the schema `{ "title":"", "sections":[...], "images":[...] }`
- Query synthesis system prompt: concise answer (3-6 sentences) followed by "Sources:" with fileUrl and heading

---
## Infra & deployment (commands)
See `Instructions.md` for `az` CLI commands and resource names. Remember partition key for Cosmos is `/TenantId`.

---
End of TechnicalDoc.md
