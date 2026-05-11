namespace AntiFraud.Core.NeighborhoodClassifier.Entities;

/// <summary>
/// Mantém os k vizinhos com maior distância ao quadrado usando buffers fixos (sem SortedList).
/// </summary>
public sealed class KnnPriorityQueueEntity
{
    private readonly int _capacity;
    private readonly int[] _indices;
    private readonly float[] _distSq;
    private int _count;
    private float _worstSq;
    private float _worstSqrt;
    private int _worstSlot;

    public int Count => _count;

    public int Capacity => _capacity;

    public bool IsFull => _count == _capacity;

    /// <summary>
    /// Maior dist² entre os k vizinhos quando cheio; caso contrário +∞ (não podar pela bola).
    /// </summary>
    public float WorstDistSquared => _count < _capacity ? float.MaxValue : _worstSq;

    /// <summary>Raiz quadrada de <see cref="WorstDistSquared"/>, cacheada para evitar sqrt no hot path de poda.</summary>
    public float WorstDist => _count < _capacity ? float.MaxValue : _worstSqrt;

    public KnnPriorityQueueEntity(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        _capacity = capacity;
        _indices = new int[capacity];
        _distSq = new float[capacity];
        _worstSq = float.MaxValue;
        _worstSqrt = float.MaxValue;
    }

    /// <summary>Limpa o estado para reuso, sem realocar buffers.</summary>
    public void Reset()
    {
        _count = 0;
        _worstSq = float.MaxValue;
        _worstSqrt = float.MaxValue;
        _worstSlot = 0;
    }

    public void TryInsert(int index, float distSq)
    {
        if (_count < _capacity)
        {
            _indices[_count] = index;
            _distSq[_count] = distSq;
            _count++;
            if (_count == _capacity)
                RecomputeWorst();
            return;
        }

        if (distSq >= _worstSq)
            return;

        _indices[_worstSlot] = index;
        _distSq[_worstSlot] = distSq;
        RecomputeWorst();
    }

    private void RecomputeWorst()
    {
        _worstSq = _distSq[0];
        _worstSlot = 0;
        for (var i = 1; i < _count; i++)
        {
            if (_distSq[i] > _worstSq)
            {
                _worstSq = _distSq[i];
                _worstSlot = i;
            }
        }
        _worstSqrt = MathF.Sqrt(_worstSq);
    }

    public int GetIndex(int i) => _indices[i];

    public float GetDistSq(int i) => _distSq[i];
}
