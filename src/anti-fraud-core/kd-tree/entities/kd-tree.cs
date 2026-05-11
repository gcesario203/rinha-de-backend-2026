using AntiFraud.Core.BallTree.Entities;
using AntiFraud.Core.NeighborhoodClassifier.Entities;
using AntiFraud.Core.Shared.Utils;
using AntiFraud.Core.VectorizedReference.Entities;

namespace AntiFraud.Core.KdTree.Entities;

/// <summary>KD-tree estática em 14-D: construção por mediana na dimensão de maior amplitude, k-NN exato.</summary>
public sealed class KdTreeEntity
{
    private const int Dimensions = VectorDatasetConstants.Dimensions;
    private const int ProgressEveryNodes = 500;

    private readonly IBallTreeDataSource _dataSource;
    private readonly int _leafSize;
    private readonly int[]? _indexBuffer;
    private readonly KdTreeNodeEntity _root;

    public Action<int, int>? ProgressCallback { get; set; }

    private int _nodesBuilt;
    private int _leavesBuilt;

    public KdTreeEntity(IBallTreeDataSource dataSource, int leafSize = VectorDatasetConstants.BallTreeLeafSize)
    {
        _dataSource = dataSource;
        _leafSize = leafSize;

        var n = dataSource.Count;
        _indexBuffer = new int[n];
        for (var i = 0; i < n; i++)
            _indexBuffer[i] = i;

        _root = Build(0, n);
    }

    private KdTreeEntity(IBallTreeDataSource dataSource, KdTreeNodeEntity root)
    {
        _dataSource = dataSource;
        _leafSize = VectorDatasetConstants.BallTreeLeafSize;
        _indexBuffer = null;
        _root = root;
    }

    public static KdTreeEntity LoadFromCache(ReadOnlySpan<byte> fileBytes, IBallTreeDataSource dataSource, int expectedLeafSize, long referencesBinLength)
    {
        if (!KdTreeBinary.TryValidateHeader(fileBytes.Slice(0, KdTreeBinary.HeaderSize), expectedLeafSize, referencesBinLength, out _))
            throw new InvalidDataException("KD-tree cache: header inválido ou incompatível com o dataset.");

        var offset = KdTreeBinary.HeaderSize;
        var root = KdTreeSerialization.ReadTree(fileBytes, ref offset);
        if (offset != fileBytes.Length)
            throw new InvalidDataException($"KD-tree cache: esperado EOF em {offset}, ficheiro tem {fileBytes.Length} bytes.");

        return new KdTreeEntity(dataSource, root);
    }

    public void WriteFullCache(Stream stream, int leafSize, long referencesBinLength)
    {
        Span<byte> header = stackalloc byte[KdTreeBinary.HeaderSize];
        KdTreeBinary.WriteHeader(header, leafSize, referencesBinLength);
        stream.Write(header);
        KdTreeSerialization.WriteTree(stream, _root);
    }

    private KdTreeNodeEntity Build(int start, int length)
    {
        if (length <= _leafSize)
        {
            var leaf = new int[length];
            Array.Copy(_indexBuffer!, start, leaf, 0, length);
            _leavesBuilt++;
            return new KdTreeNodeEntity { PointIndices = leaf };
        }

        var dim = SelectSplitDimension(start, length);
        var leftLen = length / 2;
        var mid = start + leftLen - 1;
        NthElement(start, start + length, mid, dim);

        var maxLeft = float.MinValue;
        for (var t = start; t < start + leftLen; t++)
        {
            var c = GetCoord(_indexBuffer![t], dim);
            if (c > maxLeft) maxLeft = c;
        }

        var minRight = float.MaxValue;
        for (var t = start + leftLen; t < start + length; t++)
        {
            var c = GetCoord(_indexBuffer![t], dim);
            if (c < minRight) minRight = c;
        }

        var splitValue = maxLeft <= minRight ? (maxLeft + minRight) * 0.5f : maxLeft;

        _nodesBuilt++;
        if (_nodesBuilt % ProgressEveryNodes == 0)
            ProgressCallback?.Invoke(_nodesBuilt, _leavesBuilt);

        var left = Build(start, leftLen);
        var right = Build(start + leftLen, length - leftLen);

        return new KdTreeNodeEntity
        {
            SplitDim = (byte)dim,
            SplitValue = splitValue,
            Left = left,
            Right = right,
        };
    }

