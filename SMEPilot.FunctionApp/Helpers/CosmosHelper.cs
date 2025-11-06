using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using SMEPilot.FunctionApp.Models;

namespace SMEPilot.FunctionApp.Helpers
{
    public class CosmosHelper
    {
        private readonly CosmosClient? _client;
        private Container? _container;
        private readonly bool _hasConnection;
        private readonly Config _cfg;
        private static readonly object _initLock = new object();

        public CosmosHelper(Config cfg)
        {
            _cfg = cfg;
            var connStr = cfg.CosmosConnectionString?.Trim();
            if (!string.IsNullOrWhiteSpace(connStr))
            {
                try
                {
                    // Log connection string (mask AccountKey for security)
                    var maskedConnStr = connStr;
                    if (maskedConnStr.Contains("AccountKey="))
                    {
                        var keyStart = maskedConnStr.IndexOf("AccountKey=") + 11;
                        if (keyStart < maskedConnStr.Length)
                        {
                            maskedConnStr = maskedConnStr.Substring(0, keyStart) + "***MASKED***";
                        }
                    }
                    Console.WriteLine($"🔍 [COSMOS] Initializing CosmosDB connection...");
                    Console.WriteLine($"   Connection String: {maskedConnStr}");
                    Console.WriteLine($"   Database: {cfg.CosmosDatabase}");
                    Console.WriteLine($"   Container: {cfg.CosmosContainer}");
                    
                    _client = new CosmosClient(connStr);
                    _hasConnection = true;
                    Console.WriteLine($"✅ [COSMOS] CosmosDB client initialized successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ [COSMOS] Failed to initialize CosmosDB client: {ex.GetType().Name}: {ex.Message}");
                    Console.WriteLine($"   Stack Trace: {ex.StackTrace}");
                    Console.WriteLine($"   Falling back to mock mode (no CosmosDB operations will be performed)");
                    _client = null;
                    _container = null;
                    _hasConnection = false;
                }
            }
            else
            {
                Console.WriteLine($"⚠️ [COSMOS] No connection string configured - running in mock mode");
                _client = null;
                _container = null;
                _hasConnection = false;
            }
        }

        private async Task EnsureDatabaseAndContainerAsync()
        {
            if (_client == null || !_hasConnection) return;
            
            // Double-check pattern to avoid multiple initializations
            if (_container != null) return;
            
            lock (_initLock)
            {
                if (_container != null) return;
            }
            
            try
            {
                Console.WriteLine($"🔧 [COSMOS] Creating database and container if they don't exist...");
                
                // Create database if it doesn't exist
                var databaseResponse = await _client.CreateDatabaseIfNotExistsAsync(_cfg.CosmosDatabase);
                Console.WriteLine($"✅ [COSMOS] Database '{_cfg.CosmosDatabase}' ready");
                
                // Create container if it doesn't exist (with partition key on TenantId)
                var containerResponse = await databaseResponse.Database.CreateContainerIfNotExistsAsync(
                    id: _cfg.CosmosContainer,
                    partitionKeyPath: "/TenantId"
                );
                Console.WriteLine($"✅ [COSMOS] Container '{_cfg.CosmosContainer}' ready (Partition Key: /TenantId)");
                
                lock (_initLock)
                {
                    _container = containerResponse.Container;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [COSMOS] Error ensuring database/container: {ex.GetType().Name}: {ex.Message}");
                throw;
            }
        }

        public async Task UpsertEmbeddingAsync(EmbeddingDocument doc)
        {
            if (!_hasConnection || _client == null)
            {
                // Mock mode - log but don't fail
                Console.WriteLine($"Mock: Would upsert embedding for {doc.id} in tenant {doc.TenantId}");
                return;
            }

            try
            {
                // Ensure database and container exist before upserting
                await EnsureDatabaseAndContainerAsync();
                
                if (_container == null)
                {
                    Console.WriteLine($"⚠️ [COSMOS] Container is null after initialization, skipping upsert");
                    return;
                }

                await _container.UpsertItemAsync(doc, new PartitionKey(doc.TenantId));
                Console.WriteLine($"✅ [COSMOS] Embedding stored: {doc.id} (Tenant: {doc.TenantId}, Section: {doc.Heading})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [COSMOS] Failed to upsert embedding: {ex.GetType().Name}: {ex.Message}");
                throw;
            }
        }

        public async Task<List<EmbeddingDocument>> GetEmbeddingsForTenantAsync(string tenantId, int maxItems = 1000)
        {
            if (!_hasConnection || _client == null)
            {
                // Mock mode - return empty list
                Console.WriteLine($"Mock: Would fetch embeddings for tenant {tenantId}");
                return new List<EmbeddingDocument>();
            }

            try
            {
                // Ensure database and container exist before querying
                await EnsureDatabaseAndContainerAsync();
                
                if (_container == null)
                {
                    Console.WriteLine($"⚠️ [COSMOS] Container is null after initialization, returning empty list");
                    return new List<EmbeddingDocument>();
                }

                var q = new QueryDefinition("SELECT * FROM c WHERE c.TenantId = @tenantId").WithParameter("@tenantId", tenantId);
                var it = _container.GetItemQueryIterator<EmbeddingDocument>(q, requestOptions: new QueryRequestOptions 
                { 
                    PartitionKey = new PartitionKey(tenantId), 
                    MaxItemCount = 100 
                });
                var list = new List<EmbeddingDocument>();
                while (it.HasMoreResults && list.Count < maxItems)
                {
                    var res = await it.ReadNextAsync();
                    list.AddRange(res.Resource);
                }
                return list;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [COSMOS] Failed to fetch embeddings: {ex.GetType().Name}: {ex.Message}");
                return new List<EmbeddingDocument>();
            }
        }
    }
}
