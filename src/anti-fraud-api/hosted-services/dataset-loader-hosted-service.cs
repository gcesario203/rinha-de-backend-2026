using Microsoft.Extensions.Configuration;

using AntiFraud.API.Services;
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
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await VectorDatasetMaterializer.EnsureBinaryReadyAsync(
                    _gzipDatasetPath,
                    _binaryDatasetPath,
                    _logger,
                    cancellationToken)
                .ConfigureAwait(false);

            _dataset.Open(_binaryDatasetPath);

            _logger.LogInformation("[DatasetLoader] Building Ball Tree...");
            _classifier.Initialize();

            _readinessState.MarkAsReady();
            _logger.LogInformation("[DatasetLoader] Ball Tree built. API is ready.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "[DatasetLoader] Failed to load dataset. API will not serve requests.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
