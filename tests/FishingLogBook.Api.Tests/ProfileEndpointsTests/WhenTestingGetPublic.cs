using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Tests.Common.Builders;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.ProfileEndpointsTests;

public class WhenTestingGetPublic : IClassFixture<SystemApiFactory>
{
    private readonly SystemApiFactory _factory;

    public WhenTestingGetPublic(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldHidePrivateCoordinatesFromAnotherUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var profile = new ProfileBuilder()
            .WithUserId(userId)
            .WithDisplayName("Eamonn")
            .ShowAll()
            .WithLocation(new CatchLocationDto(
                53.4,
                -7.9,
                8,
                DateTimeOffset.Parse("2026-08-16T12:00:00Z"),
                LocationDefaults.DeviceGps,
                LocationDefaults.Private,
                LocationDefaults.ConsentVersion))
            .Build();
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ProfileRepository
            .UserExistsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(true));
        _factory.ProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(profile));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync($"/api/profiles/{userId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PublicProfileDto>();
        body.Should().NotBeNull();
        body!.DisplayName.Should().Be("Eamonn");
        body.Location.Should().BeNull();
        await _factory.ProfileRepository.Received(1).GetByUserIdAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNotFoundWhenTheUserDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ProfileRepository
            .UserExistsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(false));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync($"/api/profiles/{userId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _factory.ProfileRepository.DidNotReceive().GetByUserIdAsync(
            userId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIncludePreciseCoordinatesWhenVisibilityIsPublic()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var profile = new ProfileBuilder()
            .WithUserId(userId)
            .WithDisplayName("Eamonn")
            .ShowAll()
            .WithLocation(new CatchLocationDto(
                53.4,
                -7.9,
                8,
                DateTimeOffset.Parse("2026-08-16T12:00:00Z"),
                LocationDefaults.DeviceGps,
                LocationDefaults.Public,
                LocationDefaults.ConsentVersion))
            .Build();
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ProfileRepository
            .UserExistsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(true));
        _factory.ProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(profile));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync($"/api/profiles/{userId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PublicProfileDto>();
        body.Should().NotBeNull();
        body!.Location.Should().NotBeNull();
        body.Location!.Latitude.Should().Be(53.4);
        body.Location.Longitude.Should().Be(-7.9);
        await _factory.ProfileRepository.Received(1).GetByUserIdAsync(userId, Arg.Any<CancellationToken>());
    }
}
