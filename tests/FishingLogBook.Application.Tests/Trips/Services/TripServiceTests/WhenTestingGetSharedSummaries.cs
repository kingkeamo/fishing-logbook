using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripServiceTests;

public class WhenTestingGetSharedSummaries : BaseTripServiceTest
{
    [Fact]
    public async Task ItShouldMarkATripTheAnglerOwns()
    {
        // Arrange
        GivenSummaries(Summary(ownerUserId: CurrentUserId));

        // Act
        var result = await Sut.GetSummariesAsync(
            new GetMyTripsArgs { UserId = CurrentUserId },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value[0].Role.Should().Be(TripParticipantConstants.Owner);
        result.Value[0].OwnerUserId.Should().Be(CurrentUserId);
        result.Value[0].IsShared.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldMarkASharedTripTheAnglerOwnsWithOtherParticipants()
    {
        // Arrange
        GivenSummaries(Summary(ownerUserId: CurrentUserId, participantCount: 2));

        // Act
        var result = await Sut.GetSummariesAsync(
            new GetMyTripsArgs { UserId = CurrentUserId },
            CancellationToken.None);

        // Assert
        result.Value[0].Role.Should().Be(TripParticipantConstants.Owner);
        result.Value[0].IsShared.Should().BeTrue();
        result.Value[0].ParticipantCount.Should().Be(2);
    }

    [Fact]
    public async Task ItShouldListASharedTripTheAnglerParticipatesInUnderTheSameId()
    {
        // Arrange
        GivenSummaries(Summary(ownerUserId: OtherUserId, participantCount: 1));

        // Act
        var result = await Sut.GetSummariesAsync(
            new GetMyTripsArgs { UserId = CurrentUserId },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].Id.Should().Be(TripId);
        result.Value[0].OwnerUserId.Should().Be(OtherUserId);
        result.Value[0].Role.Should().Be(TripParticipantConstants.Participant);
        result.Value[0].IsShared.Should().BeTrue();
        await MockTripRepository.Received(1).GetSummariesForUserAsync(
            Arg.Is<GetMyTripsArgs>(args => args.UserId == CurrentUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldListOwnedAndParticipatingTripsTogether()
    {
        // Arrange
        var sharedId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        GivenSummaries(
            Summary(ownerUserId: CurrentUserId),
            Summary(tripId: sharedId, ownerUserId: OtherUserId, participantCount: 1));

        // Act
        var result = await Sut.GetSummariesAsync(
            new GetMyTripsArgs { UserId = CurrentUserId },
            CancellationToken.None);

        // Assert
        result.Value.Should().HaveCount(2);
        result.Value.Single(summary => summary.Id == TripId).Role
            .Should().Be(TripParticipantConstants.Owner);
        result.Value.Single(summary => summary.Id == sharedId).Role
            .Should().Be(TripParticipantConstants.Participant);
    }

    private void GivenSummaries(params TripSummary[] summaries)
    {
        MockTripRepository.GetSummariesForUserAsync(
                Arg.Any<GetMyTripsArgs>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripSummary>>(summaries));
    }

    private static TripSummary Summary(
        Guid? tripId = null,
        Guid? ownerUserId = null,
        int participantCount = 0)
    {
        return new TripSummary
        {
            Id = tripId ?? TripId,
            OwnerUserId = ownerUserId ?? CurrentUserId,
            Status = TripStatusEnum.Active,
            StartedOn = StartedOn,
            ParticipantCount = participantCount
        };
    }
}
