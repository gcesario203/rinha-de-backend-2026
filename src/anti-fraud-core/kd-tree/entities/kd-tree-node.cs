namespace AntiFraud.Core.KdTree.Entities;

/// <summary>Nó de KD-tree: folha com índices ou interno com hiperplano ortogonal a um eixo.</summary>
public sealed class KdTreeNodeEntity
{
    public KdTreeNodeEntity? Left { get; set; }
    public KdTreeNodeEntity? Right { get; set; }

    /// <summary>Presente só em folhas.</summary>
    public int[]? PointIndices { get; set; }

    public bool IsLeaf => Left is null;

    public byte SplitDim { get; set; }
    public float SplitValue { get; set; }
}
