namespace AntiFraud.Core.VectorizedReference.Entities;

public sealed class CompiledVectorizedDataset
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

    public ReadOnlySpan<float> GetVectorSpan(int index)
        => Vectors.AsSpan(index * Dimensions, Dimensions);
}