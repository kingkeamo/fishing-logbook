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
            .RequireAuthorization()
            .Produces<TestCatchDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        endpoints.MapGet("/api/test-catches", ListAsync)
            .WithName("ListTestCatches")
            .WithTags("TestCatch")
            .RequireAuthorization()
            .Produces<IReadOnlyList<TestCatchDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        endpoints.MapPost("/api/test-catches/{id:guid}/photographs/upload-url", CreatePhotographUploadAsync)
            .WithName("CreateTestCatchPhotographUpload")
            .WithTags("TestCatch")
            .RequireAuthorization()
            .Produces<PhotographUploadDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapPost("/api/test-catches/{id:guid}/photographs", RecordPhotographAsync)
            .WithName("RecordTestCatchPhotograph")
            .WithTags("TestCatch")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

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

    private static async Task<IResult> CreatePhotographUploadAsync(
        Guid id,
        PhotographUploadRequestDto request,
        TestCatchService testCatchService,
        CancellationToken cancellationToken)
    {
        if (!testCatchService.IsObjectStorageConfigured)
        {
            return Results.Problem(
                title: "Object storage is not configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (id == Guid.Empty ||
            request.PhotographId == Guid.Empty ||
            !IsImageContentType(request.ContentType))
        {
            return Results.BadRequest();
        }

        var upload = await testCatchService.CreatePhotographUploadAsync(id, request, cancellationToken);
        return upload is null ? Results.NotFound() : Results.Ok(upload);
    }

    private static async Task<IResult> RecordPhotographAsync(
        Guid id,
        RecordPhotographDto request,
        TestCatchService testCatchService,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty ||
            request.PhotographId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.ObjectKey) ||
            !IsImageContentType(request.ContentType))
        {
            return Results.BadRequest();
        }

        try
        {
            var recorded = await testCatchService.RecordPhotographAsync(id, request, cancellationToken);
            return recorded ? Results.NoContent() : Results.NotFound();
        }
        catch (ArgumentException)
        {
            return Results.BadRequest();
        }
    }

    private static bool IsImageContentType(string? contentType)
    {
        return !string.IsNullOrWhiteSpace(contentType) &&
               contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }
}
