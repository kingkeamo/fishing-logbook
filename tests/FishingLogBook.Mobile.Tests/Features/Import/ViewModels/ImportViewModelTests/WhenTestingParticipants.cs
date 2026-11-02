using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Import.ViewModels.ImportViewModelTests;

public sealed class WhenTestingParticipants : BaseImportViewModelTest
{
    [Fact]
    public async Task ItShouldAddAnAnglerToANewTripProposal()
    {
        // Arrange
        await ReachTripReviewAsync();
        var tripId = Sut.Trips.Single().Proposal.Id;
        Sut.DecideCreateTrip(tripId);
        var angler = new AnglerSummaryDto(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            "Pat Lake",
            null,
            null,
            "pat@example.test");
        MockModals.ChooseAnglerAsync(Arg.Any<IReadOnlyList<Guid>>()).Returns(angler);

        // Act
        await Sut.AddTripParticipantAsync(tripId);

        // Assert
        var trip = Sut.Trips.Single();
        trip.ShowParticipants.Should().BeTrue();
        trip.Participants.Should().ContainSingle(participant =>
            participant.UserId == angler.UserId && participant.Name == "Pat Lake");
        trip.Proposal.Participants.Should().ContainSingle(participant => participant.UserId == angler.UserId);
        await MockModals.Received(1).ChooseAnglerAsync(Arg.Is<IReadOnlyList<Guid>>(ids => ids.Count == 0));
    }

    [Fact]
    public async Task ItShouldRemoveAnAddedAngler()
    {
        // Arrange
        await ReachTripReviewAsync();
        var tripId = Sut.Trips.Single().Proposal.Id;
        Sut.DecideCreateTrip(tripId);
        var angler = new AnglerSummaryDto(Guid.NewGuid(), "Pat Lake", null, null, null);
        MockModals.ChooseAnglerAsync(Arg.Any<IReadOnlyList<Guid>>()).Returns(angler);
        await Sut.AddTripParticipantAsync(tripId);

        // Act
        Sut.RemoveTripParticipant(tripId, angler.UserId);

        // Assert
        Sut.Trips.Single().Participants.Should().BeEmpty();
        Sut.Trips.Single().Proposal.Participants.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldClearParticipantsWhenTheTripIsKeptSeparate()
    {
        // Arrange
        await ReachTripReviewAsync();
        var tripId = Sut.Trips.Single().Proposal.Id;
        Sut.DecideCreateTrip(tripId);
        MockModals.ChooseAnglerAsync(Arg.Any<IReadOnlyList<Guid>>())
            .Returns(new AnglerSummaryDto(Guid.NewGuid(), "Pat Lake", null, null, null));
        await Sut.AddTripParticipantAsync(tripId);

        // Act
        Sut.DecideKeepSeparate(tripId);

        // Assert
        Sut.Trips.Single().ShowParticipants.Should().BeFalse();
        Sut.Trips.Single().Proposal.Participants.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotAddAnAnglerUnlessTheTripWillBeCreated()
    {
        // Arrange
        await ReachTripReviewAsync();
        var tripId = Sut.Trips.Single().Proposal.Id;
        Sut.DecideKeepSeparate(tripId);

        // Act
        await Sut.AddTripParticipantAsync(tripId);

        // Assert
        await MockModals.DidNotReceive().ChooseAnglerAsync(Arg.Any<IReadOnlyList<Guid>>());
        Sut.Trips.Single().Proposal.Participants.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRequireAnOnlineConnectionBeforeOpeningTheAnglerPicker()
    {
        // Arrange
        await ReachTripReviewAsync();
        var tripId = Sut.Trips.Single().Proposal.Id;
        Sut.DecideCreateTrip(tripId);
        MockConnectivity.IsOnline.Returns(false);

        // Act
        await Sut.AddTripParticipantAsync(tripId);

        // Assert
        Sut.Message.Should().Be(Text["Import_OnlineRequired"]);
        await MockModals.DidNotReceive().ChooseAnglerAsync(Arg.Any<IReadOnlyList<Guid>>());
    }

    [Fact]
    public async Task ItShouldIgnoreACancelledAnglerPicker()
    {
        // Arrange
        await ReachTripReviewAsync();
        var tripId = Sut.Trips.Single().Proposal.Id;
        Sut.DecideCreateTrip(tripId);
        MockModals.ChooseAnglerAsync(Arg.Any<IReadOnlyList<Guid>>()).Returns((AnglerSummaryDto?)null);

        // Act
        await Sut.AddTripParticipantAsync(tripId);

        // Assert
        Sut.Trips.Single().Participants.Should().BeEmpty();
    }
}
