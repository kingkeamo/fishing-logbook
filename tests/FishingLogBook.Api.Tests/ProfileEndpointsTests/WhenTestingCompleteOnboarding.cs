using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.ProfileEndpointsTests;

public class WhenTestingCompleteOnboarding : IClassFixture<SystemApiFactory>
{
    private readonly SystemApiFactory _factory;

    public WhenTestingCompleteOnboarding(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectTheRequestWhenAuthorizationIsMissing()
    {
        // Arrange
        _factory.ProfileRepository.ClearReceivedCalls();
        var client = _factory.CreateClient();

        // Act
        var response = await client.PutAsync("/api/profiles/me/onboarding", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.ProfileRepository.DidNotReceive().CompleteOnboardingAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnServiceUnavailableWhenCompletionFails()
    {
        // Arrange
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ProfileRepository.CompleteOnboardingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Profile>("failed"));
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken());

        // Act
        var response = await client.PutAsync("/api/profiles/me/onboarding", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.ProfileRepository.Received(1).CompleteOnboardingAsync(
            Arg.Is<Guid>(value => value != Guid.Empty), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCompleteTheAuthenticatedUsersOnboarding()
    {
        // Arrange
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ProfileRepository.CompleteOnboardingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(new Profile
            {
                UserId = call.ArgAt<Guid>(0),
                OnboardingCompletedOn = DateTimeOffset.UtcNow
            }));
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken());

        // Act
        var response = await client.PutAsync("/api/profiles/me/onboarding", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<ProfileDto>();
        profile.Should().NotBeNull();
        profile!.OnboardingCompleted.Should().BeTrue();
        await _factory.ProfileRepository.Received(1).CompleteOnboardingAsync(
            profile.UserId, Arg.Any<CancellationToken>());
    }
}
