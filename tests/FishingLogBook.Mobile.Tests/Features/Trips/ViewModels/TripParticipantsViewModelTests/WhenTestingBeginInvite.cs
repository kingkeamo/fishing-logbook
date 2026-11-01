using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripParticipantsViewModelTests;

public sealed class WhenTestingBeginInvite : BaseTripParticipantsViewModelTest
{
    [Fact]
    public async Task ItShouldNotOpenTheSearchForAParticipant()
    {
        // Arrange
        ServeParticipants(Participants(TripParticipantConstants.Participant, Owner(), Friend()));
        await Sut.LoadAsync(TripId, CancellationToken.None);

        // Act
        Sut.BeginInvite();

        // Assert
        Sut.ShowInviteSearch.Should().BeFalse();
        Sut.ShowInviteAction.Should().BeFalse();
        await MockParticipants.DidNotReceive().InviteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldExpandTheSharedSearchWithoutChangingMembership()
    {
        // Arrange
        await LoadOwnedTripAsync();
        var notified = new List<string?>();
        Sut.PropertyChanged += (_, args) => notified.Add(args.PropertyName);

        // Act
        Sut.BeginInvite();

        // Assert
        Sut.ShowInviteSearch.Should().BeTrue();
        Sut.ShowInviteAction.Should().BeFalse();
        Sut.Participants.Should().ContainSingle();
        notified.Should().NotBeEmpty();
        await MockParticipants.DidNotReceive().InviteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
