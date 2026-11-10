using System.Security.Claims;
using FishingLogBook.Api.Middleware;
using FishingLogBook.Application.Common.Contracts.Services;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Api.Tests.CurrentUserMiddlewareTests;

public class BaseCurrentUserMiddlewareTest
{
    protected static readonly Guid UserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    protected readonly IMediator MockMediator = Substitute.For<IMediator>();
    protected readonly ICurrentUser MockCurrentUser = Substitute.For<ICurrentUser>();
    protected readonly CurrentUserMiddleware Sut;
    protected bool NextCalled;

    protected BaseCurrentUserMiddlewareTest()
    {
        Sut = new CurrentUserMiddleware(
            _ =>
            {
                NextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<CurrentUserMiddleware>.Instance);
    }

    protected static DefaultHttpContext Authenticated(
        string? subject = "cognito-subject",
        string? email = "angler@example.test",
        CancellationToken requestAborted = default)
    {
        var claims = new List<Claim>();
        if (subject is not null)
        {
            claims.Add(new Claim("sub", subject));
        }

        if (email is not null)
        {
            claims.Add(new Claim("email", email));
        }

        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")),
            RequestAborted = requestAborted
        };
    }
}
