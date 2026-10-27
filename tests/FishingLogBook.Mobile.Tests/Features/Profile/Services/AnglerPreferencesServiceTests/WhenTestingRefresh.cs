using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Services.AnglerPreferencesServiceTests;

public class WhenTestingRefresh : BaseAnglerPreferencesServiceTest
{
    [Fact]
    public async Task ItShouldReportAnExpiredSessionWithoutCallingTheApiWhenNobodyIsSignedIn()
    {
        // Arrange
        ApiReturns();
        // Act
        var result = await Sut.RefreshAsync(CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(ApiOutcomeEnum.SessionExpired);
        await MockPreferenceClient.DidNotReceive().GetCatalogueAsync(Arg.Any<CancellationToken>());
        await MockPreferenceClient.DidNotReceive().GetPreferencesAsync(Arg.Any<CancellationToken>());
        await MockLocationClient.DidNotReceive().GetAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ApiOutcomeEnum.Unavailable, ApiOutcomeEnum.Success, ApiOutcomeEnum.Success, ApiOutcomeEnum.Unavailable)]
    [InlineData(ApiOutcomeEnum.Success, ApiOutcomeEnum.ServerError, ApiOutcomeEnum.Success, ApiOutcomeEnum.ServerError)]
    [InlineData(ApiOutcomeEnum.Success, ApiOutcomeEnum.Success, ApiOutcomeEnum.Forbidden, ApiOutcomeEnum.Forbidden)]
    [InlineData(ApiOutcomeEnum.InvalidResponse, ApiOutcomeEnum.Success, ApiOutcomeEnum.Success, ApiOutcomeEnum.InvalidResponse)]
    [InlineData(ApiOutcomeEnum.ServerError, ApiOutcomeEnum.Unavailable, ApiOutcomeEnum.SessionExpired, ApiOutcomeEnum.SessionExpired)]
    [InlineData(ApiOutcomeEnum.ServerError, ApiOutcomeEnum.Unavailable, ApiOutcomeEnum.Success, ApiOutcomeEnum.Unavailable)]
    public async Task ItShouldKeepTheExistingCacheAndReportTheMostActionableFailure(
        ApiOutcomeEnum catalogue,
        ApiOutcomeEnum preferences,
        ApiOutcomeEnum locations,
        ApiOutcomeEnum expected)
    {
        // Arrange
        await SignInAsync(FirstUserId);
        ApiReturns(catalogue, preferences, locations);
        // Act
        var result = await Sut.RefreshAsync(CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(expected);
        result.Value.Should().BeNull();
        await MockStore.DidNotReceive().SaveAsync(Arg.Any<Guid>(), Arg.Any<AnglerPreferencesModel>(), Arg.Any<CancellationToken>());
        await MockPreferenceClient.Received(1).GetCatalogueAsync(Arg.Any<CancellationToken>());
        await MockPreferenceClient.Received(1).GetPreferencesAsync(Arg.Any<CancellationToken>());
        await MockLocationClient.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotCacheTheFirstAccountsResponseWhenTheAccountChangesMidRefresh()
    {
        // Arrange
        await SignInAsync(FirstUserId);
        ApiReturns();
        MockLocationClient.GetAsync(Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            await SignInAsync(SecondUserId);
            return ApiResultModel<FishingLocationPreferencesDto>.Succeeded(Locations);
        });
        var action = () => Sut.RefreshAsync(CancellationToken.None);
        // Act
        await action.Should().ThrowAsync<OperationCanceledException>();
        // Assert
        await MockStore.DidNotReceive().SaveAsync(Arg.Any<Guid>(), Arg.Any<AnglerPreferencesModel>(), Arg.Any<CancellationToken>());
        (await Sut.GetCachedAsync(CancellationToken.None)).Should().BeNull();
        await MockStore.Received(1).GetAsync(SecondUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillReturnFreshPreferencesWhenTheDeviceCacheCannotBeWritten()
    {
        // Arrange
        await SignInAsync(FirstUserId);
        ApiReturns();
        MockStore.SaveAsync(FirstUserId, Arg.Any<AnglerPreferencesModel>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("disk full"));
        // Act
        var result = await Sut.RefreshAsync(CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(ApiOutcomeEnum.Success);
        result.Value!.Preferences.Should().BeSameAs(Preferences);
        await MockStore.Received(1).SaveAsync(FirstUserId, Arg.Any<AnglerPreferencesModel>(), Arg.Any<CancellationToken>());
        Logger.Entries.Should().ContainSingle(entry => entry.Message.Contains(typeof(IOException).FullName!));
        (await Sut.GetCachedAsync(CancellationToken.None)).Should().BeSameAs(result.Value);
    }

    [Fact]
    public async Task ItShouldCacheTheApiResponsesForTheSignedInAccount()
    {
        // Arrange
        await SignInAsync(FirstUserId);
        ApiReturns();
        // Act
        var result = await Sut.RefreshAsync(CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(ApiOutcomeEnum.Success);
        result.Value!.Catalogue.Should().BeSameAs(Catalogue);
        result.Value.Preferences.Should().BeSameAs(Preferences);
        result.Value.Locations.Should().BeSameAs(Locations);
        await MockStore.Received(1).SaveAsync(
            FirstUserId,
            Arg.Is<AnglerPreferencesModel>(saved =>
                ReferenceEquals(saved.Catalogue, Catalogue) &&
                ReferenceEquals(saved.Preferences, Preferences) &&
                ReferenceEquals(saved.Locations, Locations)),
            Arg.Any<CancellationToken>());
        (await Sut.GetCachedAsync(CancellationToken.None)).Should().BeSameAs(result.Value);
        await MockStore.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
