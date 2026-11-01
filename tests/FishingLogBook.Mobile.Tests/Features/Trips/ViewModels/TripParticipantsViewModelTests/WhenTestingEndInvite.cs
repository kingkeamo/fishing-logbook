using AwesomeAssertions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripParticipantsViewModelTests;

public sealed class WhenTestingEndInvite : BaseTripParticipantsViewModelTest
{
    [Fact]
    public async Task ItShouldCloseAndClearTheSearchWithoutChangingParticipants()
    {
        // Arrange
        await LoadOwnedTripAsync();
        Sut.BeginInvite();
        Sut.SearchText = "angler";

        // Act
        Sut.EndInvite();

        // Assert
        Sut.ShowInviteSearch.Should().BeFalse();
        Sut.ShowInviteAction.Should().BeTrue();
        Sut.SearchText.Should().BeEmpty();
        Sut.Participants.Should().ContainSingle();
        await MockParticipants.DidNotReceive().InviteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
