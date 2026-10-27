using System.Net;
using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Clients;
using FishingLogBook.Mobile.Api.Enums;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Mobile.Tests.Api.Clients.AuthorizedApiClientTests;

public class WhenTestingGet : BaseAuthorizedApiClientTest
{
    [Theory]
    [InlineData("https://storage.example.com/api/fishing-catalogue")]
    [InlineData("http://api.example.com/api/fishing-catalogue")]
    [InlineData("health")]
    [InlineData("api/../health")]
    public async Task ItShouldNeverSendTheBearerTokenOutsideTheApprovedApiNamespace(string path)
    {
        // Arrange
        var (sut, server) = CreateSut(_ => Json("{}"));
        var action = () => sut.GetAsync<SampleDto>(path, CancellationToken.None);
        // Act
        await action.Should().ThrowAsync<InvalidOperationException>();
        // Assert
        server.Requests.Should().BeEmpty();
        await MockAccessTokenProvider.DidNotReceive().GetAccessTokenAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportAnExpiredSessionWithoutSendingWhenNoTokenIsAvailable()
    {
        // Arrange
        MockAccessTokenProvider.GetAccessTokenAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedAccessException("The authenticated session could not be refreshed."));
        var (sut, server) = CreateSut(_ => Json("{}"));
        // Act
        var result = await sut.GetAsync<SampleDto>("api/fishing-catalogue", CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(ApiOutcomeEnum.SessionExpired);
        result.Value.Should().BeNull();
        server.Requests.Should().BeEmpty();
        await MockAccessTokenProvider.Received(1).GetAccessTokenAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, ApiOutcomeEnum.ValidationFailed)]
    [InlineData(HttpStatusCode.Unauthorized, ApiOutcomeEnum.SessionExpired)]
    [InlineData(HttpStatusCode.Forbidden, ApiOutcomeEnum.Forbidden)]
    [InlineData(HttpStatusCode.RequestTimeout, ApiOutcomeEnum.Unavailable)]
    [InlineData(HttpStatusCode.NotFound, ApiOutcomeEnum.InvalidResponse)]
    [InlineData(HttpStatusCode.InternalServerError, ApiOutcomeEnum.ServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ApiOutcomeEnum.ServerError)]
    public async Task ItShouldMapEveryFailureStatusToAConsistentOutcome(HttpStatusCode statusCode, ApiOutcomeEnum expected)
    {
        // Arrange
        var (sut, server) = CreateSut(_ => Json("{\"title\":\"failure\"}", statusCode));
        // Act
        var result = await sut.GetAsync<SampleDto>("api/fishing-catalogue", CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(expected);
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        server.Requests.Should().ContainSingle();
        await MockAccessTokenProvider.Received(1).GetAccessTokenAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportTheApiAsUnavailableWhenTheNetworkFails()
    {
        // Arrange
        var (sut, server) = CreateSut((_, _) => throw new HttpRequestException("No such host is known."));
        // Act
        var result = await sut.GetAsync<SampleDto>("api/fishing-catalogue", CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(ApiOutcomeEnum.Unavailable);
        server.Requests.Should().ContainSingle();
        Logger.Entries.Should().ContainSingle(entry => entry.Message.Contains(typeof(HttpRequestException).FullName!));
    }

    [Fact]
    public async Task ItShouldReportTheApiAsUnavailableWhenTheHttpClientTimesOut()
    {
        // Arrange
        var (sut, _) = CreateSut((_, _) => throw new TaskCanceledException("timeout", new TimeoutException()));
        // Act
        var result = await sut.GetAsync<SampleDto>("api/fishing-catalogue", CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(ApiOutcomeEnum.Unavailable);
        AuthorizedApiClient.RequestTimeout.Should().Be(TimeSpan.FromSeconds(10));
        MockSessionCoordinator.Received(1).IsCurrent(Lease);
    }

    [Fact]
    public async Task ItShouldReportTheApiAsUnavailableWhenTheNetworkStackCancelsWithoutATimeoutException()
    {
        // Arrange
        var (sut, server) = CreateSut((_, _) => throw new TaskCanceledException("The request was canceled."));
        // Act
        var result = await sut.GetAsync<SampleDto>("api/fishing-catalogue", CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(ApiOutcomeEnum.Unavailable);
        server.Requests.Should().ContainSingle();
        MockSessionCoordinator.Received(1).Capture();
        MockSessionCoordinator.Received(1).IsCurrent(Lease);
    }

    [Fact]
    public async Task ItShouldPropagateCancellationRequestedByTheCaller()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var (sut, _) = CreateSut(_ => Json("{\"name\":\"Fly\"}"));
        var action = () => sut.GetAsync<SampleDto>("api/fishing-catalogue", cancellation.Token);
        // Act
        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ItShouldPropagateCallerCancellationThatHappensDuringTheRequest()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var (sut, _) = CreateSut(async (_, token) =>
        {
            await cancellation.CancelAsync();
            token.ThrowIfCancellationRequested();
            return Json("{}");
        });
        var action = () => sut.GetAsync<SampleDto>("api/fishing-catalogue", cancellation.Token);
        // Act
        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
        MockSessionCoordinator.DidNotReceive().IsCurrent(Lease);
    }

    [Fact]
    public async Task ItShouldPropagateCancellationWhenTheAccountSwitchesMidRequest()
    {
        // Arrange
        var (sut, server) = CreateSut((_, _) =>
        {
            MockSessionCoordinator.IsCurrent(Lease).Returns(false);
            throw new TaskCanceledException("The account session was cancelled.");
        });
        var action = () => sut.GetAsync<SampleDto>("api/fishing-catalogue", CancellationToken.None);
        // Act
        await action.Should().ThrowAsync<OperationCanceledException>();
        // Assert
        server.Requests.Should().ContainSingle();
        MockSessionCoordinator.Received(1).IsCurrent(Lease);
    }

    [Fact]
    public async Task ItShouldDiscardTheResponseWhenTheAccountChangedDuringTheRequest()
    {
        // Arrange
        MockSessionCoordinator.IsCurrent(Lease).Returns(false);
        var (sut, server) = CreateSut(_ => Json("{\"name\":\"Fly\"}"));
        var action = () => sut.GetAsync<SampleDto>("api/fishing-catalogue", CancellationToken.None);
        // Act
        await action.Should().ThrowAsync<OperationCanceledException>();
        // Assert
        server.Requests.Should().ContainSingle();
        MockSessionCoordinator.Received(2).IsCurrent(Lease);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{\"name\":")]
    [InlineData("<html></html>")]
    public async Task ItShouldReportAnInvalidResponseWhenTheBodyIsEmptyOrUnreadable(string body)
    {
        // Arrange
        var (sut, _) = CreateSut(_ => Json(body));
        // Act
        var result = await sut.GetAsync<SampleDto>("api/fishing-catalogue", CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(ApiOutcomeEnum.InvalidResponse);
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldNeverWriteTheAccessTokenToTheLog()
    {
        // Arrange
        var (sut, _) = CreateSut(_ => Json("{}", HttpStatusCode.Unauthorized));
        // Act
        await sut.GetAsync<SampleDto>("api/fishing-catalogue", CancellationToken.None);
        // Assert
        Logger.Entries.Should().NotBeEmpty();
        Logger.Entries.Should().OnlyContain(entry => !entry.Message.Contains(AccessToken));
    }

    [Fact]
    public async Task ItShouldSendAnAuthenticatedGetToTheApiAndReadTheBody()
    {
        // Arrange
        var (sut, server) = CreateSut(_ => Json("{\"name\":\"Fly\"}"));
        // Act
        var result = await sut.GetAsync<SampleDto>("api/fishing-catalogue", CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(ApiOutcomeEnum.Success);
        result.Value.Should().Be(new SampleDto("Fly"));
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.Uri.Should().Be(new Uri("https://api.example.com/api/fishing-catalogue"));
        request.Authorization.Should().Be("Bearer " + AccessToken);
        await MockAccessTokenProvider.Received(1).GetAccessTokenAsync(Arg.Any<CancellationToken>());
        MockSessionCoordinator.Received(1).IsCurrent(Lease);
    }
}
