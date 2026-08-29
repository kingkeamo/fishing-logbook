using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Components.CatchProvenanceEditor;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Components.CatchProvenanceEditorTests;

public class WhenTestingRender : BaseCatchProvenanceEditorTest
{
    [Fact]
    public async Task ItShouldRenderNothingWhenTheCatchHasNoTrip()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchProvenanceEditor>(parameters => parameters
            .Add(p => p.CatchId, CatchId)
            .Add(p => p.TripId, (Guid?)null));

        // Assert
        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowTheTripHeaderAndCurrentAngler()
    {
        // Arrange
        var catchClient = QuietCatchClient();
        catchClient.GetAsync(CatchId, Arg.Any<CancellationToken>())
            .Returns(CatchView(OwnerUserId, OwnerUserId, anglerName: "Myles Costello", recordedByName: "Myles Costello"));
        var tripClient = QuietTripClient();
        tripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>()).Returns(TripDetail());
        await using var context = CreateContext(catchClient: catchClient, tripClient: tripClient);

        // Act
        var cut = context.Render<CatchProvenanceEditor>(parameters => parameters
            .Add(p => p.CatchId, CatchId)
            .Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-provenance-trip-title").TextContent.Should().Contain("Costello & Fermoyle");
            cut.Find("#catch-provenance-angler-name").TextContent.Should().Contain("Myles Costello");
        });
        cut.FindAll("#catch-provenance-recorder-name").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowRecordedByWhenTheAnglerAndRecorderDiffer()
    {
        // Arrange
        var catchClient = QuietCatchClient();
        catchClient.GetAsync(CatchId, Arg.Any<CancellationToken>())
            .Returns(CatchView(OtherUserId, OwnerUserId, anglerName: "Patrick Connolly", recordedByName: "Myles Costello"));
        var tripClient = QuietTripClient();
        tripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>()).Returns(TripDetail());
        await using var context = CreateContext(catchClient: catchClient, tripClient: tripClient);

        // Act
        var cut = context.Render<CatchProvenanceEditor>(parameters => parameters
            .Add(p => p.CatchId, CatchId)
            .Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-provenance-angler-name").TextContent.Should().Contain("Patrick Connolly");
            cut.Find("#catch-provenance-recorder-name").TextContent.Should().Contain("Myles Costello");
        });
    }

    [Fact]
    public async Task ItShouldShowEligibleTripAnglersAsChipsWithTheCurrentAngerSelected()
    {
        // Arrange
        var catchClient = QuietCatchClient();
        catchClient.GetAsync(CatchId, Arg.Any<CancellationToken>())
            .Returns(CatchView(OwnerUserId, OwnerUserId));
        var tripClient = QuietTripClient();
        tripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>()).Returns(TripDetail());
        var participantClient = QuietParticipantClient();
        participantClient.GetAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Participants(
                Owner(OwnerUserId, "Myles Costello"),
                Accepted(OtherUserId, "Patrick Connolly")));
        await using var context = CreateContext(
            catchClient: catchClient,
            tripClient: tripClient,
            participantClient: participantClient);

        // Act
        var cut = context.Render<CatchProvenanceEditor>(parameters => parameters
            .Add(p => p.CatchId, CatchId)
            .Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#catch-provenance-angler-{OwnerUserId:D}").ClassList.Should().Contain("mud-chip-filled");
            cut.Find($"#catch-provenance-angler-{OtherUserId:D}").ClassList.Should().Contain("mud-chip-outlined");
        });
        cut.FindAll("#catch-provenance-update").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldExcludePendingAndDeclinedAndRemovedParticipantsFromThePicker()
    {
        // Arrange
        var catchClient = QuietCatchClient();
        catchClient.GetAsync(CatchId, Arg.Any<CancellationToken>())
            .Returns(CatchView(OwnerUserId, OwnerUserId));
        var tripClient = QuietTripClient();
        tripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>()).Returns(TripDetail());
        var pendingUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var declinedUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var removedUserId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var participantClient = QuietParticipantClient();
        participantClient.GetAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(new TripParticipantsDto(TripId, TripParticipantConstants.Owner)
            {
                Participants =
                [
                    Owner(OwnerUserId, "Myles Costello"),
                    Accepted(OtherUserId, "Patrick Connolly"),
                    new TripParticipantDto(pendingUserId, TripParticipantConstants.Pending, "Pending Angler", null, StoredCaughtOn),
                    new TripParticipantDto(declinedUserId, TripParticipantConstants.Declined, "Declined Angler", null, StoredCaughtOn),
                    new TripParticipantDto(removedUserId, TripParticipantConstants.Accepted, "Removed Angler", null, StoredCaughtOn)
                ]
            });
        await using var context = CreateContext(
            catchClient: catchClient,
            tripClient: tripClient,
            participantClient: participantClient);

        // Act
        var cut = context.Render<CatchProvenanceEditor>(parameters => parameters
            .Add(p => p.CatchId, CatchId)
            .Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-provenance-angler-{OtherUserId:D}").Should().NotBeNull());
        cut.FindAll($"#catch-provenance-angler-{pendingUserId:D}").Should().BeEmpty();
        cut.FindAll($"#catch-provenance-angler-{declinedUserId:D}").Should().BeEmpty();
    }
}
