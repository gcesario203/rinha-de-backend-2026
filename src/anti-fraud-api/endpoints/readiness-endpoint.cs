using AntiFraud.Application.Shared.ValueObjects;
using AntiFraud.Core.Readiness.DataTransferObjects;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AntiFraud.API.Endpoints;

public static class ReadinessEndpoint
{
    public static void MapReadinessEndpoint(this WebApplication app)
    {
        app.MapGet("/ready", static Results<Ok<ReadinessResponse>, StatusCodeHttpResult> (
            DatasetReadinessState state) =>
        {
            return state.IsReady
                ? TypedResults.Ok(new ReadinessResponse("ready"))
                : TypedResults.StatusCode(503);
        });
    }
}
