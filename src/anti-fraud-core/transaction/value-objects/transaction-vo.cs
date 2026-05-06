namespace AntiFraud.Core.Transaction.ValueObjects;

public record TransactionId
{
    public string Value { get; }

    public TransactionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Transaction ID cannot be empty.");

        Value = value;
    }

    public static TransactionId New() => new($"tx-{Guid.NewGuid():N}");
    public override string ToString() => Value;
}

public record Money
{
    public float Amount { get; }
    public int Installments { get; }

    public Money(float amount, int installments)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.");
        if (installments < 1)
            throw new ArgumentException("Installments must be at least 1.");

        Amount = amount;
        Installments = installments;
    }

    public float InstallmentValue => Amount / Installments;
}

public record CustomerProfile
{
    public float AvgAmount { get; }
    public int TxCount24h { get; }
    public IReadOnlyList<string> KnownMerchants { get; }

    public CustomerProfile(float avgAmount, int txCount24h, IEnumerable<string> knownMerchants)
    {
        if (avgAmount < 0)
            throw new ArgumentException("Average amount cannot be negative.");
        if (txCount24h < 0)
            throw new ArgumentException("Transaction count cannot be negative.");

        AvgAmount = avgAmount;
        TxCount24h = txCount24h;
        KnownMerchants = knownMerchants?.ToList().AsReadOnly()
            ?? throw new ArgumentNullException(nameof(knownMerchants));
    }
}

public record MerchantProfile
{
    public string Id { get; }
    public string Mcc { get; }
    public float AvgAmount { get; }

    public MerchantProfile(string id, string mcc, float avgAmount)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Merchant ID cannot be empty.");
        if (string.IsNullOrWhiteSpace(mcc))
            throw new ArgumentException("MCC cannot be empty.");
        if (avgAmount < 0)
            throw new ArgumentException("Average amount cannot be negative.");

        Id = id;
        Mcc = mcc;
        AvgAmount = avgAmount;
    }
}

public record TerminalContext
{
    public bool IsOnline { get; }
    public bool CardPresent { get; }
    public float KmFromHome { get; }

    public TerminalContext(bool isOnline, bool cardPresent, float kmFromHome)
    {
        if (kmFromHome < 0)
            throw new ArgumentException("Distance cannot be negative.");

        IsOnline = isOnline;
        CardPresent = cardPresent;
        KmFromHome = kmFromHome;
    }
}

public record LastTransactionContext
{
    public DateTime Timestamp { get; }
    public float KmFromCurrent { get; }

    public LastTransactionContext(DateTime timestamp, float kmFromCurrent)
    {
        if (kmFromCurrent < 0)
            throw new ArgumentException("Distance cannot be negative.");

        Timestamp = timestamp;
        KmFromCurrent = kmFromCurrent;
    }
}