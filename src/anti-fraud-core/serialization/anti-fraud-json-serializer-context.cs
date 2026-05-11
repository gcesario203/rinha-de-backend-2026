using System.Text.Json.Serialization;

using AntiFraud.Core.FraudEngine.DataTransferObjects;
using AntiFraud.Core.Readiness.DataTransferObjects;
using AntiFraud.Core.Shared.ValueObjects;
using AntiFraud.Core.Transaction.DataTransferObjects;
using AntiFraud.Core.VectorizedReference.Models;

namespace AntiFraud.Core.Serialization;

/// <summary>Resolver único para JSON (hot path + bootstrap + prebuild) — necessário para Native AOT.</summary>
[JsonSerializable(typeof(TransactionRequest))]
[JsonSerializable(typeof(TransactionInfo))]
[JsonSerializable(typeof(CustomerInfo))]
[JsonSerializable(typeof(MerchantInfo))]
[JsonSerializable(typeof(TerminalInfo))]
[JsonSerializable(typeof(LastTransactionInfo))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(FraudAnalysisResult))]
[JsonSerializable(typeof(ReadinessResponse))]
[JsonSerializable(typeof(FraudHeuristics))]
[JsonSerializable(typeof(Dictionary<string, float>), TypeInfoPropertyName = "MccRiskByCode")]
[JsonSerializable(typeof(VectorizedReferenceFileModel))]
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Default,
    PropertyNameCaseInsensitive = false)]
public partial class AntiFraudJsonSerializerContext : JsonSerializerContext
{
}
