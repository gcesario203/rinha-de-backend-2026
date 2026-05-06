
using AntiFraud.Core.Shared.Utils;
using AntiFraud.Core.Shared.ValueObjects;
using AntiFraud.Core.Transaction.Entities;
using AntiFraud.Core.MCC.Repository;

namespace AntiFraud.Core.Transaction.Service;

public interface ITransactionService
{
    public Task<float[]> Vectorize(TransactionEntity transaction);
}

public class TransactionService : ITransactionService
{
    private readonly FraudHeuristics _fraudHeuristics;

    private readonly IMCCRepository _mccRepository;

    public TransactionService(FraudHeuristics fraudHeuristics, IMCCRepository mccRepository)
    {
        _fraudHeuristics = fraudHeuristics;
        _mccRepository = mccRepository;
    }

    public async Task<float[]> Vectorize(TransactionEntity transaction)
    {
        var mccAverageAmount = await _mccRepository.GetAverageAmountByMCC(transaction.Merchant.Mcc);
        return await transaction.Vectorize(_fraudHeuristics, mccAverageAmount);
    }
}