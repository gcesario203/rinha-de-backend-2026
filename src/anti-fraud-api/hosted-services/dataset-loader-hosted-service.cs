using System.Diagnostics;
using Microsoft.Extensions.Configuration;

using AntiFraud.API.Services;
using AntiFraud.Application.NeighborhoodClassifier.Services;
using AntiFraud.Application.Shared.ValueObjects;
using AntiFraud.Core.NeighborhoodClassifier.Services;
using AntiFraud.Core.VectorizedReference.Entities;

namespace AntiFraud.API.HostedServices;

public sealed class DatasetLoaderHostedService : IHostedService
{
    private readonly DatasetReadinessState _readinessState;
    private readonly MemoryMappedVectorizedDataset _dataset;
    private readonly INeighborhoodClassifier _classifier;
    private readonly ILogger<DatasetLoaderHostedService> _logger;
    private readonly string _gzipDatasetPath;
    private readonly string _binaryDatasetPath;
    private readonly string? _ballTreeCachePathOverride;

    public DatasetLoaderHostedService(
        DatasetReadinessState readinessState,
        MemoryMappedVectorizedDataset dataset,
        INeighborhoodClassifier classifier,
        IConfiguration configuration,
        ILogger<DatasetLoaderHostedService> logger)
    {
        _readinessState = readinessState;
        _dataset = dataset;
        _classifier = classifier;
        _logger = logger;

        var root = AppContext.BaseDirectory;
        _gzipDatasetPath = configuration["Data:GzipDatasetPath"]
            ?? Path.Combine(root, "shared", "resources", "references.json.gz");
        _binaryDatasetPath = configuration["Data:BinaryDatasetPath"]
            ?? Path.Combine(root, "shared", "resources", "references.bin");
        _ballTreeCachePathOverride = configuration["Data:BallTreeCachePath"];
    }

    private string ResolveBallTreeCachePath()
    {
        if (!string.IsNullOrEmpty(_ballTreeCachePathOverride))
            return Path.GetFullPath(_ballTreeCachePathOverride);

        var binFull = Path.GetFullPath(_binaryDatasetPath);
        var dir = Path.GetDirectoryName(binFull) ?? ".";
        return Path.Combine(dir, Path.GetFileNameWithoutExtension(binFull) + ".balltree.bin");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var totalSw = Stopwatch.StartNew();

            var matSw = Stopwatch.StartNew();
            await VectorDatasetMaterializer.EnsureBinaryReadyAsync(
                    _gzipDatasetPath,
                    _binaryDatasetPath,
                    _logger,
                    cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation("[DatasetLoader] Materialize step: {Elapsed}ms.", matSw.ElapsedMilliseconds);

            _dataset.Open(_binaryDatasetPath);
            var refsLen = new FileInfo(_binaryDatasetPath).Length;
            var leafSize = VectorDatasetConstants.BallTreeLeafSize;
            var cachePath = ResolveBallTreeCachePath();

            if (_classifier is BallTreeNeighborhoodClassifier ballTree)
            {
                var fastSw = Stopwatch.StartNew();
                var cached = BallTreeCacheMaterializer.TryLoad(cachePath, _binaryDatasetPath, _dataset, leafSize);
                if (cached is not null)
                {
                    ballTree.AttachTree(cached);
                    _logger.LogInformation(
                        "[DatasetLoader] Ball-tree loaded from cache in {Elapsed}ms ({Path}).",
                        fastSw.ElapsedMilliseconds, cachePath);
                }
                else
                {
                    _logger.LogWarning(
                        "[DatasetLoader] Sem cache válido em {Path}; construção pode demorar vários minutos em I/O lento. Próximo arranque será rápido.",
                        cachePath);

                    var lockPath = cachePath + ".lock";
                    var lockDir = Path.GetDirectoryName(Path.GetFullPath(lockPath));
                    if (!string.IsNullOrEmpty(lockDir))
                        Directory.CreateDirectory(lockDir);

                    await using (var lockStream = new FileStream(
                                       lockPath,
                                       FileMode.OpenOrCreate,
                                       FileAccess.ReadWrite,
                                       FileShare.None,
                                       bufferSize: 4096,
                                       FileOptions.Asynchronous))
                    {
                        fastSw.Restart();
                        cached = BallTreeCacheMaterializer.TryLoad(cachePath, _binaryDatasetPath, _dataset, leafSize);
                        if (cached is not null)
                        {
                            ballTree.AttachTree(cached);
                            _logger.LogInformation(
                                "[DatasetLoader] Ball-tree loaded from cache after lock ({Elapsed}ms).",
                                fastSw.ElapsedMilliseconds);
                        }
                        else
                        {
                            var buildSw = Stopwatch.StartNew();
                            _logger.LogInformation("[DatasetLoader] Building Ball Tree (sem PreFault)...");

                            ballTree.BuildProgressCallback = (nodes, leaves) =>
                                _logger.LogInformation(
                                    "[DatasetLoader] build progress: {Nodes} internal nodes, {Leaves} leaves ({Elapsed}ms).",
                                    nodes, leaves, buildSw.ElapsedMilliseconds);

                            ballTree.BuildPhaseCallback = (depth, phase, ms) =>
                                _logger.LogInformation(
                                    "[DatasetLoader] build phase d={Depth} {Phase} took {Ms}ms.",
                                    depth, phase, ms);

                            ballTree.Initialize();

                            _logger.LogInformation("[DatasetLoader] Ball Tree built in {Elapsed}ms.", buildSw.ElapsedMilliseconds);

                            BallTreeCacheMaterializer.SaveAtomic(cachePath, ballTree.Tree!, leafSize, refsLen, _logger);
                        }
                    }
                }
            }
            else
            {
                _classifier.Initialize();
            }

            _readinessState.MarkAsReady();
            _logger.LogInformation("[DatasetLoader] API is ready (total startup {Elapsed}ms).", totalSw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "[DatasetLoader] Failed to load dataset. API will not serve requests.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
