using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using NSubstitute;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripEditViewModelTests;

public sealed class WhenTestingState : BaseTripEditViewModelTest
{
    private static readonly Guid One = Guid.Parse("a0000000-0000-0000-0000-000000000001");

    [Fact]
    public void ItShouldShowNothingBeforeATripIsLoaded()
    {
        // Arrange
        // Act
        // Assert
        Sut.Trip.Should().BeNull();
        Sut.TripId.Should().BeNull();
        Sut.ShowContent.Should().BeFalse();
        Sut.CanInteract.Should().BeFalse();
        Sut.CanAddNotes.Should().BeFalse();
        Sut.SummaryText.Should().BeEmpty();
        Sut.HasMessage.Should().BeFalse();
        Sut.MessageText.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldExposeTheLoadedTrip()
    {
        // Arrange
        // Act
        await LoadAsync(Trip());
        // Assert
        Sut.Trip!.Detail.Trip.Id.Should().Be(TripId);
        Sut.TripId.Should().Be(TripId);
        Sut.CanInteract.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldNotRaiseAChangeWhenTheSamePlaceIsSetAgain()
    {
        // Arrange
        await LoadAsync(Trip(place: "Lough Corrib"));
        var changes = 0;
        Sut.PropertyChanged += (_, _) => changes++;
        // Act
        Sut.EditPlace = "Lough Corrib";
        // Assert
        changes.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldShowMeasurementsInPoundsAndInchesAndUnknownSpeciesForACatch()
    {
        // Arrange
        MockUnits.GetAsync(Arg.Any<CancellationToken>()).Returns(new MeasurementUnitsModel(WeightUnitEnum.Lb, LengthUnitEnum.In));
        // Act
        await LoadAsync(Trip(catches: [new TripCatchSummaryDto(One, Now.AddHours(-1)) { Weight = 2m, Length = 50m, CaughtByUserId = ViewerId, RecordedByUserId = ViewerId }]));
        // Assert
        var row = Sut.Catches.Single();
        row.Label.Should().Be(Text["TripTimelineCatchUnknownSpecies"]);
        row.Measurements.Should().Be($"4.41 {Text["MeasureUnitLb"]} · 19.69 {Text["MeasureUnitIn"]}");
    }

    [Fact]
    public async Task ItShouldShowACatchWithoutMeasurementsAsJustItsSpecies()
    {
        // Arrange
        // Act
        await LoadAsync(Trip(catches: [new TripCatchSummaryDto(One, Now.AddHours(-1)) { SpeciesName = "Pike", CaughtByUserId = ViewerId, RecordedByUserId = ViewerId }]));
        // Assert
        Sut.Catches.Single().HasMeasurements.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldIgnoreAPhotographRemovalWhileAnotherActionIsRunning()
    {
        // Arrange
        await LoadAsync(Trip(photographs: [new TripPhotographViewDto(One, "image/jpeg", Now.AddHours(-1), "https://img/1.jpg") { ContributedByUserId = ViewerId }]));
        var release = new TaskCompletionSource<LocalTripRecordModel>();
        MockTrips.UpdateAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripViewDto>(), Arg.Any<CancellationToken>()).Returns(release.Task);
        var saving = Sut.SaveAsync(CancellationToken.None);
        // Act
        await Sut.RemovePhotographAsync(One, CancellationToken.None);
        release.SetResult(Trip());
        await saving;
        // Assert
        await MockPhotographs.DidNotReceive().RemoveAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreASecondSaveWhileOneIsRunning()
    {
        // Arrange
        await LoadAsync(Trip());
        var release = new TaskCompletionSource<LocalTripRecordModel>();
        MockTrips.UpdateAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripViewDto>(), Arg.Any<CancellationToken>()).Returns(release.Task);
        var first = Sut.SaveAsync(CancellationToken.None);
        // Act
        var second = await Sut.SaveAsync(CancellationToken.None);
        release.SetResult(Trip());
        await first;
        // Assert
        second.Should().BeFalse();
        await MockTrips.Received(1).UpdateAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripViewDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDoNothingToReloadBeforeATripIsLoaded()
    {
        // Arrange
        // Act
        await Sut.ReloadAsync(CancellationToken.None);
        // Assert
        await MockTrips.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        Sut.IsLoading.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldTreatAMissingSessionAsNoViewerWhenLoading()
    {
        // Arrange
        MockSession.Capture().Returns((FishingLogBook.Mobile.Features.Authentication.Models.AccountSessionLeaseModel?)null);
        // Act
        await LoadAsync(Trip(notes: [Note(One, ViewerId)]));
        // Assert
        Sut.Notes.Single().CanChange.Should().BeFalse();
    }
}
