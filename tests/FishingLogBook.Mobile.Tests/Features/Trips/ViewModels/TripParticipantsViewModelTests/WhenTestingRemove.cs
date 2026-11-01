using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripParticipantsViewModelTests;

public sealed class WhenTestingRemove : BaseTripParticipantsViewModelTest
{
    [Fact]
    public async Task ItShouldRemoveAnAnglerAndShowTheRemainingParticipants()
    {
        // Arrange
        ServeParticipants(Participants(TripParticipantConstants.Owner, Owner(), Friend()));
        await Sut.LoadAsync(TripId, CancellationToken.None);
        MockParticipants.RemoveAsync(TripId, FriendId, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripParticipantsDto>.Succeeded(
            Participants(TripParticipantConstants.Owner, Owner())));
        // Act
        await Sut.RemoveAsync(FriendId, CancellationToken.None);
        // Assert
        Sut.Participants.Should().ContainSingle(participant => participant.UserId == ViewerId);
        await MockParticipants.Received(1).RemoveAsync(TripId, FriendId, Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task ItShouldReportAFailedRemoval()
    {
        // Arrange
        ServeParticipants(Participants(TripParticipantConstants.Owner, Owner(), Friend()));
        await Sut.LoadAsync(TripId, CancellationToken.None);
        MockParticipants.RemoveAsync(TripId, FriendId, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripParticipantsDto>.Failed(ApiOutcomeEnum.Unavailable));
        // Act
        await Sut.RemoveAsync(FriendId, CancellationToken.None);
        // Assert
        Sut.MessageText.Should().Be(Text["TripParticipantsActionFailed"]);
        Sut.Participants.Should().HaveCount(2);
    }
}
