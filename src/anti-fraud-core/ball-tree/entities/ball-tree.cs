namespace AntiFraud.Core.BallTree.Entities;

using AntiFraud.Core.NeighborhoodClassifier.ValueObjects;
using AntiFraud.Core.NeighborhoodClassifier.Entities;

public sealed class BallTreeEntity
{
    private const int Dimensions = 14;

    private readonly IBallTreeDataSource _dataSource;
    private readonly int _leafSize;
    private readonly BallTreeNodeEntity _root;

    public BallTreeEntity(IBallTreeDataSource dataSource, int leafSize = 30)
    {
        _dataSource = dataSource;
        _leafSize = leafSize;

        var allIndices = Enumerable.Range(0, dataSource.Count).ToArray();
        _root = Build(allIndices);
    }

    #region Build

    private BallTreeNodeEntity Build(int[] indices)
    {
        var node = new BallTreeNodeEntity();

        ComputeCentroid(indices, node.Centroid);
        node.Radius = ComputeRadius(indices, node.Centroid);

        if (indices.Length <= _leafSize)
        {
            node.PointIndices = indices;
            return node;
        }

        var (left, right) = Split(indices);

        node.Left = Build(left);
        node.Right = Build(right);

        return node;
    }

    private void ComputeCentroid(int[] indices, float[] centroid)
    {
        Array.Clear(centroid, 0, Dimensions);

        foreach (var idx in indices)
        {
            var span = _dataSource.GetVectorSpan(idx);
            for (int i = 0; i < Dimensions; i++)
                centroid[i] += span[i];
        }

        for (int i = 0; i < Dimensions; i++)
            centroid[i] /= indices.Length;
    }

    private float ComputeRadius(int[] indices, float[] centroid)
    {
        float max = 0f;

        foreach (var idx in indices)
        {
            var dist = Distance(_dataSource.GetVectorSpan(idx), centroid);
            if (dist > max) max = dist;
        }

        return max;
    }

    private (int[], int[]) Split(int[] indices)
    {
        int pivotA = FindFarthest(indices, _dataSource.GetVectorSpan(indices[0]));
        int pivotB = FindFarthest(indices, _dataSource.GetVectorSpan(pivotA));

        var left = new List<int>();
        var right = new List<int>();

        var a = _dataSource.GetVectorSpan(pivotA);
        var b = _dataSource.GetVectorSpan(pivotB);

        foreach (var idx in indices)
        {
            var p = _dataSource.GetVectorSpan(idx);

            var da = Distance(p, a);
            var db = Distance(p, b);

            if (da <= db) left.Add(idx);
            else right.Add(idx);
        }

        if (left.Count == 0) { left.Add(right[^1]); right.RemoveAt(right.Count - 1); }
        if (right.Count == 0) { right.Add(left[^1]); left.RemoveAt(left.Count - 1); }

        return (left.ToArray(), right.ToArray());
    }

    private int FindFarthest(int[] indices, ReadOnlySpan<float> reference)
    {
        int best = indices[0];
        float max = 0f;

        foreach (var idx in indices)
        {
            var dist = Distance(_dataSource.GetVectorSpan(idx), reference);
            if (dist > max)
            {
                max = dist;
                best = idx;
            }
        }

        return best;
    }

    #endregion

    #region Search

    public IEnumerable<KnnCandidate> Search(float[] query, int k)
    {
        var queue = new KnnPriorityQueueEntity(k);
        SearchNode(_root, query, queue);

        return queue.GetResults()
            .Select(r => new KnnCandidate(
                r.Index,
                _dataSource.GetLabel(r.Index),
                r.Distance
            ));
    }

    private void SearchNode(BallTreeNodeEntity node, float[] query, KnnPriorityQueueEntity queue)
    {
        var distToCenter = Distance(query, node.Centroid);
        var minDist = distToCenter - node.Radius;

        if (minDist > queue.WorstDistance)
            return;

        if (node.IsLeaf)
        {
            foreach (var idx in node.PointIndices!)
            {
                var dist = Distance(_dataSource.GetVectorSpan(idx), query);
                queue.TryInsert(idx, dist);
            }
            return;
        }

        var dl = Distance(query, node.Left!.Centroid);
        var dr = Distance(query, node.Right!.Centroid);

        if (dl <= dr)
        {
            SearchNode(node.Left, query, queue);
            SearchNode(node.Right, query, queue);
        }
        else
        {
            SearchNode(node.Right, query, queue);
            SearchNode(node.Left, query, queue);
        }
    }

    #endregion

    private static float Distance(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        float sum = 0f;
        for (int i = 0; i < Dimensions; i++)
        {
            float d = a[i] - b[i];
            sum += d * d;
        }
        return MathF.Sqrt(sum);
    }

    private static float Distance(float[] a, float[] b)
        => Distance(a.AsSpan(), b.AsSpan());
}