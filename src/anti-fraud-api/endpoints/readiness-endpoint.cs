using AntiFraud.Application.Shared.ValueObjects;

namespace AntiFraud.API.Endpoints;

public static class ReadinessEndpoint
{
    public static void MapReadinessEndpoint(this WebApplication app)
    {
        app.MapGet("/ready", (DatasetReadinessState state) =>
        {
            return state.IsReady
                ? Results.Ok(new { status = "ready" })
                : Results.StatusCode(503);
        });
    }
}