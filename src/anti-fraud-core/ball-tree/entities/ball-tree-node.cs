namespace AntiFraud.Core.BallTree.Entities;

using AntiFraud.Core.NeighborhoodClassifier.ValueObjects;

public sealed class BallTreeNodeEntity
{
    private const int Dimensions = 14;

    public float[] Centroid { get; }
    public float Radius { get; set; }

    public BallTreeNodeEntity? Left { get; set; }
    public BallTreeNodeEntity? Right { get; set; }

    public int[]? PointIndices { get; set; }

    public bool IsLeaf => Left is null && Right is null;

    public BallTreeNodeEntity()
    {
        Centroid = new float[Dimensions];
    }
}