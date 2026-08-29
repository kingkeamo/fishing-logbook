using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;

namespace FishingLogBook.Web.Tests.Features.Trips.Services.TripTimelineServiceTests;

public class WhenTestingBuildShared : BaseTripTimelineServiceTest
{
    private static readonly Guid OtherAnglerUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ServerNoteId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid LocalNoteId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    [Fact]
    public void ItShouldShowOnlyTheServerDiaryWhenNothingIsPendingLocally()
    {
        // Arrange
        var detail = Detail(ServerNote());

        // Act
        var timeline = Sut.BuildShared(detail, Trip(), []);

        // Assert
        timeline.Where(item => item.Kind == TripTimelineKindEnum.Note)
            .Should().ContainSingle();
        timeline.Single(item => item.Kind == TripTimelineKindEnum.Note).NoteId
            .Should().Be(ServerNoteId);
    }

    [Fact]
    public void ItShouldNotDuplicateANoteTheServerAlreadyKnows()
    {
        // Arrange
        var detail = Detail(ServerNote());
        var localTrip = Trip(notes: [LocalNote(ServerNoteId, "already synchronised")]);

        // Act
        var timeline = Sut.BuildShared(detail, localTrip, []);

        // Assert
        timeline.Count(item => item.NoteId == ServerNoteId).Should().Be(1);
    }

    [Fact]
    public void ItShouldKeepTheViewersUnsyncedNoteAlongsideTheServerDiary()
    {
        // Arrange
        var detail = Detail(ServerNote());
        var localTrip = Trip(notes: [LocalNote(LocalNoteId, "not yet synchronised")]);

        // Act
        var timeline = Sut.BuildShared(detail, localTrip, []);

        // Assert
        timeline.Where(item => item.Kind == TripTimelineKindEnum.Note)
            .Select(item => item.NoteId)
            .Should().BeEquivalentTo([ServerNoteId, LocalNoteId]);
    }

    [Fact]
    public void ItShouldKeepEveryContributorsAttributionThroughTheMerge()
    {
        // Arrange
        var detail = Detail(ServerNote());
        var localTrip = Trip(notes: [LocalNote(LocalNoteId, "mine")]);

        // Act
        var timeline = Sut.BuildShared(detail, localTrip, []);

        // Assert
        timeline.Single(item => item.NoteId == ServerNoteId).ContributedByUserId
            .Should().Be(OtherAnglerUserId);
        timeline.Single(item => item.NoteId == LocalNoteId).ContributedByUserId
            .Should().Be(OwnerUserId);
    }

    [Fact]
    public void ItShouldOrderTheMergedDiaryByTime()
    {
        // Arrange
        var detail = Detail(ServerNote(StartedOn.AddMinutes(40)));
        var localTrip = Trip(notes: [LocalNote(LocalNoteId, "earlier", StartedOn.AddMinutes(10))]);

        // Act
        var timeline = Sut.BuildShared(detail, localTrip, []);

        // Assert
        timeline.Where(item => item.Kind == TripTimelineKindEnum.Note)
            .Select(item => item.NoteId)
            .Should().Equal([LocalNoteId, ServerNoteId]);
        timeline[0].Kind.Should().Be(TripTimelineKindEnum.Started);
    }

    private static TripDetailDto Detail(params TripNoteDto[] notes)
    {
        return new TripDetailDto(
            new TripViewDto(TripId, OtherAnglerUserId, TripConstants.Active, StartedOn))
        {
            Role = TripParticipantConstants.Participant,
            Notes = notes
        };
    }

    private static TripNoteDto ServerNote(DateTimeOffset? recordedOn = null)
    {
        return new TripNoteDto(
            ServerNoteId,
            TripId,
            "the owner note",
            recordedOn ?? StartedOn.AddMinutes(20))
        {
            CreatedByUserId = OtherAnglerUserId
        };
    }

    private static TripNoteModel LocalNote(Guid noteId, string text, DateTimeOffset? recordedOn = null)
    {
        return new TripNoteModel(
            noteId,
            TripId,
            OwnerUserId,
            text,
            recordedOn ?? StartedOn.AddMinutes(30),
            SyncStatus.SavedLocally);
    }
}
