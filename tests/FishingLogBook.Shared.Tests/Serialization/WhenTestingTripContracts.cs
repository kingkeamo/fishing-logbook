using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Shared.Tests.Serialization;

public class WhenTestingTripContracts : BaseSerializationTest
{
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");
    private static readonly DateTimeOffset EndedOn = DateTimeOffset.Parse("2026-08-17T11:00:00Z");

    private static TripLocationDto Location()
    {
        return new TripLocationDto(53.3498, -6.2603, 12.5, StartedOn, LocationDefaults.DeviceGps, LocationDefaults.Private, "1");
    }

    [Fact]
    public void ItShouldRoundTripATripWithItsLocationAndOptionalFields()
    {
        // Arrange
        var original = new TripDto(TripId, TripConstants.Completed, StartedOn, EndedOn, Location())
        {
            OwnerUserId = UserId,
            PlannedStartOn = StartedOn.AddDays(-1),
            Title = "Evening session",
            PlaceName = "Lough Corrib"
        };

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var restored = JsonSerializer.Deserialize<TripDto>(json, WebOptions);

        // Assert
        restored.Should().BeEquivalentTo(original);
        json.Should().Contain("\"ownerUserId\":").And.Contain("\"plannedStartOn\":")
            .And.Contain("\"placeName\":").And.Contain("\"accuracyMetres\":");
    }

    [Fact]
    public void ItShouldRoundTripATripWithoutALocationOrEndTime()
    {
        // Arrange
        var original = new TripDto(TripId, TripConstants.Active, StartedOn);

        // Act
        var restored = JsonSerializer.Deserialize<TripDto>(JsonSerializer.Serialize(original, WebOptions), WebOptions);

        // Assert
        restored.Should().BeEquivalentTo(original);
        restored!.Location.Should().BeNull();
        restored.EndedOn.Should().BeNull();
    }

