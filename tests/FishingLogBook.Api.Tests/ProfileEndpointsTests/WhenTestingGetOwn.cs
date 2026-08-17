using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Tests.Common.Builders;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.ProfileEndpointsTests;

public class WhenTestingGetOwn : IClassFixture<SystemApiFactory>
{
    private readonly SystemApiFactory _factory;

    public WhenTestingGetOwn(SystemApiFactory factory)
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
        var response = await client.GetAsync("/api/profiles/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.ProfileRepository.DidNotReceive().GetByUserIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await _factory.ProfileRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Profile>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnServiceUnavailableWhenTheProfileCannotBeLoaded()
    {
        // Arrange
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ProfileRepository
            .GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Profile?>("Failed to load angler profile."));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/profiles/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.ProfileRepository.Received(1).GetByUserIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await _factory.ProfileRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Profile>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnADefaultProfileWithoutWritingWhenNoRowExists()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString("N");
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ProfileRepository
            .GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(null));
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: subject));

        // Act
        var response = await client.GetAsync("/api/profiles/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProfileDto>();
        body.Should().NotBeNull();
        body!.UserId.Should().NotBe(Guid.Empty);
        body.DisplayName.Should().BeNull();
        body.ShowDisplayName.Should().BeTrue();
        body.ShowPhotograph.Should().BeFalse();
        body.PreferredFishingTypes.Should().BeEmpty();
        typeof(ProfileDto).GetProperty("Location").Should().BeNull();
        await _factory.ProfileRepository.Received(1).GetByUserIdAsync(
            body.UserId,
            Arg.Any<CancellationToken>());
        await _factory.ProfileRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Profile>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheCurrentUsersStoredProfileWithoutWriting()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString("N");
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ProfileRepository
            .GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok<Profile?>(
                new ProfileBuilder()
                    .WithUserId(call.ArgAt<Guid>(0))
                    .WithDisplayName("Eamonn")
                    .WithHomeRegion("Westmeath")
                    .Build()));
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: subject));

        // Act
        var response = await client.GetAsync("/api/profiles/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProfileDto>();
        body.Should().NotBeNull();
        body!.DisplayName.Should().Be("Eamonn");
        body.HomeRegion.Should().Be("Westmeath");
        await _factory.ProfileRepository.Received(1).GetByUserIdAsync(
            body.UserId,
            Arg.Any<CancellationToken>());
        await _factory.ProfileRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Profile>(),
            Arg.Any<CancellationToken>());
    }
}
