using AntiFraud.Core.VectorizedReference.Entities;
using AntiFraud.Core.VectorizedReference.Models;
using AntiFraud.Core.VectorizedReference.Repository;
using MongoDB.Driver;

namespace AntiFraud.Infrastructure.Persistence.MongoDB;

public sealed class VectorizedReferenceMongoDBRepository : IVectorizedReferenceContract
{
    private readonly IMongoCollection<VectorizedReferenceMongoDBModel> _collection;

    public VectorizedReferenceMongoDBRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<VectorizedReferenceMongoDBModel>("vectorized_references");
    }

    public async Task<long> GetCountAsync(CancellationToken ct = default)
        => await _collection.CountDocumentsAsync(
            FilterDefinition<VectorizedReferenceMongoDBModel>.Empty,
            cancellationToken: ct);

    public async IAsyncEnumerable<VectorizedReferenceEntity> GetDataSet(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var options = new FindOptions<VectorizedReferenceMongoDBModel> { BatchSize = 10_000 };

        using var cursor = await _collection.FindAsync(
            FilterDefinition<VectorizedReferenceMongoDBModel>.Empty, options, ct);

        while (await cursor.MoveNextAsync(ct))
        {
            foreach (var model in cursor.Current)
                yield return VectorizedReferenceEntity.Create(model.IsFraud, model.Vector);
        }
    }

    public async Task SaveBatchAsync(IEnumerable<VectorizedReferenceEntity> batch, CancellationToken ct = default)
    {
        var models = batch.Select(entity => new VectorizedReferenceMongoDBModel(
            Vector: entity.Vector.ToArray(),
            IsFraud: entity.IsFraud
        ));

        await _collection.InsertManyAsync(models, new InsertManyOptions
        {
            IsOrdered = false  // Permite paralelismo interno no driver do MongoDB
        }, ct);
    }

    public async Task Save(VectorizedReferenceEntity entity)
    {
        var model = new VectorizedReferenceMongoDBModel(
            Vector: entity.Vector.ToArray(),
            IsFraud: entity.IsFraud
        );

        await _collection.InsertOneAsync(model);
    }
}