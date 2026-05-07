
namespace AntiFraud.Application.FraudScore.Services;

using AntiFraud.Core.FraudEngine.DataTransferObjects;
using AntiFraud.Application.Presentation.Rest.Exceptions;
using AntiFraud.Application.Presentation.Rest.DataTransferObjects;
using AntiFraud.Core.Transaction.DataTransferObjects;

public interface IFraudScoreRestService
{
    Task<RestResponse<FraudAnalysisResult>> AnalyzeScoreAsync(RestRequest<TransactionRequest> request);
}