using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SMEPilot.FunctionApp.Helpers;
using SMEPilot.FunctionApp.Services;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using System.IO;
using Spire.Pdf;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WorkerService;
using Microsoft.Azure.Functions.Worker.ApplicationInsights;

// Configure Serilog for file logging
// Use app folder for better tracking (instead of temp directory)
var appFolder = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
var logPath = Path.Combine(appFolder, "Logs");
if (!Directory.Exists(logPath))
{
    Directory.CreateDirectory(logPath);
}

var logFile = Path.Combine(logPath, "sme-pilot-.log");

// Configure Serilog: Console for startup only, File for all logs
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    // File: ALL logs (including processing logs from ILogger) - write first, no filters
    .WriteTo.File(
        new CompactJsonFormatter(),
        logFile,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30, // Keep logs for 30 days
        fileSizeLimitBytes: 10 * 1024 * 1024, // 10 MB per file
        rollOnFileSizeLimit: true,
        shared: true, // Allow multiple processes to write to same file
        flushToDiskInterval: TimeSpan.FromSeconds(1)) // Flush to disk every second for better accessibility
    // Console: COMPLETELY DISABLED - All logs go to files only
    // No console output at all - user requested zero logs in output window
    .CreateLogger();

try
{
    Log.Information("🚀 SMEPilot Function App starting...");
    Log.Information("📁 Log files location: {LogPath}", logPath);
    
    var host = new HostBuilder()
        .ConfigureFunctionsWorkerDefaults()
        .ConfigureServices(services =>
        {
            // Add Application Insights
            // Note: AddApplicationInsightsTelemetryWorkerService automatically registers TelemetryConfiguration
            services.AddApplicationInsightsTelemetryWorkerService(options =>
            {
                options.ConnectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
                options.EnableAdaptiveSampling = true;
                options.EnableDependencyTrackingTelemetryModule = true;
                options.EnablePerformanceCounterCollectionModule = true;
            });
            
            // Add TelemetryClient
            // TelemetryConfiguration is automatically registered by AddApplicationInsightsTelemetryWorkerService
            services.AddSingleton<TelemetryClient>(sp =>
            {
                var telemetryConfig = sp.GetRequiredService<TelemetryConfiguration>();
                return new TelemetryClient(telemetryConfig);
            });
            
            // Add TelemetryService
            services.AddSingleton<TelemetryService>(sp =>
            {
                var telemetryClient = sp.GetRequiredService<TelemetryClient>();
                var logger = sp.GetService<ILogger<TelemetryService>>();
                return new TelemetryService(telemetryClient, logger);
            });
            
            // Add NotificationService
            services.AddSingleton<NotificationService>(sp =>
            {
                var connectionString = Environment.GetEnvironmentVariable("AzureCommunicationServices_ConnectionString");
                var adminEmail = Environment.GetEnvironmentVariable("AdminEmail") 
                    ?? Environment.GetEnvironmentVariable("NotificationEmail");
                var logger = sp.GetService<ILogger<NotificationService>>();
                return new NotificationService(connectionString, adminEmail, logger);
            });
            
            // Add RateLimitingService
            services.AddSingleton<RateLimitingService>(sp =>
            {
                var maxPerMinute = int.TryParse(Environment.GetEnvironmentVariable("RateLimit_MaxPerMinute"), out var perMin) ? perMin : 60;
                var maxPerHour = int.TryParse(Environment.GetEnvironmentVariable("RateLimit_MaxPerHour"), out var perHour) ? perHour : 1000;
                var logger = sp.GetService<ILogger<RateLimitingService>>();
                return new RateLimitingService(maxPerMinute, maxPerHour, logger);
            });
            
            services.AddSingleton<Config>();
            services.AddSingleton<GraphHelper>(sp => 
            {
                var cfg = sp.GetRequiredService<Config>();
                var logger = sp.GetService<ILogger<GraphHelper>>();
                return new GraphHelper(cfg, logger);
            });
                   services.AddSingleton<SimpleExtractor>(sp => 
                   {
                       var logger = sp.GetService<ILogger<SimpleExtractor>>();
                       return new SimpleExtractor(logger);
                   });
                   services.AddSingleton<TemplateBuilder>();
                   services.AddSingleton<OcrHelper>(sp => 
                   {
                       var cfg = sp.GetRequiredService<Config>();
                       var logger = sp.GetService<ILogger<OcrHelper>>();
                       return new OcrHelper(cfg, logger);
                   }); // OCR support (optional - requires AzureVision_Endpoint and AzureVision_Key)
                   
                   // Add HybridEnricher for rule-based sectioning (NO AI)
                   var cfg = new Config();
                   services.AddSingleton<HybridEnricher>();
                   
                   // Add RuleBasedFormatter for rule-based document enrichment (NO AI, NO DB)
                   services.AddSingleton<RuleBasedFormatter>();
                   
                   // SetupSubscription requires ILogger
                   services.AddSingleton<SMEPilot.FunctionApp.Functions.SetupSubscription>(sp =>
                   {
                       var graph = sp.GetRequiredService<GraphHelper>();
                       var config = sp.GetRequiredService<Config>();
                       var logger = sp.GetService<ILogger<SMEPilot.FunctionApp.Functions.SetupSubscription>>();
                       return new SMEPilot.FunctionApp.Functions.SetupSubscription(graph, config, logger);
                   });
                   
                   // ProcessSharePointFile - Explicit registration (preventive, even though Worker should auto-instantiate)
                   // This ensures all dependencies are properly injected if auto-instantiation fails
                   services.AddScoped<SMEPilot.FunctionApp.Functions.ProcessSharePointFile>(sp =>
                   {
                       var graph = sp.GetRequiredService<GraphHelper>();
                       var extractor = sp.GetRequiredService<SimpleExtractor>();
                       var cfg = sp.GetRequiredService<Config>();
                       var logger = sp.GetService<ILogger<SMEPilot.FunctionApp.Functions.ProcessSharePointFile>>();
                       var hybridEnricher = sp.GetService<HybridEnricher>();
                       var ocrHelper = sp.GetService<OcrHelper>();
                       var ruleBasedFormatter = sp.GetService<RuleBasedFormatter>();
                       var telemetry = sp.GetService<TelemetryService>();
                       var notifications = sp.GetService<NotificationService>();
                       var rateLimiter = sp.GetService<RateLimitingService>();
                       return new SMEPilot.FunctionApp.Functions.ProcessSharePointFile(
                           graph, extractor, cfg, logger,
                           hybridEnricher, ocrHelper, ruleBasedFormatter,
                           telemetry, notifications, rateLimiter);
                   });
                   
                   // Add Serilog logging - CLEAR all default providers first to prevent console output
                   services.AddLogging(builder =>
                   {
                       // CRITICAL: Clear ALL default logging providers (including console)
                       builder.ClearProviders();
                       // Only add Serilog (which writes to files only, no console)
                       builder.AddSerilog();
                   });
               })
        .Build();
    
    // Log configuration
    var cfg = new Config();
    
    // Validate configuration
    if (!cfg.IsValid())
    {
        Log.Fatal("❌ [CONFIG] Invalid configuration - missing required Graph API credentials");
        Log.Fatal("   Required: Graph_TenantId, Graph_ClientId, Graph_ClientSecret");
        throw new InvalidOperationException("Invalid configuration - missing required Graph API credentials");
    }
    
    Log.Information("🔧 [CONFIG] Template formatting mode - Rule-based sectioning only (no AI, no database)");
    Log.Information("📋 [CONFIG] Configuration validated successfully");
    Log.Information("   MaxRetryAttempts: {MaxRetries}", cfg.MaxRetryAttempts);
    Log.Information("   MaxFileSizeBytes: {MaxSize} MB", cfg.MaxFileSizeBytes / 1024 / 1024);
    Log.Information("   MaxUploadRetries: {UploadRetries}", cfg.MaxUploadRetries);
    Log.Information("   NotificationDedupWindow: {DedupWindow}s", cfg.NotificationDedupWindowSeconds);
    
    // Initialize Spire.PDF license
    if (!string.IsNullOrWhiteSpace(cfg.SpirePdfLicense))
    {
        try
        {
            // Spire.PDF license initialization (version 10.2.2)
            // Note: LicenseProvider may be in Spire.Pdf.License namespace
            // Try different possible namespaces
            var pdfLicenseType = Type.GetType("Spire.Pdf.License.LicenseProvider, Spire.Pdf");
            if (pdfLicenseType != null)
            {
                var setLicenseKeyMethod = pdfLicenseType.GetMethod("SetLicenseKey", new[] { typeof(string) });
                if (setLicenseKeyMethod != null)
                {
                    setLicenseKeyMethod.Invoke(null, new object[] { cfg.SpirePdfLicense });
                    Log.Information("✅ [CONFIG] Spire.PDF license initialized successfully");
                }
                else
                {
                    Log.Warning("⚠️ [CONFIG] SetLicenseKey method not found in Spire.Pdf.License.LicenseProvider");
                }
            }
            else
            {
                // Fallback: Try setting license directly on PdfDocument if available
                Log.Warning("⚠️ [CONFIG] Spire.Pdf.License.LicenseProvider not found - license may need to be set differently");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ [CONFIG] Failed to initialize Spire.PDF license: {Error}", ex.Message);
        }
    }
    else
    {
        Log.Warning("⚠️ [CONFIG] Spire.PDF license not configured - PDF processing may be limited");
    }
    
    // Initialize Spire.Doc license (if configured, for future use)
    if (!string.IsNullOrWhiteSpace(cfg.SpireDocLicense))
    {
        try
        {
            // Note: Spire.Doc is not currently used, but license is set for future use
            // Spire.Doc.License.LicenseProvider.SetLicenseKey(cfg.SpireDocLicense);
            Log.Information("ℹ️ [CONFIG] Spire.Doc license configured (not currently used)");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ [CONFIG] Failed to initialize Spire.Doc license: {Error}", ex.Message);
        }
    }
    
    // Check if OCR is configured
    if (!string.IsNullOrWhiteSpace(cfg.AzureVisionEndpoint) && !string.IsNullOrWhiteSpace(cfg.AzureVisionKey))
    {
        Log.Information("✅ [CONFIG] OCR enabled - Azure Computer Vision configured");
    }
    else
    {
        Log.Information("ℹ️ [CONFIG] OCR disabled - Add AzureVision_Endpoint and AzureVision_Key to enable image text extraction");
    }
    
    Log.Information("✅ SMEPilot Function App started successfully");
    
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ SMEPilot Function App failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

