using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.FishingLocations.Commands;
using FishingLogBook.Application.FishingLocations.Errors;
using FishingLogBook.Application.FishingLocations.Queries;
using FishingLogBook.Shared.Dtos;
using MediatR;

namespace FishingLogBook.Api.Endpoints;

public static class FishingLocationEndpoints
{
    public static IEndpointRouteBuilder MapFishingLocationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/profiles/me/fishing-locations", GetLocationsAsync)
            .WithName("GetFishingLocations")
            .WithTags("FishingLocations")
            .RequireAuthorization()
            .Produces<FishingLocationPreferencesDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapPut("/api/profiles/me/fishing-locations", UpdateLocationsAsync)
            .WithName("UpdateFishingLocations")
            .WithTags("FishingLocations")
            .RequireAuthorization()
            .Produces<FishingLocationPreferencesDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> GetLocationsAsync(
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new GetFishingLocationPreferencesQuery { UserId = currentUser.UserId },
            cancellationToken);
        return ToDataResult(response, response.Locations);
    }

    private static async Task<IResult> UpdateLocationsAsync(
        UpdateFishingLocationPreferencesDto locations,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new UpdateFishingLocationPreferencesCommand
            {
                UserId = currentUser.UserId,
                Locations = locations
            },
            cancellationToken);
        return ToDataResult(response, response.Locations);
    }

    private static IResult ToDataResult<TResponse, TData>(TResponse response, TData? data)
        where TResponse : ValidatedResponse
    {
        if (response.ValidationErrors is { Count: > 0 } || response.Error is DuplicateFishingLocationError)
        {
            return Results.BadRequest(response);
        }

        if (response.IsFailure)
        {
            return Results.Problem(
                title: response.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(data);
    }
}
