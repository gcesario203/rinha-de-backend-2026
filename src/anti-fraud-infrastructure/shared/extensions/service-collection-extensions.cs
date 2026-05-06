using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using AntiFraud.Core.Shared.ValueObjects;
using AntiFraud.Core.MCC.Repository;
using AntiFraud.Core.FraudEngine.Services;
using AntiFraud.Core.NeighborhoodClassifier.Services;
using AntiFraud.Core.Transaction.Service;

namespace AntiFraud.Infrastructure.Shared.Extensions;

public static class ServiceCollectionExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IServiceCollection AddAntiFraudCore(this IServiceCollection services)
    {
        services.AddFraudHeuristics();
        services.AddMccRisk();
        services.AddFraudEngine();

        return services;
    }

    private static IServiceCollection AddFraudHeuristics(this IServiceCollection services)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "shared", "resources", "normalization.json");
        var json = File.ReadAllText(path);

        var heuristics = JsonSerializer.Deserialize<FraudHeuristics>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize normalization.json");

        services.AddSingleton(heuristics);

        return services;
    }

    private static IServiceCollection AddMccRisk(this IServiceCollection services)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "shared", "resources", "mcc_risk.json");
        var json = File.ReadAllText(path);

        var mccRisk = JsonSerializer.Deserialize<Dictionary<string, float>>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize mcc_risk.json");

        services.AddSingleton(mccRisk);

        return services;
    }

    private static IServiceCollection AddFraudEngine(this IServiceCollection services)
    {
        services.AddSingleton<IMCCRepository, MCCInMemoryRiskRepository>();
        services.AddSingleton<INeighborhoodClassifier, BallTreeNeighborhoodClassifier>();
        services.AddSingleton<IFraudEngine, FraudInferenceEngine>();
        services.AddScoped<ITransactionService, TransactionService>();

        return services;
    }
}