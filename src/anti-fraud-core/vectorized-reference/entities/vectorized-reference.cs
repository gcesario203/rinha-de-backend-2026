namespace AntiFraud.Core.VectorizedReference.Entities;

public sealed class VectorizedReferenceEntity
{
    public string Label { get; }
    public IReadOnlyList<float> Vector { get; }

    private VectorizedReferenceEntity(bool isFraud, float[] vector)
    {
        Label = isFraud ? "fraud" : "legit";
        Vector = vector.AsReadOnly();
    }

    public bool IsFraud => string.Equals(Label, "fraud", StringComparison.OrdinalIgnoreCase);

    public static VectorizedReferenceEntity Create(bool isFraud, float[] vector)
    {
        ArgumentNullException.ThrowIfNull(vector, nameof(vector));
        return new VectorizedReferenceEntity(isFraud, vector);
    }
}