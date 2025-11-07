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
        
        // Add HybridEnricher for cost-saving mode (Option 2)
        var cfg = new Config();
        if (cfg.UseHybridMode)
        {
            services.AddSingleton<HybridEnricher>();
            Console.WriteLine("🔧 [CONFIG] Hybrid Mode ENABLED - Using AI only for content enrichment (cost-saving)");
        }
        
        // Choose embedding store based on configuration
        // Priority: MongoDB (for testing) > Cosmos DB (for production)
        if (!string.IsNullOrWhiteSpace(cfg.MongoConnectionString))
        {
            // Use MongoDB for testing (cost-effective)
            Console.WriteLine("🔧 [CONFIG] Using MongoDB for embedding storage (testing mode)");
            
            // Test MongoDB connection during startup
            try
            {
                var testHelper = new MongoHelper(cfg);
                Console.WriteLine("✅ [STARTUP] MongoDB connection verified during startup");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [STARTUP] MongoDB connection failed during startup: {ex.Message}");
                Console.WriteLine($"   The app will continue but embeddings won't be stored.");
                Console.WriteLine($"   Please check MongoDB connection string and credentials.");
            }
            
            services.AddSingleton<IEmbeddingStore, MongoHelper>();
            services.AddSingleton<MongoHelper>();
        }
        else if (!string.IsNullOrWhiteSpace(cfg.CosmosConnectionString))
        {
            // Use Cosmos DB for production
            Console.WriteLine("🔧 [CONFIG] Using Cosmos DB for embedding storage (production mode)");
            services.AddSingleton<IEmbeddingStore, CosmosHelper>();
            services.AddSingleton<CosmosHelper>();
        }
        else
        {
            // Fallback to CosmosHelper (will run in mock mode)
            Console.WriteLine("⚠️ [CONFIG] No embedding store configured - using CosmosHelper in mock mode");
            services.AddSingleton<IEmbeddingStore, CosmosHelper>();
            services.AddSingleton<CosmosHelper>();
        }
    })
    .Build();

host.Run();

