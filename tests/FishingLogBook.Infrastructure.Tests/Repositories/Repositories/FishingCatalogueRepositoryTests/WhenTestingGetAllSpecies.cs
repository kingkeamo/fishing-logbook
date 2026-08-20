using AwesomeAssertions;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.FishingCatalogueRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingGetAllSpecies : BaseFishingCatalogueRepositoryTest
{
    public WhenTestingGetAllSpecies(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldReturnTheSeededSpeciesOrderedByName()
    {
        // Arrange
        // Act
        var result = await Sut.GetAllSpeciesAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Select(species => species.Name).Should().BeInAscendingOrder();
        result.Value.Should().ContainSingle(species =>
            species.Code == "BrownTrout" && species.Name == "Brown Trout");
        result.Value.Should().ContainSingle(species => species.Code == "RainbowTrout");
        result.Value.Should().OnlyContain(species => species.Id != Guid.Empty);
    }
}
