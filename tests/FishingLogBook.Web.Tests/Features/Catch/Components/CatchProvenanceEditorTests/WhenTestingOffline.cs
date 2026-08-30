using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Components.CatchProvenanceEditor;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Components.CatchProvenanceEditorTests;

public class WhenTestingOffline : BaseCatchProvenanceEditorTest
{
    [Fact]
    public async Task ItShouldNotFetchAnythingWhenOffline()
    {
        // Arrange
        var catchClient = QuietCatchClient();
        await using var context = CreateContext(catchClient: catchClient, network: OnlineNetwork(isOnline: false));

        // Act
        var cut = context.Render<CatchProvenanceEditor>(parameters => parameters
            .Add(p => p.CatchId, CatchId)
            .Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Markup.Should().NotBeNull());
        await catchClient.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        cut.FindAll("#catch-provenance-angler-chips").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldDisableThePillsAndHideUpdateWhenConnectivityDrops()
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
        var network = OnlineNetwork();
        await using var context = CreateContext(
            catchClient: catchClient,
            tripClient: tripClient,
            participantClient: participantClient,
            network: network);
        var cut = context.Render<CatchProvenanceEditor>(parameters => parameters
            .Add(p => p.CatchId, CatchId)
            .Add(p => p.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find($"#catch-provenance-angler-{OtherUserId:D}").Should().NotBeNull());

        // Act
        network.ConnectivityChanged += Raise.Event<Action<bool>>(false);

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#catch-provenance-angler-{OtherUserId:D}").ClassList.Should().Contain("mud-disabled");
            cut.Find("#catch-provenance-offline").Should().NotBeNull();
        });
        cut.FindAll("#catch-provenance-update").Should().BeEmpty();
    }
}
