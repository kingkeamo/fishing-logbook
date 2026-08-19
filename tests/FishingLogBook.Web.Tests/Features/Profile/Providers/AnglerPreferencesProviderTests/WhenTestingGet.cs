using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Features.Profile.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Profile.Providers.AnglerPreferencesProviderTests;

public class WhenTestingGet : BaseAnglerPreferencesProviderTest
{
    [Fact]
    public async Task ItShouldReturnNothingWhenTheOwnerCannotBeResolved()
    {
        // Arrange
        MockLocalCatchOwner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(Guid.Empty);

        // Act
        var result = await Sut.GetAsync(CancellationToken.None);

        // Assert
        result.Should().BeSameAs(AnglerPreferencesModel.Empty);
        await MockCache.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await MockCache.DidNotReceive().SaveAsync(
            Arg.Any<Guid>(),
            Arg.Any<AnglerPreferencesModel>(),
            Arg.Any<CancellationToken>());
        await MockProfileClient.DidNotReceive().GetOwnAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNothingWhenTheApiFailsAndNothingIsCached()
    {
        // Arrange
        MockProfileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("offline"));
        MockCache.GetAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns((AnglerPreferencesModel?)null);

        // Act
        var result = await Sut.GetAsync(CancellationToken.None);

        // Assert
        result.HasCatalogue.Should().BeFalse();
        result.WeightUnit.Should().Be(WeightUnitEnum.Kg);
        result.LengthUnit.Should().Be(LengthUnitEnum.Cm);
        await MockCache.Received(1).GetAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await MockCache.DidNotReceive().SaveAsync(
            Arg.Any<Guid>(),
            Arg.Any<AnglerPreferencesModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldUseTheCachedPreferencesWhenTheApiIsUnreachable()
    {
        // Arrange
        MockProfileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("offline"));
        MockCache.GetAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns(CachedPreferences());

        // Act
        var result = await Sut.GetAsync(CancellationToken.None);

        // Assert
        result.WeightUnit.Should().Be(WeightUnitEnum.Lb);
        result.LengthUnit.Should().Be(LengthUnitEnum.In);
        result.Preferences.Methods.Should().ContainSingle(method => method.IsDefault && method.Name == "Fly");
        result.Catalogue.AllSpecies.Should().ContainSingle(species => species.Name == "Brown Trout");
        await MockCache.Received(1).GetAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await MockCache.DidNotReceive().SaveAsync(
            Arg.Any<Guid>(),
            Arg.Any<AnglerPreferencesModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotReadAnotherAnglersCachedPreferences()
    {
        // Arrange
        MockLocalCatchOwner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(OtherUserId);
        MockProfileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("offline"));
        MockCache.GetAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns(CachedPreferences());
        MockCache.GetAsync(OtherUserId, Arg.Any<CancellationToken>())
            .Returns((AnglerPreferencesModel?)null);

        // Act
        var result = await Sut.GetAsync(CancellationToken.None);

        // Assert
        result.HasCatalogue.Should().BeFalse();
        result.WeightUnit.Should().Be(WeightUnitEnum.Kg);
        await MockCache.Received(1).GetAsync(OtherUserId, Arg.Any<CancellationToken>());
        await MockCache.DidNotReceive().GetAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillReturnFreshPreferencesWhenCachingFails()
    {
        // Arrange
        GivenOnlineProfile();
        MockCache.SaveAsync(
                Arg.Any<Guid>(),
                Arg.Any<AnglerPreferencesModel>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("quota exceeded"));

        // Act
        var result = await Sut.GetAsync(CancellationToken.None);

        // Assert
        result.WeightUnit.Should().Be(WeightUnitEnum.Lb);
        result.Catalogue.Methods.Should().ContainSingle();
        await MockCache.Received(1).GetAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefreshTheCacheForTheOwnerWhenOnline()
    {
        // Arrange
        GivenOnlineProfile();

        // Act
        var result = await Sut.GetAsync(CancellationToken.None);

        // Assert
        result.WeightUnit.Should().Be(WeightUnitEnum.Lb);
        result.LengthUnit.Should().Be(LengthUnitEnum.In);
        result.Preferences.Methods.Should().ContainSingle(method => method.FishingMethodId == FlyMethodId);
        await MockProfileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
        await MockFishingPreferenceClient.Received(1).GetCatalogueAsync(Arg.Any<CancellationToken>());
        await MockFishingPreferenceClient.Received(1).GetPreferencesAsync(Arg.Any<CancellationToken>());
        await MockCache.Received(1).SaveAsync(
            OwnerUserId,
            Arg.Is<AnglerPreferencesModel>(preferences =>
                preferences.WeightUnit == WeightUnitEnum.Lb
                && preferences.LengthUnit == LengthUnitEnum.In
                && preferences.Catalogue.Methods.Count == 1
                && preferences.Preferences.Methods.Count == 1),
            Arg.Any<CancellationToken>());
        await MockCache.Received(1).GetAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }
}
