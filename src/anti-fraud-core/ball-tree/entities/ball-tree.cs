namespace AntiFraud.Core.BallTree.Entities;

using AntiFraud.Core.NeighborhoodClassifier.Entities;
using AntiFraud.Core.Shared.Utils;
using AntiFraud.Core.VectorizedReference.Entities;

public sealed class BallTreeEntity
{
    private const int Dimensions = 14;
    /// <summary>Frequência (em nós internos construídos) com que <see cref="ProgressCallback"/> é chamado.</summary>
    private const int ProgressEveryNodes = 500;

    private readonly IBallTreeDataSource _dataSource;
    private readonly int _leafSize;
    /// <summary>Só preenchido durante <see cref="Build"/> in-place; <see langword="null"/> quando carregado do cache.</summary>
    private readonly int[]? _indexBuffer;
    private readonly BallTreeNodeEntity _root;

    /// <summary>Callback opcional (totalNodos, totalLeaves) para acompanhar progresso do build.</summary>
    public Action<int, int>? ProgressCallback { get; set; }

    private int _nodesBuilt;
    private int _leavesBuilt;

    /// <summary>Callback opcional chamado UMA vez por pass em cada nível da recursão (apenas os
    /// primeiros <c>depth ≤ 3</c>). Permite ver onde o tempo de build vai sem poluir o log.</summary>
    public Action<int, string, long>? PhaseCallback { get; set; }

    public BallTreeEntity(IBallTreeDataSource dataSource, int leafSize = VectorDatasetConstants.BallTreeLeafSize)
    {
        _dataSource = dataSource;
        _leafSize = leafSize;

        var n = dataSource.Count;
        _indexBuffer = new int[n];
        for (var i = 0; i < n; i++)
            _indexBuffer[i] = i;

        _root = Build(0, n, depth: 0);
    }

    /// <summary>Árvore restaurada do disco — sem buffer de partição.</summary>
    private BallTreeEntity(IBallTreeDataSource dataSource, BallTreeNodeEntity root)
    {
        _dataSource = dataSource;
        _leafSize = VectorDatasetConstants.BallTreeLeafSize;
        _indexBuffer = null;
        _root = root;
    }

    /// <summary>Carrega árvore pré-serializada (header + preorder). Valida leafSize e tamanho do <c>references.bin</c>.</summary>
    public static BallTreeEntity LoadFromCache(ReadOnlySpan<byte> fileBytes, IBallTreeDataSource dataSource, int expectedLeafSize, long referencesBinLength)
    {
        if (!BallTreeBinary.TryValidateHeader(fileBytes.Slice(0, BallTreeBinary.HeaderSize), expectedLeafSize, referencesBinLength, out _))
            throw new InvalidDataException("Ball-tree cache: header inválido ou incompatível com o dataset.");

        var offset = BallTreeBinary.HeaderSize;
        var root = BallTreeSerialization.ReadTree(fileBytes, ref offset);
        if (offset != fileBytes.Length)
            throw new InvalidDataException($"Ball-tree cache: esperado EOF em {offset}, ficheiro tem {fileBytes.Length} bytes.");

        return new BallTreeEntity(dataSource, root);
    }

    /// <summary>Persiste header + árvore para escrita atómica no disco.</summary>
    public void WriteFullCache(Stream stream, int leafSize, long referencesBinLength)
    {
        Span<byte> header = stackalloc byte[BallTreeBinary.HeaderSize];
        BallTreeBinary.WriteHeader(header, leafSize, referencesBinLength);
        stream.Write(header);
        BallTreeSerialization.WriteTree(stream, _root);
    }

    #region Build

    private BallTreeNodeEntity Build(int start, int length, int depth)
    {
        var node = new BallTreeNodeEntity();

        if (length <= _leafSize)
        {
            FillCentroidAndRadius(start, length, node);

            var leaf = new int[length];
            Array.Copy(_indexBuffer!, start, leaf, 0, length);
            node.PointIndices = leaf;

            _leavesBuilt++;
            return node;
        }

        var traceTop = depth <= 3 && PhaseCallback is not null;
        var sw = traceTop ? System.Diagnostics.Stopwatch.StartNew() : null;

        // Pass 1: centróide + pivot A (farthest de _indexBuffer[start]).
        var pivotA = ComputeCentroidAndPickPivotA(start, length, node.Centroid);
        if (traceTop) PhaseCallback!.Invoke(depth, $"pass1 centroid+pivotA len={length}", sw!.ElapsedMilliseconds);

        // Pass 2: raio (em torno do centróide) + pivot B (farthest de pivotA).
        if (traceTop) sw!.Restart();
        var va = _dataSource.GetVectorSpan(pivotA);
        var (radius, pivotB) = ComputeRadiusAndPickPivotB(start, length, node.Centroid, va);
        node.Radius = radius;
        if (traceTop) PhaseCallback!.Invoke(depth, $"pass2 radius+pivotB len={length}", sw!.ElapsedMilliseconds);

        // Pass 3: partição via hiperplano (1 produto interno por ponto, sem dist²).
        if (traceTop) sw!.Restart();
        var vb = _dataSource.GetVectorSpan(pivotB);
        var leftLen = PartitionByHyperplane(start, length, va, vb);
        if (traceTop) PhaseCallback!.Invoke(depth, $"pass3 partition leftLen={leftLen}", sw!.ElapsedMilliseconds);

        if (leftLen == 0)
        {
            Swap(start, start + length - 1);
            leftLen = 1;
        }
        else if (leftLen == length)
        {
            Swap(start + length - 2, start + length - 1);
            leftLen = length - 1;
        }

        _nodesBuilt++;
        if (_nodesBuilt % ProgressEveryNodes == 0)
            ProgressCallback?.Invoke(_nodesBuilt, _leavesBuilt);

        node.Left = Build(start, leftLen, depth + 1);
        node.Right = Build(start + leftLen, length - leftLen, depth + 1);
        return node;
    }

