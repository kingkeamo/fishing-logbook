using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Domain.TestCatches;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Api.Tests.TestCatchEndpointsTests;

public class WhenTestingAuthorization : IClassFixture<SystemApiFactory>
{
    private readonly SystemApiFactory _factory;

    public WhenTestingAuthorization(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectTheRequest_WhenAuthorizationHeaderIsMissing()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/test-catches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertCatchRepositoryWasNotInvoked();
    }

    [Fact]
    public async Task ItShouldRejectTheRequest_WhenTheBearerTokenIsInvalid()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

        // Act
        var response = await client.GetAsync("/api/test-catches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertCatchRepositoryWasNotInvoked();
    }

    [Fact]
    public async Task ItShouldRejectTheRequest_WhenTheAccessTokenHasExpired()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        var token = TestJwt.CreateAccessToken(expires: DateTime.UtcNow.AddMinutes(-2));
        var client = _factory.CreateAuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/api/test-catches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertCatchRepositoryWasNotInvoked();
    }

    [Fact]
    public async Task ItShouldRejectTheRequest_WhenTheIssuerIsWrong()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        var token = TestJwt.CreateAccessToken(issuer: "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_other");
        var client = _factory.CreateAuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/api/test-catches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertCatchRepositoryWasNotInvoked();
    }

    [Fact]
    public async Task ItShouldRejectTheRequest_WhenTheAudienceIsMissing()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        var token = TestJwt.CreateAccessToken(includeAudience: false);
        var client = _factory.CreateAuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/api/test-catches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertCatchRepositoryWasNotInvoked();
    }

    [Fact]
    public async Task ItShouldRejectTheRequest_WhenTheAudienceIsWrong()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        var token = TestJwt.CreateAccessToken(audience: "https://other-api.example");
        var client = _factory.CreateAuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/api/test-catches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertCatchRepositoryWasNotInvoked();
    }

    [Fact]
    public async Task ItShouldRejectTheRequest_WhenTheAppClientIsWrong()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        var token = TestJwt.CreateAccessToken(clientId: "other-client");
        var client = _factory.CreateAuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/api/test-catches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertCatchRepositoryWasNotInvoked();
    }

    [Fact]
    public async Task ItShouldRejectTheRequest_WhenAnIdTokenIsPresented()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        var token = TestJwt.CreateAccessToken(tokenUse: AuthConstants.TokenUseId);
        var client = _factory.CreateAuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/api/test-catches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertCatchRepositoryWasNotInvoked();
    }

    [Fact]
    public async Task ItShouldRejectTheRequest_WhenTheApiScopeIsMissing()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        var token = TestJwt.CreateAccessToken(scope: "openid profile email");
        var client = _factory.CreateAuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/api/test-catches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertCatchRepositoryWasNotInvoked();
    }

    [Fact]
    public async Task ItShouldRejectTheRequest_WhenTheSubjectIsMissing()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        var token = TestJwt.CreateAccessToken(includeSubject: false);
        var client = _factory.CreateAuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/api/test-catches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertCatchRepositoryWasNotInvoked();
    }

    [Fact]
    public async Task ItShouldAllowTheRequest_WhenAValidAccessTokenIsPresented()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        _factory.TestCatchRepository
            .GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TestCatchRecord>>([]));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/test-catches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IReadOnlyList<TestCatchDto>>();
        body.Should().BeEmpty();
        await _factory.TestCatchRepository.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAllowHealth_WhenUnauthenticated()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertCatchRepositoryWasNotInvoked();
    }

    private async Task AssertCatchRepositoryWasNotInvoked()
    {
        await _factory.TestCatchRepository.DidNotReceive().GetAllAsync(Arg.Any<CancellationToken>());
        await _factory.TestCatchRepository.DidNotReceive()
            .UpsertAsync(Arg.Any<TestCatchRecord>(), Arg.Any<CancellationToken>());
    }
}
