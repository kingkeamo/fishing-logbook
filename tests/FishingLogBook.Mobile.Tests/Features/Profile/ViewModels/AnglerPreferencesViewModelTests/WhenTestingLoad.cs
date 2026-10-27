using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.ViewModels;
using FishingLogBook.Mobile.Presentation;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Mobile.Tests.Features.Profile.ViewModels.AnglerPreferencesViewModelTests;

public class WhenTestingLoad : BaseAnglerPreferencesViewModelTest
{
    [Fact]
    public async Task ItShouldShowAnOfflineStateWithRetryWhenNothingIsCachedAndTheApiIsUnreachable()
    {
        // Arrange
        RefreshFails(ApiOutcomeEnum.Unavailable);
        // Act
        await Sut.LoadAsync(CancellationToken.None);
        // Assert
        Sut.HasPreferences.Should().BeFalse();
        Sut.State.State.Should().Be(ContentStateEnum.Offline);
        Sut.State.CanRetry.Should().BeTrue();
        Sut.State.Title.Should().Be(Text["PreferencesOfflineTitle"]);
        Sut.State.Message.Should().Be(Text["PreferencesOfflineBody"]);
        await MockService.Received(1).GetCachedAsync(Arg.Any<CancellationToken>());
        await MockService.Received(1).RefreshAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldExplainAnExpiredSessionWhenNothingIsCached()
    {
        // Arrange
        RefreshFails(ApiOutcomeEnum.SessionExpired);
        // Act
        await Sut.LoadAsync(CancellationToken.None);
        // Assert
        Sut.State.State.Should().Be(ContentStateEnum.Error);
        Sut.State.CanRetry.Should().BeTrue();
        Sut.State.Title.Should().Be(Text["PreferencesSessionExpiredTitle"]);
        Sut.State.Message.Should().Be(Text["PreferencesSessionExpiredBody"]);
    }

    [Theory]
    [InlineData(ApiOutcomeEnum.ServerError)]
    [InlineData(ApiOutcomeEnum.Forbidden)]
    [InlineData(ApiOutcomeEnum.ValidationFailed)]
    [InlineData(ApiOutcomeEnum.InvalidResponse)]
    public async Task ItShouldShowARetryableErrorWhenNothingIsCachedAndTheApiFails(ApiOutcomeEnum outcome)
    {
        // Arrange
        RefreshFails(outcome);
        // Act
        await Sut.LoadAsync(CancellationToken.None);
        // Assert
        Sut.HasPreferences.Should().BeFalse();
        Sut.State.State.Should().Be(ContentStateEnum.Error);
        Sut.State.CanRetry.Should().BeTrue();
        Sut.State.Title.Should().Be(Text["PreferencesErrorTitle"]);
    }

    [Theory]
    [InlineData(ApiOutcomeEnum.Unavailable, "PreferencesOfflineCached")]
    [InlineData(ApiOutcomeEnum.SessionExpired, "PreferencesSessionExpiredCached")]
    [InlineData(ApiOutcomeEnum.ServerError, "PreferencesErrorCached")]
    public async Task ItShouldKeepShowingCachedPreferencesWithANoticeWhenTheRefreshFails(ApiOutcomeEnum outcome, string noticeKey)
    {
        // Arrange
        MockService.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());
        RefreshFails(outcome);
        // Act
        await Sut.LoadAsync(CancellationToken.None);
        // Assert
        Sut.HasPreferences.Should().BeTrue();
        Sut.State.State.Should().Be(ContentStateEnum.Ready);
        Sut.HasNotice.Should().BeTrue();
        Sut.Notice.Should().Be(Text[noticeKey]);
        Sut.Methods.Should().HaveCount(2);
    }

    [Fact]
    public async Task ItShouldShowCachedPreferencesBeforeTheRefreshCompletes()
    {
        // Arrange
        var refresh = new TaskCompletionSource<ApiResultModel<AnglerPreferencesModel>>();
        MockService.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());
        MockService.RefreshAsync(Arg.Any<CancellationToken>()).Returns(refresh.Task);
        // Act
        var load = Sut.LoadAsync(CancellationToken.None);
        // Assert
        Sut.HasPreferences.Should().BeTrue();
        Sut.State.State.Should().Be(ContentStateEnum.Ready);
        Sut.Locations.Select(item => item.Title).Should().Equal("Lough Corrib (default)", "River Moy");
        refresh.SetResult(ApiResultModel<AnglerPreferencesModel>.Succeeded(TestAnglerPreferences.Empty()));
        await load;
        Sut.HasNoLocations.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldShowTheLoadingStateWhileNothingIsCached()
    {
        // Arrange
        var refresh = new TaskCompletionSource<ApiResultModel<AnglerPreferencesModel>>();
        MockService.RefreshAsync(Arg.Any<CancellationToken>()).Returns(refresh.Task);
        // Act
        var load = Sut.LoadAsync(CancellationToken.None);
        // Assert
        Sut.State.State.Should().Be(ContentStateEnum.Loading);
        Sut.State.Title.Should().Be(Text["PreferencesLoadingTitle"]);
        refresh.SetResult(ApiResultModel<AnglerPreferencesModel>.Failed(ApiOutcomeEnum.Unavailable));
        await load;
    }

