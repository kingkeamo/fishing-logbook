using AwesomeAssertions;
using FishingLogBook.Domain.Catalogue;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.FishingPreferences.Services.FishingPreferenceServiceTests;

public class WhenTestingGetCatalogueMethods : BaseFishingPreferenceServiceTest
{
    [Fact]
    public async Task ItShouldReturnFailureWhenTheCatalogueCannotBeLoaded()
    {
        // Arrange
        MockFishingCatalogueRepository
            .GetAllMethodsAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<FishingMethod>>("Failed to load fishing method catalogue."));

        // Act
        var result = await Sut.GetCatalogueMethodsAsync(CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to load fishing method catalogue.");
        await MockFishingCatalogueRepository.Received(1).GetAllMethodsAsync(Arg.Any<CancellationToken>());
        await MockFishingCatalogueRepository.DidNotReceive().GetAllSpeciesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnAnEmptyListWhenTheCatalogueIsEmpty()
    {
        // Arrange
        MockFishingCatalogueRepository
            .GetAllMethodsAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<FishingMethod>>([]));

        // Act
        var result = await Sut.GetCatalogueMethodsAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        await MockFishingCatalogueRepository.Received(1).GetAllMethodsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMapEveryCatalogueMethod()
    {
        // Arrange
        MockFishingCatalogueRepository
            .GetAllMethodsAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Ok(CatalogueMethods()));

        // Act
        var result = await Sut.GetCatalogueMethodsAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Id.Should().Be(FlyMethodId);
        result.Value[0].Code.Should().Be("Fly");
        result.Value[0].Name.Should().Be("Fly");
        result.Value[1].Id.Should().Be(SpinningMethodId);
        await MockFishingCatalogueRepository.Received(1).GetAllMethodsAsync(Arg.Any<CancellationToken>());
        await MockFishingPreferenceRepository.DidNotReceive().GetMethodPreferencesAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }
}
