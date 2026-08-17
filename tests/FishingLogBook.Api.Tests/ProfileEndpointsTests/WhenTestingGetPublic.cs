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
    public async Task ItShouldRejectTheRequestWhenAuthorizationIsMissing()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _factory.ProfileRepository.ClearReceivedCalls();
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/profiles/{userId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.ProfileRepository.DidNotReceive().UserExistsAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await _factory.ProfileRepository.DidNotReceive().GetByUserIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
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
        await _factory.ProfileRepository.Received(1).UserExistsAsync(userId, Arg.Any<CancellationToken>());
        await _factory.ProfileRepository.DidNotReceive().GetByUserIdAsync(
            userId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotMapAGenericFailureMessageToNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ProfileRepository
            .UserExistsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(true));
        _factory.ProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Profile?>("Angler profile was not found."));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync($"/api/profiles/{userId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.ProfileRepository.Received(1).UserExistsAsync(userId, Arg.Any<CancellationToken>());
        await _factory.ProfileRepository.Received(1).GetByUserIdAsync(
            userId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnServiceUnavailableWhenTheRepositoryFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ProfileRepository
            .UserExistsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<bool>("Failed to load angler profile."));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync($"/api/profiles/{userId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.ProfileRepository.Received(1).UserExistsAsync(userId, Arg.Any<CancellationToken>());
        await _factory.ProfileRepository.DidNotReceive().GetByUserIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnADefaultPublicProfileWhenTheUserHasNoProfileRow()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ProfileRepository
            .UserExistsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(true));
        _factory.ProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(null));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync($"/api/profiles/{userId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PublicProfileDto>();
        body.Should().NotBeNull();
        body!.UserId.Should().Be(userId);
        body.DisplayName.Should().BeNull();
        body.HomeRegion.Should().BeNull();
        body.PreferredFishingTypes.Should().BeEmpty();
        typeof(PublicProfileDto).GetProperty("Location").Should().BeNull();
        await _factory.ProfileRepository.Received(1).GetByUserIdAsync(userId, Arg.Any<CancellationToken>());
        await _factory.ProfileRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Profile>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnVisibilityFilteredFieldsForAnotherUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var profile = new ProfileBuilder()
            .WithUserId(userId)
            .WithDisplayName("Eamonn")
            .WithHomeRegion("Westmeath")
            .WithFishingTypes("Fly")
            .WithSpecies("Pike")
            .ShowAll()
            .HideSpecies()
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
        body!.UserId.Should().Be(userId);
        body.DisplayName.Should().Be("Eamonn");
        body.HomeRegion.Should().Be("Westmeath");
        body.PreferredFishingTypes.Should().Equal("Fly");
        body.PreferredSpecies.Should().BeEmpty();
        typeof(PublicProfileDto).GetProperty("Latitude").Should().BeNull();
        typeof(PublicProfileDto).GetProperty("Longitude").Should().BeNull();
        await _factory.ProfileRepository.Received(1).GetByUserIdAsync(userId, Arg.Any<CancellationToken>());
    }
}
