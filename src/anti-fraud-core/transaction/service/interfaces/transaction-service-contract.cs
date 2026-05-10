
using AntiFraud.Core.FraudEngine.DataTransferObjects;
using AntiFraud.Core.Transaction.Entities;

namespace AntiFraud.Core.FraudEngine.Services;

public interface ITransactionService
{
    FraudAnalysisResult Analyze(TransactionEntity transaction);
}