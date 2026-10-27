using System.Net;
using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Clients.FishingPreferenceClientTests;

public class WhenTestingGetCatalogue : BaseFishingPreferenceClientTest
{
    [Fact]
    public async Task ItShouldReturnTheFailureOutcomeWhenTheCatalogueIsUnavailable()
    {
        // Arrange
        var (sut, server) = CreateClient(_ => Json("{\"title\":\"Database unavailable\"}", HttpStatusCode.ServiceUnavailable));
        // Act
        var result = await sut.GetCatalogueAsync(CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(ApiOutcomeEnum.ServerError);
        result.Value.Should().BeNull();
        server.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldReadTheCatalogueFromTheExistingEndpoint()
    {
        // Arrange
        var flyId = Guid.Parse("6cebaa41-de9c-4264-8034-03636b05f3c0");
        var salmonId = Guid.Parse("7ff7ce2c-59da-451e-afcc-66bee96ee267");
        var (sut, server) = CreateClient(_ => Json(
            $$"""{"methods":[{"id":"{{flyId}}","code":"Fly","name":"Fly"}],"allSpecies":[{"id":"{{salmonId}}","code":"Salmon","name":"Salmon"}]}"""));
        // Act
        var result = await sut.GetCatalogueAsync(CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(ApiOutcomeEnum.Success);
        result.Value!.Methods.Should().Equal(new FishingMethodDto(flyId, "Fly", "Fly"));
        result.Value.AllSpecies.Should().Equal(new SpeciesDto(salmonId, "Salmon", "Salmon"));
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.Uri.Should().Be(new Uri("https://api.example.com/api/fishing-catalogue"));
        request.Authorization.Should().Be("Bearer " + AccessToken);
    }
}
