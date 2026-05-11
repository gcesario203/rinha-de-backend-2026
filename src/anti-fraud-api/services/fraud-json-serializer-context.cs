using System.Text.Json.Serialization;
using AntiFraud.Core.FraudEngine.DataTransferObjects;
using AntiFraud.Core.Transaction.DataTransferObjects;

namespace AntiFraud.API.Services;

[JsonSerializable(typeof(TransactionRequest))]
[JsonSerializable(typeof(TransactionInfo))]
[JsonSerializable(typeof(CustomerInfo))]
[JsonSerializable(typeof(MerchantInfo))]
[JsonSerializable(typeof(TerminalInfo))]
[JsonSerializable(typeof(LastTransactionInfo))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(FraudAnalysisResult))]
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Default,
    PropertyNameCaseInsensitive = false)]
public partial class FraudJsonSerializerContext : JsonSerializerContext
{
}
