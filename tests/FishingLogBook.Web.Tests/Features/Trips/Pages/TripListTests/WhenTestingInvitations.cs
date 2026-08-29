using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;
using TripListPage = FishingLogBook.Web.Features.Trips.Pages.TripList.TripList;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.TripListTests;

public class WhenTestingInvitations : BaseTripListTest
{
    private static readonly Guid SharedTripId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
    private static readonly Guid InvitingOwnerUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task ItShouldShowNoInvitationsWhenThereAreNone()
    {
        // Arrange
        var participantClient = QuietParticipantClient();
        await using var context = CreateContext(
            StoreWith(),
            ClientWith(),
            participantClient: participantClient);

        // Act
        var cut = context.Render<TripListPage>();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#trip-invitations").Should().BeEmpty());
        await participantClient.Received(1).GetMyInvitationsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStayUsableWhenTheInvitationsCannotBeRead()
    {
        // Arrange
        var participantClient = Substitute.For<ITripParticipantClient>();
        participantClient.GetMyInvitationsAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<TripInvitationDto>>(_ => throw new HttpRequestException("boom"));
        await using var context = CreateContext(
            StoreWith(),
            ClientWith(),
            participantClient: participantClient);

        // Act
        var cut = context.Render<TripListPage>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-list-heading").Should().NotBeNull());
        cut.FindAll("#trip-invitations").Should().BeEmpty();
        await participantClient.Received(1).GetMyInvitationsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowAPendingInvitationWithAcceptAndDecline()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var participantClient = ParticipantClientWith(Invitation());
        await using var context = CreateContext(
            StoreWith(),
            ClientWith(),
            participantClient: participantClient);

        // Act
        var cut = context.Render<TripListPage>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-invitation-{SharedTripId:D}").Should().NotBeNull());
        cut.Find($"#trip-invitation-from-{SharedTripId:D}").TextContent
            .Should().Contain("Mark invited you to fish this trip.");
        cut.Find($"#trip-invitation-accept-{SharedTripId:D}").Should().NotBeNull();
        cut.Find($"#trip-invitation-decline-{SharedTripId:D}").Should().NotBeNull();
        await participantClient.Received(1).GetMyInvitationsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNameAnUnknownInviterSafely()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var participantClient = ParticipantClientWith(Invitation(ownerDisplayName: null));
        await using var context = CreateContext(
            StoreWith(),
            ClientWith(),
            participantClient: participantClient);

        // Act
        var cut = context.Render<TripListPage>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-invitation-from-{SharedTripId:D}").TextContent
                .Should().Contain("Another angler invited you"));
    }

    [Fact]
    public async Task ItShouldKeepTheInvitationWhenTheDeclineIsRefused()
    {
        // Arrange
        var participantClient = ParticipantClientWith(Invitation());
        participantClient.DeclineAsync(SharedTripId, Arg.Any<CancellationToken>()).Returns(false);
        await using var context = CreateContext(
            StoreWith(),
            ClientWith(),
            participantClient: participantClient);
        var cut = context.Render<TripListPage>();
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-invitation-decline-{SharedTripId:D}").Should().NotBeNull());

        // Act
        await cut.Find($"#trip-invitation-decline-{SharedTripId:D}").ClickAsync();

        // Assert
        cut.Find($"#trip-invitation-{SharedTripId:D}").Should().NotBeNull();
        await participantClient.Received(1).DeclineAsync(SharedTripId, Arg.Any<CancellationToken>());
        await participantClient.Received(1).GetMyInvitationsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDeclineTheInvitationAndDropItFromTheList()
    {
        // Arrange
        var participantClient = ParticipantClientWith(Invitation());
        participantClient.DeclineAsync(SharedTripId, Arg.Any<CancellationToken>()).Returns(true);
        await using var context = CreateContext(
            StoreWith(),
            ClientWith(),
            participantClient: participantClient);
        var cut = context.Render<TripListPage>();
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-invitation-decline-{SharedTripId:D}").Should().NotBeNull());
        participantClient.GetMyInvitationsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TripInvitationDto>>([]));

        // Act
        await cut.Find($"#trip-invitation-decline-{SharedTripId:D}").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#trip-invitations").Should().BeEmpty());
        await participantClient.Received(1).DeclineAsync(SharedTripId, Arg.Any<CancellationToken>());
        await participantClient.DidNotReceive().AcceptAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAcceptTheInvitationAndListTheSharedTripUnderTheSameId()
    {
        // Arrange
        var participantClient = ParticipantClientWith(Invitation());
        participantClient.AcceptAsync(SharedTripId, Arg.Any<CancellationToken>()).Returns(true);
        var tripClient = Substitute.For<ITripClient>();
        tripClient.GetMyAsync(Arg.Any<CancellationToken>())
            .Returns(
                [],
                [
                    new TripSummaryDto(SharedTripId, TripConstants.Active, StartedOn)
                    {
                        OwnerUserId = InvitingOwnerUserId,
                        Role = TripParticipantConstants.Participant,
                        ParticipantCount = 1
                    }
                ]);
        await using var context = CreateContext(
            StoreWith(),
            tripClient,
            participantClient: participantClient);
        var cut = context.Render<TripListPage>();
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-invitation-accept-{SharedTripId:D}").Should().NotBeNull());
        participantClient.GetMyInvitationsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TripInvitationDto>>([]));

        // Act
        await cut.Find($"#trip-invitation-accept-{SharedTripId:D}").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-list-item-{SharedTripId:D}").Should().NotBeNull());
        cut.Find($"#trip-list-shared-{SharedTripId:D}").Should().NotBeNull();
        await participantClient.Received(1).AcceptAsync(SharedTripId, Arg.Any<CancellationToken>());
        await tripClient.Received(2).GetMyAsync(Arg.Any<CancellationToken>());
    }

    private static ITripParticipantClient ParticipantClientWith(params TripInvitationDto[] invitations)
    {
        var client = Substitute.For<ITripParticipantClient>();
        client.GetMyInvitationsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TripInvitationDto>>(invitations));
        return client;
    }

    private static TripInvitationDto Invitation(string? ownerDisplayName = "Mark")
    {
        return new TripInvitationDto(
            SharedTripId,
            InvitingOwnerUserId,
            ownerDisplayName,
            null,
            "Lough Corrib",
            StartedOn,
            StartedOn.AddMinutes(-30));
    }
}
