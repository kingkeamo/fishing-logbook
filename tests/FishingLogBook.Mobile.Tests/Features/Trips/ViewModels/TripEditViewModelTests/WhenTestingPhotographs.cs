using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripEditViewModelTests;

public sealed class WhenTestingPhotographs : BaseTripEditViewModelTest
{
    private static readonly Guid PhotoId = Guid.Parse("c0000000-0000-0000-0000-000000000001");

    private static TripPhotographViewDto Photograph(Guid by)
    {
        return new TripPhotographViewDto(PhotoId, "image/jpeg", Now.AddHours(-1), "https://img/1.jpg") { ContributedByUserId = by };
    }

    [Fact]
    public async Task ItShouldNotAddPhotographsToAFinishedTrip()
    {
        // Arrange
        await LoadAsync(Trip(TripConstants.Completed, endedOn: Now.AddHours(-1)));
        // Act
        await Sut.AddPhotographAsync(useCamera: true, CancellationToken.None);
        // Assert
        await MockPhotographs.DidNotReceive().AddAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheFailureReasonWhenThePhotographCannotBeAdded()
    {
        // Arrange
        await LoadAsync(Trip());
        MockPhotographs.AddAsync(Arg.Any<LocalTripRecordModel>(), false, Arg.Any<CancellationToken>()).Returns("PhotoTooLarge");
        // Act
        await Sut.AddPhotographAsync(useCamera: false, CancellationToken.None);
        // Assert
        Sut.MessageText.Should().Be(Text["PhotoTooLarge"]);
    }

    [Fact]
    public async Task ItShouldAddThePhotographAndReloadTheTrip()
    {
        // Arrange
        await LoadAsync(Trip());
        MockPhotographs.AddAsync(Arg.Any<LocalTripRecordModel>(), true, Arg.Any<CancellationToken>()).Returns((string?)null);
        MockTrips.ClearReceivedCalls();
        // Act
        await Sut.AddPhotographAsync(useCamera: true, CancellationToken.None);
        // Assert
        await MockPhotographs.Received(1).AddAsync(Arg.Is<LocalTripRecordModel>(trip => trip.Detail.Trip.Id == TripId), true, Arg.Any<CancellationToken>());
        Sut.HasMessage.Should().BeFalse();
        await MockTrips.Received(1).GetAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRemoveAPhotographThatIsNotListed()
    {
        // Arrange
        await LoadAsync(Trip(photographs: [Photograph(ViewerId)]));
        // Act
        await Sut.RemovePhotographAsync(Guid.NewGuid(), CancellationToken.None);
        // Assert
        await MockPhotographs.DidNotReceive().RemoveAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportAFailureWhenThePhotographCannotBeRemoved()
    {
        // Arrange
        await LoadAsync(Trip(photographs: [Photograph(ViewerId)]));
        MockPhotographs.RemoveAsync(Arg.Any<LocalTripRecordModel>(), PhotoId, Arg.Any<CancellationToken>()).Returns(false);
        // Act
        await Sut.RemovePhotographAsync(PhotoId, CancellationToken.None);
        // Assert
        Sut.MessageText.Should().Be(Text["TripPhotographRemoveFailed"]);
    }

    [Fact]
    public async Task ItShouldRemoveThePhotographAndReloadTheTrip()
    {
        // Arrange
        await LoadAsync(Trip(photographs: [Photograph(ViewerId)]));
        MockPhotographs.RemoveAsync(Arg.Any<LocalTripRecordModel>(), PhotoId, Arg.Any<CancellationToken>()).Returns(true);
        MockTrips.ClearReceivedCalls();
        // Act
        await Sut.RemovePhotographAsync(PhotoId, CancellationToken.None);
        // Assert
        await MockPhotographs.Received(1).RemoveAsync(Arg.Is<LocalTripRecordModel>(trip => trip.Detail.Trip.Id == TripId), PhotoId, Arg.Any<CancellationToken>());
        Sut.HasMessage.Should().BeFalse();
        await MockTrips.Received(1).GetAsync(TripId, Arg.Any<CancellationToken>());
    }
}
