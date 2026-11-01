using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripDetailViewModelTests;

public sealed class WhenTestingFinish : BaseTripDetailViewModelTest
{
    [Fact]
    public async Task ItShouldNotLetAParticipantFinishTheTrip()
    {
        // Arrange
        await LoadAsync(Trip(role: TripParticipantConstants.Participant, owner: FriendId));
        // Act
        await Sut.FinishAsync(CancellationToken.None);
        // Assert
        Sut.CanManageTrip.Should().BeFalse();
        await MockTrips.DidNotReceive().UpdateAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripViewDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotFinishATripThatIsAlreadyFinished()
    {
        // Arrange
        await LoadAsync(Trip(TripConstants.Completed, endedOn: Now.AddHours(-1)));
        // Act
        await Sut.FinishAsync(CancellationToken.None);
        // Assert
        await MockTrips.DidNotReceive().UpdateAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripViewDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowAnErrorWhenTheTripCannotBeFinished()
    {
        // Arrange
        await LoadAsync(Trip());
        MockTrips.UpdateAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripViewDto>(), Arg.Any<CancellationToken>())
            .Returns<LocalTripRecordModel>(_ => throw new ArgumentException());
        // Act
        await Sut.FinishAsync(CancellationToken.None);
        // Assert
        Sut.MessageText.Should().Be(Text["TripEditSaveFailed"]);
        Sut.Active!.MessageText.Should().Be(Text["TripEditSaveFailed"]);
    }

    [Fact]
    public async Task ItShouldFinishTheTripNowKeepingItsTitleAndReloadIt()
    {
        // Arrange
        await LoadAsync(Trip(title: "Corrib"));
        MockTrips.ClearReceivedCalls();
        // Act
        await Sut.FinishAsync(CancellationToken.None);
        // Assert
        await MockTrips.Received(1).UpdateAsync(
            Arg.Any<LocalTripRecordModel>(),
            Arg.Is<TripViewDto>(trip => trip.Status == TripConstants.Completed && trip.EndedOn == Now && trip.Title == "Corrib"),
            Arg.Any<CancellationToken>());
        await MockTrips.Received(1).GetAsync(TripId, Arg.Any<CancellationToken>());
    }
}
