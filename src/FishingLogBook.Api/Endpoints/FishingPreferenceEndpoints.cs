using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.FishingPreferences.Commands;
using FishingLogBook.Application.FishingPreferences.Errors;
using FishingLogBook.Application.FishingPreferences.Queries;
using FishingLogBook.Shared.Dtos;
using MediatR;

namespace FishingLogBook.Api.Endpoints;

public static class FishingPreferenceEndpoints
{
    public static IEndpointRouteBuilder MapFishingPreferenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/fishing-catalogue", GetCatalogueAsync)
            .WithName("GetFishingCatalogue")
            .WithTags("FishingPreferences")
            .RequireAuthorization()
            .Produces<FishingCatalogueDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet("/api/profiles/me/fishing-preferences", GetPreferencesAsync)
            .WithName("GetFishingPreferences")
            .WithTags("FishingPreferences")
            .RequireAuthorization()
            .Produces<FishingPreferencesDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapPut("/api/profiles/me/fishing-preferences", UpdatePreferencesAsync)
            .WithName("UpdateFishingPreferences")
            .WithTags("FishingPreferences")
            .RequireAuthorization()
            .Produces<FishingPreferencesDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> GetCatalogueAsync(
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(new GetFishingCatalogueQuery(), cancellationToken);
        if (response.IsFailure)
        {
            return Results.Problem(
                title: response.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(new FishingCatalogueDto(response.Methods ?? [], response.AllSpecies ?? []));
    }

    private static async Task<IResult> GetPreferencesAsync(
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new GetFishingPreferencesQuery { UserId = currentUser.UserId },
            cancellationToken);
        return ToDataResult(response, response.Preferences);
    }

    private static async Task<IResult> UpdatePreferencesAsync(
        UpdateFishingPreferencesDto preferences,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new UpdateFishingPreferencesCommand
            {
                UserId = currentUser.UserId,
                Preferences = preferences
            },
            cancellationToken);
        return ToDataResult(response, response.Preferences);
    }

    private static IResult ToDataResult<TResponse, TData>(TResponse response, TData? data)
        where TResponse : ValidatedResponse
    {
        if (response.ValidationErrors is { Count: > 0 } || response.Error is UnknownFishingCatalogueEntryError)
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
