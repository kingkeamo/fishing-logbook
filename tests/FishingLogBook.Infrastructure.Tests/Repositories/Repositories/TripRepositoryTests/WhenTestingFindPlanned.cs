using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.TripRepositoryTests;

public class WhenTestingFindPlanned : BaseTripRepositoryTest
{
    private static readonly DateTimeOffset From = DateTimeOffset.Parse("2026-10-01T09:00:00Z");
    private static readonly DateTimeOffset To = DateTimeOffset.Parse("2026-10-07T09:00:00Z");

    public WhenTestingFindPlanned(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldIncludeTripsPlannedExactlyOnTheWindowEdges()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var atFrom = await PlanAsync(ownerUserId, From);
        var atTo = await PlanAsync(ownerUserId, To);

        // Act
        var result = await FindAsync(ownerUserId);

        // Assert
        result.Select(trip => trip.Id).Should().Equal(atFrom, atTo);
    }

    [Fact]
    public async Task ItShouldExcludeTripsPlannedJustOutsideTheWindow()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        await PlanAsync(ownerUserId, From.AddSeconds(-1));
        await PlanAsync(ownerUserId, To.AddSeconds(1));
        var inside = await PlanAsync(ownerUserId, From.AddDays(1));

        // Act
        var result = await FindAsync(ownerUserId);

        // Assert
        result.Select(trip => trip.Id).Should().Equal(inside);
    }

    [Fact]
    public async Task ItShouldReturnOnlyPlannedTrips()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var planned = await PlanAsync(ownerUserId, From.AddDays(1));
        var cancelled = await PlanAsync(ownerUserId, From.AddDays(2));
        await Sut.UpsertAsync(
            NewTrip(ownerUserId, cancelled, TripStatusEnum.Cancelled, plannedStartOn: From.AddDays(2)),
            CancellationToken.None);
        await Sut.UpsertAsync(NewTrip(ownerUserId, startedOn: From.AddDays(1)), CancellationToken.None);

        // Act
        var result = await FindAsync(ownerUserId);

        // Assert
        result.Select(trip => trip.Id).Should().Equal(planned);
    }

    [Fact]
    public async Task ItShouldNotReturnAnotherAnglersUnsharedTrip()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var strangerUserId = await CreateUserAsync();
        await PlanAsync(ownerUserId, From.AddDays(1));

        // Act
        var result = await FindAsync(strangerUserId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReturnATripForAnAngelWhoAcceptedTheInvitation()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var guestUserId = await CreateUserAsync();
        var tripId = await PlanAsync(ownerUserId, From.AddDays(1));
        await InviteAsync(tripId, ownerUserId, guestUserId, TripParticipantStatusEnum.Accepted);

        // Act
        var result = await FindAsync(guestUserId);

        // Assert
        result.Select(trip => trip.Id).Should().Equal(tripId);
    }

    [Theory]
    [InlineData(TripParticipantStatusEnum.Pending)]
    [InlineData(TripParticipantStatusEnum.Declined)]
    public async Task ItShouldNotReturnATripForAnAnglerWhoHasNotAccepted(TripParticipantStatusEnum status)
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var guestUserId = await CreateUserAsync();
        var tripId = await PlanAsync(ownerUserId, From.AddDays(1));
        await InviteAsync(tripId, ownerUserId, guestUserId, status);

        // Act
        var result = await FindAsync(guestUserId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotReturnATripForAnAnglerWhoWasRemovedAfterAccepting()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var guestUserId = await CreateUserAsync();
        var tripId = await PlanAsync(ownerUserId, From.AddDays(1));
        var accepted = await InviteAsync(tripId, ownerUserId, guestUserId, TripParticipantStatusEnum.Accepted);
        await Participants.UpsertAsync(accepted.RemovedAt(From), CancellationToken.None);

        // Act
        var result = await FindAsync(guestUserId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldOrderTripsByPlannedStartThenId()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var later = await PlanAsync(ownerUserId, From.AddDays(3));
        var earlier = await PlanAsync(ownerUserId, From.AddDays(1));

        // Act
        var result = await FindAsync(ownerUserId);

        // Assert
        result.Select(trip => trip.Id).Should().Equal(earlier, later);
    }

    private async Task<IReadOnlyList<Trip>> FindAsync(Guid userId)
    {
        var result = await Sut.FindPlannedAsync(
            new FindPlannedTripsArgs { UserId = userId, From = From, To = To },
            CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    private async Task<Guid> PlanAsync(Guid ownerUserId, DateTimeOffset plannedStartOn)
    {
        var trip = NewTrip(ownerUserId, status: TripStatusEnum.Planned, plannedStartOn: plannedStartOn);
        var saved = await Sut.UpsertAsync(trip, CancellationToken.None);
        saved.IsSuccess.Should().BeTrue();
        return trip.Id;
    }

    private async Task<TripParticipant> InviteAsync(
        Guid tripId,
        Guid ownerUserId,
        Guid guestUserId,
        TripParticipantStatusEnum status)
    {
        var invitedOn = From.AddDays(-1);
        var participant = new TripParticipant
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            UserId = guestUserId,
            Status = status,
            InvitedByUserId = ownerUserId,
            InvitedOn = invitedOn,
            RespondedOn = status == TripParticipantStatusEnum.Pending ? null : invitedOn.AddHours(1)
        };
        var saved = await Participants.UpsertAsync(participant, CancellationToken.None);
        saved.IsSuccess.Should().BeTrue();
        return saved.Value;
    }
}
