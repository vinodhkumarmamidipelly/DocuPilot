using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Bson;
using SMEPilot.FunctionApp.Models;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// MongoDB helper class - alternative to CosmosHelper
    /// Uses MongoDB on VM for testing/development (cost-effective)
    /// </summary>
    public class MongoHelper : IEmbeddingStore
    {
        private readonly IMongoDatabase? _database;
        private readonly IMongoCollection<EmbeddingDocument>? _collection;
        private readonly bool _hasConnection;
        private readonly Config _cfg;

        public MongoHelper(Config cfg)
        {
            _cfg = cfg;
            var connStr = cfg.MongoConnectionString?.Trim();
            
            if (!string.IsNullOrWhiteSpace(connStr))
            {
                try
                {
                    // Mask password in connection string for logging
                    var maskedConnStr = connStr;
                    if (maskedConnStr.Contains("password=") || maskedConnStr.Contains("Password="))
                    {
                        var parts = maskedConnStr.Split('@');
                        if (parts.Length > 0)
                        {
                            maskedConnStr = parts[0].Split(':')[0] + ":***MASKED***@" + string.Join("@", parts.Skip(1));
                        }
                    }
                    
                    Console.WriteLine($"🔍 [MONGO] Initializing MongoDB connection...");
                    Console.WriteLine($"   Connection String: {maskedConnStr}");
                    Console.WriteLine($"   Database: {cfg.MongoDatabase}");
                    Console.WriteLine($"   Collection: {cfg.MongoContainer}");
                    
                    // Try multiple authentication mechanisms
                    var authMechanisms = new[] { "SCRAM-SHA-256", "SCRAM-SHA-1", null };
                    MongoClient? client = null;
                    Exception? lastException = null;
                    
                    foreach (var authMechanism in authMechanisms)
                    {
                        try
                        {
                            var testConnStr = connStr;
                            if (authMechanism != null)
                            {
                                // Update connection string with auth mechanism
                                if (testConnStr.Contains("authMechanism="))
                                {
                                    testConnStr = System.Text.RegularExpressions.Regex.Replace(
                                        testConnStr, 
                                        @"authMechanism=[^&]+", 
                                        $"authMechanism={authMechanism}");
                                }
                                else
                                {
                                    testConnStr += $"&authMechanism={authMechanism}";
                                }
                                Console.WriteLine($"🔍 [MONGO] Trying authentication mechanism: {authMechanism}");
                            }
                            else
                            {
                                // Remove authMechanism to let MongoDB auto-detect
                                testConnStr = System.Text.RegularExpressions.Regex.Replace(
                                    testConnStr, 
                                    @"&?authMechanism=[^&]+", 
                                    "");
                                Console.WriteLine($"🔍 [MONGO] Trying auto-detect authentication mechanism");
                            }
                            
                            var mongoUrl = new MongoDB.Driver.MongoUrl(testConnStr);
                            var clientSettings = MongoClientSettings.FromUrl(mongoUrl);
                            client = new MongoClient(clientSettings);
                            
                            // Test connection by pinging the server
                            Console.WriteLine($"🔍 [MONGO] Testing connection...");
                            client.GetDatabase("admin").RunCommand<MongoDB.Bson.BsonDocument>(
                                new MongoDB.Bson.BsonDocument("ping", 1),
                                cancellationToken: System.Threading.CancellationToken.None);
                            
                            Console.WriteLine($"✅ [MONGO] Connection test successful with mechanism: {authMechanism ?? "auto-detect"}");
                            break; // Success!
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            Console.WriteLine($"⚠️ [MONGO] Failed with {authMechanism ?? "auto-detect"}: {ex.Message}");
                            continue; // Try next mechanism
                        }
                    }
                    
                    if (client == null)
                    {
                        throw new InvalidOperationException(
                            $"Failed to connect to MongoDB with any authentication mechanism. Last error: {lastException?.Message}",
                            lastException);
                    }
                    
                    _database = client.GetDatabase(cfg.MongoDatabase);
                    _collection = _database.GetCollection<EmbeddingDocument>(cfg.MongoContainer);
                    _hasConnection = true;
                    
                    Console.WriteLine($"✅ [MONGO] MongoDB client initialized successfully");
                    
                    // Ensure indexes exist
                    EnsureIndexesAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ [MONGO] Failed to initialize MongoDB client: {ex.GetType().Name}: {ex.Message}");
                    Console.WriteLine($"   Stack Trace: {ex.StackTrace}");
                    Console.WriteLine($"   Falling back to mock mode (no MongoDB operations will be performed)");
                    _database = null;
                    _collection = null;
                    _hasConnection = false;
                }
            }
            else
            {
                Console.WriteLine($"⚠️ [MONGO] No connection string configured - running in mock mode");
                _database = null;
                _collection = null;
                _hasConnection = false;
            }
        }

        private async Task EnsureIndexesAsync()
        {
            if (_collection == null || !_hasConnection) return;

            try
            {
                // Create index on TenantId for faster queries (similar to Cosmos partition key)
                var tenantIdIndex = Builders<EmbeddingDocument>.IndexKeys.Ascending(x => x.TenantId);
                var tenantIdIndexModel = new CreateIndexModel<EmbeddingDocument>(tenantIdIndex);
                await _collection.Indexes.CreateOneAsync(tenantIdIndexModel);
                
                // Create index on id for faster lookups
                var idIndex = Builders<EmbeddingDocument>.IndexKeys.Ascending(x => x.id);
                var idIndexModel = new CreateIndexModel<EmbeddingDocument>(idIndex);
                await _collection.Indexes.CreateOneAsync(idIndexModel);
                
                Console.WriteLine($"✅ [MONGO] Indexes created/verified");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ [MONGO] Error creating indexes: {ex.Message}");
                // Don't throw - indexes might already exist
            }
        }

        public async Task UpsertEmbeddingAsync(EmbeddingDocument doc)
        {
            if (!_hasConnection || _collection == null)
            {
                Console.WriteLine($"Mock: Would upsert embedding for {doc.id} in tenant {doc.TenantId}");
                return;
            }

            try
            {
                // MongoDB upsert: replace if exists, insert if not
                var filter = Builders<EmbeddingDocument>.Filter.Eq(x => x.id, doc.id);
                var options = new ReplaceOptions { IsUpsert = true };
                
                await _collection.ReplaceOneAsync(filter, doc, options);
                Console.WriteLine($"✅ [MONGO] Embedding stored: {doc.id} (Tenant: {doc.TenantId}, Section: {doc.Heading})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [MONGO] Failed to upsert embedding: {ex.GetType().Name}: {ex.Message}");
                throw;
            }
        }

        public async Task<List<EmbeddingDocument>> GetEmbeddingsForTenantAsync(string tenantId, int maxItems = 1000)
        {
            if (!_hasConnection || _collection == null)
            {
                Console.WriteLine($"Mock: Would fetch embeddings for tenant {tenantId}");
                return new List<EmbeddingDocument>();
            }

            try
            {
                // Query by TenantId (similar to Cosmos partition key query)
                var filter = Builders<EmbeddingDocument>.Filter.Eq(x => x.TenantId, tenantId);
                var options = new FindOptions<EmbeddingDocument>
                {
                    Limit = maxItems
                };
                
                var cursor = await _collection.FindAsync(filter, options);
                var list = await cursor.ToListAsync();
                
                Console.WriteLine($"✅ [MONGO] Retrieved {list.Count} embeddings for tenant {tenantId}");
                return list;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [MONGO] Failed to fetch embeddings: {ex.GetType().Name}: {ex.Message}");
                return new List<EmbeddingDocument>();
            }
        }
    }
}

