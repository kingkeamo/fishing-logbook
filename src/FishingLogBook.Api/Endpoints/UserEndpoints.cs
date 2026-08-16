using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Dtos;

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

        return endpoints;
    }

    private static IResult GetCurrentAsync(ICurrentUser currentUser)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new CurrentUserDto(currentUser.UserId, currentUser.Email));
    }
}
