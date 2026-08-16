using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.TestCatches;
using FishingLogBook.Domain.Users;
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
    public async Task ItShouldRejectTheRequestWhenAuthorizationHeaderIsMissing()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        _factory.UserIdentityRepository.ClearReceivedCalls();
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/test-catches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertCatchRepositoryWasNotInvoked();
    }

    [Fact]
    public async Task ItShouldRejectTheRequestWhenTheBearerTokenIsInvalid()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        _factory.UserIdentityRepository.ClearReceivedCalls();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

        // Act
        var response = await client.GetAsync("/api/test-catches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertCatchRepositoryWasNotInvoked();
    }

    [Fact]
    public async Task ItShouldRejectTheRequestWhenTheAccessTokenHasExpired()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        _factory.UserIdentityRepository.ClearReceivedCalls();
        var token = TestJwt.CreateAccessToken(expires: DateTime.UtcNow.AddMinutes(-2));
        var client = _factory.CreateAuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/api/test-catches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertCatchRepositoryWasNotInvoked();
    }

    [Fact]
    public async Task ItShouldRejectTheRequestWhenTheIssuerIsWrong()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        _factory.UserIdentityRepository.ClearReceivedCalls();
        var token = TestJwt.CreateAccessToken(issuer: "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_other");
        var client = _factory.CreateAuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/api/test-catches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertCatchRepositoryWasNotInvoked();
    }

    [Fact]
    public async Task ItShouldRejectTheRequestWhenTheAudienceIsMissing()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        _factory.UserIdentityRepository.ClearReceivedCalls();
        var token = TestJwt.CreateAccessToken(includeAudience: false);
        var client = _factory.CreateAuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/api/test-catches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertCatchRepositoryWasNotInvoked();
    }

    [Fact]
    public async Task ItShouldRejectTheRequestWhenTheAudienceIsWrong()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        _factory.UserIdentityRepository.ClearReceivedCalls();
        var token = TestJwt.CreateAccessToken(audience: "https://other-api.example");
        var client = _factory.CreateAuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/api/test-catches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertCatchRepositoryWasNotInvoked();
    }

    [Fact]
    public async Task ItShouldRejectTheRequestWhenTheAppClientIsWrong()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        _factory.UserIdentityRepository.ClearReceivedCalls();
        var token = TestJwt.CreateAccessToken(clientId: "other-client");
        var client = _factory.CreateAuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/api/test-catches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertCatchRepositoryWasNotInvoked();
    }

    [Fact]
    public async Task ItShouldRejectTheRequestWhenAnIdTokenIsPresented()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        _factory.UserIdentityRepository.ClearReceivedCalls();
        var token = TestJwt.CreateAccessToken(tokenUse: AuthConstants.TokenUseId);
        var client = _factory.CreateAuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/api/test-catches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertCatchRepositoryWasNotInvoked();
    }

    [Fact]
    public async Task ItShouldRejectTheRequestWhenTheApiScopeIsMissing()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        _factory.UserIdentityRepository.ClearReceivedCalls();
        var token = TestJwt.CreateAccessToken(scope: "openid profile email");
        var client = _factory.CreateAuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/api/test-catches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertCatchRepositoryWasNotInvoked();
    }

    [Fact]
    public async Task ItShouldRejectTheRequestWhenTheSubjectIsMissing()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        _factory.UserIdentityRepository.ClearReceivedCalls();
        var token = TestJwt.CreateAccessToken(includeSubject: false);
        var client = _factory.CreateAuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/api/test-catches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertCatchRepositoryWasNotInvoked();
    }

    [Fact]
    public async Task ItShouldAllowHealthWhenUnauthenticated()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        _factory.UserIdentityRepository.ClearReceivedCalls();
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertCatchRepositoryWasNotInvoked();
    }

    [Fact]
    public async Task ItShouldAllowTheRequestWhenAValidAccessTokenIsPresented()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        _factory.UserIdentityRepository.ClearReceivedCalls();
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
        await _factory.UserIdentityRepository.Received(1).FindUserIdAsync(
            Arg.Is<FindUserIdentityArgs>(args =>
                args.Provider == IdentityProviderConstants.Cognito
                && args.Subject == TestJwt.Subject),
            Arg.Any<CancellationToken>());
    }

    private async Task AssertCatchRepositoryWasNotInvoked()
    {
        await _factory.TestCatchRepository.DidNotReceive().GetAllAsync(Arg.Any<CancellationToken>());
        await _factory.TestCatchRepository.DidNotReceive()
            .UpsertAsync(Arg.Any<TestCatchRecord>(), Arg.Any<CancellationToken>());
        await _factory.UserIdentityRepository.DidNotReceive().FindUserIdAsync(
            Arg.Any<FindUserIdentityArgs>(),
            Arg.Any<CancellationToken>());
        await _factory.UserIdentityRepository.DidNotReceive().CreateAsync(
            Arg.Any<User>(),
            Arg.Any<UserIdentity>(),
            Arg.Any<CancellationToken>());
        await _factory.UserIdentityRepository.DidNotReceive().UpdateEmailAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }
}
