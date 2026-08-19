using AwesomeAssertions;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Features.Profile.Models;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Profile.Services.AnglerPreferencesProviderTests;

public class WhenTestingInvalidate : BaseAnglerPreferencesProviderTest
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
    public async Task ItShouldReloadFromTheApiAfterInvalidation()
    {
        // Arrange
        GivenOnlineProfile();
        await Sut.GetAsync(CancellationToken.None);
        GivenOnlineProfile(WeightUnitEnum.Kg, LengthUnitEnum.Cm);

        // Act
        Sut.Invalidate();
        var reloaded = await Sut.GetAsync(CancellationToken.None);

        // Assert
        reloaded.WeightUnit.Should().Be(WeightUnitEnum.Kg);
        reloaded.LengthUnit.Should().Be(LengthUnitEnum.Cm);
        await MockProfileClient.Received(2).GetOwnAsync(Arg.Any<CancellationToken>());
        await MockFishingPreferenceClient.Received(2).GetCatalogueAsync(Arg.Any<CancellationToken>());
    }
}