    private int SelectSplitDimension(int start, int length)
    {
        var bestDim = 0;
        var bestSpread = -1f;
        for (var d = 0; d < Dimensions; d++)
        {
            var minV = float.MaxValue;
            var maxV = float.MinValue;
            for (var t = 0; t < length; t++)
            {
                var v = GetCoord(_indexBuffer![start + t], d);
                if (v < minV) minV = v;
                if (v > maxV) maxV = v;
            }

            var spread = maxV - minV;
            if (spread > bestSpread)
            {
                bestSpread = spread;
                bestDim = d;
            }
        }

        return bestDim;
    }

    private float GetCoord(int pointIndex, int dim) => _dataSource.GetVectorSpan(pointIndex)[dim];

    private int CompareIndicesByDim(int ia, int ib, int dim)
    {
        var va = GetCoord(ia, dim);
        var vb = GetCoord(ib, dim);
        var c = va.CompareTo(vb);
        return c != 0 ? c : ia.CompareTo(ib);
    }

    private void Swap(int i, int j) =>
        (_indexBuffer![i], _indexBuffer![j]) = (_indexBuffer![j], _indexBuffer![i]);

    /// <summary>Coloca os <c>(mid - from + 1)</c> menores elementos de <c>[from, toExclusive)</c> em <c>[from, mid]</c> por <c>CompareIndicesByDim</c>.</summary>
    private void NthElement(int from, int toExclusive, int mid, int dim)
    {
        for (;;)
        {
            if (toExclusive - from <= 1)
                return;

            var pivotIdx = PickPivotIndex(from, toExclusive, dim);
            pivotIdx = PartitionByDim(from, toExclusive, pivotIdx, dim);

            if (mid < pivotIdx)
                toExclusive = pivotIdx;
            else if (mid > pivotIdx)
                from = pivotIdx + 1;
            else
                return;
        }
    }

    private int PickPivotIndex(int from, int toExclusive, int dim)
    {
        var a = from;
        var b = from + (toExclusive - from - 1) / 2;
        var c = toExclusive - 1;
        if (CompareIndicesByDim(_indexBuffer![a], _indexBuffer![b], dim) > 0)
            Swap(a, b);
        if (CompareIndicesByDim(_indexBuffer![a], _indexBuffer![c], dim) > 0)
            Swap(a, c);
        if (CompareIndicesByDim(_indexBuffer![b], _indexBuffer![c], dim) > 0)
            Swap(b, c);
        return b;
    }

    /// <summary>Partição Lomuto em torno do pivô (índice <c>pivotIdx</c>).</summary>
    private int PartitionByDim(int from, int toExclusive, int pivotIdx, int dim)
    {
        Swap(pivotIdx, toExclusive - 1);
        var pivotId = _indexBuffer![toExclusive - 1];
        var store = from;
        for (var i = from; i < toExclusive - 1; i++)
        {
            if (CompareIndicesByDim(_indexBuffer![i], pivotId, dim) <= 0)
            {
                Swap(i, store);
                store++;
            }
        }

        Swap(store, toExclusive - 1);
        return store;
    }

    [ThreadStatic] private static KnnPriorityQueueEntity? _queueCache;

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

        SearchNode(_root, query, queue);

        neighborCount = queue.Count;
        var fraud = 0;
        for (var i = 0; i < queue.Count; i++)
        {
            if (_dataSource.GetLabel(queue.GetIndex(i)))
                fraud++;
        }

        return fraud;
    }

    private void SearchNode(KdTreeNodeEntity node, ReadOnlySpan<float> query, KnnPriorityQueueEntity queue)
    {
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
                var distSq = VectorMath14.DistanceSquared(_dataSource.GetVectorSpan(idx), query);
                queue.TryInsert(idx, distSq);
            }

            return;
        }

        var dim = node.SplitDim;
        var sv = node.SplitValue;
        var qv = query[dim];
        var diff = qv - sv;

        if (diff < 0)
        {
            SearchNode(node.Left!, query, queue);
            var planeDistSq = diff * diff;
            if (!queue.IsFull || planeDistSq < queue.WorstDistSquared)
                SearchNode(node.Right!, query, queue);
        }
        else
        {
            SearchNode(node.Right!, query, queue);
            var planeDistSq = diff * diff;
            if (!queue.IsFull || planeDistSq < queue.WorstDistSquared)
                SearchNode(node.Left!, query, queue);
        }
    }
}
