using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripListViewModelTests;

public sealed class WhenTestingState : BaseTripListViewModelTest
{
    [Fact]
    public void ItShouldStartLoadingWithNothingToShow()
    {
        // Arrange
        // Act
        // Assert
        Sut.IsLoading.Should().BeTrue();
        Sut.IsEmpty.Should().BeFalse();
        Sut.CanInteract.Should().BeFalse();
        Sut.HasNotice.Should().BeFalse();
        Sut.NoticeText.Should().BeEmpty();
        Sut.HasActionFailure.Should().BeFalse();
        Sut.ActionFailureText.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotAskForInvitationsWhileTheDeviceIsOfflineEvenIfTheServerListWorked()
    {
        // Arrange
        ServeHistory();
        MockConnectivity.IsOnline.Returns(false);
        // Act
        await Sut.LoadAsync(CancellationToken.None);
        // Assert
        Sut.Invitations.Should().BeEmpty();
        await MockParticipants.DidNotReceive().ListInvitationsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreASecondStartWhileOneIsStillRunning()
    {
        // Arrange
        ServeHistory();
        await Sut.LoadAsync(CancellationToken.None);
        var release = new TaskCompletionSource<LocalTripRecordModel>();
        MockTrips.StartAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(release.Task);
        var first = Sut.StartAsync(CancellationToken.None);
        // Act
        var second = await Sut.StartAsync(CancellationToken.None);
        release.SetResult(Started(Guid.NewGuid()));
        await first;
        // Assert
        second.Should().BeNull();
        await MockTrips.Received(1).StartAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreAnInvitationResponseWhileAnotherActionIsRunning()
    {
        // Arrange
        ServeHistory();
        await Sut.LoadAsync(CancellationToken.None);
        var release = new TaskCompletionSource<LocalTripRecordModel>();
        MockTrips.StartAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(release.Task);
        var starting = Sut.StartAsync(CancellationToken.None);
        // Act
        await Sut.AcceptAsync(Guid.NewGuid(), CancellationToken.None);
        release.SetResult(Started(Guid.NewGuid()));
        await starting;
        // Assert
        await MockParticipants.DidNotReceive().RespondAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefreshTheNoticeTextWhenTheLanguageChanges()
    {
        // Arrange
        ServeHistory(serverUnavailable: true);
        MockConnectivity.IsOnline.Returns(false);
        await Sut.LoadAsync(CancellationToken.None);
        var english = Sut.NoticeText;
        var changed = new List<string?>();
        Sut.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        // Act
        Text.SetCulture("fr");
        // Assert
        changed.Should().Contain(nameof(Sut.NoticeText)).And.Contain(nameof(Sut.ActionFailureText));
        Sut.NoticeText.Should().NotBe(english);
    }
}
