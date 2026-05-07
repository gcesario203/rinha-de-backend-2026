namespace AntiFraud.Application.FraudEngine.Services;

using AntiFraud.Core.FraudEngine.Services;
using AntiFraud.Core.NeighborhoodClassifier.Services;
using AntiFraud.Core.FraudEngine.DataTransferObjects;
public class FraudInferenceEngine : IFraudEngine
{
    private readonly INeighborhoodClassifier _neighborhoodClassifier;

    public FraudInferenceEngine(INeighborhoodClassifier neighborhoodClassifier)
    {
        _neighborhoodClassifier = neighborhoodClassifier;
    }

    public Task<FraudAnalysisResult> Analyze(float[] vectorizedTransaction, int k = 5, float threshold = 0.6f)
    {
        var candidates = _neighborhoodClassifier.ClassifyByNeighborhood(vectorizedTransaction, k).ToList();

        if (candidates.Count == 0)
        {
            // Sem vizinhos: fraud_score 0 → aprovado (mesma regra score < threshold)
            return Task.FromResult(new FraudAnalysisResult(true, 0f));
        }

        int fraudCount = candidates.Count(c => c.IsFraud);

        float score = (float)fraudCount / candidates.Count;

        // approved = fraud_score < threshold (especificação Rinha 2026)
        return Task.FromResult(new FraudAnalysisResult(score < threshold, score));
    }
}