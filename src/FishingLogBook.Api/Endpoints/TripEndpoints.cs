using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.Trips.Commands;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Application.Trips.Queries;
using FishingLogBook.Shared.Dtos;
using MediatR;

namespace FishingLogBook.Api.Endpoints;

public static class TripEndpoints
{
    public static IEndpointRouteBuilder MapTripEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/trips", UpsertAsync)
            .WithName("UpsertTrip")
            .WithTags("Trips")
            .RequireAuthorization()
            .Produces<TripDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet("/api/trips", ListMyAsync)
            .WithName("ListMyTrips")
            .WithTags("Trips")
            .RequireAuthorization()
            .Produces<IReadOnlyList<TripViewDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet("/api/trips/{tripId:guid}", GetAsync)
            .WithName("GetTrip")
            .WithTags("Trips")
            .RequireAuthorization()
            .Produces<TripViewDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> UpsertAsync(
        TripDto tripDto,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new UpsertTripCommand
            {
                UserId = currentUser.UserId,
                Trip = tripDto
            },
            cancellationToken);
        if (response.ValidationErrors is { Count: > 0 })
        {
            return Results.BadRequest(response);
        }

        if (response.Error is TripAlreadyActiveError)
        {
            return Results.Json(response, statusCode: StatusCodes.Status409Conflict);
        }

        if (response.Error is TripLocationInvalidError
            or TripLifecycleInvalidError
            or TripOwnershipConflictError)
        {
            return Results.BadRequest(response);
        }

        if (response.IsFailure)
        {
            return Results.Problem(
                title: response.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(response.Trip);
    }

    private static async Task<IResult> ListMyAsync(
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new GetMyTripsQuery { UserId = currentUser.UserId },
            cancellationToken);
        if (response.IsFailure)
        {
            return Results.Problem(
                title: response.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(response.Trips);
    }

    private static async Task<IResult> GetAsync(
        Guid tripId,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new GetTripQuery { TripId = tripId },
            cancellationToken);
        if (response.Error is CurrentUserUnresolvedError)
        {
            return Results.Unauthorized();
        }

        if (response.Error is TripNotFoundError)
        {
            return Results.NotFound();
        }

        if (response.IsFailure)
        {
            return Results.Problem(
                title: response.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(response.Trip);
    }
}
