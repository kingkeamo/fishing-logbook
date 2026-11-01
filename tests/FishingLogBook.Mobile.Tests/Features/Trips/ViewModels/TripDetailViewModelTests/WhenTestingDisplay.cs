using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Trips.Enums;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripDetailViewModelTests;

public sealed class WhenTestingDisplay : BaseTripDetailViewModelTest
{
    private static readonly Guid One = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid Two = Guid.Parse("a0000000-0000-0000-0000-000000000002");

    [Fact]
    public void ItShouldShowNothingAndAllowNothingBeforeATripIsLoaded()
    {
        // Arrange
        // Act
        // Assert
        Sut.Trip.Should().BeNull();
        Sut.TripId.Should().BeNull();
        Sut.ShowContent.Should().BeFalse();
        Sut.CanInteract.Should().BeFalse();
        (Sut.DateHeading, Sut.TitleText, Sut.PlaceText, Sut.StartedText, Sut.ElapsedText).Should().Be((string.Empty, string.Empty, string.Empty, string.Empty, string.Empty));
        Sut.IsCompleted.Should().BeFalse();
        Sut.CanAddCatches.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldExposeTheLoadedTripAndAllowInteraction()
    {
        // Arrange
        // Act
        await LoadAsync(Trip());
        // Assert
        Sut.Trip!.Detail.Trip.Id.Should().Be(TripId);
        Sut.TripId.Should().Be(TripId);
        Sut.CanInteract.Should().BeTrue();
        Sut.NotFound.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldShowElapsedTimeInHoursAndMinutesForALongTripAndMinutesOnlyForAShortOne()
    {
        // Arrange
        await LoadAsync(Trip());
        var long3h = Sut.ElapsedText;
        await LoadAsync(Trip(TripConstants.Completed, endedOn: Now.AddHours(-3).AddMinutes(25)));
        // Act
        var short25m = Sut.ElapsedText;
        // Assert
        long3h.Should().Be(string.Format(Text.Culture, Text["TripDurationHoursMinutes"], 3, 0));
        short25m.Should().Be(string.Format(Text.Culture, Text["TripDurationMinutes"], 25));
    }

    [Fact]
    public async Task ItShouldNeverShowANegativeElapsedTime()
    {
        // Arrange
        // Act
        await LoadAsync(Trip(TripConstants.Completed, endedOn: Now.AddHours(-4)));
        // Assert
        Sut.ElapsedText.Should().Be(string.Format(Text.Culture, Text["TripDurationMinutes"], 0));
    }

    [Fact]
    public async Task ItShouldCountCatchesPhotographsAndNotesWithSingularAndPluralWording()
    {
        // Arrange
        // Act
        await LoadAsync(Trip(
            notes: [Note(One, ViewerId), Note(Two, ViewerId, at: Now.AddMinutes(-30))],
            photographs: [new TripPhotographViewDto(One, "image/jpeg", Now.AddHours(-2), "https://img/1.jpg") { ContributedByUserId = ViewerId }],
            catches:
            [
                new TripCatchSummaryDto(One, Now.AddHours(-2)) { CaughtByUserId = ViewerId, RecordedByUserId = ViewerId },
                new TripCatchSummaryDto(Two, Now.AddHours(-1)) { CaughtByUserId = ViewerId, RecordedByUserId = ViewerId }
            ]));
        // Assert
        Sut.CatchSummary.Should().Be(string.Format(Text.Culture, Text["TripCatchesMany"], 2));
        Sut.PhotographSummary.Should().Be(Text["TripListPhotographsOne"]);
        Sut.NoteSummary.Should().Be(string.Format(Text.Culture, Text["TripListNotesMany"], 2));
    }

    [Fact]
    public async Task ItShouldShowATripPhotographRowWithItsImageAndContributor()
    {
        // Arrange
        // Act
        await LoadAsync(Trip(
            photographs: [new TripPhotographViewDto(One, "image/jpeg", Now.AddHours(-2), "https://img/1.jpg") { ContributedByUserId = FriendId }],
            contributors: [new TripContributorDto(ViewerId, "Me", null), new TripContributorDto(FriendId, null, null)]));
        // Assert
        var row = Sut.Timeline.Single(item => item.Kind == TripTimelineKindEnum.Photograph);
        row.PhotoSource.Should().Be("https://img/1.jpg");
        row.ShowPhotograph.Should().BeTrue();
        row.ContributorText.Should().Be(Text["TripContributorUnknown"]);
        row.CanDelete.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldShowCatchMeasurementsInPoundsAndInchesWhenThoseAreTheAccountUnits()
    {
        // Arrange
        MockUnits.GetAsync(Arg.Any<CancellationToken>()).Returns(new MeasurementUnitsModel(WeightUnitEnum.Lb, LengthUnitEnum.In));
        // Act
        await LoadAsync(Trip(catches: [new TripCatchSummaryDto(One, Now.AddHours(-1)) { SpeciesName = "Pike", Weight = 2m, Length = 50m, CaughtByUserId = ViewerId, RecordedByUserId = ViewerId }]));
        // Assert
        Sut.Timeline.Single(row => row.IsCatch).Detail.Should().Be($"4.41 {Text["MeasureUnitLb"]} · 19.69 {Text["MeasureUnitIn"]}");
    }

    [Fact]
    public async Task ItShouldNameTheRecorderOfACatchRecordedForSomeoneElse()
    {
        // Arrange
        // Act
        await LoadAsync(Trip(
            contributors: [new TripContributorDto(ViewerId, "Me", null)],
            catches: [new TripCatchSummaryDto(One, Now.AddHours(-1)) { CaughtByUserId = ViewerId, RecordedByUserId = FriendId }]));
        // Assert
        Sut.Timeline.Single(row => row.IsCatch).RecordedByText.Should().Be(string.Format(Text.Culture, Text["TripTimelineRecordedBy"], Text["TripContributorUnknown"]));
    }

    [Fact]
    public async Task ItShouldIgnoreASecondActionWhileOneIsStillRunning()
    {
        // Arrange
        await LoadAsync(Trip());
        var release = new TaskCompletionSource<LocalTripRecordModel>();
        MockTrips.UpdateAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<FishingLogBook.Shared.Dtos.TripViewDto>(), Arg.Any<CancellationToken>()).Returns(release.Task);
        var first = Sut.FinishAsync(CancellationToken.None);
        // Act
        await Sut.FinishAsync(CancellationToken.None);
        release.SetResult(Trip());
        await first;
        // Assert
        await MockTrips.Received(1).UpdateAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<FishingLogBook.Shared.Dtos.TripViewDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReloadTheLoadedTripAndDoNothingBeforeOneIsLoaded()
    {
        // Arrange
        await Sut.ReloadAsync(CancellationToken.None);
        await MockTrips.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await LoadAsync(Trip());
        MockTrips.ClearReceivedCalls();
        // Act
        await Sut.ReloadAsync(CancellationToken.None);
        // Assert
        await MockTrips.Received(1).GetAsync(TripId, Arg.Any<CancellationToken>());
    }
}
