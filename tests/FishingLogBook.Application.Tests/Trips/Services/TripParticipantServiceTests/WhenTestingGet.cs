using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripParticipantServiceTests;

public class WhenTestingGet : BaseTripParticipantServiceTest
{
    [Fact]
    public async Task ItShouldReportNotFoundForAnAnglerWhoIsNotOnTheTrip()
    {
        // Arrange
        GivenNoAccess();

        // Act
        var result = await Sut.GetAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
        await MockTripParticipantRepository.DidNotReceive().GetByTripIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldHideDeclinedAndRemovedAnglers()
    {
        // Arrange
        MockTripParticipantRepository.GetByTripIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripParticipant>>(
            [
                Membership(InvitedUserId, TripParticipantStatusEnum.Accepted),
                Membership(OtherUserId, TripParticipantStatusEnum.Declined)
            ]));

        // Act
        var result = await Sut.GetAsync(Args(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Participants.Select(participant => participant.UserId)
            .Should().BeEquivalentTo([CurrentUserId, InvitedUserId]);
        await MockTripParticipantRepository.Received(1).GetByTripIdAsync(
            TripId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTellAParticipantTheyAreNotTheOwner()
    {
        // Arrange
        GivenParticipantView();

        // Act
        var result = await Sut.GetAsync(Args(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(TripParticipantConstants.Participant);
    }

    [Fact]
    public async Task ItShouldListTheOwnerFirstWithTheirPrivacyFilteredName()
    {
        // Arrange
        MockAnglerLookupService
            .DescribeAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyDictionary<Guid, AnglerSummaryDto>>(
                new Dictionary<Guid, AnglerSummaryDto>
                {
                    [CurrentUserId] = new(CurrentUserId, "Eamonn", null, null),
                    [InvitedUserId] = new(InvitedUserId, null, null, null)
                }));
        MockTripParticipantRepository.GetByTripIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripParticipant>>(
                [Membership(InvitedUserId, TripParticipantStatusEnum.Pending)]));

        // Act
        var result = await Sut.GetAsync(Args(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(TripParticipantConstants.Owner);
        result.Value.Participants[0].IsOwner.Should().BeTrue();
        result.Value.Participants[0].DisplayName.Should().Be("Eamonn");
        result.Value.Participants[1].UserId.Should().Be(InvitedUserId);
        result.Value.Participants[1].Status.Should().Be(TripParticipantConstants.Pending);
        result.Value.Participants[1].DisplayName.Should().BeNull();
        await MockAnglerLookupService.Received(1).DescribeAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(userIds =>
                userIds.Contains(CurrentUserId) && userIds.Contains(InvitedUserId)),
            Arg.Any<CancellationToken>());
    }

    private static GetTripParticipantsArgs Args()
    {
        return new GetTripParticipantsArgs { TripId = TripId };
    }
}
