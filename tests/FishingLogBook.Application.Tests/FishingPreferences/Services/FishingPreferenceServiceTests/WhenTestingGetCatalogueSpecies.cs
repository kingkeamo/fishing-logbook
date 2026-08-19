using AwesomeAssertions;
using FishingLogBook.Domain.Catalogue;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.FishingPreferences.Services.FishingPreferenceServiceTests;

public class WhenTestingGetCatalogueSpecies : BaseFishingPreferenceServiceTest
{
    [Fact]
    public async Task ItShouldReturnFailureWhenTheCatalogueCannotBeLoaded()
    {
        // Arrange
        MockFishingCatalogueRepository
            .GetAllSpeciesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<Species>>("Failed to load species catalogue."));

        // Act
        var result = await Sut.GetCatalogueSpeciesAsync(CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to load species catalogue.");
        await MockFishingCatalogueRepository.Received(1).GetAllSpeciesAsync(Arg.Any<CancellationToken>());
        await MockFishingCatalogueRepository.DidNotReceive().GetAllMethodsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnAnEmptyListWhenTheCatalogueIsEmpty()
    {
        // Arrange
        MockFishingCatalogueRepository
            .GetAllSpeciesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<Species>>([]));

        // Act
        var result = await Sut.GetCatalogueSpeciesAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        await MockFishingCatalogueRepository.Received(1).GetAllSpeciesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMapEveryCatalogueSpecies()
    {
        // Arrange
        MockFishingCatalogueRepository
            .GetAllSpeciesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Ok(CatalogueSpecies()));

        // Act
        var result = await Sut.GetCatalogueSpeciesAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Id.Should().Be(BrownTroutSpeciesId);
        result.Value[0].Code.Should().Be("BrownTrout");
        result.Value[0].Name.Should().Be("Brown Trout");
        result.Value[1].Id.Should().Be(PikeSpeciesId);
        await MockFishingCatalogueRepository.Received(1).GetAllSpeciesAsync(Arg.Any<CancellationToken>());
        await MockFishingPreferenceRepository.DidNotReceive().GetSpeciesPreferencesAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }
}
