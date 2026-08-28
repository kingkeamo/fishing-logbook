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
            .Produces<IReadOnlyList<TripSummaryDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet("/api/trips/{tripId:guid}", GetAsync)
            .WithName("GetTrip")
            .WithTags("Trips")
            .RequireAuthorization()
            .Produces<TripDetailDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapPost("/api/trips/{tripId:guid}/photographs/upload-url", CreatePhotographUploadAsync)
            .WithName("CreateTripPhotographUpload")
            .WithTags("Trips")
            .RequireAuthorization()
            .Produces<PhotographUploadDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapPost("/api/trips/{tripId:guid}/photographs", RecordPhotographAsync)
            .WithName("RecordTripPhotograph")
            .WithTags("Trips")
            .RequireAuthorization()
            .Produces<TripPhotographDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapDelete("/api/trips/{tripId:guid}/photographs/{photographId:guid}", DeletePhotographAsync)
            .WithName("DeleteTripPhotograph")
            .WithTags("Trips")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapPost("/api/trips/{tripId:guid}/notes", RecordNoteAsync)
            .WithName("RecordTripNote")
            .WithTags("Trips")
            .RequireAuthorization()
            .Produces<TripNoteDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapDelete("/api/trips/{tripId:guid}/notes/{noteId:guid}", DeleteNoteAsync)
            .WithName("DeleteTripNote")
            .WithTags("Trips")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapPost("/api/trips/{tripId:guid}/catches", AssociateCatchesAsync)
            .WithName("AssociateTripCatches")
            .WithTags("Trips")
            .RequireAuthorization()
            .Produces<TripCatchAssociationDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> AssociateCatchesAsync(
        Guid tripId,
        AssociateTripCatchesDto request,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new AssociateTripCatchesCommand
            {
                TripId = tripId,
                CatchIds = request.CatchIds
            },
            cancellationToken);
        if (response.ValidationErrors is { Count: > 0 })
        {
            return Results.BadRequest(response);
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

        return Results.Ok(response.Association);
    }

    private static async Task<IResult> RecordNoteAsync(
        Guid tripId,
        RecordTripNoteDto note,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new RecordTripNoteCommand
            {
                TripId = tripId,
                Note = note
            },
            cancellationToken);
        if (response.ValidationErrors is { Count: > 0 })
        {
            return Results.BadRequest(response);
        }

        if (response.Error is TripNoteNotFoundError)
        {
            return Results.NotFound();
        }

        if (response.Error is TripNoteInvalidError or TripNoteOutsideTripError)
        {
            return Results.BadRequest(response);
        }

        if (response.IsFailure)
        {
            return Results.Problem(
                title: response.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(response.Note);
    }

    private static async Task<IResult> DeleteNoteAsync(
        Guid tripId,
        Guid noteId,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new DeleteTripNoteCommand
            {
                TripId = tripId,
                NoteId = noteId
            },
            cancellationToken);
        if (response.ValidationErrors is { Count: > 0 })
        {
            return Results.BadRequest(response);
        }

        if (response.Error is TripNoteNotFoundError)
        {
            return Results.NotFound();
        }

        if (response.IsFailure)
        {
            return Results.Problem(
                title: response.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> CreatePhotographUploadAsync(
        Guid tripId,
        PhotographUploadRequestDto request,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new CreateTripPhotographUploadCommand
            {
                TripId = tripId,
                Request = request
            },
            cancellationToken);
        if (response.ValidationErrors is { Count: > 0 })
        {
            return Results.BadRequest(response);
        }

        if (response.Error is TripPhotographNotFoundError)
        {
            return Results.NotFound();
        }

        if (response.IsFailure)
        {
            return Results.Problem(
                title: response.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(response.Upload);
    }

    private static async Task<IResult> RecordPhotographAsync(
        Guid tripId,
        RecordTripPhotographDto photograph,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new RecordTripPhotographCommand
            {
                TripId = tripId,
                Photograph = photograph
            },
            cancellationToken);
        if (response.ValidationErrors is { Count: > 0 })
        {
            return Results.BadRequest(response);
        }

        if (response.Error is TripPhotographNotFoundError)
        {
            return Results.NotFound();
        }

        if (response.Error is TripPhotographObjectKeyMismatchError)
        {
            return Results.BadRequest(response);
        }

        if (response.IsFailure)
        {
            return Results.Problem(
                title: response.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(response.Photograph);
    }

    private static async Task<IResult> DeletePhotographAsync(
        Guid tripId,
        Guid photographId,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new DeleteTripPhotographCommand
            {
                TripId = tripId,
                PhotographId = photographId
            },
            cancellationToken);
        if (response.ValidationErrors is { Count: > 0 })
        {
            return Results.BadRequest(response);
        }

        if (response.Error is TripPhotographNotFoundError)
        {
            return Results.NotFound();
        }

        if (response.IsFailure)
        {
            return Results.Problem(
                title: response.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.NoContent();
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
