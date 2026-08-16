using System.Net;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Domain.Users;
using NSubstitute;

namespace FishingLogBook.Api.Tests.UserEndpointsTests;

public class WhenTestingMissingEmail
{
    [Fact]
    public async Task ItShouldRejectTheRequestWithoutResolvingAUser()
    {
        // Arrange
        using var factory = new SystemApiFactory();
        var token = TestJwt.CreateAccessToken(includeEmail: false);
        var client = factory.CreateAuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/api/users/current");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await factory.UserIdentityRepository.DidNotReceive().FindUserIdAsync(
            Arg.Any<UserIdentity>(),
            Arg.Any<CancellationToken>());
        await factory.UserIdentityRepository.DidNotReceive().CreateAsync(
            Arg.Any<User>(),
            Arg.Any<UserIdentity>(),
            Arg.Any<CancellationToken>());
    }
}
