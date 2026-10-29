using System.Net;
using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Clients.CognitoSessionClientTests;

public sealed class WhenTestingRevokeRefreshToken : BaseCognitoSessionClientTest
{
    [Fact]
    public async Task ItShouldPropagateCancellation()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var (sut, _) = CreateSut((_, token) => Task.FromCanceled<HttpResponseMessage>(token));

        // Act
        var action = () => sut.RevokeRefreshTokenAsync("refresh-token", cancellation.Token);

        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ItShouldRejectAnUnsuccessfulResponse()
    {
        // Arrange
        var (sut, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));

        // Act
        var action = () => sut.RevokeRefreshTokenAsync("refresh-token", CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ItShouldPostTheRefreshTokenAndClientId()
    {
        // Arrange
        string? body = null;
        var (sut, server) = CreateSut(async (request, _) =>
        {
            body = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        // Act
        await sut.RevokeRefreshTokenAsync("refresh token/+", CancellationToken.None);

        // Assert
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.Uri.Should().Be(new Uri("https://auth.example.com/oauth2/revoke"));
        body.Should().Be("token=refresh+token%2F%2B&client_id=client-123");
    }
}
