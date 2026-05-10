namespace AntiFraud.Core.NeighborhoodClassifier.Services;

public interface INeighborhoodClassifier
{
    void Initialize();

    /// <summary>Votos dos k vizinhos mais próximos (distância euclidiana exata, mesmo gabarito).</summary>
    (int FraudCount, int Total) GetNeighborVote(ReadOnlySpan<float> queryVector, int k);
}
