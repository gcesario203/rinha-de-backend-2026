using AntiFraud.Application.FraudScore.Services;
using AntiFraud.Application.Shared.ValueObjects;
using AntiFraud.Core.FraudEngine.DataTransferObjects;
using AntiFraud.Core.Transaction.DataTransferObjects;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AntiFraud.API.Endpoints;

public static class FraudScoreEndpoint
{
    public static void MapFraudScoreEndpoint(this WebApplication app)
    {
        app.MapPost("/fraud-score", static Results<Ok<FraudAnalysisResult>, StatusCodeHttpResult> (
            TransactionRequest request,
            IFraudScoreRestService fraudScoreService,
            DatasetReadinessState state) =>
        {
            if (!state.IsReady)
                return TypedResults.StatusCode(503);

            var result = fraudScoreService.AnalyzeScore(request);
            return TypedResults.Ok(result);
        });
    }
}
