using System.Net;
using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Clients.FishingPreferenceClientTests;

public class WhenTestingGetPreferences : BaseFishingPreferenceClientTest
{
    [Fact]
    public async Task ItShouldReportAnExpiredSessionWhenTheApiRejectsTheToken()
    {
        // Arrange
        var (sut, server) = CreateClient(_ => Json(string.Empty, HttpStatusCode.Unauthorized));
        // Act
        var result = await sut.GetPreferencesAsync(CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(ApiOutcomeEnum.SessionExpired);
        server.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldReadMethodsSpeciesAndDefaultsFromTheExistingEndpoint()
    {
        // Arrange
        var flyId = Guid.Parse("6cebaa41-de9c-4264-8034-03636b05f3c0");
        var salmonId = Guid.Parse("7ff7ce2c-59da-451e-afcc-66bee96ee267");
        var (sut, server) = CreateClient(_ => Json(
            $$"""{"methods":[{"fishingMethodId":"{{flyId}}","code":"Fly","name":"Fly","isDefault":true,"species":[{"speciesId":"{{salmonId}}","code":"Salmon","name":"Salmon","isDefault":true}]}]}"""));
        // Act
        var result = await sut.GetPreferencesAsync(CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(ApiOutcomeEnum.Success);
        var method = result.Value!.Methods.Should().ContainSingle().Subject;
        method.FishingMethodId.Should().Be(flyId);
        method.Code.Should().Be("Fly");
        method.IsDefault.Should().BeTrue();
        var species = method.Species.Should().ContainSingle().Subject;
        species.SpeciesId.Should().Be(salmonId);
        species.Code.Should().Be("Salmon");
        species.IsDefault.Should().BeTrue();
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.Uri.Should().Be(new Uri("https://api.example.com/api/profiles/me/fishing-preferences"));
        request.Authorization.Should().Be("Bearer " + AccessToken);
    }
}
