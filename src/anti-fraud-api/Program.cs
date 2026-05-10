using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http.Json;
using AntiFraud.Application.Extensions;
using AntiFraud.Infrastructure.Extensions;
using AntiFraud.API.Endpoints;
using AntiFraud.API.HostedServices;
using AntiFraud.API.Services;
using AntiFraud.Application.Shared.ValueObjects;
using AntiFraud.Core.NeighborhoodClassifier;
using AntiFraud.Application.FraudScore.Services;
using AntiFraud.Core.NeighborhoodClassifier.ValueObjects;
using AntiFraud.Core.NeighborhoodClassifier.Services;
using AntiFraud.Core.VectorizedReference.Entities;

// Evita ramp-up tardio do thread pool sob 0→900 rps.
// Com 0,475 CPU por replica, manter um pool quente reduz a cauda inicial.
ThreadPool.SetMinThreads(workerThreads: 32, completionPortThreads: 32);

var builder = WebApplication.CreateBuilder(args);

// Kestrel: enxugar pipeline pra reduzir cauda sob saturação.
builder.WebHost.ConfigureKestrel(o =>
{
    o.AddServerHeader = false;
    o.AllowSynchronousIO = false;
    o.Limits.MaxConcurrentConnections = null;
    o.Limits.MaxConcurrentUpgradedConnections = 0;
    o.Limits.MaxRequestBodySize = 8 * 1024;
    o.Limits.MinRequestBodyDataRate = null;
    o.Limits.MinResponseDataRate = null;
    o.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(30);
    o.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(5);
});

// Source-generated JSON serializer (sem reflection no hot path)
builder.Services.Configure<JsonOptions>(o =>
    o.SerializerOptions.TypeInfoResolverChain.Insert(0, FraudJsonSerializerContext.Default));

// --------------------------------------------------
// Configuração da strategy sem depender de ConfigurationBinder
// --------------------------------------------------
var strategyString = builder.Configuration["AntiFraud:ClassifierStrategy"] ?? "BallTree";
if (!Enum.TryParse<NeighborhoodClassifierStrategy>(strategyString, ignoreCase: true, out var strategy))
{
    strategy = NeighborhoodClassifierStrategy.BallTree;
}

// --------------------------------------------------
// Services - Infrastructure + Application
// --------------------------------------------------
builder.Services.AddAntiFraudInfrastructure();
builder.Services.AddAntiFraudApplication(strategy);

// Adapter REST sem estado: singleton para evitar alocação por request
builder.Services.AddSingleton<IFraudScoreRestService, FraudScoreRestService>();

// Estado de prontidão
builder.Services.AddSingleton<DatasetReadinessState>();

// Hosted service: INeighborhoodClassifier só existe como serviço keyed (mesma instância do FraudEngine)
builder.Services.AddHostedService(sp => new DatasetLoaderHostedService(
    sp.GetRequiredService<DatasetReadinessState>(),
    sp.GetRequiredService<MemoryMappedVectorizedDataset>(),
    sp.GetRequiredKeyedService<INeighborhoodClassifier>(strategy),
    sp.GetRequiredService<IConfiguration>(),
    sp.GetRequiredService<ILogger<DatasetLoaderHostedService>>()));

// Logging mínimo
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// --------------------------------------------------
// Build
// --------------------------------------------------
var app = builder.Build();

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("Startup");
startupLogger.LogInformation("Starting AntiFraud API with strategy {Strategy}", strategy);

// --------------------------------------------------
// Endpoints (Minimal API)
app.MapReadinessEndpoint();
app.MapFraudScoreEndpoint();

// Run
app.Run();