namespace AntiFraud.Core.NeighborhoodClassifier.Services;

using AntiFraud.Core.NeighborhoodClassifier.ValueObjects;

public interface INeighborhoodClassifier
{
    void Initialize();
    
    IEnumerable<KnnCandidate> ClassifyByNeighborhood(float[] queryVector, int k);
}