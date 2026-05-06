using AntiFraud.Core.VectorizedReference.Entities;

namespace AntiFraud.Core.VectorizedReference.Repository;

public interface IVectorizedReferenceContract
{
    Task Save(VectorizedReferenceEntity vectorizedReference);

    Task<long> GetCountAsync(CancellationToken ct = default);

    IAsyncEnumerable<VectorizedReferenceEntity> GetDataSet(CancellationToken ct = default);
}