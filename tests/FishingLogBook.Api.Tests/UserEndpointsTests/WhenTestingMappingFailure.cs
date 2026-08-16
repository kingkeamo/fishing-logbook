using System.Net;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Domain.Users;
using NSubstitute;

namespace FishingLogBook.Api.Tests.UserEndpointsTests;

public class WhenTestingMappingFailure
{
    [Fact]
    public async Task ItShouldFailWithoutUsingAFallbackUserId()
    {
        // Arrange
        using var factory = new SystemApiFactory();
        factory.MappingFailed = true;
        factory.UserIdentityRepository.ClearReceivedCalls();
        var client = factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/users/current");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        body.Should().NotContain(Guid.Empty.ToString());
        body.Should().NotContain("Failed to resolve FishingLogBook user.");
        body.Should().NotContain(TestJwt.Subject);
        await factory.UserIdentityRepository.Received(1).FindUserIdAsync(
            Arg.Is<UserIdentity>(identity => identity.Subject == TestJwt.Subject),
            Arg.Any<CancellationToken>());
        await factory.UserIdentityRepository.DidNotReceive().CreateAsync(
            Arg.Any<User>(),
            Arg.Any<UserIdentity>(),
            Arg.Any<CancellationToken>());
        await factory.UserIdentityRepository.DidNotReceive().UpdateEmailAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }
}
