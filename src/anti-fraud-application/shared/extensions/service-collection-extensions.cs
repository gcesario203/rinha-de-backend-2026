using Microsoft.Extensions.DependencyInjection;
using AntiFraud.Application.FraudEngine.Services;
using AntiFraud.Core.FraudEngine.Services;
using AntiFraud.Core.NeighborhoodClassifier.Services;
using AntiFraud.Core.NeighborhoodClassifier;
using AntiFraud.Application.Transaction;
using AntiFraud.Application.FraudEngine;
using AntiFraud.Application.NeighborhoodClassifier;
using AntiFraud.Core.NeighborhoodClassifier.ValueObjects;
using AntiFraud.Application.Transaction.Services;

using AntiFraud.Application.NeighborhoodClassifier.Services;

namespace AntiFraud.Application.Extensions;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddAntiFraudApplication(
        this IServiceCollection services,
        NeighborhoodClassifierStrategy strategy = NeighborhoodClassifierStrategy.BallTree)
    {
        services.AddAntiFraudNeighborhoodClassifiers();
        services.AddAntiFraudFraudEngine(strategy);
        services.AddAntiFraudTransactionService();

        return services;
    }

    private static IServiceCollection AddAntiFraudNeighborhoodClassifiers(this IServiceCollection services)
    {
        // Registra ambos via Keyed Services
        services.AddKeyedSingleton<INeighborhoodClassifier, BallTreeNeighborhoodClassifier>(
            NeighborhoodClassifierStrategy.BallTree);

        services.AddKeyedSingleton<INeighborhoodClassifier, KdTreeNeighborhoodClassifier>(
            NeighborhoodClassifierStrategy.KdTree);

        services.AddKeyedSingleton<INeighborhoodClassifier, BruteForceNeighborhoodClassifier>(
            NeighborhoodClassifierStrategy.BruteForce);

        return services;
    }

    private static IServiceCollection AddAntiFraudFraudEngine(
        this IServiceCollection services,
        NeighborhoodClassifierStrategy strategy)
    {
        // Injeta o classificador correto no FraudEngine baseado na estratégia escolhida
        services.AddSingleton<IFraudEngine>(sp =>
        {
            var classifier = sp.GetRequiredKeyedService<INeighborhoodClassifier>(strategy);
            return new FraudInferenceEngine(classifier);
        });

        return services;
    }

    private static IServiceCollection AddAntiFraudTransactionService(this IServiceCollection services)
    {
        services.AddSingleton<ITransactionService, TransactionService>();
        return services;
    }
}