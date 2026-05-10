namespace AntiFraud.Core.BallTree.Entities;

using AntiFraud.Core.NeighborhoodClassifier.Entities;
using AntiFraud.Core.Shared.Utils;

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

    [ThreadStatic] private static KnnPriorityQueueEntity? _queueCache;

    /// <summary>Conta quantos dos k vizinhos mais próximos são fraude (dist² na folha; mesmo resultado que Search enumerável).</summary>
    public int CountFraudAmongKNearest(ReadOnlySpan<float> query, int k, out int neighborCount)
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

        var rootDistSq = DistanceSquared(query, _root.Centroid.AsSpan());
        SearchNode(_root, query, queue, rootDistSq);
        neighborCount = queue.Count;
        var fraud = 0;
        for (var i = 0; i < queue.Count; i++)
        {
            if (_dataSource.GetLabel(queue.GetIndex(i)))
                fraud++;
        }
        return fraud;
    }

    /// <summary>
    /// Poda em distância ao quadrado pura: o pai já fez <c>DistanceSquared(query, node.Centroid)</c>
    /// e passa <paramref name="distSqToCenter"/> aqui, evitando sqrt no caminho de poda.
    /// Quando a fila está cheia, o nó pode ser descartado se
    /// <c>distSqToCenter &gt; (radius + sqrt(worstSq))²</c>.
    /// </summary>
    private void SearchNode(BallTreeNodeEntity node, ReadOnlySpan<float> query, KnnPriorityQueueEntity queue, float distSqToCenter)
    {
        if (queue.IsFull)
        {
            var bound = node.Radius + queue.WorstDist; // worstDist é sqrt(worstSq) cacheado
            if (distSqToCenter > bound * bound)
                return;
        }

        if (node.IsLeaf)
        {
            var indices = node.PointIndices!;
            var n = indices.Length;

            if (n > 0)
                VectorMath14.Prefetch(_dataSource.GetVectorSpan(indices[0]));

            for (var i = 0; i < n; i++)
            {
                if (i + 1 < n)
                    VectorMath14.Prefetch(_dataSource.GetVectorSpan(indices[i + 1]));

                var idx = indices[i];
                var distSq = DistanceSquared(_dataSource.GetVectorSpan(idx), query);
                queue.TryInsert(idx, distSq);
            }
            return;
        }

        var dl = DistanceSquared(query, node.Left!.Centroid.AsSpan());
        var dr = DistanceSquared(query, node.Right!.Centroid.AsSpan());

        if (dl <= dr)
        {
            SearchNode(node.Left!, query, queue, dl);
            SearchNode(node.Right!, query, queue, dr);
        }
        else
        {
            SearchNode(node.Right!, query, queue, dr);
            SearchNode(node.Left!, query, queue, dl);
        }
    }

    #endregion

    private static float DistanceSquared(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        => VectorMath14.DistanceSquared(a, b);

    private static float Distance(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        => VectorMath14.Distance(a, b);
}