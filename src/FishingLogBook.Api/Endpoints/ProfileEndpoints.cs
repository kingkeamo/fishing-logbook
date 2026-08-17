using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.Profiles.Commands;
using FishingLogBook.Application.Profiles.Errors;
using FishingLogBook.Application.Profiles.Queries;
using FishingLogBook.Shared.Dtos;
using MediatR;

namespace FishingLogBook.Api.Endpoints;

public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/profiles/me", GetOwnAsync)
            .WithName("GetOwnProfile")
            .WithTags("Profiles")
            .RequireAuthorization()
            .Produces<ProfileDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapPut("/api/profiles/me", UpdateOwnAsync)
            .WithName("UpdateOwnProfile")
            .WithTags("Profiles")
            .RequireAuthorization()
            .Produces<ProfileDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet("/api/profiles/{userId:guid}", GetPublicAsync)
            .WithName("GetPublicProfile")
            .WithTags("Profiles")
            .RequireAuthorization()
            .Produces<PublicProfileDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapPost("/api/profiles/me/photograph/upload-url", CreatePhotographUploadAsync)
            .WithName("CreateProfilePhotographUpload")
            .WithTags("Profiles")
            .RequireAuthorization()
            .Produces<PhotographUploadDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapPost("/api/profiles/me/photograph", RecordPhotographAsync)
            .WithName("RecordProfilePhotograph")
            .WithTags("Profiles")
            .RequireAuthorization()
            .Produces<ProfileDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> GetOwnAsync(
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new GetOwnProfileQuery { UserId = currentUser.UserId },
            cancellationToken);
        return ToDataResult(response.IsFailure, response.ErrorMessage, response.Profile, StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> UpdateOwnAsync(
        UpdateProfileDto profile,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new UpdateOwnProfileCommand
            {
                UserId = currentUser.UserId,
                Profile = profile
            },
            cancellationToken);
        if (response.ValidationErrors is { Count: > 0 })
        {
            return Results.BadRequest(response);
        }

        return ToDataResult(response.IsFailure, response.ErrorMessage, response.Profile, StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> GetPublicAsync(
        Guid userId,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new GetPublicProfileQuery { UserId = userId },
            cancellationToken);
        if (response.Error is ProfileNotFoundError)
        {
            return Results.NotFound();
        }

        return ToDataResult(response.IsFailure, response.ErrorMessage, response.Profile, StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> CreatePhotographUploadAsync(
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
            new CreateProfilePhotographUploadCommand
            {
                UserId = currentUser.UserId,
                Request = request
            },
            cancellationToken);
        if (response.ValidationErrors is { Count: > 0 })
        {
            return Results.BadRequest(response);
        }

        if (response.Error is ObjectStorageNotConfiguredError)
        {
            return Results.Problem(
                title: response.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return ToDataResult(response.IsFailure, response.ErrorMessage, response.Upload, StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> RecordPhotographAsync(
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
            new RecordProfilePhotographCommand
            {
                UserId = currentUser.UserId,
                Photograph = photograph
            },
            cancellationToken);
        if (response.ValidationErrors is { Count: > 0 })
        {
            return Results.BadRequest(response);
        }

        if (response.Error is PhotographObjectKeyMismatchError or ProfileNotFoundError)
        {
            return Results.BadRequest(response);
        }

        return ToDataResult(response.IsFailure, response.ErrorMessage, response.Profile, StatusCodes.Status503ServiceUnavailable);
    }

    private static IResult ToDataResult<T>(
        bool isFailure,
        string? errorMessage,
        T? data,
        int failureStatusCode)
    {
        if (isFailure)
        {
            return Results.Problem(
                title: errorMessage,
                statusCode: failureStatusCode);
        }

        return Results.Ok(data);
    }
}
