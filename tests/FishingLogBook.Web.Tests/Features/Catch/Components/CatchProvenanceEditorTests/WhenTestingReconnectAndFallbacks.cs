using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Catch.Components.CatchProvenanceEditor;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Components.CatchProvenanceEditorTests;

public class WhenTestingReconnectAndFallbacks : BaseCatchProvenanceEditorTest
{
    [Fact]
    public async Task ItShouldNameAnAnglerWithoutADisplayNameAsAnotherAngler()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchClient = QuietCatchClient();
        catchClient.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(CatchView(OwnerUserId, OwnerUserId));
        var participantClient = QuietParticipantClient();
        participantClient.GetAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Participants(
                Owner(OwnerUserId, "Myles Costello"),
                new TripParticipantDto(OtherUserId, TripParticipantConstants.Accepted, " ", null, StoredCaughtOn)));
        await using var context = CreateContext(catchClient: catchClient, participantClient: participantClient);

        // Act
        var cut = context.Render<CatchProvenanceEditor>(parameters => parameters
            .Add(p => p.CatchId, CatchId)
            .Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-provenance-angler-{OtherUserId:D}").TextContent.Should().Contain("Another angler"));
        await participantClient.Received(1).GetAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOmitTheStartedOnLabelWhenTheBrowserReturnsAnUnreadableTime()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchClient = QuietCatchClient();
        catchClient.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(CatchView(OwnerUserId, OwnerUserId));
        var tripClient = QuietTripClient();
        tripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>()).Returns(TripDetail());
        var time = Substitute.For<ITimeService>();
        time.ToDateTimeLocalValueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns("not-a-date");
        await using var context = CreateContext(catchClient: catchClient, tripClient: tripClient, time: time);

        // Act
        var cut = context.Render<CatchProvenanceEditor>(parameters => parameters
            .Add(p => p.CatchId, CatchId)
            .Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-provenance-trip-title").TextContent.Should().Contain("Costello & Fermoyle"));
        cut.FindAll("#catch-provenance-trip-started-on").Should().BeEmpty();
        await time.Received(1).ToDateTimeLocalValueAsync(StoredCaughtOn, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLoadTheTripAnglersWhenConnectivityReturnsAfterStartingOffline()
    {
        // Arrange
        var catchClient = QuietCatchClient();
        catchClient.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(CatchView(OwnerUserId, OwnerUserId));
        var participantClient = QuietParticipantClient();
        participantClient.GetAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Participants(Owner(OwnerUserId, "Myles Costello"), Accepted(OtherUserId, "Patrick Connolly")));
        var network = OnlineNetwork(isOnline: false);
        await using var context = CreateContext(
            catchClient: catchClient,
            participantClient: participantClient,
            network: network);
        var cut = context.Render<CatchProvenanceEditor>(parameters => parameters
            .Add(p => p.CatchId, CatchId)
            .Add(p => p.TripId, TripId));
        cut.WaitForAssertion(() => cut.Markup.Should().NotBeNull());
        await catchClient.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        // Act
        network.ConnectivityChanged += Raise.Event<Action<bool>>(true);

        // Assert
        cut.WaitForAssertion(() => cut.Find($"#catch-provenance-angler-{OtherUserId:D}").Should().NotBeNull());
        await catchClient.Received(1).GetAsync(CatchId, Arg.Any<CancellationToken>());
        await participantClient.Received(1).GetAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotReloadTheTripAnglersWhenConnectivityReturnsAndTheyAreAlreadyLoaded()
    {
        // Arrange
        var catchClient = QuietCatchClient();
        catchClient.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(CatchView(OwnerUserId, OwnerUserId));
        var participantClient = QuietParticipantClient();
        participantClient.GetAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Participants(Owner(OwnerUserId, "Myles Costello"), Accepted(OtherUserId, "Patrick Connolly")));
        var network = OnlineNetwork();
        await using var context = CreateContext(
            catchClient: catchClient,
            participantClient: participantClient,
            network: network);
        var cut = context.Render<CatchProvenanceEditor>(parameters => parameters
            .Add(p => p.CatchId, CatchId)
            .Add(p => p.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find($"#catch-provenance-angler-{OtherUserId:D}").Should().NotBeNull());
        network.ConnectivityChanged += Raise.Event<Action<bool>>(false);
        cut.WaitForAssertion(() => cut.Find("#catch-provenance-offline").Should().NotBeNull());

        // Act
        network.ConnectivityChanged += Raise.Event<Action<bool>>(true);

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#catch-provenance-offline").Should().BeEmpty());
        await catchClient.Received(1).GetAsync(CatchId, Arg.Any<CancellationToken>());
        await participantClient.Received(1).GetAsync(TripId, Arg.Any<CancellationToken>());
    }
}
