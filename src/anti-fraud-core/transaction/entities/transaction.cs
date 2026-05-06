// TransactionEntity.cs
using AntiFraud.Core.Shared.Utils;
using AntiFraud.Core.Shared.ValueObjects;
using AntiFraud.Core.Transaction.ValueObjects;

namespace AntiFraud.Core.Transaction.Entities;

public sealed class TransactionEntity
{
    public TransactionId Id { get; private set; }
    public Money Payment { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public CustomerProfile Customer { get; private set; }
    public MerchantProfile Merchant { get; private set; }
    public TerminalContext Terminal { get; private set; }
    public LastTransactionContext? LastTransaction { get; private set; }

    public async Task<float[]> Vectorize(FraudHeuristics fraudHeuristics, float mccAverageAmount)
    {
        var @return = new float[14];

        @return[0] = Utils.Clamp(Payment.Amount / fraudHeuristics.MaxAmount);
        @return[1] = Utils.Clamp(Payment.Installments / fraudHeuristics.MaxInstallments);
        @return[2] = Utils.Clamp((Payment.Amount / Customer.AvgAmount) / fraudHeuristics.AmountVsAvgRatio);
        @return[3] = (RequestedAt.Hour / 23.0f).VectorizeRound();
        @return[4] = ((float)RequestedAt.DayOfWeek / 6.0f).VectorizeRound();
        @return[5] = LastTransaction is null ? -1.0f : Utils.Clamp(LastTransaction.Timestamp.Minute / fraudHeuristics.MaxMinutes);
        @return[6] = LastTransaction is null ? -1.0f : Utils.Clamp(LastTransaction.KmFromCurrent / fraudHeuristics.MaxKm);
        @return[7] = Utils.Clamp(Terminal.KmFromHome / fraudHeuristics.MaxKm);
        @return[8] = Utils.Clamp(Customer.TxCount24h / fraudHeuristics.MaxTxCount24h);
        @return[9] = Terminal.IsOnline ? 1.0f : 0.0f;
        @return[10] = Terminal.CardPresent ? 1.0f : 0.0f;
        @return[11] = Customer.KnownMerchants.Contains(Merchant.Id) ? 0.0f : 1.0f;
        @return[12] = mccAverageAmount;
        @return[13] = Utils.Clamp(Merchant.AvgAmount / fraudHeuristics.MaxMerchantAvgAmount);

        return @return;
    }

    #region Constructors
    internal static TransactionEntity Create(
    TransactionId id,
    Money payment,
    DateTime requestedAt,
    CustomerProfile customer,
    MerchantProfile merchant,
    TerminalContext terminal,
    LastTransactionContext lastTransaction)
    {
        return new TransactionEntity
        {
            Id = id,
            Payment = payment,
            RequestedAt = requestedAt,
            Customer = customer,
            Merchant = merchant,
            Terminal = terminal,
            LastTransaction = lastTransaction
        };
    }
    #endregion
}