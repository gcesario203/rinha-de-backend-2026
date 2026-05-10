using AntiFraud.Core.Shared.ValueObjects;
using AntiFraud.Core.Transaction.Entities;
using AntiFraud.Core.MCC.Repository;
using AntiFraud.Core.FraudEngine.Services;
using AntiFraud.Core.FraudEngine.DataTransferObjects;
using AntiFraud.Core.VectorizedReference.Entities;

namespace AntiFraud.Application.Transaction.Services;

public sealed class TransactionService : ITransactionService
{
    private readonly FraudHeuristics _fraudHeuristics;
    private readonly IMCCRepository _mccRepository;
    private readonly IFraudEngine _fraudEngine;

    public TransactionService(
        FraudHeuristics fraudHeuristics,
        IMCCRepository mccRepository,
        IFraudEngine fraudEngine)
    {
        _fraudHeuristics = fraudHeuristics;
        _mccRepository = mccRepository;
        _fraudEngine = fraudEngine;
    }

    public FraudAnalysisResult Analyze(TransactionEntity transaction)
    {
        var mccAverageAmount = _mccRepository.GetAverageAmountByMCC(transaction.Merchant.Mcc);
        Span<float> vector = stackalloc float[VectorDatasetConstants.Dimensions];
        transaction.Vectorize(_fraudHeuristics, mccAverageAmount, vector);
        return _fraudEngine.Analyze(vector);
    }
}
