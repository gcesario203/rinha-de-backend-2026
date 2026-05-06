
using AntiFraud.Core.FraudEngine.DataTransferObjects;

namespace AntiFraud.Core.FraudEngine.Services;

public interface IFraudEngine
{
    Task<FraudAnalysisResult> Analyze(float[] vectorizedTransaction, int k = 5, float threshold = 0.6f);
}