    /// <summary>Folha: centróide e raio em duas passadas curtas (length ≤ leafSize).</summary>
    private void FillCentroidAndRadius(int start, int length, BallTreeNodeEntity node)
    {
        Array.Clear(node.Centroid, 0, Dimensions);

        for (var t = 0; t < length; t++)
        {
            var span = _dataSource.GetVectorSpan(_indexBuffer![start + t]);
            for (var d = 0; d < Dimensions; d++)
                node.Centroid[d] += span[d];
        }

        var inv = 1f / length;
        for (var d = 0; d < Dimensions; d++)
            node.Centroid[d] *= inv;

        var maxSq = 0f;
        for (var t = 0; t < length; t++)
        {
            var dsq = DistanceSquared(_dataSource.GetVectorSpan(_indexBuffer![start + t]), node.Centroid);
            if (dsq > maxSq) maxSq = dsq;
        }
        node.Radius = MathF.Sqrt(maxSq);
    }

    /// <summary>
    /// Passada 1 (internos): soma o centróide e, no mesmo loop, encontra o índice mais distante
    /// do primeiro ponto da fatia (heurística de pivot A do diâmetro).
    /// </summary>
    private int ComputeCentroidAndPickPivotA(int start, int length, float[] centroid)
    {
        Array.Clear(centroid, 0, Dimensions);

        var refSpan = _dataSource.GetVectorSpan(_indexBuffer![start]);
        var pivotA = _indexBuffer![start];
        var maxSq = 0f;

        for (var t = 0; t < length; t++)
        {
            var idx = _indexBuffer![start + t];
            var span = _dataSource.GetVectorSpan(idx);

            for (var d = 0; d < Dimensions; d++)
                centroid[d] += span[d];

            var dsq = DistanceSquared(span, refSpan);
            if (dsq > maxSq)
            {
                maxSq = dsq;
                pivotA = idx;
            }
        }

        var inv = 1f / length;
        for (var d = 0; d < Dimensions; d++)
            centroid[d] *= inv;

        return pivotA;
    }

    /// <summary>
    /// Passada 2 (internos): calcula raio em torno do centróide e, no mesmo loop, escolhe pivot B
    /// (farthest de pivot A). Cada ponto sofre apenas <b>uma</b> leitura de mmap.
    /// </summary>
    private (float Radius, int PivotB) ComputeRadiusAndPickPivotB(
        int start, int length, float[] centroid, ReadOnlySpan<float> va)
    {
        var centSpan = centroid.AsSpan();
        var maxRadiusSq = 0f;
        var maxFromASq = 0f;
        var pivotB = _indexBuffer![start];

        for (var t = 0; t < length; t++)
        {
            var idx = _indexBuffer![start + t];
            var span = _dataSource.GetVectorSpan(idx);

            var dsqCentroid = DistanceSquared(span, centSpan);
            if (dsqCentroid > maxRadiusSq) maxRadiusSq = dsqCentroid;

            var dsqA = DistanceSquared(span, va);
            if (dsqA > maxFromASq)
            {
                maxFromASq = dsqA;
                pivotB = idx;
            }
        }

        return (MathF.Sqrt(maxRadiusSq), pivotB);
    }

    /// <summary>
    /// Partição in-place por hiperplano: ponto vai à esquerda se <c>p · delta ≤ threshold</c>,
    /// equivalente a <c>d²(p, a) ≤ d²(p, b)</c> mas com apenas <b>um</b> produto interno por ponto.
    /// </summary>
    private int PartitionByHyperplane(int start, int length, ReadOnlySpan<float> va, ReadOnlySpan<float> vb)
    {
        Span<float> delta = stackalloc float[Dimensions];
        for (var d = 0; d < Dimensions; d++)
            delta[d] = vb[d] - va[d];

        var threshold = (VectorMath14.NormSquared(vb) - VectorMath14.NormSquared(va)) * 0.5f;

        var i = start;
        var j = start + length - 1;
        while (i <= j)
        {
            while (i <= j)
            {
                var p = _dataSource.GetVectorSpan(_indexBuffer![i]);
                if (VectorMath14.DotProduct(p, delta) <= threshold) i++;
                else break;
            }
            while (i <= j)
            {
                var p = _dataSource.GetVectorSpan(_indexBuffer![j]);
                if (VectorMath14.DotProduct(p, delta) > threshold) j--;
                else break;
            }
            if (i < j)
            {
                Swap(i, j);
                i++;
                j--;
            }
        }

        return i - start;
    }

    private void Swap(int i, int j) =>
        (_indexBuffer![i], _indexBuffer![j]) = (_indexBuffer![j], _indexBuffer![i]);

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
}
