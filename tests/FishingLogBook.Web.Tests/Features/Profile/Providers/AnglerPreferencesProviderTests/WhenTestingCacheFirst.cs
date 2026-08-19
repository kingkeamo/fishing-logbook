using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Features.Profile.Models;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Profile.Providers.AnglerPreferencesProviderTests;

public class WhenTestingCacheFirst : BaseAnglerPreferencesProviderTest
{
    [Fact]
    public async Task ItShouldReturnTheCachedPreferencesWithoutWaitingForTheApi()
    {
        // Arrange
        MockCache.GetAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns(CachedPreferences());
        MockProfileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(new TaskCompletionSource<ProfileDto>().Task);

        // Act
        var result = await Sut.GetAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        result.WeightUnit.Should().Be(WeightUnitEnum.Lb);
        result.LengthUnit.Should().Be(LengthUnitEnum.In);
        result.Catalogue.AllSpecies.Should().ContainSingle(species => species.Name == "Brown Trout");
        await MockCache.Received(1).GetAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await MockCache.DidNotReceive().SaveAsync(
            Arg.Any<Guid>(),
            Arg.Any<AnglerPreferencesModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRefreshInTheBackgroundWhenNothingWasCached()
    {
        // Arrange
        GivenOnlineProfile();
        MockCache.GetAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns((AnglerPreferencesModel?)null);

        // Act
        var result = await Sut.GetAsync(CancellationToken.None);
        await Task.Delay(50);

        // Assert
        result.WeightUnit.Should().Be(WeightUnitEnum.Lb);
        await MockProfileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
        await MockFishingPreferenceClient.Received(1).GetCatalogueAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReplaceTheCachedPreferencesOnceTheBackgroundRefreshCompletes()
    {
        // Arrange
        MockCache.GetAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns(CachedPreferences());
        GivenOnlineProfile(WeightUnitEnum.Kg, LengthUnitEnum.Cm);

        // Act
        var cached = await Sut.GetAsync(CancellationToken.None);
        var refreshed = await WaitForRefreshedWeightUnitAsync(WeightUnitEnum.Kg);

        // Assert
        cached.WeightUnit.Should().Be(WeightUnitEnum.Lb);
        refreshed.WeightUnit.Should().Be(WeightUnitEnum.Kg);
        refreshed.LengthUnit.Should().Be(LengthUnitEnum.Cm);
        await MockProfileClient.Received(1).GetOwnAsync(Arg.Any<CancellationToken>());
        await MockCache.Received(1).SaveAsync(
            OwnerUserId,
            Arg.Is<AnglerPreferencesModel>(preferences => preferences.WeightUnit == WeightUnitEnum.Kg),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotCacheABackgroundRefreshUnderTheOldOwnerWhenTheAccountChanges()
    {
        // Arrange
        var apiCalled = new TaskCompletionSource();
        var apiReleased = new TaskCompletionSource<ProfileDto>();
        MockCache.GetAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns(CachedPreferences());
        GivenOnlineProfile(WeightUnitEnum.Kg, LengthUnitEnum.Cm);
        MockProfileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                apiCalled.TrySetResult();
                return apiReleased.Task;
            });
        var cached = await Sut.GetAsync(CancellationToken.None);
        await apiCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        MockLocalCatchOwner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(OtherUserId);
        apiReleased.SetResult(OnlineProfile(OtherUserId, WeightUnitEnum.Kg, LengthUnitEnum.Cm));
        await Sut.SetAsync(OtherUserId, SavedPreferences(), CancellationToken.None);

        // Assert
        cached.WeightUnit.Should().Be(WeightUnitEnum.Lb);
        await MockCache.DidNotReceive().SaveAsync(
            OwnerUserId,
            Arg.Any<AnglerPreferencesModel>(),
            Arg.Any<CancellationToken>());
        await MockCache.Received(1).SaveAsync(
            OtherUserId,
            Arg.Any<AnglerPreferencesModel>(),
            Arg.Any<CancellationToken>());
    }

    private async Task<AnglerPreferencesModel> WaitForRefreshedWeightUnitAsync(WeightUnitEnum expected)
    {
        AnglerPreferencesModel latest;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            latest = await Sut.GetAsync(CancellationToken.None);
            if (latest.WeightUnit == expected)
            {
                return latest;
            }

            await Task.Delay(20);
        }

        return await Sut.GetAsync(CancellationToken.None);
    }
}
