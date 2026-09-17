using FishingLogBook.Application.Common.Contracts.Services;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.LocationLookup.Queries;
using FishingLogBook.Shared.Dtos;
using MediatR;

namespace FishingLogBook.Api.Endpoints;

public static class LocationLookupEndpoints
{
    public static IEndpointRouteBuilder MapLocationLookupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/locations/label", GetLocationLabelAsync)
            .WithName("GetLocationLabel")
            .WithTags("Locations")
            .RequireAuthorization()
            .Produces<LocationLookupDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> GetLocationLabelAsync(
        LocationLookupRequestDto request,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new GetLocationLabelQuery
            {
                Latitude = request.Latitude,
                Longitude = request.Longitude
            },
            cancellationToken);
        if (response.ValidationErrors is { Count: > 0 })
        {
            return Results.BadRequest(response);
        }

        if (response.IsFailure)
        {
            return Results.Problem(
                title: response.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return response.Location is null ? Results.NoContent() : Results.Ok(response.Location);
    }
}
