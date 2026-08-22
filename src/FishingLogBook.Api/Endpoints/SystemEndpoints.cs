using FishingLogBook.Api.Configuration;
using FishingLogBook.Application.SystemStatus;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Api.Endpoints;

public static class SystemEndpoints
{
    private const string HealthyStatus = "Healthy";

    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", () => Results.Ok(new HealthDto(HealthyStatus)))
            .WithName("GetHealth")
            .WithTags("System")
            .Produces<HealthDto>(StatusCodes.Status200OK);

        endpoints.MapGet("/api/system/database", GetDatabaseStatusAsync)
            .WithName("GetDatabaseStatus")
            .WithTags("System")
            .Produces<DatabaseTestDto>(StatusCodes.Status200OK)
            .Produces<DatabaseTestDto>(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet("/api/system/build", (BuildMetadataConfig buildMetadata) =>
                Results.Ok(buildMetadata.ToDto()))
            .WithName("GetBuildMetadata")
            .WithTags("System")
            .Produces<BuildMetadataDto>(StatusCodes.Status200OK);

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
                new DatabaseTestDto("Unhealthy", null),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
