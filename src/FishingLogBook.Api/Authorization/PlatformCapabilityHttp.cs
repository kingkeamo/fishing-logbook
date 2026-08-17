using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;

namespace FishingLogBook.Api.Authorization;

public static class PlatformCapabilityHttp
{
    public static IResult From(ICurrentUser currentUser, ValidatedResponse response)
    {
        if (!currentUser.IsResolved || response.Error is CurrentUserUnresolvedError)
        {
            return Results.Unauthorized();
        }

        if (response.Error is MissingPlatformCapabilityError)
        {
            return Results.Json(response, statusCode: StatusCodes.Status403Forbidden);
        }

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

        return Results.NoContent();
    }
}
