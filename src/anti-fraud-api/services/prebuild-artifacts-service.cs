using System.Diagnostics;

using AntiFraud.Application.NeighborhoodClassifier.Services;
using AntiFraud.Core.BallTree.Entities;
using AntiFraud.Core.VectorizedReference.Entities;

namespace AntiFraud.API.Services;

/// <summary>
/// Gera <c>references.bin</c> e <c>references.balltree.bin</c> antes do runtime
/// (executado durante <c>docker build</c>). Evita custo de cold-start no <c>StartAsync</c>.
/// </summary>
public static class PrebuildArtifactsService
{
    public static async Task RunAsync(string gzPath, string binPath, string ballTreeCachePath, ILogger logger, CancellationToken ct = default)
    {
        var totalSw = Stopwatch.StartNew();

        var matSw = Stopwatch.StartNew();
        await VectorDatasetMaterializer.EnsureBinaryReadyAsync(gzPath, binPath, logger, ct).ConfigureAwait(false);
        logger.LogInformation("[Prebuild] references.bin ready in {Elapsed}ms.", matSw.ElapsedMilliseconds);

        using var dataset = new MemoryMappedVectorizedDataset();
        dataset.Open(binPath);

        var refsLen = new FileInfo(binPath).Length;
        var leafSize = VectorDatasetConstants.BallTreeLeafSize;

        if (BallTreeBinary.IsValidCacheFile(ballTreeCachePath, leafSize, refsLen))
        {
            logger.LogInformation("[Prebuild] Cache ball-tree já válido em {Path}; nada a fazer.", ballTreeCachePath);
            return;
        }

        var buildSw = Stopwatch.StartNew();
        logger.LogInformation("[Prebuild] Building ball-tree (leafSize={Leaf})...", leafSize);

        var tree = new BallTreeEntity(dataset, leafSize)
        {
            ProgressCallback = (nodes, leaves) =>
                logger.LogInformation("[Prebuild] build progress: {Nodes} internal, {Leaves} leaves ({Elapsed}ms).",
                    nodes, leaves, buildSw.ElapsedMilliseconds),
            PhaseCallback = (depth, phase, ms) =>
                logger.LogInformation("[Prebuild] build phase d={Depth} {Phase} took {Ms}ms.", depth, phase, ms),
        };

        logger.LogInformation("[Prebuild] Ball-tree built in {Elapsed}ms.", buildSw.ElapsedMilliseconds);

        BallTreeCacheMaterializer.SaveAtomic(ballTreeCachePath, tree, leafSize, refsLen, logger);
        logger.LogInformation("[Prebuild] Done (total {Elapsed}ms).", totalSw.ElapsedMilliseconds);
    }
}
