using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripParticipantsViewModelTests;

public sealed class WhenTestingLoad : BaseTripParticipantsViewModelTest
{
    [Fact]
    public async Task ItShouldExplainThatANewPlanMustBeSavedBeforeManagingParticipants()
    {
        // Arrange

        // Act
        await Sut.LoadAsync(Guid.Empty, CancellationToken.None);

        // Assert
        Sut.MessageText.Should().Be(Text["TripPlanInviteHint"]);
        Sut.CanInvite.Should().BeFalse();
        Sut.CanRetry.Should().BeFalse();
        await MockParticipants.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowParticipantsWithoutManagementActionsOnAReadOnlyPlan()
    {
        // Arrange
        Sut.AllowManagement = false;
        ServeParticipants(Participants(TripParticipantConstants.Owner, Owner(), Friend()));

        // Act
        await Sut.LoadAsync(TripId, CancellationToken.None);

        // Assert
        Sut.Participants.Should().HaveCount(2);
        Sut.Participants.Should().OnlyContain(person => !person.CanRemove);
        Sut.CanInvite.Should().BeFalse();
        await MockParticipants.Received(1).GetAsync(TripId, Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task ItShouldExplainThatParticipantsNeedAConnectionWhenOffline()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        // Act
        await Sut.LoadAsync(TripId, CancellationToken.None);
        // Assert
        Sut.MessageText.Should().Be(Text["TripParticipantsLoadFailed"]);
        Sut.IsLoading.Should().BeFalse();
        await MockParticipants.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task ItShouldReportWhenTheParticipantsCannotBeLoaded()
    {
        // Arrange
        MockParticipants.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripParticipantsDto>.Failed(ApiOutcomeEnum.Unavailable));
        // Act
        await Sut.LoadAsync(TripId, CancellationToken.None);
        // Assert
        Sut.MessageText.Should().Be(Text["TripParticipantsLoadFailed"]);
        Sut.Participants.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldOnlyLetTheOwnerInviteAndRemoveAndLabelEachParticipantStatus()
    {
        // Arrange
        ServeParticipants(Participants(TripParticipantConstants.Owner, Owner(), Friend(), new TripParticipantDto(PendingId, TripParticipantConstants.Pending, null, null, Now)));
        // Act
        await Sut.LoadAsync(TripId, CancellationToken.None);
        // Assert
        Sut.CanInvite.Should().BeTrue();
        Sut.ShowOwnerOnlyNote.Should().BeFalse();
        Sut.Participants.Select(participant => (participant.Name, participant.Status, participant.CanRemove)).Should().Equal(
            ("Me", Text["TripParticipantOwner"], false),
            ("Sam", Text["TripParticipantAccepted"], true),
            (Text["TripContributorUnknown"], Text["TripParticipantPending"], true));
    }



    [Fact]
    public async Task ItShouldTellAParticipantOnlyTheOwnerCanInviteOrRemove()
    {
        // Arrange
        ServeParticipants(Participants(TripParticipantConstants.Participant, Friend() with { }, new TripParticipantDto(ViewerId, TripParticipantConstants.Accepted, "Me", null, Now)));
        // Act
        await Sut.LoadAsync(TripId, CancellationToken.None);
        // Assert
        Sut.CanInvite.Should().BeFalse();
        Sut.ShowOwnerOnlyNote.Should().BeTrue();
        Sut.Participants.Should().OnlyContain(participant => !participant.CanRemove);
    }


}
