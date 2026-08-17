using FishingLogBook.Application.Catches.Commands;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Application.Contracts.Services;
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
            or CatchLocationInvalidError)
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
}
