using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Components.CatchProvenanceEditor;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Trips.Clients;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Components.CatchProvenanceEditorTests;

public class WhenTestingUpdate : BaseCatchProvenanceEditorTest
{
    [Fact]
    public async Task ItShouldShowUpdateOnlyAfterSelectingADifferentAngler()
    {
        // Arrange
        var catchClient = GivenTwoEligibleAnglers(out var tripClient, out var participantClient);
        await using var context = CreateContext(
            catchClient: catchClient,
            tripClient: tripClient,
            participantClient: participantClient);
        var cut = context.Render<CatchProvenanceEditor>(parameters => parameters
            .Add(p => p.CatchId, CatchId)
            .Add(p => p.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find($"#catch-provenance-angler-{OtherUserId:D}").Should().NotBeNull());
        cut.FindAll("#catch-provenance-update").Should().BeEmpty();

        // Act
        await cut.Find($"#catch-provenance-angler-{OtherUserId:D}").ClickAsync();

        // Assert
        cut.Find("#catch-provenance-update").Should().NotBeNull();
        await catchClient.DidNotReceive().CorrectAnglerAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotPersistAnythingUntilUpdateIsClicked()
    {
        // Arrange
        var catchClient = GivenTwoEligibleAnglers(out var tripClient, out var participantClient);
        await using var context = CreateContext(
            catchClient: catchClient,
            tripClient: tripClient,
            participantClient: participantClient);
        var cut = context.Render<CatchProvenanceEditor>(parameters => parameters
            .Add(p => p.CatchId, CatchId)
            .Add(p => p.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find($"#catch-provenance-angler-{OtherUserId:D}").Should().NotBeNull());

        // Act
        await cut.Find($"#catch-provenance-angler-{OtherUserId:D}").ClickAsync();
        await cut.Find($"#catch-provenance-angler-{OwnerUserId:D}").ClickAsync();

        // Assert
        cut.FindAll("#catch-provenance-update").Should().BeEmpty();
        await catchClient.DidNotReceive().CorrectAnglerAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldUpdateThePersistedAnglerWhenTheApiSucceeds()
    {
        // Arrange
        var catchClient = GivenTwoEligibleAnglers(out var tripClient, out var participantClient);
        catchClient.CorrectAnglerAsync(CatchId, OtherUserId, Arg.Any<CancellationToken>())
            .Returns(new CatchAnglerCorrectionResult(
                CatchView(OtherUserId, OwnerUserId, anglerName: "Patrick Connolly", recordedByName: "Myles Costello"),
                null));
        await using var context = CreateContext(
            catchClient: catchClient,
            tripClient: tripClient,
            participantClient: participantClient);
        var cut = context.Render<CatchProvenanceEditor>(parameters => parameters
            .Add(p => p.CatchId, CatchId)
            .Add(p => p.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find($"#catch-provenance-angler-{OtherUserId:D}").Should().NotBeNull());

        // Act
        await cut.Find($"#catch-provenance-angler-{OtherUserId:D}").ClickAsync();
        await cut.Find("#catch-provenance-update").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#catch-provenance-angler-{OtherUserId:D}").ClassList.Should().Contain("mud-chip-filled");
            cut.Find("#catch-provenance-recorder-name").TextContent.Should().Contain("Myles Costello");
        });
        cut.FindAll("#catch-provenance-update").Should().BeEmpty();
        await catchClient.Received(1).CorrectAnglerAsync(CatchId, OtherUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowAnErrorAndKeepThePendingSelectionWhenTheApiFails()
    {
        // Arrange
        var catchClient = GivenTwoEligibleAnglers(out var tripClient, out var participantClient);
        catchClient.CorrectAnglerAsync(CatchId, OtherUserId, Arg.Any<CancellationToken>())
            .Returns(new CatchAnglerCorrectionResult(null, "Only the angler or the recorder may edit this catch."));
        await using var context = CreateContext(
            catchClient: catchClient,
            tripClient: tripClient,
            participantClient: participantClient);
        var cut = context.Render<CatchProvenanceEditor>(parameters => parameters
            .Add(p => p.CatchId, CatchId)
            .Add(p => p.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find($"#catch-provenance-angler-{OtherUserId:D}").Should().NotBeNull());

        // Act
        await cut.Find($"#catch-provenance-angler-{OtherUserId:D}").ClickAsync();
        await cut.Find("#catch-provenance-update").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-provenance-update-failed").TextContent
                .Should().Contain("Only the angler or the recorder may edit this catch."));
        cut.Find($"#catch-provenance-angler-{OtherUserId:D}").ClassList.Should().Contain("mud-chip-filled");
        cut.Find("#catch-provenance-update").Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldFallBackToAGenericMessageWhenNoServerMessageIsAvailable()
    {
        // Arrange
        var catchClient = GivenTwoEligibleAnglers(out var tripClient, out var participantClient);
        catchClient.CorrectAnglerAsync(CatchId, OtherUserId, Arg.Any<CancellationToken>())
            .Returns(new CatchAnglerCorrectionResult(null, null));
        await using var context = CreateContext(
            catchClient: catchClient,
            tripClient: tripClient,
            participantClient: participantClient);
        var cut = context.Render<CatchProvenanceEditor>(parameters => parameters
            .Add(p => p.CatchId, CatchId)
            .Add(p => p.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find($"#catch-provenance-angler-{OtherUserId:D}").Should().NotBeNull());

        // Act
        await cut.Find($"#catch-provenance-angler-{OtherUserId:D}").ClickAsync();
        await cut.Find("#catch-provenance-update").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-provenance-update-failed").TextContent
                .Should().Contain("Couldn't update who caught this fish"));
    }

    private ICatchClient GivenTwoEligibleAnglers(out ITripClient tripClient, out ITripParticipantClient participantClient)
    {
        var catchClient = QuietCatchClient();
        catchClient.GetAsync(CatchId, Arg.Any<CancellationToken>())
            .Returns(CatchView(OwnerUserId, OwnerUserId, anglerName: "Myles Costello", recordedByName: "Myles Costello"));
        tripClient = QuietTripClient();
        tripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>()).Returns(TripDetail());
        participantClient = QuietParticipantClient();
        participantClient.GetAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Participants(
                Owner(OwnerUserId, "Myles Costello"),
                Accepted(OtherUserId, "Patrick Connolly")));
        return catchClient;
    }
}
