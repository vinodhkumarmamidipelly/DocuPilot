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

