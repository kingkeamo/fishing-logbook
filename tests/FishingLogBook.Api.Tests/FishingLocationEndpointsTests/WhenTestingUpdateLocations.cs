using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Application.FishingLocations.Errors;
using FishingLogBook.Domain.FishingLocations;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.FishingLocationEndpointsTests;

public class WhenTestingUpdateLocations : IClassFixture<SystemApiFactory>
{
    private const string ResourcePath = "/api/profiles/me/fishing-locations";

    private readonly SystemApiFactory _factory;

    public WhenTestingUpdateLocations(SystemApiFactory factory)
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
        var response = await client.PutAsJsonAsync(ResourcePath, ValidUpdate());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.FishingLocationPreferenceRepository.DidNotReceive().ReplaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<UserFishingLocationPreference>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectABlankLocationName()
    {
        // Arrange
        _factory.ResetFishingLocations();
        _factory.FishingLocationPreferenceRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();
        var update = new UpdateFishingLocationPreferencesDto(
            [new UpdateFishingLocationPreferenceDto(Guid.Empty, "   ", false)]);

        // Act
        var response = await client.PutAsJsonAsync(ResourcePath, update);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.FishingLocationPreferenceRepository.DidNotReceive().ReplaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<UserFishingLocationPreference>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectALocationNameLongerThanTheMaximum()
    {
        // Arrange
        _factory.ResetFishingLocations();
        _factory.FishingLocationPreferenceRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();
        var update = new UpdateFishingLocationPreferencesDto(
        [
            new UpdateFishingLocationPreferenceDto(
                Guid.Empty,
                new string('a', FishingLocationConstants.MaxNameLength + 1),
                false)
        ]);

        // Act
        var response = await client.PutAsJsonAsync(ResourcePath, update);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.FishingLocationPreferenceRepository.DidNotReceive().ReplaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<UserFishingLocationPreference>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectTwoDefaultLocations()
    {
        // Arrange
        _factory.ResetFishingLocations();
        _factory.FishingLocationPreferenceRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();
        var update = new UpdateFishingLocationPreferencesDto(
        [
            new UpdateFishingLocationPreferenceDto(Guid.Empty, "Lough Corrib", true),
            new UpdateFishingLocationPreferenceDto(Guid.Empty, "River Moy", true)
        ]);

        // Act
        var response = await client.PutAsJsonAsync(ResourcePath, update);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.FishingLocationPreferenceRepository.DidNotReceive().ReplaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<UserFishingLocationPreference>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectADuplicateLocationName()
    {
        // Arrange
        _factory.ResetFishingLocations();
        _factory.FishingLocationPreferenceRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();
        var update = new UpdateFishingLocationPreferencesDto(
        [
            new UpdateFishingLocationPreferenceDto(Guid.Empty, "Lough Corrib", true),
            new UpdateFishingLocationPreferenceDto(Guid.Empty, "lough corrib", false)
        ]);

        // Act
        var response = await client.PutAsJsonAsync(ResourcePath, update);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.FishingLocationPreferenceRepository.DidNotReceive().ReplaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<UserFishingLocationPreference>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnBadRequestWhenTheDatabaseRejectsADuplicate()
    {
        // Arrange
        _factory.ResetFishingLocations();
        _factory.FishingLocationPreferenceRepository
            .ReplaceAsync(
                Arg.Any<Guid>(),
                Arg.Any<IReadOnlyList<UserFishingLocationPreference>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Fail(new DuplicateFishingLocationError(
                "A fishing location with that name is already saved.")));
        _factory.FishingLocationPreferenceRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PutAsJsonAsync(ResourcePath, ValidUpdate());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.FishingLocationPreferenceRepository.Received(1).ReplaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<UserFishingLocationPreference>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportServiceUnavailableWhenSavingFails()
    {
        // Arrange
        _factory.ResetFishingLocations();
        _factory.FishingLocationPreferenceRepository
            .ReplaceAsync(
                Arg.Any<Guid>(),
                Arg.Any<IReadOnlyList<UserFishingLocationPreference>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Fail("Failed to save fishing locations."));
        _factory.FishingLocationPreferenceRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PutAsJsonAsync(ResourcePath, ValidUpdate());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.FishingLocationPreferenceRepository.Received(1).ReplaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<UserFishingLocationPreference>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveTheLocationsOwnedByTheAuthenticatedAngler()
    {
        // Arrange
        _factory.ResetFishingLocations();
        _factory.FishingLocationPreferenceRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PutAsJsonAsync(ResourcePath, ValidUpdate());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.FishingLocationPreferenceRepository.Received(1).ReplaceAsync(
            Arg.Is<Guid>(userId => userId != Guid.Empty),
            Arg.Is<IReadOnlyList<UserFishingLocationPreference>>(locations =>
                locations.Count == 2 &&
                locations[0].Name == "Lough Corrib" &&
                locations[0].IsDefault &&
                locations[1].Name == "River Moy" &&
                !locations[1].IsDefault &&
                locations[0].UserId == locations[1].UserId &&
                locations[0].Id != Guid.Empty &&
                locations[1].Id != Guid.Empty),
            Arg.Any<CancellationToken>());
        await _factory.FishingLocationPreferenceRepository.Received(1).GetByUserIdAsync(
            Arg.Is<Guid>(userId => userId != Guid.Empty),
            Arg.Any<CancellationToken>());
    }

    private static UpdateFishingLocationPreferencesDto ValidUpdate()
    {
        return new UpdateFishingLocationPreferencesDto(
        [
            new UpdateFishingLocationPreferenceDto(Guid.Empty, "Lough Corrib", true),
            new UpdateFishingLocationPreferenceDto(Guid.Empty, "River Moy", false)
        ]);
    }
}
