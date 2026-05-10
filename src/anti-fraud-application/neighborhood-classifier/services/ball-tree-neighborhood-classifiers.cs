namespace AntiFraud.Application.NeighborhoodClassifier.Services;

using AntiFraud.Core.BallTree.Entities;
using AntiFraud.Core.NeighborhoodClassifier.Services;

public sealed class BallTreeNeighborhoodClassifier : INeighborhoodClassifier
{
    private readonly IBallTreeDataSource _dataSource;
    private readonly int _leafSize;
    private BallTreeEntity? _tree;

    public BallTreeNeighborhoodClassifier(IBallTreeDataSource dataSource, int leafSize = 30)
    {
        _dataSource = dataSource;
        _leafSize = leafSize;
    }

    public void Initialize()
    {
        _tree = new BallTreeEntity(_dataSource, _leafSize);
    }

    public (int FraudCount, int Total) GetNeighborVote(ReadOnlySpan<float> queryVector, int k)
    {
        if (_tree is null)
            throw new InvalidOperationException("BallTree has not been initialized. Call Initialize() first.");

        var fraud = _tree.CountFraudAmongKNearest(queryVector, k, out var total);
        return (fraud, total);
    }
}
