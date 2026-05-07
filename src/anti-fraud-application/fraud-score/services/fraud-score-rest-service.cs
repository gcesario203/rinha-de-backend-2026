

namespace AntiFraud.Application.FraudScore.Services;

using AntiFraud.Core.FraudEngine.DataTransferObjects;
using AntiFraud.Application.Presentation.Rest.Exceptions;
using AntiFraud.Application.Presentation.Rest.DataTransferObjects;
using AntiFraud.Core.Transaction.DataTransferObjects;
using AntiFraud.Core.FraudEngine.Services;
using AntiFraud.Core.Transaction.Mappers;

public sealed class FraudScoreRestService : IFraudScoreRestService
{
    private readonly ITransactionService _transactionService;

    public FraudScoreRestService(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    public async Task<RestResponse<FraudAnalysisResult>> AnalyzeScoreAsync(RestRequest<TransactionRequest> request)
    {
        try
        {
            var transactionEntity = TransactionMapper.MapToEntity(request.Data);

            var analysisResult = await _transactionService.Analyze(transactionEntity);

            return new RestResponse<FraudAnalysisResult>(
                Data: analysisResult,
                Message: "Fraud analysis completed successfully.",
                StatusCode: 200
            );
        }
        catch (System.Exception ex)
        {
            return new RestResponse<FraudAnalysisResult>(
                Data: null,
                Message: "An error occurred while analyzing the fraud score.",
                StatusCode: 500,
                Error: new RestPresentationException(ex.Message)
            );
        }
    }
}