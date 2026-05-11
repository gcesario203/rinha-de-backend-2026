using System.Diagnostics;

using AntiFraud.Core.NeighborhoodClassifier.Services;
using AntiFraud.Core.VectorizedReference.Entities;

namespace AntiFraud.API.Services;

/// <summary>
/// Aquece a API antes de aceitar tráfego:
/// <list type="number">
/// <item><b>PreFault</b>: toca todas as páginas do mmap em sequência → popula o page-cache do kernel
/// e elimina major page-faults durante o k-NN.</item>
/// <item><b>Burn-in</b>: queries iniciais para o JIT promover métodos quentes a tier-2 com PGO real.</item>
/// <item><b>Measured</b>: queries pós burn-in cronometradas isoladamente para refletir steady-state.</item>
/// </list>
/// Custo típico no ambiente do desafio: ~500 ms – 1 s. Cabe folgado no health-check de 60 s.
/// </summary>
public static class WarmupService
{
    public static void Run(
        MemoryMappedVectorizedDataset dataset,
        INeighborhoodClassifier classifier,
        int burnInQueries,
        int measuredQueries,
        int k,
        ILogger logger)
    {
        if (dataset.Count == 0 || (burnInQueries + measuredQueries) <= 0)
        {
            logger.LogInformation("[Warmup] Skipped (datasetCount={Count}).", dataset.Count);
            return;
        }

        var swFault = Stopwatch.StartNew();
        var checksum = dataset.PreFault();
        swFault.Stop();
        logger.LogInformation(
            "[Warmup] PreFault concluído em {Ms}ms (checksum={Checksum}).",
            swFault.ElapsedMilliseconds, checksum);

        var rng = new Random(unchecked((int)0xC0FFEE));
        var maxIdx = dataset.Count;
        Span<float> query = stackalloc float[VectorDatasetConstants.Dimensions];

        // Burn-in: deixa o JIT/PGO promover métodos quentes; cronômetro corre mas não é o que reportamos.
        var swBurn = Stopwatch.StartNew();
        long burnNeighbors = 0;
        long burnFraud = 0;
        for (var i = 0; i < burnInQueries; i++)
        {
            RunOne(dataset, classifier, rng, maxIdx, k, query, out var fraud, out var total);
            burnNeighbors += total;
            burnFraud += fraud;
        }
        swBurn.Stop();

        // Measured: estimativa próxima do steady-state.
        var swMeasured = Stopwatch.StartNew();
        long measNeighbors = 0;
        long measFraud = 0;
        for (var i = 0; i < measuredQueries; i++)
        {
            RunOne(dataset, classifier, rng, maxIdx, k, query, out var fraud, out var total);
            measNeighbors += total;
            measFraud += fraud;
        }
        swMeasured.Stop();

        var usBurn = burnInQueries > 0 ? swBurn.Elapsed.TotalMicroseconds / burnInQueries : 0.0;
        var usMeasured = measuredQueries > 0 ? swMeasured.Elapsed.TotalMicroseconds / measuredQueries : 0.0;

        logger.LogInformation(
            "[Warmup] burn-in {BurnN} em {BurnMs}ms (~{BurnUs:F1}μs/req); " +
            "measured {MeasN} em {MeasMs}ms (~{MeasUs:F1}μs/req). " +
            "Total vizinhos={Vizinhos}, votos fraude={Fraudes}.",
            burnInQueries, swBurn.ElapsedMilliseconds, usBurn,
            measuredQueries, swMeasured.ElapsedMilliseconds, usMeasured,
            burnNeighbors + measNeighbors, burnFraud + measFraud);
    }

    private static void RunOne(
        MemoryMappedVectorizedDataset dataset,
        INeighborhoodClassifier classifier,
        Random rng,
        int maxIdx,
        int k,
        Span<float> query,
        out int fraud,
        out int total)
    {
        var idx = rng.Next(maxIdx);
        dataset.GetVectorSpan(idx).CopyTo(query);

        // Pequena perturbação ε ≈ ±0.005 por dimensão para diversificar caminhos na árvore.
        for (var d = 0; d < query.Length; d++)
            query[d] += ((float)rng.NextDouble() - 0.5f) * 0.01f;

        (fraud, total) = classifier.GetNeighborVote(query, k);
    }
}
