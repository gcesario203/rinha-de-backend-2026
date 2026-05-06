using System.Text.Json.Serialization;

namespace AntiFraud.Core.Shared.ValueObjects;
public record FraudHeuristics(
    [property: JsonPropertyName("max_amount")] float MaxAmount,
    [property: JsonPropertyName("max_installments")] int MaxInstallments,
    [property: JsonPropertyName("amount_vs_avg_ratio")] float AmountVsAvgRatio,
    [property: JsonPropertyName("max_minutes")] int MaxMinutes,
    [property: JsonPropertyName("max_km")] float MaxKm,
    [property: JsonPropertyName("max_tx_count_24h")] int MaxTxCount24h,
    [property: JsonPropertyName("max_merchant_avg_amount")] float MaxMerchantAvgAmount
);