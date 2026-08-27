using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Profile.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Profile.Providers.AnglerPreferencesProviderTests;

public class WhenTestingGetFishingLocations : BaseAnglerPreferencesProviderTest
{
    private static readonly Guid CorribId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid MoyId = Guid.Parse("dddddddd-0000-0000-0000-000000000002");

    [Fact]
    public async Task ItShouldFallBackToNoPreferencesWhenTheLocationsCannotBeLoaded()
    {
        // Arrange
        GivenOnlineProfile();
        MockFishingLocationClient.GetAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("offline"));
        MockCache.GetAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns((AnglerPreferencesModel?)null);

        // Act
        var result = await Sut.GetAsync(CancellationToken.None);

        // Assert
        result.Locations.Should().BeEmpty();
        result.DefaultLocation.Should().BeNull();
        await MockCache.DidNotReceive().SaveAsync(
            Arg.Any<Guid>(),
            Arg.Any<AnglerPreferencesModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReadTheCachedLocationsWhenTheApiIsUnreachable()
    {
        // Arrange
        MockCache.GetAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(CachedPreferences() with { Locations = SavedLocations() });
        MockFishingLocationClient.GetAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("offline"));

        // Act
        var result = await Sut.GetAsync(CancellationToken.None);

        // Assert
        result.Locations.Select(location => location.Name).Should().Equal("Lough Corrib", "River Moy");
        result.DefaultLocation!.Name.Should().Be("Lough Corrib");
        result.Preferences.Methods.Should().HaveCount(1);
        await MockCache.Received(1).GetAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepNoDefaultWhenNoSavedLocationIsTheDefault()
    {
        // Arrange
        GivenOnlineProfile();
        MockCache.GetAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns((AnglerPreferencesModel?)null);
        MockFishingLocationClient.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new FishingLocationPreferencesDto(
                [new FishingLocationPreferenceDto(MoyId, "River Moy", false)]));

        // Act
        var result = await Sut.GetAsync(CancellationToken.None);

        // Assert
        result.Locations.Should().HaveCount(1);
        result.DefaultLocation.Should().BeNull();
        await MockFishingLocationClient.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCacheTheLocationsAlongsideTheExistingPreferences()
    {
        // Arrange
        GivenOnlineProfile();
        MockCache.GetAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns((AnglerPreferencesModel?)null);
        MockFishingLocationClient.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new FishingLocationPreferencesDto(SavedLocations()));

        // Act
        var result = await Sut.GetAsync(CancellationToken.None);

        // Assert
        result.DefaultLocation!.Name.Should().Be("Lough Corrib");
        result.HasCatalogue.Should().BeTrue();
        await MockCache.Received(1).SaveAsync(
            OwnerUserId,
            Arg.Is<AnglerPreferencesModel>(preferences =>
                preferences.Locations.Count == 2 &&
                preferences.Locations[0].Id == CorribId &&
                preferences.Locations[0].IsDefault &&
                preferences.Catalogue.Methods.Count == 1 &&
                preferences.Preferences.Methods.Count == 1),
            Arg.Any<CancellationToken>());
        await MockFishingLocationClient.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    private static FishingLocationPreferenceDto[] SavedLocations()
    {
        return
        [
            new FishingLocationPreferenceDto(CorribId, "Lough Corrib", true),
            new FishingLocationPreferenceDto(MoyId, "River Moy", false)
        ];
    }
}
