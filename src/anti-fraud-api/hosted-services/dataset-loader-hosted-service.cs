using System.IO.Compression;
using System.Text.Json;
using AntiFraud.Application.Shared.ValueObjects;
using AntiFraud.Core.NeighborhoodClassifier.Services;
using AntiFraud.Core.VectorizedReference.Entities;
using AntiFraud.Core.VectorizedReference.Repository;
using AntiFraud.Core.VectorizedReference.Models;

namespace AntiFraud.API.HostedServices;

public sealed class DatasetLoaderHostedService : IHostedService
{
    private readonly DatasetReadinessState _readinessState;
    private readonly CompiledVectorizedDataset _dataset;
    private readonly INeighborhoodClassifier _classifier;
    private readonly IVectorizedReferenceContract _repository;
    private readonly ILogger<DatasetLoaderHostedService> _logger;

    private static readonly string DatasetPath = Path.Combine(
        AppContext.BaseDirectory, "shared", "resources", "references.json.gz");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DatasetLoaderHostedService(
        DatasetReadinessState readinessState,
        CompiledVectorizedDataset dataset,
        INeighborhoodClassifier classifier,
        IVectorizedReferenceContract repository,
        ILogger<DatasetLoaderHostedService> logger)
    {
        _readinessState = readinessState;
        _dataset = dataset;
        _classifier = classifier;
        _repository = repository;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            // FASE 1: Boot blocking
            var count = await _repository.GetCountAsync(cancellationToken);

            if (count > 0)
            {
                _logger.LogInformation("[DatasetLoader] MongoDB has {Count} records. Loading from MongoDB...", count);
                await LoadFromMongoAsync(cancellationToken);
            }
            else
            {
                _logger.LogInformation("[DatasetLoader] MongoDB is empty. Loading from references.json.gz...");
                await LoadFromFileAsync(cancellationToken);

                // FASE 2: Sync fire-and-forget
                _ = PersistToMongoAsync();
            }

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

    // -------------------------------------------------------------------------
    // FASE 1a: Carrega do MongoDB
    // -------------------------------------------------------------------------
    private async Task LoadFromMongoAsync(CancellationToken cancellationToken)
    {
        var index = 0;

        await foreach (var entry in _repository.GetDataSet(cancellationToken))
        {
            _dataset.SetEntry(index++, entry.Vector, entry.IsFraud);

            if (index % 100_000 == 0)
                _logger.LogInformation("[DatasetLoader] {Index} records loaded from MongoDB...", index);
        }

        _logger.LogInformation("[DatasetLoader] {Total} records loaded from MongoDB.", index);
    }

    // -------------------------------------------------------------------------
    // FASE 1b: Carrega do arquivo .json.gz
    // -------------------------------------------------------------------------
    private async Task LoadFromFileAsync(CancellationToken cancellationToken)
    {
        await using var fileStream = new FileStream(
            DatasetPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 65536,
            useAsync: true);

        await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);

        var index = 0;

        await foreach (var record in JsonSerializer
            .DeserializeAsyncEnumerable<VectorizedReferenceFileModel>(gzipStream, JsonOptions, cancellationToken))
        {
            if (record is null) continue;

            _dataset.SetEntry(index++, record.Vector, record.Label == "fraud");

            if (index % 100_000 == 0)
                _logger.LogInformation("[DatasetLoader] {Index} records loaded from file...", index);
        }

        _logger.LogInformation("[DatasetLoader] {Total} records loaded from file.", index);
    }

    // -------------------------------------------------------------------------
    // FASE 2: Persiste no MongoDB em background (fire-and-forget)
    // -------------------------------------------------------------------------
    private async Task PersistToMongoAsync()
    {
        try
        {
            _logger.LogInformation("[DatasetLoader] Starting background MongoDB persistence...");

            var batch = new List<VectorizedReferenceEntity>(10_000);
            var total = 0;

            for (var i = 0; i < _dataset.Count; i++)
            {
                var (vector, isFraud) = _dataset.GetEntry(i);

                batch.Add(VectorizedReferenceEntity.Create(isFraud, vector));

                if (batch.Count == 10_000)
                {
                    await _repository.SaveBatchAsync(batch);
                    total += batch.Count;
                    batch.Clear();

                    _logger.LogInformation("[DatasetLoader] {Total} records persisted to MongoDB...", total);
                }
            }

            if (batch.Count > 0)
            {
                await _repository.SaveBatchAsync(batch);
                total += batch.Count;
            }

            _logger.LogInformation("[DatasetLoader] MongoDB persistence completed. {Total} records saved.", total);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DatasetLoader] Background MongoDB persistence failed.");
        }
    }
}