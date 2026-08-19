using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Domain.Catalogue;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.FishingPreferenceEndpointsTests;

public class WhenTestingGetPreferences : IClassFixture<SystemApiFactory>
{
    private readonly SystemApiFactory _factory;

    public WhenTestingGetPreferences(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectTheRequestWhenAuthorizationIsMissing()
    {
        // Arrange
        _factory.FishingPreferenceRepository.ClearReceivedCalls();
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/profiles/me/fishing-preferences");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.FishingPreferenceRepository.DidNotReceive().GetMethodPreferencesAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnServiceUnavailableWhenThePreferencesCannotBeLoaded()
    {
        // Arrange
        _factory.ResetFishingCatalogue();
        _factory.FishingPreferenceRepository.ClearReceivedCalls();
        _factory.FishingPreferenceRepository
            .GetMethodPreferencesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<UserFishingMethodPreference>>(
                "Failed to load fishing method preferences."));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/profiles/me/fishing-preferences");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.FishingPreferenceRepository.Received(1).GetMethodPreferencesAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await _factory.FishingPreferenceRepository.DidNotReceive().GetSpeciesPreferencesAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        _factory.ResetFishingPreferences();
    }

    [Fact]
    public async Task ItShouldReturnAnEmptySelectionForANewAngler()
    {
        // Arrange
        _factory.ResetFishingCatalogue();
        _factory.ResetFishingPreferences();
        _factory.FishingPreferenceRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/profiles/me/fishing-preferences");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var preferences = await response.Content.ReadFromJsonAsync<FishingPreferencesDto>();
        preferences!.Methods.Should().BeEmpty();
        await _factory.FishingPreferenceRepository.Received(1).GetMethodPreferencesAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnOnlyTheAuthenticatedAnglersPreferences()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString("N");
        _factory.ResetFishingCatalogue();
        _factory.ResetFishingPreferences();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: subject));
        var resolved = await client.GetFromJsonAsync<ProfileDto>("/api/profiles/me");
        var userId = resolved!.UserId;
        _factory.FishingPreferenceRepository
            .GetMethodPreferencesAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<UserFishingMethodPreference>>(
            [
                new UserFishingMethodPreference
                {
                    UserId = userId,
                    FishingMethodId = SystemApiFactory.FlyMethodId,
                    IsDefault = true
                }
            ]));
        _factory.FishingPreferenceRepository
            .GetSpeciesPreferencesAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<UserFishingSpeciesPreference>>(
            [
                new UserFishingSpeciesPreference
                {
                    UserId = userId,
                    FishingMethodId = SystemApiFactory.FlyMethodId,
                    SpeciesId = SystemApiFactory.BrownTroutSpeciesId,
                    IsDefault = true
                }
            ]));
        _factory.FishingPreferenceRepository.ClearReceivedCalls();

        // Act
        var response = await client.GetAsync("/api/profiles/me/fishing-preferences");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var preferences = await response.Content.ReadFromJsonAsync<FishingPreferencesDto>();
        preferences!.Methods.Should().ContainSingle();
        preferences.Methods[0].FishingMethodId.Should().Be(SystemApiFactory.FlyMethodId);
        preferences.Methods[0].Name.Should().Be("Fly");
        preferences.Methods[0].IsDefault.Should().BeTrue();
        preferences.Methods[0].Species.Should().ContainSingle();
        preferences.Methods[0].Species[0].Name.Should().Be("Brown Trout");
        await _factory.FishingPreferenceRepository.Received(1).GetMethodPreferencesAsync(
            userId,
            Arg.Any<CancellationToken>());
        await _factory.FishingPreferenceRepository.Received(1).GetSpeciesPreferencesAsync(
            userId,
            Arg.Any<CancellationToken>());
        _factory.ResetFishingPreferences();
    }
}
