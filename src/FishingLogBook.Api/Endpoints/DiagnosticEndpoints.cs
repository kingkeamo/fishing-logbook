using FishingLogBook.Api.Middleware;
using FishingLogBook.Application.Diagnostics;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Api.Endpoints;

public static class DiagnosticEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/diagnostics/client", UploadAsync)
            .WithName("UploadClientDiagnostics")
            .WithTags("Diagnostics")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static async Task<IResult> UploadAsync(
        ClientDiagnosticBatchDto batch,
        DiagnosticLogService diagnosticLogService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await diagnosticLogService.AcceptAsync(
            batch,
            CorrelationIdMiddleware.GetCorrelationId(httpContext),
            cancellationToken);

        return result.IsValid ? Results.NoContent() : Results.BadRequest();
    }
}
