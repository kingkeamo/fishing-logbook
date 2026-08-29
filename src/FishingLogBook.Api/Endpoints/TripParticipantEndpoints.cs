using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.Trips.Commands;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Application.Trips.Queries;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Shared.Dtos;
using MediatR;

namespace FishingLogBook.Api.Endpoints;

public static class TripParticipantEndpoints
{
    public static IEndpointRouteBuilder MapTripParticipantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/trips/{tripId:guid}/participants", GetAsync)
            .WithName("GetTripParticipants")
            .WithTags("Trips")
            .RequireAuthorization()
            .Produces<TripParticipantsDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapPost("/api/trips/{tripId:guid}/participants", InviteAsync)
            .WithName("InviteTripParticipant")
            .WithTags("Trips")
            .RequireAuthorization()
            .Produces<TripParticipantsDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapDelete("/api/trips/{tripId:guid}/participants/{participantUserId:guid}", RemoveAsync)
            .WithName("RemoveTripParticipant")
            .WithTags("Trips")
            .RequireAuthorization()
            .Produces<TripParticipantsDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet("/api/trips/invitations", ListInvitationsAsync)
            .WithName("ListMyTripInvitations")
            .WithTags("Trips")
            .RequireAuthorization()
            .Produces<IReadOnlyList<TripInvitationDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapPost("/api/trips/{tripId:guid}/invitation/accept", AcceptAsync)
            .WithName("AcceptTripInvitation")
            .WithTags("Trips")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapPost("/api/trips/{tripId:guid}/invitation/decline", DeclineAsync)
            .WithName("DeclineTripInvitation")
            .WithTags("Trips")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
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
            new GetTripParticipantsQuery { TripId = tripId },
            cancellationToken);
        return ToResult(response, response.Participants);
    }

    private static async Task<IResult> InviteAsync(
        Guid tripId,
        InviteTripParticipantDto request,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new InviteTripParticipantCommand
            {
                TripId = tripId,
                InvitedUserId = request.UserId
            },
            cancellationToken);
        if (response.Error is TripParticipantAlreadyInvitedError)
        {
            return Results.Json(response, statusCode: StatusCodes.Status409Conflict);
        }

        if (response.Error is TripParticipantSelfInviteError or TripParticipantUserNotFoundError)
        {
            return Results.BadRequest(response);
        }

        return ToResult(response, response.Participants);
    }

    private static async Task<IResult> RemoveAsync(
        Guid tripId,
        Guid participantUserId,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new RemoveTripParticipantCommand
            {
                TripId = tripId,
                ParticipantUserId = participantUserId
            },
            cancellationToken);
        if (response.Error is TripParticipantNotFoundError)
        {
            return Results.NotFound();
        }

        return ToResult(response, response.Participants);
    }

    private static async Task<IResult> ListInvitationsAsync(
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(new GetMyTripInvitationsQuery(), cancellationToken);
        if (response.Error is CurrentUserUnresolvedError)
        {
            return Results.Unauthorized();
        }

        if (response.IsFailure)
        {
            return Results.Problem(
                title: response.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(response.Invitations);
    }

    private static Task<IResult> AcceptAsync(
        Guid tripId,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        return RespondAsync(
            tripId,
            TripParticipantStatusEnum.Accepted,
            mediator,
            currentUser,
            cancellationToken);
    }

    private static Task<IResult> DeclineAsync(
        Guid tripId,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        return RespondAsync(
            tripId,
            TripParticipantStatusEnum.Declined,
            mediator,
            currentUser,
            cancellationToken);
    }

    private static async Task<IResult> RespondAsync(
        Guid tripId,
        TripParticipantStatusEnum response,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var result = await mediator.Send(
            new RespondToTripInvitationCommand
            {
                TripId = tripId,
                Response = response
            },
            cancellationToken);
        if (result.ValidationErrors is { Count: > 0 })
        {
            return Results.BadRequest(result);
        }

        if (result.Error is CurrentUserUnresolvedError)
        {
            return Results.Unauthorized();
        }

        if (result.Error is TripInvitationNotFoundError)
        {
            return Results.NotFound();
        }

        if (result.Error is TripParticipantAlreadyRespondedError)
        {
            return Results.Json(result, statusCode: StatusCodes.Status409Conflict);
        }

        if (result.IsFailure)
        {
            return Results.Problem(
                title: result.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.NoContent();
    }

    private static IResult ToResult<TResponse>(TResponse response, TripParticipantsDto? participants)
        where TResponse : Application.Common.Responses.ValidatedResponse
    {
        if (response.ValidationErrors is { Count: > 0 })
        {
            return Results.BadRequest(response);
        }

        if (response.Error is CurrentUserUnresolvedError)
        {
            return Results.Unauthorized();
        }

        if (response.Error is TripOwnerActionRequiredError)
        {
            return Results.Json(response, statusCode: StatusCodes.Status403Forbidden);
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

        return Results.Ok(participants);
    }
}
