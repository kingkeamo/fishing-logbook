using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Presentation;
using FishingLogBook.Mobile.Tests.Support;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Profile.ViewModels.AnglerPreferencesViewModelTests;

public class WhenTestingAttach : BaseAnglerPreferencesViewModelTest
{
    [Fact]
    public async Task ItShouldClearDisplayedPreferencesWhenTheAccountChanges()
    {
        // Arrange
        RefreshReturns(TestAnglerPreferences.Create());
        await Sut.LoadAsync(CancellationToken.None);
        // Act
        ChangeAccount();
        // Assert
        Sut.HasPreferences.Should().BeFalse();
        Sut.Methods.Should().BeEmpty();
        Sut.Locations.Should().BeEmpty();
        Sut.State.State.Should().Be(ContentStateEnum.Loading);
    }

    [Fact]
    public async Task ItShouldDiscardTheFirstAccountsResultWhenItArrivesAfterTheAccountChanged()
    {
        // Arrange
        var refresh = new TaskCompletionSource<ApiResultModel<AnglerPreferencesModel>>();
        MockService.RefreshAsync(Arg.Any<CancellationToken>()).Returns(refresh.Task);
        var load = Sut.LoadAsync(CancellationToken.None);
        // Act
        ChangeAccount();
        refresh.SetResult(ApiResultModel<AnglerPreferencesModel>.Succeeded(TestAnglerPreferences.Create()));
        await load;
        // Assert
        Sut.HasPreferences.Should().BeFalse();
        Sut.Methods.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldLoadTheNextAccountEvenWhileThePreviousLoadIsStillRunning()
    {
        // Arrange
        var firstRefresh = new TaskCompletionSource<ApiResultModel<AnglerPreferencesModel>>();
        MockService.RefreshAsync(Arg.Any<CancellationToken>())
            .Returns(firstRefresh.Task, Task.FromResult(ApiResultModel<AnglerPreferencesModel>.Succeeded(TestAnglerPreferences.Empty())));
        var firstLoad = Sut.LoadAsync(CancellationToken.None);
        ChangeAccount();
        // Act
        await Sut.LoadAsync(CancellationToken.None);
        // Assert
        firstRefresh.SetResult(ApiResultModel<AnglerPreferencesModel>.Succeeded(TestAnglerPreferences.Create()));
        await firstLoad;
        await MockService.Received(2).RefreshAsync(Arg.Any<CancellationToken>());
        Sut.HasPreferences.Should().BeTrue();
        Sut.HasNoMethods.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldNotShowThePreviousAccountsPreferencesBeforeTheResetIsDispatched()
    {
        // Arrange
        var dispatched = new List<Action>();
        Sut.Attach(dispatched.Add);
        RefreshReturns(TestAnglerPreferences.Create());
        await Sut.LoadAsync(CancellationToken.None);
        ChangeAccount();
        var nextRefresh = new TaskCompletionSource<ApiResultModel<AnglerPreferencesModel>>();
        MockService.RefreshAsync(Arg.Any<CancellationToken>()).Returns(nextRefresh.Task);
        // Act
        var load = Sut.LoadAsync(CancellationToken.None);
        // Assert
        Sut.HasPreferences.Should().BeFalse();
        await MockService.Received(2).GetCachedAsync(Arg.Any<CancellationToken>());
        nextRefresh.SetResult(ApiResultModel<AnglerPreferencesModel>.Succeeded(TestAnglerPreferences.Empty()));
        await load;
        dispatched.Should().ContainSingle();
    }
}
