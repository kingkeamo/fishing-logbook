using AwesomeAssertions;
using FishingLogBook.Application.FishingPreferences.Queries;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.FishingPreferences.Queries.GetFishingCatalogueQueryTests;

public class WhenTestingHandle : BaseGetFishingCatalogueQueryTest
{
    [Fact]
    public async Task ItShouldReturnFailureWhenMethodsCannotBeLoaded()
    {
        // Arrange
        MockFishingPreferenceService
            .GetCatalogueMethodsAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<FishingMethodDto>>("Failed to load fishing method catalogue."));

        // Act
        var response = await Sut.Handle(new GetFishingCatalogueQuery(), CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("Failed to load fishing method catalogue.");
        response.Methods.Should().BeNull();
        await MockFishingPreferenceService.Received(1).GetCatalogueMethodsAsync(Arg.Any<CancellationToken>());
        await MockFishingPreferenceService.DidNotReceive().GetCatalogueSpeciesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenSpeciesCannotBeLoaded()
    {
        // Arrange
        MockFishingPreferenceService
            .GetCatalogueMethodsAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<FishingMethodDto>>([]));
        MockFishingPreferenceService
            .GetCatalogueSpeciesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<SpeciesDto>>("Failed to load species catalogue."));

        // Act
        var response = await Sut.Handle(new GetFishingCatalogueQuery(), CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("Failed to load species catalogue.");
        response.AllSpecies.Should().BeNull();
        await MockFishingPreferenceService.Received(1).GetCatalogueSpeciesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnBothCatalogues()
    {
        // Arrange
        IReadOnlyList<FishingMethodDto> methods = [new FishingMethodDto(FlyMethodId, "Fly", "Fly")];
        IReadOnlyList<SpeciesDto> species = [new SpeciesDto(BrownTroutSpeciesId, "BrownTrout", "Brown Trout")];
        MockFishingPreferenceService
            .GetCatalogueMethodsAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Ok(methods));
        MockFishingPreferenceService
            .GetCatalogueSpeciesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Ok(species));

        // Act
        var response = await Sut.Handle(new GetFishingCatalogueQuery(), CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Methods.Should().BeSameAs(methods);
        response.AllSpecies.Should().BeSameAs(species);
        await MockFishingPreferenceService.Received(1).GetCatalogueMethodsAsync(Arg.Any<CancellationToken>());
        await MockFishingPreferenceService.Received(1).GetCatalogueSpeciesAsync(Arg.Any<CancellationToken>());
        await MockFishingPreferenceService.DidNotReceive().GetPreferencesAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }
}