    [Fact]
    public async Task ItShouldIgnoreASecondLoadWhileTheFirstIsRunning()
    {
        // Arrange
        var refresh = new TaskCompletionSource<ApiResultModel<AnglerPreferencesModel>>();
        MockService.RefreshAsync(Arg.Any<CancellationToken>()).Returns(refresh.Task);
        var first = Sut.LoadAsync(CancellationToken.None);
        // Act
        await Sut.LoadAsync(CancellationToken.None);
        // Assert
        refresh.SetResult(ApiResultModel<AnglerPreferencesModel>.Succeeded(TestAnglerPreferences.Create()));
        await first;
        await MockService.Received(1).RefreshAsync(Arg.Any<CancellationToken>());
        Sut.HasPreferences.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldLeaveTheDisplayUnchangedWhenTheRefreshIsAbandonedForAnAccountChange()
    {
        // Arrange
        MockService.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());
        MockService.RefreshAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new OperationCanceledException());
        // Act
        await Sut.LoadAsync(CancellationToken.None);
        // Assert
        Sut.HasPreferences.Should().BeTrue();
        Sut.HasNotice.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldReplaceTheFailureWithFreshPreferencesWhenRetried()
    {
        // Arrange
        RefreshFails(ApiOutcomeEnum.Unavailable);
        await Sut.LoadAsync(CancellationToken.None);
        RefreshReturns(TestAnglerPreferences.Create());
        // Act
        await Sut.LoadAsync(CancellationToken.None);
        // Assert
        Sut.HasPreferences.Should().BeTrue();
        Sut.HasNotice.Should().BeFalse();
        Sut.State.State.Should().Be(ContentStateEnum.Ready);
        await MockService.Received(2).RefreshAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportEmptyMethodsAndLocations()
    {
        // Arrange
        RefreshReturns(TestAnglerPreferences.Empty());
        // Act
        await Sut.LoadAsync(CancellationToken.None);
        // Assert
        Sut.HasPreferences.Should().BeTrue();
        Sut.HasNoMethods.Should().BeTrue();
        Sut.HasNoLocations.Should().BeTrue();
        Sut.Methods.Should().BeEmpty();
        Sut.Locations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldFallBackToTheServerNameForAnUnknownCatalogueCode()
    {
        // Arrange
        var preferences = TestAnglerPreferences.Create() with
        {
            Preferences = new FishingPreferencesDto(
            [
                new FishingMethodPreferenceDto(Guid.NewGuid(), "Ice", "Ice fishing", false,
                    [new FishingSpeciesPreferenceDto(Guid.NewGuid(), "Zander", "Zander", false)])
            ])
        };
        RefreshReturns(preferences);
        // Act
        await Sut.LoadAsync(CancellationToken.None);
        // Assert
        var method = Sut.Methods.Should().ContainSingle().Subject;
        method.Title.Should().Be("Ice fishing");
        method.Detail.Should().Be("Zander");
    }

    [Fact]
    public async Task ItShouldRelocaliseCatalogueLabelsWithoutTranslatingLocationNames()
    {
        // Arrange
        RefreshReturns(TestAnglerPreferences.Create());
        await Sut.LoadAsync(CancellationToken.None);
        // Act
        Text.SetCulture("fr-FR");
        // Assert
        Sut.Methods.Select(item => item.Title).Should().Equal("Pêche à la mouche (par défaut)", "Pêche au lancer");
        Sut.Methods[0].Detail.Should().Be("Saumon (par défaut), Truite brune");
        Sut.Methods[1].Detail.Should().Be("Aucune espèce choisie");
        Sut.Locations.Select(item => item.Title).Should().Equal("Lough Corrib (par défaut)", "River Moy");
    }

    [Fact]
    public async Task ItShouldSummariseMethodsSpeciesDefaultsAndLocations()
    {
        // Arrange
        RefreshReturns(TestAnglerPreferences.Create());
        // Act
        await Sut.LoadAsync(CancellationToken.None);
        // Assert
        Sut.HasPreferences.Should().BeTrue();
        Sut.HasNotice.Should().BeFalse();
        Sut.State.State.Should().Be(ContentStateEnum.Ready);
        Sut.Methods.Should().Equal(
            new PreferenceSummaryItemModel("Fly (default)", "Salmon (default), Brown Trout"),
            new PreferenceSummaryItemModel("Spinning", "No species chosen"));
        Sut.Locations.Should().Equal(
            new PreferenceSummaryItemModel("Lough Corrib (default)", null),
            new PreferenceSummaryItemModel("River Moy", null));
        await MockService.Received(1).RefreshAsync(Arg.Any<CancellationToken>());
    }
}
