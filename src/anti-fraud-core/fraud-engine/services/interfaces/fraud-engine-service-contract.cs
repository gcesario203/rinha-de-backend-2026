
using AntiFraud.Core.FraudEngine.DataTransferObjects;

namespace AntiFraud.Core.FraudEngine.Services;

public interface IFraudEngine
{
    FraudAnalysisResult Analyze(ReadOnlySpan<float> vectorizedTransaction, int k = 5, float threshold = 0.6f);
}
