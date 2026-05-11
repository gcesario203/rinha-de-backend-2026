namespace AntiFraud.Application.FraudScore.Services;

using AntiFraud.Core.FraudEngine.DataTransferObjects;
using AntiFraud.Core.Transaction.DataTransferObjects;

public interface IFraudScoreRestService
{
    FraudAnalysisResult AnalyzeScore(TransactionRequest request);
}
