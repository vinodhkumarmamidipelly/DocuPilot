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

