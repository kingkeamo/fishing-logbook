using AwesomeAssertions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Clients.CognitoSessionClientTests;

public sealed class WhenTestingOpenLogout : BaseCognitoSessionClientTest
{
    [Fact]
    public async Task ItShouldOpenTheConfiguredLogoutUrl()
    {
        // Arrange
        var (sut, server) = CreateSut(_ => throw new InvalidOperationException());

        // Act
        await sut.OpenLogoutAsync(CancellationToken.None);

        // Assert
        await MockBrowser.Received(1).OpenAsync(
            new Uri("https://auth.example.com/logout?client_id=client-123&logout_uri=https%3A%2F%2Ffishing-logbook-dev.pages.dev%2Fnative%2Fsignedout"),
            new Uri("https://fishing-logbook-dev.pages.dev/native/signedout"),
            CancellationToken.None);
        server.Requests.Should().BeEmpty();
    }
}
