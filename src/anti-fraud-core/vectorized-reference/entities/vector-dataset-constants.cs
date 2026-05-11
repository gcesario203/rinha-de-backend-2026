namespace AntiFraud.Core.VectorizedReference.Entities;

public static class VectorDatasetConstants
{
    /// <summary>Number of reference vectors in <c>references.json.gz</c>.</summary>
    public const int ReferenceCount = 3_000_000;

    public const int Dimensions = 14;

    /// <summary>Folhas da ball-tree (build + cache devem usar o mesmo valor).</summary>
    public const int BallTreeLeafSize = 60;

    public const int HeaderSize = 32;
}
