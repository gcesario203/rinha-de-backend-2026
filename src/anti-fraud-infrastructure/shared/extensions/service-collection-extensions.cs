using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

using AntiFraud.Core.Shared.ValueObjects;
using AntiFraud.Core.MCC.Repository;

using AntiFraud.Core.BallTree.Entities;
using AntiFraud.Core.VectorizedReference.Entities;

using AntiFraud.Infrastructure.MCC.Repository;

namespace AntiFraud.Infrastructure.Extensions;

public static class InfrastructureServiceCollectionExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static IServiceCollection AddAntiFraudInfrastructure(this IServiceCollection services)
    {
        services.AddJsonResources();
        services.AddVectorizedDataset();

        return services;
    }

    private static void AddJsonResources(this IServiceCollection services)
    {
        // Heurísticas de Normalização
        var heuristicsPath = Path.Combine(AppContext.BaseDirectory, "shared", "resources", "normalization.json");
        var heuristicsJson = File.ReadAllText(heuristicsPath);
        var heuristics = JsonSerializer.Deserialize<FraudHeuristics>(heuristicsJson, JsonOptions);
        services.AddSingleton(heuristics!);

        // Riscos de MCC
        var mccPath = Path.Combine(AppContext.BaseDirectory, "shared", "resources", "mcc_risk.json");
        var mccJson = File.ReadAllText(mccPath);
        var mccRisk = JsonSerializer.Deserialize<Dictionary<string, float>>(mccJson, JsonOptions);
        services.AddSingleton(mccRisk!);

        services.AddSingleton<IMCCRepository, MCCInMemoryRiskRepository>();
    }

    private static void AddVectorizedDataset(this IServiceCollection services)
    {
        services.AddSingleton<MemoryMappedVectorizedDataset>();
        services.AddSingleton<IBallTreeDataSource>(sp => sp.GetRequiredService<MemoryMappedVectorizedDataset>());
    }
}
