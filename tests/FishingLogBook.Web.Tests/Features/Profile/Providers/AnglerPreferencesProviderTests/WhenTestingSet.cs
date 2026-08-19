using AwesomeAssertions;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Features.Profile.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Profile.Providers.AnglerPreferencesProviderTests;

public class WhenTestingSet : BaseAnglerPreferencesProviderTest
{
    [Fact]
    public async Task ItShouldNotCallTheApiAgainForASecondRead()
    {
        // Arrange
        GivenOnlineProfile();
        await Sut.GetAsync(CancellationToken.None);

        // Act
        var second = await Sut.GetAsync(CancellationToken.None);

        // Assert
        second.WeightUnit.Should().Be(WeightUnitEnum.Lb);
        second.HasCatalogue.Should().BeTrue();
        await MockProfileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
        await MockFishingPreferenceClient.Received(1).GetCatalogueAsync(Arg.Any<CancellationToken>());
        await MockFishingPreferenceClient.Received(1).GetPreferencesAsync(Arg.Any<CancellationToken>());
        await MockCache.Received(1).SaveAsync(
            OwnerUserId,
            Arg.Any<AnglerPreferencesModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotServeAnotherAnglerRememberedPreferences()
    {
        // Arrange
        GivenOnlineProfile();
        await Sut.GetAsync(CancellationToken.None);
        MockLocalCatchOwner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(OtherUserId);

        // Act
        await Sut.GetAsync(CancellationToken.None);

        // Assert
        await MockProfileClient.Received(2).GetOwnAsync(Arg.Any<CancellationToken>());
        await MockCache.Received(1).SaveAsync(
            OtherUserId,
            Arg.Any<AnglerPreferencesModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldServeTheSuppliedPreferencesEvenWhenPersistingThemFails()
    {
        // Arrange
        MockCache.GetAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns(CachedPreferences());
        MockCache.SaveAsync(
                Arg.Any<Guid>(),
                Arg.Any<AnglerPreferencesModel>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("quota exceeded"));
        var stale = await Sut.GetAsync(CancellationToken.None);

        // Act
        await Sut.SetAsync(OwnerUserId, SavedPreferences(), CancellationToken.None);
        var served = await Sut.GetAsync(CancellationToken.None);

        // Assert
        stale.WeightUnit.Should().Be(WeightUnitEnum.Lb);
        served.WeightUnit.Should().Be(WeightUnitEnum.Kg);
        served.Preferences.Methods.Should().ContainSingle(method => method.Name == "Spinning");
        await MockCache.Received(1).SaveAsync(
            OwnerUserId,
            Arg.Any<AnglerPreferencesModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreASetForAnAnglerWhoIsNotSignedIn()
    {
        // Arrange
        GivenOnlineProfile();
        await Sut.GetAsync(CancellationToken.None);

        // Act
        await Sut.SetAsync(Guid.Empty, SavedPreferences(), CancellationToken.None);
        var served = await Sut.GetAsync(CancellationToken.None);

        // Assert
        served.WeightUnit.Should().Be(WeightUnitEnum.Lb);
        await MockCache.DidNotReceive().SaveAsync(
            Guid.Empty,
            Arg.Any<AnglerPreferencesModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldServeTheSavedPreferencesToTheNextRead()
    {
        // Arrange
        GivenOnlineProfile();
        await Sut.GetAsync(CancellationToken.None);

        // Act
        await Sut.SetAsync(OwnerUserId, SavedPreferences(), CancellationToken.None);
        var served = await Sut.GetAsync(CancellationToken.None);

        // Assert
        served.WeightUnit.Should().Be(WeightUnitEnum.Kg);
        served.LengthUnit.Should().Be(LengthUnitEnum.Cm);
        served.Preferences.Methods.Should().ContainSingle(method => method.IsDefault && method.Name == "Spinning");
        await MockProfileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
        await MockCache.Received(1).SaveAsync(
            OwnerUserId,
            Arg.Is<AnglerPreferencesModel>(preferences => preferences.WeightUnit == WeightUnitEnum.Kg),
            Arg.Any<CancellationToken>());
    }
}
