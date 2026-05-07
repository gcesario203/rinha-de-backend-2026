using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

using MongoDB.Driver;
using AntiFraud.Core.Shared.ValueObjects;
using AntiFraud.Core.MCC.Repository;

using AntiFraud.Core.VectorizedReference.Entities;

using AntiFraud.Infrastructure.MCC.Repository;
using Microsoft.Extensions.Configuration;
using AntiFraud.Core.BallTree.Entities;
using AntiFraud.Core.NeighborhoodClassifier.ValueObjects;
using AntiFraud.Infrastructure.Persistence.MongoDB;
using AntiFraud.Core.VectorizedReference.Repository;
namespace AntiFraud.Infrastructure.Extensions;

public static class InfrastructureServiceCollectionExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static IServiceCollection AddAntiFraudInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddJsonResources();
        services.AddMongoDB(configuration);
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

    private static void AddMongoDB(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["MongoDB:ConnectionString"];
        var databaseName = configuration["MongoDB:DatabaseName"];

        services.AddSingleton<IMongoClient>(new MongoClient(connectionString));
        services.AddScoped(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(databaseName));

        services.AddScoped<IVectorizedReferenceContract, VectorizedReferenceMongoDBRepository>();
    }

    private static void AddVectorizedDataset(this IServiceCollection services)
    {
        // Criamos o container de 3 milhões de registros como Singleton.
        // O DatasetLoaderHostedService (na API) vai preencher esse cara.
        const int DatasetSize = 3_000_000;
        var dataset = new CompiledVectorizedDataset(DatasetSize);
        
        // Ele atende tanto à BallTree (IBallTreeDataSource) 
        // quanto ao Loader que precisa dar o SetEntry
        services.AddSingleton<IBallTreeDataSource>(dataset);
        services.AddSingleton(dataset); 
    }
}