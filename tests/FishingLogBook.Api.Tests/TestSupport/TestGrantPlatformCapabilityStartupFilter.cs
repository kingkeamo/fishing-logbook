using FishingLogBook.Api.Authorization;
using FishingLogBook.Application.Capabilities.Commands;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FishingLogBook.Api.Tests.TestSupport;

internal sealed class TestGrantPlatformCapabilityStartupFilter : IStartupFilter
{
    public const string Path = "/__test/platform-capabilities/grant";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            next(app);
            Map(app);
        };
    }

    private static void Map(IApplicationBuilder app)
    {
        IEndpointRouteBuilder? endpoints = app as IEndpointRouteBuilder;
        if (endpoints is null &&
            app.Properties.TryGetValue("__EndpointRouteBuilder", out var stored))
        {
            endpoints = stored as IEndpointRouteBuilder;
        }

        if (endpoints is null)
        {
            throw new InvalidOperationException("The test grant endpoint could not be mapped.");
        }

        endpoints.MapPost(Path, GrantAsync)
            .ExcludeFromDescription()
            .RequireAuthorization();
    }

    private static async Task<IResult> GrantAsync(
        GrantPlatformCapabilityRequest body,
        IMediator mediator,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsResolved)
        {
            return Results.Unauthorized();
        }

        var response = await mediator.Send(
            new GrantPlatformCapabilityCommand
            {
                TargetUserId = body.TargetUserId,
                Capability = body.Capability
            },
            cancellationToken);
        return PlatformCapabilityHttp.From(currentUser, response);
    }
}

internal sealed class GrantPlatformCapabilityRequest
{
    public Guid TargetUserId { get; init; }

    public PlatformCapabilityEnum Capability { get; init; }

    public Guid? UserId { get; init; }

    public bool Administrator { get; init; }

    public IReadOnlyList<string>? Capabilities { get; init; }
}
