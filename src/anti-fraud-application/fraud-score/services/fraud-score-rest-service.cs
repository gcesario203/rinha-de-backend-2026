namespace AntiFraud.Application.FraudScore.Services;

using AntiFraud.Core.FraudEngine.DataTransferObjects;
using AntiFraud.Core.FraudEngine.Services;
using AntiFraud.Core.Transaction.DataTransferObjects;
using AntiFraud.Core.Transaction.Mappers;

public sealed class FraudScoreRestService : IFraudScoreRestService
{
    private readonly ITransactionService _transactionService;

    public FraudScoreRestService(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    public FraudAnalysisResult AnalyzeScore(TransactionRequest request)
    {
        var transactionEntity = TransactionMapper.MapToEntity(request);
        return _transactionService.Analyze(transactionEntity);
    }
}
