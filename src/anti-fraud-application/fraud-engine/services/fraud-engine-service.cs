namespace AntiFraud.Application.FraudEngine.Services;

using AntiFraud.Core.FraudEngine.Services;
using AntiFraud.Core.NeighborhoodClassifier.Services;
using AntiFraud.Core.FraudEngine.DataTransferObjects;

public sealed class FraudInferenceEngine : IFraudEngine
{
    private readonly INeighborhoodClassifier _neighborhoodClassifier;

    public FraudInferenceEngine(INeighborhoodClassifier neighborhoodClassifier)
    {
        _neighborhoodClassifier = neighborhoodClassifier;
    }

    public FraudAnalysisResult Analyze(ReadOnlySpan<float> vectorizedTransaction, int k = 5, float threshold = 0.6f)
    {
        var (fraudCount, total) = _neighborhoodClassifier.GetNeighborVote(vectorizedTransaction, k);

        if (total == 0)
            return new FraudAnalysisResult(true, 0f);

        var score = (float)fraudCount / total;
        return new FraudAnalysisResult(score < threshold, score);
    }
}
