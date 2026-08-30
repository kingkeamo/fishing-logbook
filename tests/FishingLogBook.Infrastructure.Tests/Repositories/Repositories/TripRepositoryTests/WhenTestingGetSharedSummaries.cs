using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.TripRepositoryTests;

public class WhenTestingGetSharedSummaries : BaseTripRepositoryTest
{
    private readonly TripParticipantRepository _participants;

    public WhenTestingGetSharedSummaries(PostgresFixture fixture)
        : base(fixture)
    {
        _participants = new TripParticipantRepository(
            ConnectionFactory,
            NullLogger<TripParticipantRepository>.Instance,
            TestMapper.Create());
    }

    [Fact]
    public async Task ItShouldNotListATripTheAnglerWasOnlyInvitedTo()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var invitedUserId = await CreateUserAsync();
        var trip = NewTrip(ownerUserId, status: TripStatusEnum.Completed, endedOn: StartedOn.AddHours(2));
        await Sut.UpsertAsync(trip, CancellationToken.None);
        await AddParticipantAsync(trip.Id, invitedUserId, ownerUserId, TripParticipantStatusEnum.Pending);

        // Act
        var result = await Sut.GetSummariesForUserAsync(
            new GetMyTripsArgs { UserId = invitedUserId },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotListATripAfterTheAnglerWasRemoved()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var participantUserId = await CreateUserAsync();
        var trip = NewTrip(ownerUserId, status: TripStatusEnum.Completed, endedOn: StartedOn.AddHours(2));
        await Sut.UpsertAsync(trip, CancellationToken.None);
        await AddParticipantAsync(
            trip.Id,
            participantUserId,
            ownerUserId,
            TripParticipantStatusEnum.Accepted,
            removedOn: StartedOn.AddHours(3));

        // Act
        var result = await Sut.GetSummariesForUserAsync(
            new GetMyTripsArgs { UserId = participantUserId },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldListTheSameTripIdForTheOwnerAndTheAcceptedParticipant()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var participantUserId = await CreateUserAsync();
        var trip = NewTrip(ownerUserId, status: TripStatusEnum.Completed, endedOn: StartedOn.AddHours(2));
        await Sut.UpsertAsync(trip, CancellationToken.None);
        await AddParticipantAsync(
            trip.Id,
            participantUserId,
            ownerUserId,
            TripParticipantStatusEnum.Accepted);

        // Act
        var forOwner = await Sut.GetSummariesForUserAsync(
            new GetMyTripsArgs { UserId = ownerUserId },
            CancellationToken.None);
        var forParticipant = await Sut.GetSummariesForUserAsync(
            new GetMyTripsArgs { UserId = participantUserId },
            CancellationToken.None);

        // Assert
        forOwner.Value.Should().ContainSingle();
        forParticipant.Value.Should().ContainSingle();
        forParticipant.Value[0].Id.Should().Be(forOwner.Value[0].Id);
        forParticipant.Value[0].Id.Should().Be(trip.Id);
        forParticipant.Value[0].OwnerUserId.Should().Be(ownerUserId);
        forParticipant.Value[0].ParticipantCount.Should().Be(1);
        forParticipant.Value[0].IsShared.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldCountEveryContributorsContentOnTheSharedTrip()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var participantUserId = await CreateUserAsync();
        var trip = NewTrip(ownerUserId, status: TripStatusEnum.Completed, endedOn: StartedOn.AddHours(2));
        await Sut.UpsertAsync(trip, CancellationToken.None);
        await AddParticipantAsync(
            trip.Id,
            participantUserId,
            ownerUserId,
            TripParticipantStatusEnum.Accepted);
        await AddCatchAsync(ownerUserId, trip.Id, "Pike");
        await AddCatchAsync(participantUserId, trip.Id, "Brown Trout");
        await AddNoteAsync(trip.Id, ownerUserId);
        await AddNoteAsync(trip.Id, participantUserId);
        await AddPhotographAsync(trip.Id, participantUserId);

        // Act
        var result = await Sut.GetSummariesForUserAsync(
            new GetMyTripsArgs { UserId = participantUserId },
            CancellationToken.None);

        // Assert
        result.Value[0].CatchCount.Should().Be(2);
        result.Value[0].NoteCount.Should().Be(2);
        result.Value[0].PhotographCount.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldNotDuplicateATripTheAnglerBothOwnsAndParticipatesIn()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var trip = NewTrip(ownerUserId, status: TripStatusEnum.Completed, endedOn: StartedOn.AddHours(2));
        await Sut.UpsertAsync(trip, CancellationToken.None);
        await AddParticipantAsync(trip.Id, otherUserId, ownerUserId, TripParticipantStatusEnum.Accepted);

        // Act
        var result = await Sut.GetSummariesForUserAsync(
            new GetMyTripsArgs { UserId = ownerUserId },
            CancellationToken.None);

        // Assert
        result.Value.Should().ContainSingle();
        result.Value[0].Id.Should().Be(trip.Id);
        result.Value[0].ParticipantCount.Should().Be(1);
    }

    private async Task AddParticipantAsync(
        Guid tripId,
        Guid userId,
        Guid invitedByUserId,
        TripParticipantStatusEnum status,
        DateTimeOffset? removedOn = null)
    {
        var saved = await _participants.UpsertAsync(
            new TripParticipant
            {
                Id = Guid.NewGuid(),
                TripId = tripId,
                UserId = userId,
                Status = status,
                InvitedByUserId = invitedByUserId,
                InvitedOn = StartedOn.AddDays(-1),
                RespondedOn = status == TripParticipantStatusEnum.Pending
                    ? null
                    : StartedOn.AddHours(-1),
                RemovedOn = removedOn
            },
            CancellationToken.None);
        if (saved.IsFailed)
        {
            throw new InvalidOperationException(saved.Errors[0].Message);
        }
    }
}
