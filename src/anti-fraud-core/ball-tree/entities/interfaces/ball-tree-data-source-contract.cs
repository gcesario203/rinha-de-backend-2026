namespace AntiFraud.Core.BallTree.Entities;

using AntiFraud.Core.NeighborhoodClassifier.ValueObjects;

public interface IBallTreeDataSource
{
    int Count { get; }
    ReadOnlySpan<float> GetVectorSpan(int index);
    bool GetLabel(int index);
}