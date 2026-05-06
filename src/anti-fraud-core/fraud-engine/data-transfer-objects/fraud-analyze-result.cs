using System.Text.Json.Serialization;
namespace AntiFraud.Core.FraudEngine.DataTransferObjects;

public record FraudAnalysisResult(
    [property: JsonPropertyName("approved")] bool Approved,
    [property: JsonPropertyName("fraud_score")] float FraudScore
);