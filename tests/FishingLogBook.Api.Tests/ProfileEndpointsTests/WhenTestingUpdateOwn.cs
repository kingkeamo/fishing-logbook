using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.ProfileEndpointsTests;

public class WhenTestingUpdateOwn : IClassFixture<SystemApiFactory>
{
    private readonly SystemApiFactory _factory;

    public WhenTestingUpdateOwn(SystemApiFactory factory)
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
        var response = await client.PutAsJsonAsync("/api/profiles/me", ValidRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.ProfileRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Profile>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnServiceUnavailableWhenTheProfileCannotBeLoaded()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString("N");
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ProfileRepository
            .GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(null));
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: subject));
        var own = await client.GetFromJsonAsync<ProfileDto>("/api/profiles/me");
        own.Should().NotBeNull();
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ProfileRepository
            .GetByUserIdAsync(own!.UserId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Profile?>("Failed to load angler profile."));

        // Act
        var response = await client.PutAsJsonAsync("/api/profiles/me", ValidRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.ProfileRepository.Received(1).GetByUserIdAsync(
            own.UserId,
            Arg.Any<CancellationToken>());
        await _factory.ProfileRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Profile>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnServiceUnavailableWhenTheProfileCannotBeSaved()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString("N");
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ProfileRepository
            .GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(null));
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: subject));
        var own = await client.GetFromJsonAsync<ProfileDto>("/api/profiles/me");
        own.Should().NotBeNull();
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ProfileRepository
            .GetByUserIdAsync(own!.UserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(null));
        _factory.ProfileRepository
            .UpsertAsync(Arg.Is<Profile>(profile => profile.UserId == own.UserId), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Profile>("Failed to save angler profile."));

        // Act
        var response = await client.PutAsJsonAsync("/api/profiles/me", ValidRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.ProfileRepository.Received(1).GetByUserIdAsync(
            own.UserId,
            Arg.Any<CancellationToken>());
        await _factory.ProfileRepository.Received(1).UpsertAsync(
            Arg.Is<Profile>(profile => profile.UserId == own.UserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreAClientSuppliedUserIdAndUpdateTheAuthenticatedUser()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString("N");
        var otherUserId = Guid.NewGuid();
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ProfileRepository
            .GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(null));
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: subject));
        var own = await client.GetFromJsonAsync<ProfileDto>("/api/profiles/me");
        own.Should().NotBeNull();
        _factory.ProfileRepository.ClearReceivedCalls();
        var payload = JsonSerializer.Serialize(new
        {
            userId = otherUserId,
            displayName = "Eamonn",
            homeRegion = "Westmeath",
            showDisplayName = true,
            showPhotograph = false,
            showHomeRegion = true,
            showPreferredFishingMethods = true,
            showPreferredSpecies = false
        });

        // Act
        var response = await client.PutAsync(
            "/api/profiles/me",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProfileDto>();
        body.Should().NotBeNull();
        body!.UserId.Should().Be(own!.UserId);
        body.UserId.Should().NotBe(otherUserId);
        body.DisplayName.Should().Be("Eamonn");
        await _factory.ProfileRepository.Received(1).UpsertAsync(
            Arg.Is<Profile>(profile =>
                profile.UserId == own.UserId
                && profile.DisplayName == "Eamonn"
                && profile.HomeRegion == "Westmeath"
                && profile.ShowDisplayName
                && !profile.ShowPhotograph
                && profile.ShowHomeRegion
                && profile.ShowPreferredFishingMethods
                && !profile.ShowPreferredSpecies),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveTheCurrentUsersProfileFields()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString("N");
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ProfileRepository
            .GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(null));
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: subject));
        var own = await client.GetFromJsonAsync<ProfileDto>("/api/profiles/me");
        own.Should().NotBeNull();
        _factory.ProfileRepository.ClearReceivedCalls();

        // Act
        var response = await client.PutAsJsonAsync("/api/profiles/me", ValidRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProfileDto>();
        body.Should().NotBeNull();
        body!.UserId.Should().Be(own!.UserId);
        body.DisplayName.Should().Be("Eamonn");
        body.HomeRegion.Should().Be("Westmeath");
        typeof(ProfileDto).GetProperty("Latitude").Should().BeNull();
        await _factory.ProfileRepository.Received(1).GetByUserIdAsync(
            own.UserId,
            Arg.Any<CancellationToken>());
        await _factory.ProfileRepository.Received(1).UpsertAsync(
            Arg.Is<Profile>(profile =>
                profile.UserId == own.UserId
                && profile.DisplayName == "Eamonn"
                && profile.HomeRegion == "Westmeath"),
            Arg.Any<CancellationToken>());
    }

    private static UpdateProfileDto ValidRequest()
    {
        return new UpdateProfileDto(
            "Eamonn",
            "Westmeath",
            true,
            false,
            true,
            true,
            false);
    }
}
