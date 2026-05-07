using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AntiFraud.Application.Extensions;
using AntiFraud.Infrastructure.Extensions;
using AntiFraud.API.Endpoints;
using AntiFraud.API.HostedServices;
using AntiFraud.Application.Shared.ValueObjects;
using AntiFraud.Core.NeighborhoodClassifier;
using AntiFraud.Application.FraudScore.Services;
using AntiFraud.Core.NeighborhoodClassifier.ValueObjects;
using AntiFraud.Core.NeighborhoodClassifier.Services;
using AntiFraud.Core.VectorizedReference.Entities;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddAntiFraudInfrastructure(builder.Configuration);
builder.Services.AddAntiFraudApplication(strategy);

// Registrar adapter REST que adapta Application -> REST
builder.Services.AddScoped<IFraudScoreRestService, FraudScoreRestService>();

// Estado de prontidão
builder.Services.AddSingleton<DatasetReadinessState>();

// Hosted service: INeighborhoodClassifier só existe como serviço keyed (mesma instância do FraudEngine)
builder.Services.AddHostedService(sp => new DatasetLoaderHostedService(
    sp.GetRequiredService<DatasetReadinessState>(),
    sp.GetRequiredService<CompiledVectorizedDataset>(),
    sp.GetRequiredKeyedService<INeighborhoodClassifier>(strategy),
    sp.GetRequiredService<IServiceScopeFactory>(),
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