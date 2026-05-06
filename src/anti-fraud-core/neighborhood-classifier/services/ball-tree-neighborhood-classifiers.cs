

namespace AntiFraud.Core.NeighborhoodClassifier.Services;

using AntiFraud.Core.BallTree.Entities;
using AntiFraud.Core.NeighborhoodClassifier.ValueObjects;

public sealed class BallTreeNeighborhoodClassifier : INeighborhoodClassifier
{
    private readonly BallTreeEntity _tree;

    public BallTreeNeighborhoodClassifier(IBallTreeDataSource dataSource, int leafSize = 30)
    {
        _tree = new BallTreeEntity(dataSource, leafSize);
    }

    public IEnumerable<KnnCandidate> ClassifyByNeighborhood(float[] queryVector, int k)
        => _tree.Search(queryVector, k);
}