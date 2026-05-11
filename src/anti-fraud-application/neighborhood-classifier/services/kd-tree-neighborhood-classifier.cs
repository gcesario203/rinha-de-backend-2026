using AntiFraud.Core.BallTree.Entities;
using AntiFraud.Core.KdTree.Entities;
using AntiFraud.Core.NeighborhoodClassifier.Services;
using AntiFraud.Core.VectorizedReference.Entities;

namespace AntiFraud.Application.NeighborhoodClassifier.Services;

public sealed class KdTreeNeighborhoodClassifier : INeighborhoodClassifier
{
    private readonly IBallTreeDataSource _dataSource;
    private readonly int _leafSize;
    private KdTreeEntity? _tree;

    public Action<int, int>? BuildProgressCallback { get; set; }

    public KdTreeEntity? Tree => _tree;

    public KdTreeNeighborhoodClassifier(IBallTreeDataSource dataSource, int leafSize = VectorDatasetConstants.BallTreeLeafSize)
    {
        _dataSource = dataSource;
        _leafSize = leafSize;
    }

    public void AttachTree(KdTreeEntity tree) =>
        _tree = tree ?? throw new ArgumentNullException(nameof(tree));

    public void Initialize()
    {
        _tree = new KdTreeEntity(_dataSource, _leafSize)
        {
            ProgressCallback = BuildProgressCallback,
        };
    }

    public (int FraudCount, int Total) GetNeighborVote(ReadOnlySpan<float> queryVector, int k)
    {
        if (_tree is null)
            throw new InvalidOperationException("KD-tree has not been initialized. Call Initialize() first.");

        var fraud = _tree.CountFraudAmongKNearest(queryVector, k, out var total);
        return (fraud, total);
    }
}
