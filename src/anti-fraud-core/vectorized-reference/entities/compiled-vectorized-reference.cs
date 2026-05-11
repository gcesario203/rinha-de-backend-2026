namespace AntiFraud.Core.VectorizedReference.Entities;

using AntiFraud.Core.BallTree.Entities;

public sealed class CompiledVectorizedDataset : IBallTreeDataSource
{
    private const int Dimensions = 14;

    public float[] Vectors { get; }
    public bool[] Labels { get; }
    public int Count { get; }

    public CompiledVectorizedDataset(int count)
    {
        if (count <= 0)
            throw new ArgumentException("Dataset count must be greater than zero.", nameof(count));

        Count = count;
        Vectors = new float[count * Dimensions];
        Labels = new bool[count];
    }

    public void SetEntry(int index, IReadOnlyList<float> vector, bool isFraud)
    {
        if (index < 0 || index >= Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        Labels[index] = isFraud;

        var offset = index * Dimensions;
        for (int i = 0; i < Dimensions; i++)
            Vectors[offset + i] = vector[i];
    }

    public (float[] Vector, bool IsFraud) GetEntry(int index)
    {
        if (index < 0 || index >= Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        var vector = new float[Dimensions];
        var offset = index * Dimensions;

        for (int i = 0; i < Dimensions; i++)
            vector[i] = Vectors[offset + i];

        return (vector, Labels[index]);
    }

    public ReadOnlySpan<float> GetVectorSpan(int index)
        => Vectors.AsSpan(index * Dimensions, Dimensions);

    public bool GetLabel(int index)
        => Labels[index];
}