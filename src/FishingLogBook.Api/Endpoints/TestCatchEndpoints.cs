using FishingLogBook.Application.TestCatches;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Api.Endpoints;

public static class TestCatchEndpoints
{
    public static IEndpointRouteBuilder MapTestCatchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/test-catches", UpsertAsync)
            .WithName("UpsertTestCatch")
            .WithTags("TestCatch")
            .Produces<TestCatchDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        endpoints.MapGet("/api/test-catches", ListAsync)
            .WithName("ListTestCatches")
            .WithTags("TestCatch")
            .Produces<IReadOnlyList<TestCatchDto>>(StatusCodes.Status200OK);

        return endpoints;
    }

    private static async Task<IResult> UpsertAsync(
        TestCatchDto testCatch,
        TestCatchService testCatchService,
        CancellationToken cancellationToken)
    {
        if (testCatch.Id == Guid.Empty || string.IsNullOrWhiteSpace(testCatch.SpeciesName))
        {
            return Results.BadRequest();
        }

        var saved = await testCatchService.UpsertAsync(testCatch, cancellationToken);
        return Results.Ok(saved);
    }

    private static async Task<IResult> ListAsync(
        TestCatchService testCatchService,
        CancellationToken cancellationToken)
    {
        var catches = await testCatchService.ListAsync(cancellationToken);
        return Results.Ok(catches);
    }
}
