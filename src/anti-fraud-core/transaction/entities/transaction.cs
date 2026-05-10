// TransactionEntity.cs
using AntiFraud.Core.Shared.Utils;
using AntiFraud.Core.Shared.ValueObjects;
using AntiFraud.Core.Transaction.ValueObjects;
using AntiFraud.Core.VectorizedReference.Entities;

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

    public void Vectorize(FraudHeuristics fraudHeuristics, float mccAverageAmount, Span<float> destination)
    {
        if (destination.Length != VectorDatasetConstants.Dimensions)
            throw new ArgumentException($"Destination length must be {VectorDatasetConstants.Dimensions}.", nameof(destination));

        // Timestamps do payload são UTC (doc); Unspecified vindo do JSON é tratado como UTC.
        var requestedAtUtc = ToUtcVectorClock(RequestedAt);

        destination[0] = Utils.Clamp(Payment.Amount / fraudHeuristics.MaxAmount);
        destination[1] = Utils.Clamp(((float)Payment.Installments / (float)fraudHeuristics.MaxInstallments));
        destination[2] = Utils.Clamp((Payment.Amount / Customer.AvgAmount) / fraudHeuristics.AmountVsAvgRatio);
        destination[3] = (requestedAtUtc.Hour / 23.0f).VectorizeRound();
        // Doc: dia_da_semana com seg=0 … dom=6; .NET DayOfWeek: dom=0 … sáb=6
        var dayOfWeekSpec = ((int)requestedAtUtc.DayOfWeek + 6) % 7;
        destination[4] = ((float)dayOfWeekSpec / 6.0f).VectorizeRound();
        if (LastTransaction is null)
        {
            destination[5] = -1.0f;
            destination[6] = -1.0f;
        }
        else
        {
            var lastUtc = ToUtcVectorClock(LastTransaction.Timestamp);
            var minutesSinceLast = (float)(requestedAtUtc - lastUtc).TotalMinutes;
            if (minutesSinceLast < 0f)
                minutesSinceLast = 0f;

            destination[5] = Utils.Clamp(minutesSinceLast / fraudHeuristics.MaxMinutes);
            destination[6] = Utils.Clamp(LastTransaction.KmFromCurrent / fraudHeuristics.MaxKm);
        }
        destination[7] = Utils.Clamp(Terminal.KmFromHome / fraudHeuristics.MaxKm);
        destination[8] = Utils.Clamp((float)Customer.TxCount24h / (float)fraudHeuristics.MaxTxCount24h);
        destination[9] = Terminal.IsOnline ? 1.0f : 0.0f;
        destination[10] = Terminal.CardPresent ? 1.0f : 0.0f;
        destination[11] = Customer.KnownMerchants.Contains(Merchant.Id) ? 0.0f : 1.0f;
        destination[12] = mccAverageAmount;
        destination[13] = Utils.Clamp(Merchant.AvgAmount / fraudHeuristics.MaxMerchantAvgAmount);
    }

    private static DateTime ToUtcVectorClock(DateTime dt) =>
        dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
        };

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