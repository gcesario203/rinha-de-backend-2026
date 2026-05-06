using System.Text.Json.Serialization;

namespace AntiFraud.Core.Transaction.DataTransferObjects;

public record TransactionRequest(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("transaction")] TransactionInfo Transaction,
    [property: JsonPropertyName("customer")] CustomerInfo Customer,
    [property: JsonPropertyName("merchant")] MerchantInfo Merchant,
    [property: JsonPropertyName("terminal")] TerminalInfo Terminal,
    [property: JsonPropertyName("last_transaction")] LastTransactionInfo LastTransaction
);

public record TransactionInfo(
    [property: JsonPropertyName("amount")] float Amount,
    [property: JsonPropertyName("installments")] int Installments,
    [property: JsonPropertyName("requested_at")] DateTime RequestedAt
);

public record CustomerInfo(
    [property: JsonPropertyName("avg_amount")] float AvgAmount,
    [property: JsonPropertyName("tx_count_24h")] int TxCount24h,
    [property: JsonPropertyName("known_merchants")] List<string> KnownMerchants
);

public record MerchantInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("mcc")] string Mcc,
    [property: JsonPropertyName("avg_amount")] float AvgAmount
);

public record TerminalInfo(
    [property: JsonPropertyName("is_online")] bool IsOnline,
    [property: JsonPropertyName("card_present")] bool CardPresent,
    [property: JsonPropertyName("km_from_home")] float KmFromHome
);

public record LastTransactionInfo(
    [property: JsonPropertyName("timestamp")] DateTime Timestamp,
    [property: JsonPropertyName("km_from_current")] float KmFromCurrent
);