    [Fact]
    public void ItShouldRoundTripATripView()
    {
        // Arrange
        var original = new TripViewDto(TripId, UserId, TripConstants.Active, StartedOn, null, Location())
        {
            PlannedStartOn = StartedOn,
            Title = "Dawn",
            PlaceName = "Moy",
            CreatedOn = StartedOn.AddDays(-2),
            UpdatedOn = StartedOn.AddDays(-1)
        };

        // Act
        var restored = JsonSerializer.Deserialize<TripViewDto>(JsonSerializer.Serialize(original, WebOptions), WebOptions);

        // Assert
        restored.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void ItShouldFlagATripSummaryAsSharedOnlyWhenItHasParticipants()
    {
        // Arrange
        var solo = new TripSummaryDto(TripId, TripConstants.Active, StartedOn) { ParticipantCount = 0 };
        var shared = solo with { ParticipantCount = 2 };

        // Act
        var restored = JsonSerializer.Deserialize<TripSummaryDto>(JsonSerializer.Serialize(shared, WebOptions), WebOptions);

        // Assert
        solo.IsShared.Should().BeFalse();
        shared.IsShared.Should().BeTrue();
        restored.Should().BeEquivalentTo(shared);
        restored!.IsShared.Should().BeTrue();
    }

    [Fact]
    public void ItShouldRoundTripATripSummaryWithEveryCount()
    {
        // Arrange
        var original = new TripSummaryDto(TripId, TripConstants.Completed, StartedOn, EndedOn)
        {
            PlannedStartOn = StartedOn,
            Title = "Session",
            PlaceName = "Corrib",
            CatchCount = 3,
            PhotographCount = 5,
            NoteCount = 2,
            OwnerUserId = UserId,
            Role = TripParticipantConstants.Participant,
            ParticipantCount = 1
        };

        // Act
        var json = JsonSerializer.Serialize(original, WebOptions);
        var restored = JsonSerializer.Deserialize<TripSummaryDto>(json, WebOptions);

        // Assert
        restored.Should().BeEquivalentTo(original);
        json.Should().Contain("\"catchCount\":3").And.Contain("\"photographCount\":5").And.Contain("\"noteCount\":2");
    }

    [Fact]
    public void ItShouldDefaultATripSummaryRoleToOwner()
    {
        // Arrange
        // Act
        var summary = new TripSummaryDto(TripId, TripConstants.Active, StartedOn);

        // Assert
        summary.Role.Should().Be(TripParticipantConstants.Owner);
    }

    [Fact]
    public void ItShouldRoundTripATripDetailWithItsContent()
    {
        // Arrange
        var original = new TripDetailDto(new TripViewDto(TripId, UserId, TripConstants.Active, StartedOn))
        {
            Notes = [new TripNoteDto(Guid.NewGuid(), TripId, "Fish rising", StartedOn) { CreatedByUserId = UserId }],
            Photographs = [new TripPhotographViewDto(Guid.NewGuid(), "image/jpeg", StartedOn, "https://x/p", StartedOn) { ContributedByUserId = UserId }],
            Catches =
            [
                new TripCatchSummaryDto(Guid.NewGuid(), StartedOn)
                {
                    SpeciesName = "Pike",
                    Weight = 2.5m,
                    Length = 60m,
                    PhotographUrl = "https://x/c",
                    CaughtByUserId = UserId,
                    RecordedByUserId = UserId
                }
            ],
            Role = TripParticipantConstants.Owner,
            Contributors = [new TripContributorDto(UserId, "Eamonn", "https://x/u") { IsOwner = true }]
        };

        // Act
        var restored = JsonSerializer.Deserialize<TripDetailDto>(JsonSerializer.Serialize(original, WebOptions), WebOptions);

        // Assert
        restored.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void ItShouldDefaultATripDetailToNoContentAndNoRole()
    {
        // Arrange
        // Act
        var detail = new TripDetailDto(new TripViewDto(TripId, UserId, TripConstants.Active, StartedOn));

        // Assert
        detail.Notes.Should().BeEmpty();
        detail.Photographs.Should().BeEmpty();
        detail.Catches.Should().BeEmpty();
        detail.Contributors.Should().BeEmpty();
        detail.Role.Should().Be(TripParticipantConstants.None);
    }

    [Fact]
    public void ItShouldRoundTripTripParticipants()
    {
        // Arrange
        var original = new TripParticipantsDto(TripId, TripParticipantConstants.Owner)
        {
            Participants =
            [
                new TripParticipantDto(UserId, TripParticipantConstants.Accepted, "Sam", "https://x/u", StartedOn)
                {
                    IsOwner = false,
                    RespondedOn = EndedOn
                }
            ]
        };

        // Act
        var restored = JsonSerializer.Deserialize<TripParticipantsDto>(JsonSerializer.Serialize(original, WebOptions), WebOptions);

        // Assert
        restored.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void ItShouldRoundTripATripInvitation()
    {
        // Arrange
        var original = new TripInvitationDto(TripId, UserId, "Sam", "Lough trip", "Corrib", StartedOn, EndedOn)
        {
            PlannedStartOn = StartedOn
        };

        // Act
        var restored = JsonSerializer.Deserialize<TripInvitationDto>(JsonSerializer.Serialize(original, WebOptions), WebOptions);

        // Assert
        restored.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void ItShouldRoundTripAPlannedTripOptionWithItsParticipants()
    {
        // Arrange
        var original = new PlannedTripOptionDto(TripId, StartedOn, UserId, "Sam")
        {
            Title = "Plan",
            PlaceName = "Moy",
            Role = TripParticipantConstants.Participant,
            Participants = [new PlannedTripParticipantDto(UserId, "Sam", TripParticipantConstants.Accepted)]
        };

        // Act
        var restored = JsonSerializer.Deserialize<PlannedTripOptionDto>(JsonSerializer.Serialize(original, WebOptions), WebOptions);

        // Assert
        restored.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void ItShouldRoundTripTripCatchAssociationRequestsAndResults()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var rejectedId = Guid.NewGuid();
        var request = new AssociateTripCatchesDto([catchId]);
        var result = new TripCatchAssociationDto([catchId], [rejectedId]);

        // Act
        var restoredRequest = JsonSerializer.Deserialize<AssociateTripCatchesDto>(JsonSerializer.Serialize(request, WebOptions), WebOptions);
        var restoredResult = JsonSerializer.Deserialize<TripCatchAssociationDto>(JsonSerializer.Serialize(result, WebOptions), WebOptions);

        // Assert
        restoredRequest.Should().BeEquivalentTo(request);
        restoredResult.Should().BeEquivalentTo(result);
    }

    [Fact]
    public void ItShouldRoundTripTripNoteAndPhotographRecordRequests()
    {
        // Arrange
        var note = new RecordTripNoteDto(Guid.NewGuid(), "Fish rising", StartedOn);
        var photograph = new RecordTripPhotographDto(Guid.NewGuid(), "trips/key.jpg", "image/jpeg", StartedOn, EndedOn);
        var stored = new TripPhotographDto(Guid.NewGuid(), TripId, "trips/key.jpg", "image/jpeg", StartedOn, EndedOn)
        {
            ContributedByUserId = UserId
        };

        // Act
        var restoredNote = JsonSerializer.Deserialize<RecordTripNoteDto>(JsonSerializer.Serialize(note, WebOptions), WebOptions);
        var restoredPhotograph = JsonSerializer.Deserialize<RecordTripPhotographDto>(JsonSerializer.Serialize(photograph, WebOptions), WebOptions);
        var restoredStored = JsonSerializer.Deserialize<TripPhotographDto>(JsonSerializer.Serialize(stored, WebOptions), WebOptions);

        // Assert
        restoredNote.Should().BeEquivalentTo(note);
        restoredPhotograph.Should().BeEquivalentTo(photograph);
        restoredStored.Should().BeEquivalentTo(stored);
    }

    [Fact]
    public void ItShouldRoundTripTripInvitationRequests()
    {
        // Arrange
        var invite = new InviteTripParticipantDto(UserId);

        // Act
        var json = JsonSerializer.Serialize(invite, WebOptions);
        var restored = JsonSerializer.Deserialize<InviteTripParticipantDto>(json, WebOptions);

        // Assert
        restored.Should().Be(invite);
        json.Should().Be($"{{\"userId\":\"{UserId:D}\"}}");
    }
}
