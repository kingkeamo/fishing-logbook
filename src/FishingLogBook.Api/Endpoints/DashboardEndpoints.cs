using FishingLogBook.Application.Common.Contracts.Services;
using FishingLogBook.Application.Dashboard.Queries;
using FishingLogBook.Shared.Dtos;
using MediatR;

namespace FishingLogBook.Api.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/dashboard", GetAsync)
            .WithName("GetDashboard")
            .WithTags("Dashboard")
            .RequireAuthorization()
            .Produces<DashboardDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status503ServiceUnavailable);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        [AsParameters] DashboardFilterDto filter,
        IMediator mediator,
        ICurrentUser currentUser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store, private";
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(new GetDashboardQuery
        {
            UserId = currentUser.UserId,
            FromInclusive = filter.FromInclusive?.ToUniversalTime(),
            ToExclusive = filter.ToExclusive?.ToUniversalTime(),
            Species = filter.Species,
            Method = filter.Method,
            Venue = filter.Venue
        }, cancellationToken);
        if (response.ValidationErrors is { Count: > 0 })
        {
            return Results.BadRequest(response);
        }

        return response.IsFailure
            ? Results.Problem(title: response.ErrorMessage, statusCode: StatusCodes.Status503ServiceUnavailable)
            : Results.Ok(response.Dashboard);
    }
}
