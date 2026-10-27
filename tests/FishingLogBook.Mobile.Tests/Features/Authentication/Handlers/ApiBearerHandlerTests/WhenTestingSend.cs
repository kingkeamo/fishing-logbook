using System.Net;
using AwesomeAssertions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Handlers.ApiBearerHandlerTests;

public class WhenTestingSend : BaseApiBearerHandlerTest
{
    [Theory]
    [InlineData("https://storage.example.com/object")]
    [InlineData("http://api.example.com/api/catches")]
    [InlineData("https://api.example.com.evil.test/api/catches")]
    public async Task ItShouldRejectEveryEndpointOutsideTheApprovedHttpsOrigin(string url)
    {
        // Arrange
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var action = () => client.GetAsync(url);
        // Act
        await action.Should().ThrowAsync<InvalidOperationException>();
        // Assert
        await MockAccessTokenProvider.DidNotReceive().GetAccessTokenAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("https://api.example.com/")]
    [InlineData("https://api.example.com/health")]
    [InlineData("https://api.example.com/apix/catches")]
    [InlineData("https://api.example.com/API/catches")]
    [InlineData("https://api.example.com/api/../health")]
    [InlineData("https://api.example.com/api/%2E%2E/health")]
    [InlineData("https://api.example.com/api/..%2Fhealth")]
    [InlineData("https://api.example.com/api/%5C..%5Chealth")]
    public async Task ItShouldRejectPathsOutsideTheApiNamespaceOnTheApprovedOrigin(string url)
    {
        // Arrange
        var sent = false;
        using var client = CreateClient(_ =>
        {
            sent = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var action = () => client.GetAsync(url);
        // Act
        await action.Should().ThrowAsync<InvalidOperationException>();
        // Assert
        sent.Should().BeFalse();
        await MockAccessTokenProvider.DidNotReceive().GetAccessTokenAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDiscardAResponseCompletedAfterTheSessionChanged()
    {
        // Arrange
        MockSessionCoordinator.IsCurrent(Lease).Returns(false);
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var action = () => client.GetAsync("https://api.example.com/api/catches");
        // Act
        await action.Should().ThrowAsync<OperationCanceledException>();
        // Assert
        await MockAccessTokenProvider.Received(1).GetAccessTokenAsync(Arg.Any<CancellationToken>());
        MockSessionCoordinator.Received(1).IsCurrent(Lease);
    }

    [Fact]
    public async Task ItShouldAttachBearerOnlyToTheApprovedApiOrigin()
    {
        // Arrange
        string? authorization = null;
        using var client = CreateClient(request =>
        {
            authorization = request.Headers.Authorization?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        // Act
        using var response = await client.GetAsync("https://api.example.com/api/catches");
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        authorization.Should().Be("Bearer access-token");
        await MockAccessTokenProvider.Received(1).GetAccessTokenAsync(Arg.Any<CancellationToken>());
        MockSessionCoordinator.Received(1).IsCurrent(Lease);
    }
}
