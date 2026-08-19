using System.Net;
using System.Net.Http.Json;
using System.Text;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Domain.Catalogue;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.FishingPreferenceEndpointsTests;

public class WhenTestingUpdatePreferences : IClassFixture<SystemApiFactory>
{
    private readonly SystemApiFactory _factory;

    public WhenTestingUpdatePreferences(SystemApiFactory factory)
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
        var response = await client.PutAsJsonAsync(
            "/api/profiles/me/fishing-preferences",
            ValidUpdate());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.FishingPreferenceRepository.DidNotReceive().ReplacePreferencesAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<UserFishingMethodPreference>>(),
            Arg.Any<IReadOnlyList<UserFishingSpeciesPreference>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAMethodSelectedTwice()
    {
        // Arrange
        _factory.ResetFishingCatalogue();
        _factory.ResetFishingPreferences();
        _factory.FishingPreferenceRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();
        var update = new UpdateFishingPreferencesDto(
        [
            new UpdateFishingMethodPreferenceDto(SystemApiFactory.FlyMethodId, true, []),
            new UpdateFishingMethodPreferenceDto(SystemApiFactory.FlyMethodId, false, [])
        ]);

        // Act
        var response = await client.PutAsJsonAsync("/api/profiles/me/fishing-preferences", update);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.FishingPreferenceRepository.DidNotReceive().ReplacePreferencesAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<UserFishingMethodPreference>>(),
            Arg.Any<IReadOnlyList<UserFishingSpeciesPreference>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAMethodThatIsNotInTheCatalogue()
    {
        // Arrange
        _factory.ResetFishingCatalogue();
        _factory.ResetFishingPreferences();
        _factory.FishingPreferenceRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();
        var update = new UpdateFishingPreferencesDto(
            [new UpdateFishingMethodPreferenceDto(Guid.NewGuid(), true, [])]);

        // Act
        var response = await client.PutAsJsonAsync("/api/profiles/me/fishing-preferences", update);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.FishingPreferenceRepository.DidNotReceive().ReplacePreferencesAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<UserFishingMethodPreference>>(),
            Arg.Any<IReadOnlyList<UserFishingSpeciesPreference>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnServiceUnavailableWhenTheSaveFails()
    {
        // Arrange
        _factory.ResetFishingCatalogue();
        _factory.ResetFishingPreferences();
        _factory.FishingPreferenceRepository
            .ReplacePreferencesAsync(
                Arg.Any<Guid>(),
                Arg.Any<IReadOnlyList<UserFishingMethodPreference>>(),
                Arg.Any<IReadOnlyList<UserFishingSpeciesPreference>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Fail("Failed to save fishing preferences."));
        _factory.FishingPreferenceRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PutAsJsonAsync("/api/profiles/me/fishing-preferences", ValidUpdate());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.FishingPreferenceRepository.Received(1).ReplacePreferencesAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<UserFishingMethodPreference>>(),
            Arg.Any<IReadOnlyList<UserFishingSpeciesPreference>>(),
            Arg.Any<CancellationToken>());
        _factory.ResetFishingPreferences();
    }

    [Fact]
    public async Task ItShouldIgnoreAClientSuppliedUserIdAndSaveForTheAuthenticatedAngler()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString("N");
        _factory.ResetFishingCatalogue();
        _factory.ResetFishingPreferences();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: subject));
        var resolved = await client.GetFromJsonAsync<ProfileDto>("/api/profiles/me");
        var userId = resolved!.UserId;
        _factory.FishingPreferenceRepository.ClearReceivedCalls();
        var payload = $$"""
            {
              "userId": "{{Guid.NewGuid()}}",
              "methods": [
                {
                  "fishingMethodId": "{{SystemApiFactory.FlyMethodId}}",
                  "isDefault": true,
                  "species": []
                }
              ]
            }
            """;
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PutAsync("/api/profiles/me/fishing-preferences", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.FishingPreferenceRepository.Received(1).ReplacePreferencesAsync(
            userId,
            Arg.Is<IReadOnlyList<UserFishingMethodPreference>>(methods =>
                methods.Count == 1 && methods[0].UserId == userId),
            Arg.Any<IReadOnlyList<UserFishingSpeciesPreference>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveTheSelectionAndReturnThePersistedPreferences()
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
        var response = await client.PutAsJsonAsync("/api/profiles/me/fishing-preferences", ValidUpdate());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var preferences = await response.Content.ReadFromJsonAsync<FishingPreferencesDto>();
        preferences!.Methods.Should().ContainSingle();
        preferences.Methods[0].Name.Should().Be("Fly");
        preferences.Methods[0].Species[0].Name.Should().Be("Brown Trout");
        await _factory.FishingPreferenceRepository.Received(1).ReplacePreferencesAsync(
            userId,
            Arg.Is<IReadOnlyList<UserFishingMethodPreference>>(methods =>
                methods.Count == 1
                && methods[0].UserId == userId
                && methods[0].FishingMethodId == SystemApiFactory.FlyMethodId
                && methods[0].IsDefault),
            Arg.Is<IReadOnlyList<UserFishingSpeciesPreference>>(species =>
                species.Count == 1
                && species[0].UserId == userId
                && species[0].SpeciesId == SystemApiFactory.BrownTroutSpeciesId
                && species[0].IsDefault),
            Arg.Any<CancellationToken>());
        _factory.ResetFishingPreferences();
    }

    private static UpdateFishingPreferencesDto ValidUpdate()
    {
        return new UpdateFishingPreferencesDto(
        [
            new UpdateFishingMethodPreferenceDto(
                SystemApiFactory.FlyMethodId,
                true,
                [new UpdateFishingSpeciesPreferenceDto(SystemApiFactory.BrownTroutSpeciesId, true)])
        ]);
    }
}
