using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Storage.Blobs;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using TradeIntelligence.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Trade Intelligence Platform",
        Version = "v1",
        Description = "RAG-powered document intelligence for regulated trading — MiFID II, EMIR compliance automation"
    });
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// Azure AI Search
builder.Services.AddSingleton(sp =>
{
    var cfg = builder.Configuration;
    return new SearchIndexClient(
        new Uri(cfg["AzureSearch:Endpoint"]!),
        new AzureKeyCredential(cfg["AzureSearch:ApiKey"]!));
});
builder.Services.AddSingleton(sp =>
{
    var cfg = builder.Configuration;
    return new SearchClient(
        new Uri(cfg["AzureSearch:Endpoint"]!),
        cfg["AzureSearch:IndexName"]!,
        new AzureKeyCredential(cfg["AzureSearch:ApiKey"]!));
});

// Azure Blob Storage
builder.Services.AddSingleton(sp =>
    new BlobServiceClient(builder.Configuration["AzureStorage:ConnectionString"]!));

// Azure OpenAI
builder.Services.AddSingleton(sp =>
{
    var cfg = builder.Configuration;
    return new AzureOpenAIClient(
        new Uri(cfg["AzureOpenAI:Endpoint"]!),
        new AzureKeyCredential(cfg["AzureOpenAI:ApiKey"]!));
});

// Semantic Kernel
builder.Services.AddSingleton(sp =>
{
    var cfg = builder.Configuration;
    return Kernel.CreateBuilder()
        .AddAzureOpenAIChatCompletion(
            deploymentName: cfg["AzureOpenAI:ChatDeployment"]!,
            endpoint: cfg["AzureOpenAI:Endpoint"]!,
            apiKey: cfg["AzureOpenAI:ApiKey"]!)
        .Build();
});

builder.Services.AddSingleton<ISearchIndexService, AzureSearchIndexService>();
builder.Services.AddScoped<IRagQueryService, RagQueryService>();
builder.Services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();
builder.Services.AddScoped<IComplianceCheckService, ComplianceCheckService>();

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Trade Intelligence v1"));
app.UseCors();
app.MapControllers();
app.MapHealthChecks("/health");

// Ensure search index exists on startup
using (var scope = app.Services.CreateScope())
{
    var indexService = scope.ServiceProvider.GetRequiredService<ISearchIndexService>();
    await indexService.EnsureIndexExistsAsync();
}

app.Run();
