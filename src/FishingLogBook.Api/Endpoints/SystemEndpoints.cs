using FishingLogBook.Application.SystemStatus;
using FishingLogBook.Shared.SystemStatus;

namespace FishingLogBook.Api.Endpoints;

public static class SystemEndpoints
{
    private const string HealthyStatus = "Healthy";

    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", () => Results.Ok(new HealthResponse(HealthyStatus)))
            .WithName("GetHealth");

        endpoints.MapGet("/api/system/database", GetDatabaseStatusAsync)
            .WithName("GetDatabaseStatus");

        return endpoints;
    }

    private static async Task<IResult> GetDatabaseStatusAsync(
        SystemStatusService systemStatusService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("SystemDatabaseEndpoint");

        try
        {
            var response = await systemStatusService.GetDatabaseStatusAsync(cancellationToken);

            if (response.Status == HealthyStatus)
            {
                return Results.Ok(response);
            }

            return Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Database connectivity check failed.");
            return Results.Json(
                new DatabaseTestResponse("Unhealthy", null),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
