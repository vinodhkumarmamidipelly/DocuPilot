using System.Collections.Generic;
using System.Threading.Tasks;
using SMEPilot.FunctionApp.Models;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Interface for embedding storage operations
    /// Allows easy switching between Cosmos DB and MongoDB
    /// </summary>
    public interface IEmbeddingStore
    {
        /// <summary>
        /// Upsert (insert or update) an embedding document
        /// </summary>
        Task UpsertEmbeddingAsync(EmbeddingDocument doc);

        /// <summary>
        /// Get all embeddings for a specific tenant
        /// </summary>
        /// <param name="tenantId">Tenant ID to filter by</param>
        /// <param name="maxItems">Maximum number of items to return (default: 1000)</param>
        /// <returns>List of embedding documents for the tenant</returns>
        Task<List<EmbeddingDocument>> GetEmbeddingsForTenantAsync(string tenantId, int maxItems = 1000);
    }
}

