using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Enums;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripTimelineServiceTests;

public sealed class WhenTestingBuild
{
    private static readonly Guid TripId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Start = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);
    private readonly TripTimelineService _sut = new();

    private static TripDetailDto Detail(string status = TripConstants.Active, DateTimeOffset? endedOn = null)
    {
        return new TripDetailDto(new TripViewDto(TripId, UserId, status, Start, endedOn));
    }

    private static LocalCatchRecordModel Local(Guid id, DateTimeOffset at, Guid? tripId, string? path = "a.jpg", LocalCatchLifecycleEnum lifecycle = LocalCatchLifecycleEnum.Saved)
    {
        return new LocalCatchRecordModel
        {
            Id = id,
            UserId = UserId,
            TripId = tripId,
            CaughtByUserId = UserId,
            RecordedByUserId = UserId,
            CaughtOn = at,
            SpeciesName = "Pike",
            Weight = 3.2,
            Revision = 1,
            Lifecycle = lifecycle,
            CreatedOn = at,
            UpdatedOn = at,
            Photographs = path is null ? [] : [new LocalCatchPhotographModel
            {
                Id = Guid.NewGuid(), CatchId = id, UserId = UserId, SortOrder = 0, LocalFilePath = path, ContentType = "image/jpeg", CreatedOn = at
            }]
        };
    }

    [Fact]
    public void ItShouldAlwaysStartWithTheTripStartAndNotFinishAnActiveTrip()
    {
        // Arrange
        var detail = Detail();
        // Act
        var items = _sut.Build(detail, []);
        // Assert
        items.Should().ContainSingle().Which.Kind.Should().Be(TripTimelineKindEnum.Started);
        items[0].OccurredOn.Should().Be(Start);
    }

    [Fact]
    public void ItShouldEndAFinishedTripWithItsFinishTime()
    {
        // Arrange
        var detail = Detail(TripConstants.Completed, Start.AddHours(4));
        // Act
        var items = _sut.Build(detail, []);
        // Assert
        items.Select(item => item.Kind).Should().Equal(TripTimelineKindEnum.Started, TripTimelineKindEnum.Finished);
        items[1].OccurredOn.Should().Be(Start.AddHours(4));
    }

    [Fact]
    public void ItShouldOrderCatchesPhotographsAndNotesChronologicallyAcrossKinds()
    {
        // Arrange
        var detail = Detail() with
        {
            Notes =
            [
                new TripNoteDto(Guid.NewGuid(), TripId, "late", Start.AddHours(3)) { CreatedByUserId = UserId },
                new TripNoteDto(Guid.NewGuid(), TripId, "early", Start.AddMinutes(10)) { CreatedByUserId = UserId }
            ],
            Photographs = [new TripPhotographViewDto(Guid.NewGuid(), "image/jpeg", Start.AddHours(5), "https://p/1", Start.AddHours(1))],
            Catches = [new TripCatchSummaryDto(Guid.NewGuid(), Start.AddHours(2))]
        };
        // Act
        var items = _sut.Build(detail, []);
        // Assert
        items.Select(item => item.Kind).Should().Equal(
            TripTimelineKindEnum.Started, TripTimelineKindEnum.Note, TripTimelineKindEnum.Photograph, TripTimelineKindEnum.Catch, TripTimelineKindEnum.Note);
        items[2].PhotographUrl.Should().Be("https://p/1");
    }

    [Fact]
    public void ItShouldUseTheCaptureTimeRatherThanTheUploadTimeForAPhotograph()
    {
        // Arrange
        var detail = Detail() with { Photographs = [new TripPhotographViewDto(Guid.NewGuid(), "image/jpeg", Start.AddHours(8), null, Start.AddHours(1))] };
        // Act
        var items = _sut.Build(detail, []);
        // Assert
        items[1].OccurredOn.Should().Be(Start.AddHours(1));
    }

    [Fact]
    public void ItShouldShowALocalCatchForThisTripOnceWithItsPendingStateAndFirstPhoto()
    {
        // Arrange
        var local = Guid.NewGuid();
        // Act
        var items = _sut.Build(Detail(), [Local(local, Start.AddHours(1), TripId, "photos/one.jpg")]);
        // Assert
        var row = items.Single(item => item.Kind == TripTimelineKindEnum.Catch);
        row.CatchId.Should().Be(local);
        row.IsPending.Should().BeTrue();
        row.LocalFilePath.Should().Be("photos/one.jpg");
        row.Weight.Should().Be(3.2m);
    }

    [Fact]
    public void ItShouldNotDuplicateACatchTheServerAlreadyListsAndShouldIgnoreCatchesOfOtherTrips()
    {
        // Arrange
        var shared = Guid.NewGuid();
        var detail = Detail() with { Catches = [new TripCatchSummaryDto(shared, Start.AddHours(1)) { SpeciesName = "Pike" }] };
        // Act
        var items = _sut.Build(detail,
        [
            Local(shared, Start.AddHours(1), TripId),
            Local(Guid.NewGuid(), Start.AddHours(2), Guid.NewGuid()),
            Local(Guid.NewGuid(), Start.AddHours(3), null)
        ]);
        // Assert
        var catches = items.Where(item => item.Kind == TripTimelineKindEnum.Catch).ToArray();
        catches.Should().ContainSingle();
        catches[0].IsPending.Should().BeFalse();
    }

    [Fact]
    public void ItShouldNotShowAnUnsavedDraftAsACatchOnTheTrip()
    {
        // Arrange
        var draft = Local(Guid.NewGuid(), Start.AddHours(1), TripId, lifecycle: LocalCatchLifecycleEnum.Draft);
        var saved = Local(Guid.NewGuid(), Start.AddHours(2), TripId);
        // Act
        var items = _sut.Build(Detail(), [draft, saved]);
        // Assert
        items.Where(item => item.Kind == TripTimelineKindEnum.Catch).Select(item => item.CatchId).Should().Equal(saved.Id);
    }

    [Fact]
    public void ItShouldCarryTheLocalPhotographPathOntoACatchTheServerAlreadyListsEvenWhenTheLocalCopyIsNotOnThisTrip()
    {
        // Arrange
        var shared = Guid.NewGuid();
        var detail = Detail() with { Catches = [new TripCatchSummaryDto(shared, Start.AddHours(1)) { SpeciesName = "Pike", PhotographUrl = "https://img/pike.jpg" }] };
        // Act
        var items = _sut.Build(detail, [Local(shared, Start.AddHours(1), null, "photos/pike.jpg", LocalCatchLifecycleEnum.Draft)]);
        // Assert
        var row = items.Single(item => item.Kind == TripTimelineKindEnum.Catch);
        row.LocalFilePath.Should().Be("photos/pike.jpg");
        row.PhotographUrl.Should().Be("https://img/pike.jpg");
        row.IsPending.Should().BeFalse();
    }

    [Fact]
    public void ItShouldLeaveTheLocalPathEmptyOnAServerCatchThatHasNoLocalCopy()
    {
        // Arrange
        var detail = Detail() with { Catches = [new TripCatchSummaryDto(Guid.NewGuid(), Start.AddHours(1)) { SpeciesName = "Pike" }] };
        // Act
        var items = _sut.Build(detail, []);
        // Assert
        items.Single(item => item.Kind == TripTimelineKindEnum.Catch).LocalFilePath.Should().BeNull();
    }

    [Fact]
    public void ItShouldAddNoFinishedEntryForACompletedTripThatHasNoEndTime()
    {
        // Arrange
        var detail = Detail(TripConstants.Completed, null);
        // Act
        var items = _sut.Build(detail, []);
        // Assert
        items.Select(item => item.Kind).Should().Equal(TripTimelineKindEnum.Started);
    }

    [Fact]
    public void ItShouldAddNoFinishedEntryForAnActiveTrip()
    {
        // Arrange
        // Act
        var items = _sut.Build(Detail(), []);
        // Assert
        items.Select(item => item.Kind).Should().Equal(TripTimelineKindEnum.Started);
    }

    [Fact]
    public void ItShouldShowAPendingSavedCatchOnlyWhenItBelongsToThisTrip()
    {
        // Arrange
        var mine = Local(Guid.NewGuid(), Start.AddHours(1), TripId);
        var elsewhere = Local(Guid.NewGuid(), Start.AddHours(2), Guid.NewGuid());
        var standalone = Local(Guid.NewGuid(), Start.AddHours(3), null);
        // Act
        var items = _sut.Build(Detail(), [mine, elsewhere, standalone]);
        // Assert
        items.Where(item => item.Kind == TripTimelineKindEnum.Catch).Select(item => item.CatchId).Should().Equal(mine.Id);
    }

    [Fact]
    public void ItShouldKeepAStableOrderForItemsAtTheSameInstant()
    {
        // Arrange
        var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var detail = Detail() with
        {
            Notes = [new TripNoteDto(second, TripId, "b", Start.AddHours(1)), new TripNoteDto(first, TripId, "a", Start.AddHours(1))]
        };
        // Act
        var items = _sut.Build(detail, []);
        // Assert
        items.Where(item => item.Kind == TripTimelineKindEnum.Note).Select(item => item.NoteId).Should().Equal(first, second);
    }
}
