namespace AntiFraud.Application.NeighborhoodClassifier.Services;

using AntiFraud.Core.BallTree.Entities;
using AntiFraud.Core.NeighborhoodClassifier.Entities;
using AntiFraud.Core.NeighborhoodClassifier.Services;
using AntiFraud.Core.Shared.Utils;

public sealed class BruteForceNeighborhoodClassifier : INeighborhoodClassifier
{
    private readonly IBallTreeDataSource _dataSource;

    public BruteForceNeighborhoodClassifier(IBallTreeDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public void Initialize()
    {
        return;
    }

    [ThreadStatic] private static KnnPriorityQueueEntity? _queueCache;

    public (int FraudCount, int Total) GetNeighborVote(ReadOnlySpan<float> queryVector, int k)
    {
        var queue = _queueCache;
        if (queue is null || queue.Capacity != k)
        {
            queue = new KnnPriorityQueueEntity(k);
            _queueCache = queue;
        }
        else
        {
            queue.Reset();
        }

        var n = _dataSource.Count;

        if (n > 0)
            VectorMath14.Prefetch(_dataSource.GetVectorSpan(0));

        for (var i = 0; i < n; i++)
        {
            if (i + 1 < n)
                VectorMath14.Prefetch(_dataSource.GetVectorSpan(i + 1));

            var distSq = VectorMath14.DistanceSquared(queryVector, _dataSource.GetVectorSpan(i));
            queue.TryInsert(i, distSq);
        }

        var fraud = 0;
        for (var i = 0; i < queue.Count; i++)
        {
            if (_dataSource.GetLabel(queue.GetIndex(i)))
                fraud++;
        }

        return (fraud, queue.Count);
    }
}
