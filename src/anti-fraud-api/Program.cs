using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http.Json;
using AntiFraud.Application.Extensions;
using AntiFraud.Infrastructure.Extensions;
using AntiFraud.API.Endpoints;
using AntiFraud.API.HostedServices;
using AntiFraud.API.Services;
using AntiFraud.Core.Serialization;
using AntiFraud.Application.Shared.ValueObjects;
using AntiFraud.Core.NeighborhoodClassifier;
using AntiFraud.Application.FraudScore.Services;
using AntiFraud.Core.NeighborhoodClassifier.ValueObjects;
using AntiFraud.Core.NeighborhoodClassifier.Services;
using AntiFraud.Core.VectorizedReference.Entities;

// --------------------------------------------------
// Modo offline: usado durante `docker build` para materializar references.bin
// e references.balltree.bin DENTRO da imagem. Evita custo de cold-start.
//   dotnet anti-fraud-api.dll --prebuild <gzPath> <binPath> <ballTreeBinPath>
// --------------------------------------------------
if (args.Length >= 1 && args[0] == "--prebuild")
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: --prebuild <gzPath> <binPath> <ballTreeBinPath>");
        return 1;
    }

    using var loggerFactory = LoggerFactory.Create(b => b.AddSimpleConsole(o => o.SingleLine = true));
    var prebuildLogger = loggerFactory.CreateLogger("Prebuild");
    try
    {
        await PrebuildArtifactsService.RunAsync(args[1], args[2], args[3], prebuildLogger);
        return 0;
    }
    catch (Exception ex)
    {
        prebuildLogger.LogCritical(ex, "Prebuild falhou.");
        return 2;
    }
}

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
    o.SerializerOptions.TypeInfoResolverChain.Insert(0, AntiFraudJsonSerializerContext.Default));

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

return 0;