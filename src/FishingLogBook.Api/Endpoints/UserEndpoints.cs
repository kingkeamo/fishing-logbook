using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.OfflineAccess.Commands;
using FishingLogBook.Application.OfflineAccess.Queries;
using FishingLogBook.Shared.Dtos;
using MediatR;

namespace FishingLogBook.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/users/current", GetCurrentAsync)
            .WithName("GetCurrentUser")
            .WithTags("Users")
            .RequireAuthorization()
            .Produces<CurrentUserDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet("/api/users/current/offline-access-preference", GetOfflineAccessPreferenceAsync)
            .WithName("GetOfflineAccessPreference")
            .WithTags("Users")
            .RequireAuthorization()
            .Produces<OfflineAccessPreferenceDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapPut("/api/users/current/offline-access-preference", UpdateOfflineAccessPreferenceAsync)
            .WithName("UpdateOfflineAccessPreference")
            .WithTags("Users")
            .RequireAuthorization()
            .Produces<OfflineAccessPreferenceDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static IResult GetCurrentAsync(ICurrentUser currentUser)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new CurrentUserDto(
            currentUser.UserId,
            currentUser.Email,
            currentUser.Provider,
            currentUser.Subject));
    }

    private static async Task<IResult> GetOfflineAccessPreferenceAsync(
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new GetOfflineAccessPreferenceQuery { UserId = currentUser.UserId }, cancellationToken);
        return response.IsFailure || response.Preference is null
            ? Results.Problem(response.ErrorMessage, statusCode: StatusCodes.Status503ServiceUnavailable)
            : Results.Ok(response.Preference);
    }

    private static async Task<IResult> UpdateOfflineAccessPreferenceAsync(
        OfflineAccessPreferenceDto preference,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(new UpdateOfflineAccessPreferenceCommand
        {
            UserId = currentUser.UserId,
            Enabled = preference.Enabled
        }, cancellationToken);
        return response.IsFailure || response.Preference is null
            ? Results.Problem(response.ErrorMessage, statusCode: StatusCodes.Status503ServiceUnavailable)
            : Results.Ok(response.Preference);
    }
}
