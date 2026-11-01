using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripListViewModelTests;

public sealed class WhenTestingRespond : BaseTripListViewModelTest
{
    private static readonly Guid TripId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task ItShouldKeepTheInvitationAndShowAnErrorWhenDecliningFails()
    {
        // Arrange
        MockParticipants.RespondAsync(TripId, false, Arg.Any<CancellationToken>()).Returns(ApiResultModel<NoContentModel>.Failed(ApiOutcomeEnum.Unavailable));
        // Act
        await Sut.DeclineAsync(TripId, CancellationToken.None);
        // Assert
        Sut.ActionFailureText.Should().Be(Text["TripParticipantsActionFailed"]);
        await MockTrips.DidNotReceive().LoadAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReloadTheListAfterDecliningAnInvitation()
    {
        // Arrange
        MockParticipants.RespondAsync(TripId, false, Arg.Any<CancellationToken>()).Returns(ApiResultModel<NoContentModel>.Succeeded(NoContentModel.Instance));
        // Act
        await Sut.DeclineAsync(TripId, CancellationToken.None);
        // Assert
        Sut.HasActionFailure.Should().BeFalse();
        await MockParticipants.Received(1).RespondAsync(TripId, false, Arg.Any<CancellationToken>());
        await MockParticipants.DidNotReceive().RespondAsync(TripId, true, Arg.Any<CancellationToken>());
        await MockTrips.Received(1).LoadAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReloadTheListSoTheAcceptedTripAppearsAfterAcceptingAnInvitation()
    {
        // Arrange
        MockParticipants.RespondAsync(TripId, true, Arg.Any<CancellationToken>()).Returns(ApiResultModel<NoContentModel>.Succeeded(NoContentModel.Instance));
        // Act
        await Sut.AcceptAsync(TripId, CancellationToken.None);
        // Assert
        await MockParticipants.Received(1).RespondAsync(TripId, true, Arg.Any<CancellationToken>());
        await MockTrips.Received(1).LoadAsync(Arg.Any<CancellationToken>());
        Sut.CanInteract.Should().BeTrue();
    }
}
