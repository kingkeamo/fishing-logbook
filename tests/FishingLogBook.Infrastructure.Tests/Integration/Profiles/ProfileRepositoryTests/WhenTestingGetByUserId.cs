using AwesomeAssertions;
using FishingLogBook.Infrastructure.Tests.Integration.TestSupport;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Tests.Common.Builders;

namespace FishingLogBook.Infrastructure.Tests.Integration.Profiles.ProfileRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingGetByUserId : BaseProfileRepositoryTest
{
    public WhenTestingGetByUserId(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldReturnNullWhenNoProfileExists()
    {
        // Arrange
        var userId = await CreateUserAsync();

        // Act
        var result = await Sut.GetByUserIdAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldReturnEveryMappedProfileField()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var photographId = Guid.NewGuid();
        var objectKey = $"profiles/{userId:D}/{photographId:D}";
        var profile = new ProfileBuilder()
            .WithUserId(userId)
            .WithDisplayName("Eamonn")
            .WithPhotograph(photographId, objectKey, PhotographContentTypeConstants.Webp)
            .WithHomeRegion("Westmeath")
            .WithFishingTypes("Coarse", "Fly")
            .WithSpecies("Pike")
            .ShowAll()
            .Build();
        var saved = await Sut.UpsertAsync(profile, CancellationToken.None);
        saved.IsSuccess.Should().BeTrue();

        // Act
        var result = await Sut.GetByUserIdAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.UserId.Should().Be(userId);
        result.Value.DisplayName.Should().Be("Eamonn");
        result.Value.PhotographId.Should().Be(photographId);
        result.Value.PhotographObjectKey.Should().Be(objectKey);
        result.Value.PhotographContentType.Should().Be(PhotographContentTypeConstants.Webp);
        result.Value.HomeRegion.Should().Be("Westmeath");
        result.Value.PreferredFishingTypes.Should().Equal("Coarse", "Fly");
        result.Value.PreferredSpecies.Should().Equal("Pike");
        result.Value.ShowDisplayName.Should().BeTrue();
        result.Value.ShowPhotograph.Should().BeTrue();
        result.Value.ShowHomeRegion.Should().BeTrue();
        result.Value.ShowPreferredFishingTypes.Should().BeTrue();
        result.Value.ShowPreferredSpecies.Should().BeTrue();
    }
}
