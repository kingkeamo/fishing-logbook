using System.Net;
using System.Text;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Application.Common.Contracts.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace FishingLogBook.Api.Tests.UnresolvedUserEndpointsTests;

public class WhenTestingEveryAuthenticatedEndpoint : IClassFixture<WhenTestingEveryAuthenticatedEndpoint.UnresolvedUserApiFactory>
{
    private static readonly string Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa").ToString("D");
    private static readonly string OtherId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb").ToString("D");

    private readonly UnresolvedUserApiFactory _factory;

    public WhenTestingEveryAuthenticatedEndpoint(UnresolvedUserApiFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<string, string> Endpoints
    {
        get
        {
            return new TheoryData<string, string>
            {
                { "POST", "/api/catches" },
                { "GET", "/api/catches" },
                { "GET", $"/api/catches/{Id}" },
                { "PATCH", $"/api/catches/{Id}/location-visibility" },
                { "PATCH", $"/api/catches/{Id}/angler" },
                { "POST", $"/api/catches/{Id}/photographs/upload-url" },
                { "POST", $"/api/catches/{Id}/photographs" },
                { "DELETE", $"/api/catches/{Id}/photographs/{OtherId}" },
                { "GET", "/api/dashboard" },
                { "GET", "/api/profiles/me/fishing-locations" },
                { "PUT", "/api/profiles/me/fishing-locations" },
                { "GET", "/api/fishing-catalogue" },
                { "GET", "/api/profiles/me/fishing-preferences" },
                { "PUT", "/api/profiles/me/fishing-preferences" },
                { "POST", "/api/locations/label" },
                { "GET", "/api/profiles/me" },
                { "PUT", "/api/profiles/me" },
                { "PUT", "/api/profiles/me/onboarding" },
                { "GET", "/api/profiles/lookup?q=abc" },
                { "GET", $"/api/profiles/{Id}" },
                { "POST", "/api/profiles/me/photograph/upload-url" },
                { "POST", "/api/profiles/me/photograph" },
                { "POST", "/api/trips" },
                { "GET", "/api/trips" },
                { "POST", $"/api/trips/{Id}/activate" },
                { "GET", "/api/trips/planned" },
                { "GET", $"/api/trips/{Id}" },
                { "POST", $"/api/trips/{Id}/photographs/upload-url" },
                { "POST", $"/api/trips/{Id}/photographs" },
                { "DELETE", $"/api/trips/{Id}/photographs/{OtherId}" },
                { "POST", $"/api/trips/{Id}/notes" },
                { "DELETE", $"/api/trips/{Id}/notes/{OtherId}" },
                { "POST", $"/api/trips/{Id}/catches" },
                { "GET", $"/api/trips/{Id}/participants" },
                { "POST", $"/api/trips/{Id}/participants" },
                { "DELETE", $"/api/trips/{Id}/participants/{OtherId}" },
                { "GET", "/api/trips/invitations" },
                { "POST", $"/api/trips/{Id}/invitation/accept" },
                { "POST", $"/api/trips/{Id}/invitation/decline" },
                { "GET", "/api/users/current" },
                { "GET", "/api/users/current/offline-access-preference" },
                { "PUT", "/api/users/current/offline-access-preference" }
            };
        }
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task ItShouldRefuseARequestWhenTheCurrentUserWasNotResolved(string method, string route)
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), route);
        if (method is "POST" or "PUT" or "PATCH")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    public sealed class UnresolvedUserApiFactory : SystemApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICurrentUser>();
                services.AddScoped(_ =>
                {
                    var currentUser = Substitute.For<ICurrentUser>();
                    currentUser.IsResolved.Returns(false);
                    return currentUser;
                });
            });
        }
    }
}
