using AntiFraud.Core.VectorizedReference.Entities;

namespace AntiFraud.Core.VectorizedReference.Repository;

public interface IVectorizedReferenceContract
{
    Task<long> GetCountAsync(CancellationToken ct = default);

    IAsyncEnumerable<VectorizedReferenceEntity> GetDataSet(CancellationToken ct = default);

    Task SaveBatchAsync(IEnumerable<VectorizedReferenceEntity> batch, CancellationToken ct = default);

    Task Save(VectorizedReferenceEntity vectorizedReference);
}