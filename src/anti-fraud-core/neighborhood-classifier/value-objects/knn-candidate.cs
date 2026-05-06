namespace AntiFraud.Core.NeighborhoodClassifier.ValueObjects;

public record KnnCandidate(
    int Index,
    bool IsFraud,
    float Distance
);