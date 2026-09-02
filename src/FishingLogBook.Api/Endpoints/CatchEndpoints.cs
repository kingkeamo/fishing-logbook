using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Application.Catches.Commands;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Application.Catches.Queries;
using FishingLogBook.Application.Common.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using MediatR;

namespace FishingLogBook.Api.Endpoints;

public static class CatchEndpoints
{
    public static IEndpointRouteBuilder MapCatchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/catches", UpsertAsync)
            .WithName("UpsertCatch")
            .WithTags("Catches")
            .RequireAuthorization()
            .Produces<CatchDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet("/api/catches", ListMyAsync)
            .WithName("ListMyCatches")
            .WithTags("Catches")
            .RequireAuthorization()
            .Produces<IReadOnlyList<CatchViewDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet("/api/catches/{catchId:guid}", GetAsync)
            .WithName("GetCatch")
            .WithTags("Catches")
            .RequireAuthorization()
            .Produces<CatchViewDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapPatch("/api/catches/{catchId:guid}/location-visibility", UpdateLocationVisibilityAsync)
            .WithName("UpdateCatchLocationVisibility")
            .WithTags("Catches")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapPatch("/api/catches/{catchId:guid}/angler", CorrectAnglerAsync)
            .WithName("CorrectCatchAngler")
            .WithTags("Catches")
            .RequireAuthorization()
            .Produces<CatchViewDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapPost("/api/catches/{catchId:guid}/photographs/upload-url", CreatePhotographUploadAsync)
            .WithName("CreateCatchPhotographUpload")
            .WithTags("Catches")
            .RequireAuthorization()
            .Produces<PhotographUploadDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapPost("/api/catches/{catchId:guid}/photographs", RecordPhotographAsync)
            .WithName("RecordCatchPhotograph")
            .WithTags("Catches")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapDelete("/api/catches/{catchId:guid}/photographs/{photographId:guid}", DeletePhotographAsync)
            .WithName("DeleteCatchPhotograph")
            .WithTags("Catches")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> UpsertAsync(
        CatchDto catchDto,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new UpsertCatchCommand
            {
                UserId = currentUser.UserId,
                Catch = catchDto
            },
            cancellationToken);
        if (response.ValidationErrors is { Count: > 0 })
        {
            return Results.BadRequest(response);
        }

        if (response.Error is CatchHasNoPhotographsError
            or CatchPhotographIdentityError
            or CatchOwnershipConflictError
            or CatchLocationInvalidError
            or CatchTripInvalidError
            or CatchAnglerNotEligibleError)
        {
            return Results.BadRequest(response);
        }

        if (response.Error is CatchEditNotPermittedError)
        {
            return Results.Json(response, statusCode: StatusCodes.Status403Forbidden);
        }

        if (response.IsFailure)
        {
            return Results.Problem(
                title: response.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(response.Catch);
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
            new GetMyCatchesQuery { UserId = currentUser.UserId },
            cancellationToken);
        if (response.IsFailure)
        {
            return Results.Problem(
                title: response.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(response.Catches);
    }

    private static async Task<IResult> GetAsync(
        Guid catchId,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new GetCatchQuery { CatchId = catchId },
            cancellationToken);
        if (response.Error is CurrentUserUnresolvedError)
        {
            return Results.Unauthorized();
        }

        if (response.Error is CatchNotFoundError)
        {
            return Results.NotFound();
        }

        if (response.IsFailure)
        {
            return Results.Problem(
                title: response.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(response.Catch);
    }

    private static async Task<IResult> UpdateLocationVisibilityAsync(
        Guid catchId,
        UpdateCatchLocationVisibilityDto body,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new UpdateCatchLocationVisibilityCommand
            {
                CatchId = catchId,
                Visibility = body.Visibility
            },
            cancellationToken);
        if (response.Error is CurrentUserUnresolvedError)
        {
            return Results.Unauthorized();
        }

        if (response.ValidationErrors is { Count: > 0 })
        {
            return Results.BadRequest(response);
        }

        if (response.Error is CatchNotFoundError)
        {
            return Results.NotFound();
        }

        if (response.Error is CatchNotOwnedError)
        {
            return Results.Json(response, statusCode: StatusCodes.Status403Forbidden);
        }

        if (response.Error is CatchHasNoLocationError)
        {
            return Results.BadRequest(response);
        }

        if (response.IsFailure)
        {
            return Results.Problem(
                title: response.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> CorrectAnglerAsync(
        Guid catchId,
        CorrectCatchAnglerDto body,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new CorrectCatchAnglerCommand
            {
                CatchId = catchId,
                CaughtByUserId = body.CaughtByUserId
            },
            cancellationToken);
        if (response.Error is CurrentUserUnresolvedError)
        {
            return Results.Unauthorized();
        }

        if (response.ValidationErrors is { Count: > 0 })
        {
            return Results.BadRequest(response);
        }

        if (response.Error is CatchNotFoundError)
        {
            return Results.NotFound();
        }

        if (response.Error is CatchEditNotPermittedError)
        {
            return Results.Json(response, statusCode: StatusCodes.Status403Forbidden);
        }

        if (response.Error is CatchNotOnTripError or CatchAnglerNotEligibleError)
        {
            return Results.BadRequest(response);
        }

        if (response.IsFailure)
        {
            return Results.Problem(
                title: response.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(response.Catch);
    }

    private static async Task<IResult> CreatePhotographUploadAsync(
        Guid catchId,
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
            new CreateCatchPhotographUploadCommand
            {
                CatchId = catchId,
                Request = request
            },
            cancellationToken);
        if (response.ValidationErrors is { Count: > 0 })
        {
            return Results.BadRequest(response);
        }

        if (response.Error is CatchPhotographNotFoundError)
        {
            return Results.NotFound();
        }

        if (response.Error is CatchObjectStorageNotConfiguredError)
        {
            return Results.Problem(
                title: response.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
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
        Guid catchId,
        RecordPhotographDto photograph,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new RecordCatchPhotographCommand
            {
                CatchId = catchId,
                Photograph = photograph
            },
            cancellationToken);
        if (response.ValidationErrors is { Count: > 0 })
        {
            return Results.BadRequest(response);
        }

        if (response.Error is CatchPhotographNotFoundError)
        {
            return Results.NotFound();
        }

        if (response.Error is CatchPhotographObjectKeyMismatchError)
        {
            return Results.BadRequest(response);
        }

        if (response.IsFailure)
        {
            return Results.Problem(
                title: response.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> DeletePhotographAsync(
        Guid catchId,
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
            new DeleteCatchPhotographCommand
            {
                CatchId = catchId,
                PhotographId = photographId
            },
            cancellationToken);
        if (response.ValidationErrors is { Count: > 0 })
        {
            return Results.BadRequest(response);
        }

        if (response.Error is CatchPhotographNotFoundError)
        {
            return Results.NotFound();
        }

        if (response.Error is CatchObjectStorageNotConfiguredError)
        {
            return Results.Problem(
                title: response.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (response.IsFailure)
        {
            return Results.Problem(
                title: response.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.NoContent();
    }
}
