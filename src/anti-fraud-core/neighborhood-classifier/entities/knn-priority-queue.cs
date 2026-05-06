namespace AntiFraud.Core.NeighborhoodClassifier.Entities;

using AntiFraud.Core.NeighborhoodClassifier.ValueObjects;

public sealed class KnnPriorityQueueEntity
{
    private readonly SortedList<float, int> _items;
    private readonly int _capacity;

    public int Count => _items.Count;
    public bool IsFull => _items.Count >= _capacity;

    public float WorstDistance => IsFull ? _items.Keys[^1] : float.MaxValue;

    public KnnPriorityQueueEntity(int capacity)
    {
        _capacity = capacity;
        _items = new SortedList<float, int>(Comparer<float>.Default);
    }

    public void TryInsert(int index, float distance)
    {
        if (IsFull && distance >= WorstDistance)
            return;

        var key = distance;
        while (_items.ContainsKey(key))
            key = BitIncrement(key);

        _items.Add(key, index);

        if (_items.Count > _capacity)
            _items.RemoveAt(_items.Count - 1);
    }

    public IEnumerable<(int Index, float Distance)> GetResults()
        => _items.Select(kv => (kv.Value, kv.Key));

    private static float BitIncrement(float value)
    {
        int bits = BitConverter.SingleToInt32Bits(value);
        return BitConverter.Int32BitsToSingle(bits + 1);
    }
}