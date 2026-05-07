namespace AntiFraud.Application.NeighborhoodClassifier.Services;

using AntiFraud.Core.BallTree.Entities;
using AntiFraud.Core.NeighborhoodClassifier.ValueObjects;
using AntiFraud.Core.NeighborhoodClassifier.Services;

public sealed class BruteForceNeighborhoodClassifier : INeighborhoodClassifier
{
    private readonly IBallTreeDataSource _dataSource;
    private const int Dimensions = 14;

    public BruteForceNeighborhoodClassifier(IBallTreeDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public void Initialize()
    {
        return;
    }

    public IEnumerable<KnnCandidate> ClassifyByNeighborhood(float[] queryVector, int k)
    {
        return Enumerable
            .Range(0, _dataSource.Count)
            .Select(i => new KnnCandidate(
                i,
                _dataSource.GetLabel(i),
                Distance(queryVector, _dataSource.GetVectorSpan(i))
            ))
            .OrderBy(x => x.Distance)
            .Take(k);
    }

    private static float Distance(float[] a, ReadOnlySpan<float> b)
    {
        float sum = 0f;
        for (int i = 0; i < Dimensions; i++)
        {
            float d = a[i] - b[i];
            sum += d * d;
        }
        return MathF.Sqrt(sum);
    }
}