using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Domain.Catalogue;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.FishingPreferenceEndpointsTests;

public class WhenTestingGetCatalogue : IClassFixture<SystemApiFactory>
{
    private readonly SystemApiFactory _factory;

    public WhenTestingGetCatalogue(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectTheRequestWhenAuthorizationIsMissing()
    {
        // Arrange
        _factory.FishingCatalogueRepository.ClearReceivedCalls();
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/fishing-catalogue");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.FishingCatalogueRepository.DidNotReceive().GetAllMethodsAsync(
            Arg.Any<CancellationToken>());
        await _factory.FishingCatalogueRepository.DidNotReceive().GetAllSpeciesAsync(
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnServiceUnavailableWhenTheCatalogueCannotBeLoaded()
    {
        // Arrange
        _factory.FishingCatalogueRepository.ClearReceivedCalls();
        _factory.FishingCatalogueRepository
            .GetAllMethodsAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<FishingMethod>>("Failed to load fishing method catalogue."));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/fishing-catalogue");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.FishingCatalogueRepository.Received(1).GetAllMethodsAsync(
            Arg.Any<CancellationToken>());
        await _factory.FishingCatalogueRepository.DidNotReceive().GetAllSpeciesAsync(
            Arg.Any<CancellationToken>());
        _factory.ResetFishingCatalogue();
    }

    [Fact]
    public async Task ItShouldReturnBothCatalogues()
    {
        // Arrange
        _factory.ResetFishingCatalogue();
        _factory.FishingCatalogueRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/fishing-catalogue");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var catalogue = await response.Content.ReadFromJsonAsync<FishingCatalogueDto>();
        catalogue.Should().NotBeNull();
        catalogue!.Methods.Should().HaveCount(2);
        catalogue.Methods.Should().ContainSingle(method =>
            method.Id == SystemApiFactory.FlyMethodId && method.Code == "Fly");
        catalogue.AllSpecies.Should().HaveCount(2);
        catalogue.AllSpecies.Should().ContainSingle(species =>
            species.Id == SystemApiFactory.BrownTroutSpeciesId && species.Name == "Brown Trout");
        await _factory.FishingCatalogueRepository.Received(1).GetAllMethodsAsync(
            Arg.Any<CancellationToken>());
        await _factory.FishingCatalogueRepository.Received(1).GetAllSpeciesAsync(
            Arg.Any<CancellationToken>());
    }
}
