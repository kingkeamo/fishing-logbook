using AwesomeAssertions;
using FishingLogBook.Mobile.Interactions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripRemovalServiceTests;

public sealed class WhenTestingRemoveCatch : BaseTripRemovalServiceTest
{
    private static readonly Guid CatchId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    [Fact]
    public async Task ItShouldLeaveTheCatchOnTheTripAndStaySilentWhenTheAnglerDeclines()
    {
        // Arrange
        AnswerConfirmation(false);
        // Act
        var removed = await Sut.RemoveCatchAsync(CatchId, "Pike", CancellationToken.None);
        // Assert
        removed.Should().BeFalse();
        await MockTripCatches.DidNotReceive().RemoveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await MockPresenter.DidNotReceive().NotifyAsync(Arg.Any<NotificationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTellTheAnglerWhenTheCatchCannotBeTakenOffTheTrip()
    {
        // Arrange
        MockTripCatches.RemoveAsync(CatchId, Arg.Any<CancellationToken>()).Returns(false);
        // Act
        var removed = await Sut.RemoveCatchAsync(CatchId, "Pike", CancellationToken.None);
        // Assert
        removed.Should().BeFalse();
        await MockPresenter.Received(1).NotifyAsync(
            Arg.Is<NotificationRequest>(request => request.Kind == NotificationKindEnum.Error && request.Message.Contains(Text["TripEditSaveFailed"])),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportAFailureInsteadOfThrowingWhenTheRemovalFails()
    {
        // Arrange
        MockTripCatches.RemoveAsync(CatchId, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException());
        // Act
        var removed = await Sut.RemoveCatchAsync(CatchId, "Pike", CancellationToken.None);
        // Assert
        removed.Should().BeFalse();
        await MockPresenter.Received(1).NotifyAsync(Arg.Is<NotificationRequest>(request => request.Kind == NotificationKindEnum.Error), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotHideACancellationOrReportItAsAFailure()
    {
        // Arrange
        MockTripCatches.RemoveAsync(CatchId, Arg.Any<CancellationToken>()).ThrowsAsync(new OperationCanceledException());
        // Act
        var act = () => Sut.RemoveCatchAsync(CatchId, "Pike", CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        await MockPresenter.DidNotReceive().NotifyAsync(Arg.Any<NotificationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNameTheCatchInTheDestructiveConfirmationThenTakeItOffTheTrip()
    {
        // Arrange
        MockTripCatches.RemoveAsync(CatchId, Arg.Any<CancellationToken>()).Returns(true);
        // Act
        var removed = await Sut.RemoveCatchAsync(CatchId, "Pike", CancellationToken.None);
        // Assert
        removed.Should().BeTrue();
        await MockPresenter.Received(1).ShowDialogAsync(
            Arg.Is<DialogRequest>(request => request.Title == Text["TripCatchRemoveTitle"] && request.Message.StartsWith("Pike")
                && request.Accept == Text["TripCatchRemoveConfirm"] && request.IsDestructive),
            Arg.Any<CancellationToken>());
        await MockTripCatches.Received(1).RemoveAsync(CatchId, Arg.Any<CancellationToken>());
        await MockPresenter.DidNotReceive().NotifyAsync(Arg.Any<NotificationRequest>(), Arg.Any<CancellationToken>());
    }
}
