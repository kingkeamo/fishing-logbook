using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripNoteEditorViewModelTests;

public sealed class WhenTestingState : BaseTripNoteEditorViewModelTest
{
    [Fact]
    public void ItShouldOfferAnUnboundedDateRangeAndNowBeforeATripIsChosen()
    {
        // Arrange
        // Act
        // Assert
        Sut.NoteDateMinimum.Should().Be(DateTime.MinValue);
        Sut.NoteDateMaximum.Should().Be(DateTime.MaxValue);
        Sut.NoteDate.Should().Be(Now.DateTime.Date);
        Sut.NoteTime.Should().Be(new TimeSpan(12, 0, 0));
        Sut.HasWhenError.Should().BeFalse();
        Sut.WhenText.Should().BeEmpty();
        Sut.CanSave.Should().BeFalse();
    }

    [Fact]
    public void ItShouldLimitTheDatePickerToTheTripWindow()
    {
        // Arrange
        // Act
        Sut.Initialize(Trip(TripConstants.Completed, endedOn: Now.AddHours(-1)), null);
        // Assert
        Sut.NoteDateMinimum.Should().Be(new DateTime(2026, 10, 31));
        Sut.NoteDateMaximum.Should().Be(new DateTime(2026, 10, 31));
    }

    [Fact]
    public void ItShouldDefaultToTheTripStartWhenTheTripStartsInTheFuture()
    {
        // Arrange
        var future = Trip() with
        {
            Detail = Trip().Detail with { Trip = Trip().Detail.Trip with { StartedOn = Now.AddHours(2) } }
        };
        // Act
        Sut.Initialize(future, null);
        // Assert
        Sut.NoteTime.Should().Be(new TimeSpan(14, 0, 0));
    }

    [Fact]
    public void ItShouldChangeTheDateAndKeepTheTime()
    {
        // Arrange
        InitializeEditing(Trip(), Note(Guid.NewGuid(), ViewerId, at: Now.AddHours(-1)));
        // Act
        Sut.NoteDate = new DateTime(2026, 10, 30, 8, 15, 0);
        // Assert
        Sut.NoteDate.Should().Be(new DateTime(2026, 10, 30));
        Sut.NoteTime.Should().Be(new TimeSpan(11, 0, 0));
        Sut.HasWhenError.Should().BeTrue();
    }

    [Fact]
    public void ItShouldNotRaiseAChangeWhenTheSameTextIsSetAgain()
    {
        // Arrange
        Sut.Initialize(Trip(), null);
        Sut.NoteText = "Fish rising";
        var changes = 0;
        Sut.PropertyChanged += (_, _) => changes++;
        // Act
        Sut.NoteText = "Fish rising";
        // Assert
        changes.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldNotSaveWhenThereIsNoTripOrNoSession()
    {
        // Arrange
        var beforeInit = await Sut.SaveAsync(CancellationToken.None);
        Sut.Initialize(Trip(), null);
        Sut.NoteText = "Fish rising";
        MockSession.Capture().Returns((FishingLogBook.Mobile.Features.Authentication.Models.AccountSessionLeaseModel?)null);
        // Act
        var noSession = await Sut.SaveAsync(CancellationToken.None);
        // Assert
        beforeInit.Should().BeFalse();
        noSession.Should().BeFalse();
        await MockTrips.DidNotReceive().SaveNoteAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripNoteDto>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }
}
