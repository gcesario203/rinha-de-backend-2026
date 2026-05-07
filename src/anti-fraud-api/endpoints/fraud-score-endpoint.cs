using AntiFraud.Application.FraudScore.Services;
using AntiFraud.Application.Shared.ValueObjects;
using AntiFraud.Core.Transaction.DataTransferObjects;
using AntiFraud.Application.Presentation.Rest.DataTransferObjects;
using System.Net;

namespace AntiFraud.API.Endpoints;

public static class FraudScoreEndpoint
{
    public static void MapFraudScoreEndpoint(this WebApplication app)
    {
        app.MapPost("/fraud-score", async (
            TransactionRequest request,
            IFraudScoreRestService fraudScoreService,
            DatasetReadinessState state,
            ILogger<IFraudScoreRestService> logger) =>
        {
            try
            {
                if (!state.IsReady)
                    return Results.StatusCode(503);

                var result = await fraudScoreService.AnalyzeScoreAsync(
                    new RestRequest<TransactionRequest>(request));

                return result.StatusCode == (int)HttpStatusCode.OK
                    ? Results.Ok(result.Data)
                    : Results.StatusCode(result.StatusCode);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[FraudScore] Unhandled exception while analyzing transaction.");
                return Results.Problem();
            }
        });
    }
}