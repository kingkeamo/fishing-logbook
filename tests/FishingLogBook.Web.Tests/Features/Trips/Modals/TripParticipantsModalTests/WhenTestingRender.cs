using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Modals.InviteAngler;
using FishingLogBook.Web.Features.Trips.Modals.TripParticipants;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Modals.TripParticipantsModalTests;

public class WhenTestingRender : BaseTripParticipantsModalTest
{
    [Fact]
    public async Task ItShouldWarnWhenTheParticipantsCannotBeRead()
    {
        // Arrange
        var client = Substitute.For<ITripParticipantClient>();
        client.GetAsync(TripId, Arg.Any<CancellationToken>())
            .Returns((TripParticipantsDto?)null);
        await using var context = CreateContext(client);

        // Act
        var cut = await ShowModalAsync(context);

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-participants-load-failed").Should().NotBeNull());
        cut.FindAll("#trip-participants-invite").Should().BeEmpty();
        await client.Received(1).GetAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldHideTheInviteActionFromAParticipant()
    {
        // Arrange
        var client = Substitute.For<ITripParticipantClient>();
        client.GetAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Participants(TripParticipantConstants.Participant, Owner(), Participant()));
        await using var context = CreateContext(client);

        // Act
        var cut = await ShowModalAsync(context);

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-participants-list").Should().NotBeNull());
        cut.FindAll("#trip-participants-invite").Should().BeEmpty();
        cut.Find("#trip-participants-owner-only").Should().NotBeNull();
        cut.FindAll($"#trip-participant-remove-{ParticipantUserId:D}").Should().BeEmpty();
        await client.Received(1).GetAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowAPlaceholderForAnAnglerWhoHidesTheirName()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var client = Substitute.For<ITripParticipantClient>();
        client.GetAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Participants(
                TripParticipantConstants.Owner,
                Owner(),
                Participant(displayName: null)));
        await using var context = CreateContext(client);

        // Act
        var cut = await ShowModalAsync(context);

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-participant-{ParticipantUserId:D}").TextContent
                .Should().Contain("Another angler"));
    }

    [Fact]
    public async Task ItShouldLabelAPendingInvitation()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var client = Substitute.For<ITripParticipantClient>();
        client.GetAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Participants(
                TripParticipantConstants.Owner,
                Owner(),
                Participant(status: TripParticipantConstants.Pending)));
        await using var context = CreateContext(client);

        // Act
        var cut = await ShowModalAsync(context);

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-participant-status-{ParticipantUserId:D}").TextContent
                .Should().Contain("Invitation pending"));
    }

    [Fact]
    public async Task ItShouldShowFrenchCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var client = Substitute.For<ITripParticipantClient>();
        client.GetAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Participants(TripParticipantConstants.Owner, Owner(), Participant()));
        await using var context = CreateContext(client);

        // Act
        var cut = await ShowModalAsync(context);

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-participants-invite").TextContent.Should().Contain("Inviter un pêcheur"));
        cut.Find("#trip-participants-modal-title").TextContent
            .Should().Contain("Pêcheurs de cette sortie");
    }

    [Fact]
    public async Task ItShouldOfferTheOwnerInviteAndRemoveActions()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var client = Substitute.For<ITripParticipantClient>();
        client.GetAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Participants(TripParticipantConstants.Owner, Owner(), Participant()));
        await using var context = CreateContext(client);

        // Act
        var cut = await ShowModalAsync(context);

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-participants-invite").Should().NotBeNull());
        cut.Find($"#trip-participant-remove-{ParticipantUserId:D}").Should().NotBeNull();
        cut.FindAll($"#trip-participant-remove-{OwnerUserId:D}").Should().BeEmpty();
        cut.Find($"#trip-participant-status-{OwnerUserId:D}").TextContent
            .Should().Contain("Started this trip");
        await client.Received(1).GetAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOpenTheInviteModalForThisTrip()
    {
        // Arrange
        var client = Substitute.For<ITripParticipantClient>();
        client.GetAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Participants(TripParticipantConstants.Owner, Owner()));
        var modalService = Substitute.For<IModalService>();
        modalService
            .ShowAsync<InviteAnglerModal, InviteAnglerModalModel, InviteAnglerModalResult>(
                Arg.Any<InviteAnglerModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(new InviteAnglerModalResult(
                Participants(TripParticipantConstants.Owner, Owner(), Participant("Pending"))));
        await using var context = CreateContext(client, modalService);
        var cut = await ShowModalAsync(context);
        cut.WaitForAssertion(() => cut.Find("#trip-participants-invite").Should().NotBeNull());

        // Act
        await cut.Find("#trip-participants-invite").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-participant-{ParticipantUserId:D}").Should().NotBeNull());
        await modalService.Received(1)
            .ShowAsync<InviteAnglerModal, InviteAnglerModalModel, InviteAnglerModalResult>(
                Arg.Is<InviteAnglerModalModel>(model => model.TripId == TripId),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRemoveAParticipantAfterConfirmation()
    {
        // Arrange
        var client = Substitute.For<ITripParticipantClient>();
        client.GetAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Participants(TripParticipantConstants.Owner, Owner(), Participant()));
        client.RemoveAsync(TripId, ParticipantUserId, Arg.Any<CancellationToken>())
            .Returns(Participants(TripParticipantConstants.Owner, Owner()));
        var modalService = Substitute.For<IModalService>();
        modalService.ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>())
            .Returns(true);
        await using var context = CreateContext(client, modalService);
        var cut = await ShowModalAsync(context);
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-participant-remove-{ParticipantUserId:D}").Should().NotBeNull());

        // Act
        await cut.Find($"#trip-participant-remove-{ParticipantUserId:D}").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.FindAll($"#trip-participant-{ParticipantUserId:D}").Should().BeEmpty());
        await client.Received(1).RemoveAsync(
            TripId,
            ParticipantUserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRemoveAParticipantWhenTheConfirmationIsCancelled()
    {
        // Arrange
        var client = Substitute.For<ITripParticipantClient>();
        client.GetAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Participants(TripParticipantConstants.Owner, Owner(), Participant()));
        var modalService = Substitute.For<IModalService>();
        modalService.ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>())
            .Returns(false);
        await using var context = CreateContext(client, modalService);
        var cut = await ShowModalAsync(context);
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-participant-remove-{ParticipantUserId:D}").Should().NotBeNull());

        // Act
        await cut.Find($"#trip-participant-remove-{ParticipantUserId:D}").ClickAsync();

        // Assert
        cut.Find($"#trip-participant-{ParticipantUserId:D}").Should().NotBeNull();
        await client.DidNotReceive().RemoveAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldWarnWhenTheRemovalIsRefused()
    {
        // Arrange
        var client = Substitute.For<ITripParticipantClient>();
        client.GetAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Participants(TripParticipantConstants.Owner, Owner(), Participant()));
        client.RemoveAsync(TripId, ParticipantUserId, Arg.Any<CancellationToken>())
            .Returns((TripParticipantsDto?)null);
        var modalService = Substitute.For<IModalService>();
        modalService.ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>())
            .Returns(true);
        await using var context = CreateContext(client, modalService);
        var cut = await ShowModalAsync(context);
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-participant-remove-{ParticipantUserId:D}").Should().NotBeNull());

        // Act
        await cut.Find($"#trip-participant-remove-{ParticipantUserId:D}").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-participants-action-failed").Should().NotBeNull());
        cut.Find($"#trip-participant-{ParticipantUserId:D}").Should().NotBeNull();
        await client.Received(1).RemoveAsync(
            TripId,
            ParticipantUserId,
            Arg.Any<CancellationToken>());
    }
}
