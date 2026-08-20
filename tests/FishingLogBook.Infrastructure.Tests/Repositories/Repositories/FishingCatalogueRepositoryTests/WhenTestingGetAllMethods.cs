using AwesomeAssertions;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.FishingCatalogueRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingGetAllMethods : BaseFishingCatalogueRepositoryTest
{
    public WhenTestingGetAllMethods(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldReturnTheSeededMethodsOrderedByName()
    {
        // Arrange
        // Act
        var result = await Sut.GetAllMethodsAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Select(method => method.Name).Should().BeInAscendingOrder();
        result.Value.Should().ContainSingle(method => method.Code == "Fly" && method.Name == "Fly");
        result.Value.Should().OnlyContain(method => method.Id != Guid.Empty);
        result.Value.Should().OnlyContain(method => method.CreatedOn != default);
    }
}
