using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripEditViewModelTests;

public sealed class WhenTestingSave : BaseTripEditViewModelTest
{
    [Fact]
    public async Task ItShouldNotSaveATripThatTheViewerCannotManage()
    {
        // Arrange
        await LoadAsync(Trip(role: TripParticipantConstants.Participant, owner: FriendId));
        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);
        // Assert
        saved.Should().BeFalse();
        await MockTrips.DidNotReceive().UpdateAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripViewDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheEditorOpenWithAnErrorWhenSavingFails()
    {
        // Arrange
        await LoadAsync(Trip());
        MockTrips.UpdateAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripViewDto>(), Arg.Any<CancellationToken>())
            .Returns<LocalTripRecordModel>(_ => throw new ArgumentException());
        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);
        // Assert
        saved.Should().BeFalse();
        Sut.MessageText.Should().Be(Text["TripEditSaveFailed"]);
        Sut.CanInteract.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldSaveTheEditedTitleAndLocationWithoutChangingTheTripLifecycle()
    {
        // Arrange
        await LoadAsync(Trip(title: "Old"));
        Sut.EditTitle = "Corrib weekend";
        Sut.EditPlace = "Lough Corrib";
        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);
        // Assert
        saved.Should().BeTrue();
        await MockTrips.Received(1).UpdateAsync(
            Arg.Is<LocalTripRecordModel>(trip => trip.Detail.Trip.Id == TripId),
            Arg.Is<TripViewDto>(trip => trip.Id == TripId && trip.Title == "Corrib weekend" && trip.PlaceName == "Lough Corrib"
                && trip.Status == TripConstants.Active && trip.EndedOn == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveAClearedLocationAsNoLocation()
    {
        // Arrange
        await LoadAsync(Trip(place: "Lough Corrib"));
        Sut.EditPlace = string.Empty;
        // Act
        await Sut.SaveAsync(CancellationToken.None);
        // Assert
        Sut.HasEditPlace.Should().BeFalse();
        await MockTrips.Received(1).UpdateAsync(
            Arg.Any<LocalTripRecordModel>(),
            Arg.Is<TripViewDto>(trip => string.IsNullOrEmpty(trip.PlaceName)),
            Arg.Any<CancellationToken>());
    }
}
