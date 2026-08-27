using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Domain.FishingLocations;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.FishingLocationEndpointsTests;

public class WhenTestingGetLocations : IClassFixture<SystemApiFactory>
{
    private const string ResourcePath = "/api/profiles/me/fishing-locations";

    private readonly SystemApiFactory _factory;

    public WhenTestingGetLocations(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectTheRequestWhenAuthorizationIsMissing()
    {
        // Arrange
        _factory.ResetFishingLocations();
        _factory.FishingLocationPreferenceRepository.ClearReceivedCalls();
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(ResourcePath);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.FishingLocationPreferenceRepository.DidNotReceive().GetByUserIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportServiceUnavailableWhenTheRepositoryFails()
    {
        // Arrange
        _factory.ResetFishingLocations();
        _factory.FishingLocationPreferenceRepository
            .GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<UserFishingLocationPreference>>(
                "Failed to load fishing locations."));
        _factory.FishingLocationPreferenceRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync(ResourcePath);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.FishingLocationPreferenceRepository.Received(1).GetByUserIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNoLocationsWhenTheAnglerHasSavedNone()
    {
        // Arrange
        _factory.ResetFishingLocations();
        _factory.FishingLocationPreferenceRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync(ResourcePath);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var locations = await response.Content.ReadFromJsonAsync<FishingLocationPreferencesDto>();
        locations!.Locations.Should().BeEmpty();
        await _factory.FishingLocationPreferenceRepository.Received(1).GetByUserIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnOnlyTheAuthenticatedAnglersLocations()
    {
        // Arrange
        _factory.ResetFishingLocations();
        var corribId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        _factory.FishingLocationPreferenceRepository
            .GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok<IReadOnlyList<UserFishingLocationPreference>>(
            [
                new UserFishingLocationPreference
                {
                    Id = corribId,
                    UserId = call.ArgAt<Guid>(0),
                    Name = "Lough Corrib",
                    IsDefault = true,
                    CreatedOn = DateTimeOffset.Parse("2026-08-27T09:00:00Z")
                }
            ]));
        _factory.FishingLocationPreferenceRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync(ResourcePath);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var locations = await response.Content.ReadFromJsonAsync<FishingLocationPreferencesDto>();
        locations!.Locations.Single().Should().BeEquivalentTo(
            new FishingLocationPreferenceDto(corribId, "Lough Corrib", true));
        await _factory.FishingLocationPreferenceRepository.Received(1).GetByUserIdAsync(
            Arg.Is<Guid>(userId => userId != Guid.Empty),
            Arg.Any<CancellationToken>());
    }
}
