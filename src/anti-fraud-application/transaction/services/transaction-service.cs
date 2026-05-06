
using AntiFraud.Core.Shared.Utils;
using AntiFraud.Core.Shared.ValueObjects;
using AntiFraud.Core.Transaction.Entities;
using AntiFraud.Core.MCC.Repository;
using AntiFraud.Core.FraudEngine.Services;
using AntiFraud.Core.FraudEngine.DataTransferObjects;
using AntiFraud.Core.VectorizedReference.Repository;
using AntiFraud.Core.NeighborhoodClassifier.Services;

namespace AntiFraud.Application.Transaction.Services;

public class TransactionService : ITransactionService
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

    public async Task<FraudAnalysisResult> Analyze(TransactionEntity transaction)
    {
        var mccAverageAmount = await _mccRepository.GetAverageAmountByMCC(transaction.Merchant.Mcc);
        var vector = await transaction.Vectorize(_fraudHeuristics, mccAverageAmount);

        return await _fraudEngine.Analyze(vector);
    }
}