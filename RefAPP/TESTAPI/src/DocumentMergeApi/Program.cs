using DocumentMergeApi.Services;
using DocumentMergeApi.Swagger;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Document Merge API", Version = "v1" });
    c.OperationFilter<FileUploadOperationFilter>();
    c.CustomSchemaIds(type => type.FullName);
    c.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });
});

// Register services
builder.Services.AddSingleton<TokenDetector>();
builder.Services.AddSingleton<RawSectionExtractor>();
builder.Services.AddSingleton<MatcherEngine>();
builder.Services.AddSingleton<MetadataExtractor>();
builder.Services.AddSingleton<AltChunkInserter>();
builder.Services.AddSingleton<SectionInserter>();
builder.Services.AddSingleton<TocManager>();
builder.Services.AddSingleton<TableUpdater>();
builder.Services.AddSingleton<MergeService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();
