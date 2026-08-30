using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.TripRepositoryTests;

public class WhenTestingActiveReconciliation : BaseTripRepositoryTest
{
    public WhenTestingActiveReconciliation(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldCompleteTheEarlierTripWhenALaterOneArrives()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var earlier = NewTrip(ownerUserId, startedOn: StartedOn);
        await Sut.UpsertAsync(earlier, CancellationToken.None);
        var later = NewTrip(ownerUserId, startedOn: StartedOn.AddHours(2));

        // Act
        var result = await Sut.UpsertAsync(later, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TripStatusEnum.Active);
        var stored = await Sut.GetSummariesForUserAsync(
            new GetMyTripsArgs { UserId = ownerUserId }, CancellationToken.None);
        stored.Value.Should().HaveCount(2);
        var demoted = stored.Value.Single(trip => trip.Id == earlier.Id);
        demoted.Status.Should().Be(TripStatusEnum.Completed);
        demoted.EndedOn.Should().Be(later.StartedOn);
        stored.Value.Single(trip => trip.Id == later.Id).Status.Should().Be(TripStatusEnum.Active);
    }

    [Fact]
    public async Task ItShouldStoreAnEarlierArrivalAsCompletedWhenALaterTripIsAlreadyActive()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var later = NewTrip(ownerUserId, startedOn: StartedOn.AddHours(2));
        await Sut.UpsertAsync(later, CancellationToken.None);
        var earlier = NewTrip(ownerUserId, startedOn: StartedOn);

        // Act
        var result = await Sut.UpsertAsync(earlier, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TripStatusEnum.Completed);
        result.Value.EndedOn.Should().Be(later.StartedOn);
        var stored = await Sut.GetSummariesForUserAsync(
            new GetMyTripsArgs { UserId = ownerUserId }, CancellationToken.None);
        stored.Value.Should().HaveCount(2);
        stored.Value.Single(trip => trip.Id == later.Id).Status.Should().Be(TripStatusEnum.Active);
    }

    [Fact]
    public async Task ItShouldReachTheSameOutcomeWhicheverOrderTheTripsArrive()
    {
        // Arrange
        var firstOwner = await CreateUserAsync();
        var secondOwner = await CreateUserAsync();
        var firstEarlier = NewTrip(firstOwner, startedOn: StartedOn);
        var firstLater = NewTrip(firstOwner, startedOn: StartedOn.AddHours(2));
        var secondEarlier = NewTrip(secondOwner, startedOn: StartedOn);
        var secondLater = NewTrip(secondOwner, startedOn: StartedOn.AddHours(2));

        // Act
        await Sut.UpsertAsync(firstEarlier, CancellationToken.None);
        await Sut.UpsertAsync(firstLater, CancellationToken.None);
        await Sut.UpsertAsync(secondLater, CancellationToken.None);
        await Sut.UpsertAsync(secondEarlier, CancellationToken.None);

        // Assert
        var first = await Sut.GetSummariesForUserAsync(
            new GetMyTripsArgs { UserId = firstOwner }, CancellationToken.None);
        var second = await Sut.GetSummariesForUserAsync(
            new GetMyTripsArgs { UserId = secondOwner }, CancellationToken.None);
        first.Value.Single(trip => trip.Status == TripStatusEnum.Active).Id.Should().Be(firstLater.Id);
        second.Value.Single(trip => trip.Status == TripStatusEnum.Active).Id.Should().Be(secondLater.Id);
        first.Value.Single(trip => trip.Id == firstEarlier.Id).EndedOn
            .Should().Be(second.Value.Single(trip => trip.Id == secondEarlier.Id).EndedOn);
    }

    [Fact]
    public async Task ItShouldLeaveExactlyOneActiveTripForTheOwner()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();

        // Act
        for (var hour = 0; hour < 4; hour++)
        {
            await Sut.UpsertAsync(
                NewTrip(ownerUserId, startedOn: StartedOn.AddHours(hour)),
                CancellationToken.None);
        }

        // Assert
        var stored = await Sut.GetSummariesForUserAsync(
            new GetMyTripsArgs { UserId = ownerUserId }, CancellationToken.None);
        stored.Value.Should().HaveCount(4);
        stored.Value.Count(trip => trip.Status == TripStatusEnum.Active).Should().Be(1);
        stored.Value.Where(trip => trip.Status == TripStatusEnum.Completed)
            .Should().AllSatisfy(trip => trip.EndedOn.Should().NotBeNull());
    }

    [Fact]
    public async Task ItShouldNotDemoteAnotherAnglersActiveTrip()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var theirs = NewTrip(otherUserId, startedOn: StartedOn);
        await Sut.UpsertAsync(theirs, CancellationToken.None);

        // Act
        await Sut.UpsertAsync(
            NewTrip(ownerUserId, startedOn: StartedOn.AddHours(2)),
            CancellationToken.None);

        // Assert
        var stored = await Sut.GetByIdAsync(theirs.Id, CancellationToken.None);
        stored.Value!.Status.Should().Be(TripStatusEnum.Active);
        stored.Value.EndedOn.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldKeepReplayingTheSameActiveTripIdempotent()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var trip = NewTrip(ownerUserId, startedOn: StartedOn);
        await Sut.UpsertAsync(trip, CancellationToken.None);

        // Act
        var replay = await Sut.UpsertAsync(trip, CancellationToken.None);

        // Assert
        replay.IsSuccess.Should().BeTrue();
        replay.Value.Status.Should().Be(TripStatusEnum.Active);
        replay.Value.EndedOn.Should().BeNull();
        var stored = await Sut.GetSummariesForUserAsync(
            new GetMyTripsArgs { UserId = ownerUserId }, CancellationToken.None);
        stored.Value.Should().ContainSingle();
    }


    [Fact]
    public async Task ItShouldBreakATieOnTheStartTimeDeterministically()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var first = NewTrip(ownerUserId, startedOn: StartedOn);
        var second = NewTrip(ownerUserId, startedOn: StartedOn);
        var expectedActive = first.Id.CompareTo(second.Id) > 0 ? first : second;
        var expectedCompleted = expectedActive.Id == first.Id ? second : first;

        // Act
        await Sut.UpsertAsync(first, CancellationToken.None);
        await Sut.UpsertAsync(second, CancellationToken.None);

        // Assert
        var stored = await Sut.GetSummariesForUserAsync(
            new GetMyTripsArgs { UserId = ownerUserId }, CancellationToken.None);
        stored.Value.Should().HaveCount(2);
        stored.Value.Single(trip => trip.Status == TripStatusEnum.Active).Id
            .Should().Be(expectedActive.Id);
        var completed = stored.Value.Single(trip => trip.Id == expectedCompleted.Id);
        completed.Status.Should().Be(TripStatusEnum.Completed);
        completed.EndedOn.Should().Be(completed.StartedOn);
    }

    [Fact]
    public async Task ItShouldNeverEndATripBeforeItStarted()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var later = NewTrip(ownerUserId, startedOn: StartedOn.AddHours(2));
        await Sut.UpsertAsync(later, CancellationToken.None);

        // Act
        var result = await Sut.UpsertAsync(
            NewTrip(ownerUserId, startedOn: StartedOn.AddHours(5)),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var stored = await Sut.GetByIdAsync(later.Id, CancellationToken.None);
        stored.Value!.EndedOn.Should().NotBeNull();
        stored.Value.EndedOn!.Value.Should().BeOnOrAfter(stored.Value.StartedOn);
    }
}
