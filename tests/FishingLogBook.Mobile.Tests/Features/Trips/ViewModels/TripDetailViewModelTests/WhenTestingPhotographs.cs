using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Shared.Constants;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripDetailViewModelTests;

public sealed class WhenTestingPhotographs : BaseTripDetailViewModelTest
{
    [Fact]
    public async Task ItShouldNotOfferPhotographsOnAFinishedTrip()
    {
        // Arrange
        await LoadAsync(Trip(TripConstants.Completed, endedOn: Now.AddHours(-1)));
        // Act
        Sut.TogglePhotographPicker();
        await Sut.AddPhotographAsync(useCamera: false, CancellationToken.None);
        // Assert
        Sut.CanAddPhotographs.Should().BeFalse();
        Sut.ShowPhotographPicker.Should().BeFalse();
        await MockPhotographs.DidNotReceive().AddAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowAndHideThePhotographPickerWhenToggled()
    {
        // Arrange
        await LoadAsync(Trip());
        // Act
        Sut.TogglePhotographPicker();
        var shown = Sut.Active!.ShowPhotographPicker;
        Sut.TogglePhotographPicker();
        // Assert
        shown.Should().BeTrue();
        Sut.ShowPhotographPicker.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldShowTheFailureReasonAndKeepThePickerOpenWhenThePhotographCannotBeAdded()
    {
        // Arrange
        await LoadAsync(Trip());
        Sut.TogglePhotographPicker();
        MockPhotographs.AddAsync(Arg.Any<LocalTripRecordModel>(), true, Arg.Any<CancellationToken>()).Returns("PhotoPermissionDenied");
        // Act
        await Sut.AddPhotographAsync(useCamera: true, CancellationToken.None);
        // Assert
        Sut.MessageText.Should().Be(Text["PhotoPermissionDenied"]);
        Sut.ShowPhotographPicker.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ItShouldAddThePhotographToTheTripCloseThePickerAndReloadTheTimeline(bool useCamera)
    {
        // Arrange
        await LoadAsync(Trip());
        Sut.TogglePhotographPicker();
        MockPhotographs.AddAsync(Arg.Any<LocalTripRecordModel>(), useCamera, Arg.Any<CancellationToken>()).Returns((string?)null);
        MockTrips.ClearReceivedCalls();
        // Act
        await Sut.AddPhotographAsync(useCamera, CancellationToken.None);
        // Assert
        await MockPhotographs.Received(1).AddAsync(Arg.Is<LocalTripRecordModel>(trip => trip.Detail.Trip.Id == TripId), useCamera, Arg.Any<CancellationToken>());
        Sut.HasMessage.Should().BeFalse();
        Sut.ShowPhotographPicker.Should().BeFalse();
        await MockTrips.Received(1).GetAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTellTheViewTheTripModelChangedWhenThePickerIsToggledSoTheButtonsAppear()
    {
        // Arrange
        await LoadAsync(Trip());
        var changed = new List<string?>();
        Sut.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        // Act
        Sut.TogglePhotographPicker();
        // Assert
        changed.Should().Contain(nameof(Sut.Active)).And.Contain(nameof(Sut.ShowPhotographPicker));
    }
}
