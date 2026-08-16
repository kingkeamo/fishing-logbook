using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Shared.Dtos;
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
        var request = new UpdateProfileDto(
            "Eamonn",
            null,
            [],
            [],
            true,
            false,
            false,
            false,
            false);

        // Act
        var response = await client.PutAsJsonAsync("/api/profiles/me", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.ProfileRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Profile>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveProfileFieldsIncludingPrivateLocation()
    {
        // Arrange
        _factory.ProfileRepository.ClearReceivedCalls();
        var capturedOn = DateTimeOffset.Parse("2026-08-16T12:00:00Z");
        var request = new UpdateProfileDto(
            "Eamonn",
            "Westmeath",
            ["Coarse"],
            ["Pike"],
            true,
            false,
            true,
            true,
            false,
            new CatchLocationDto(
                53.4,
                -7.9,
                10,
                capturedOn,
                LocationDefaults.DeviceGps,
                LocationDefaults.Private,
                LocationDefaults.ConsentVersion));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PutAsJsonAsync("/api/profiles/me", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProfileDto>();
        body.Should().NotBeNull();
        body!.DisplayName.Should().Be("Eamonn");
        body.HomeRegion.Should().Be("Westmeath");
        body.Location!.Visibility.Should().Be(LocationDefaults.Private);
        await _factory.ProfileRepository.Received(1).UpsertAsync(
            Arg.Is<Profile>(profile =>
                profile.DisplayName == "Eamonn"
                && profile.HomeRegion == "Westmeath"
                && profile.Latitude == 53.4
                && profile.LocationVisibility == LocationDefaults.Private),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnUnknownFishingType()
    {
        // Arrange
        _factory.ProfileRepository.ClearReceivedCalls();
        var request = new UpdateProfileDto(
            "Eamonn",
            null,
            ["NotAType"],
            [],
            true,
            false,
            false,
            false,
            false);
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PutAsJsonAsync("/api/profiles/me", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.ProfileRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Profile>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSavePublicLocationWhenTheUserChoseToShare()
    {
        // Arrange
        _factory.ProfileRepository.ClearReceivedCalls();
        var capturedOn = DateTimeOffset.Parse("2026-08-16T12:00:00Z");
        var request = new UpdateProfileDto(
            "Eamonn",
            "Westmeath",
            ["Fly"],
            ["Pike"],
            true,
            false,
            true,
            true,
            false,
            new CatchLocationDto(
                53.4,
                -7.9,
                10,
                capturedOn,
                LocationDefaults.DeviceGps,
                LocationDefaults.Public,
                LocationDefaults.ConsentVersion));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PutAsJsonAsync("/api/profiles/me", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProfileDto>();
        body.Should().NotBeNull();
        body!.Location!.Visibility.Should().Be(LocationDefaults.Public);
        await _factory.ProfileRepository.Received(1).UpsertAsync(
            Arg.Is<Profile>(profile =>
                profile.LocationVisibility == LocationDefaults.Public
                && profile.Latitude == 53.4),
            Arg.Any<CancellationToken>());
    }
}
