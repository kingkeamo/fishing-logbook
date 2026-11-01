using AwesomeAssertions;
using Dapper;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using Npgsql;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.TripRepositoryTests;

public class WhenTestingPlannedLifecycle : BaseTripRepositoryTest
{
    private static readonly DateTimeOffset PlannedStartOn = DateTimeOffset.Parse("2026-09-12T06:00:00Z");

    public WhenTestingPlannedLifecycle(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldRejectAPlannedRowWithoutAPlannedStartInTheDatabase()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);

        // Act
        var insert = async () => await connection.ExecuteAsync(
            """
            insert into trips (id, owneruserid, status, startedon)
            values (@Id, @OwnerUserId, 'Planned', now());
            """,
            new { Id = Guid.NewGuid(), OwnerUserId = ownerUserId });

        // Assert
        var exception = await insert.Should().ThrowAsync<PostgresException>();
        exception.Which.ConstraintName.Should().Be("cktripsplannedhasplannedstart");
    }

    [Fact]
    public async Task ItShouldRejectAPlannedRowThatHasAnEndInTheDatabase()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);

        // Act
        var insert = async () => await connection.ExecuteAsync(
            """
            insert into trips (id, owneruserid, status, startedon, plannedstarton, endedon)
            values (@Id, @OwnerUserId, 'Planned', now(), now(), now() + interval '1 hour');
            """,
            new { Id = Guid.NewGuid(), OwnerUserId = ownerUserId });

        // Assert
        var exception = await insert.Should().ThrowAsync<PostgresException>();
        exception.Which.ConstraintName.Should().Be("cktripsplannedhasnoend");
    }

    [Fact]
    public async Task ItShouldRejectCreatingACancelledTrip()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var trip = NewTrip(ownerUserId, status: TripStatusEnum.Cancelled, plannedStartOn: PlannedStartOn);

        // Act
        var result = await Sut.UpsertAsync(trip, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripTransitionInvalidError>();
        (await Sut.GetByIdAsync(trip.Id, CancellationToken.None)).Value.Should().BeNull();
    }

    [Theory]
    [InlineData(TripStatusEnum.Active)]
    [InlineData(TripStatusEnum.Completed)]
    public async Task ItShouldRejectReturningAStartedTripToPlanned(TripStatusEnum existingStatus)
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var started = NewTrip(
            ownerUserId,
            status: existingStatus,
            endedOn: existingStatus == TripStatusEnum.Completed ? StartedOn.AddHours(2) : null);
        await Sut.UpsertAsync(started, CancellationToken.None);
        var replanned = NewTrip(ownerUserId, started.Id, TripStatusEnum.Planned, plannedStartOn: PlannedStartOn);

        // Act
        var result = await Sut.UpsertAsync(replanned, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripTransitionInvalidError>();
        (await Sut.GetByIdAsync(started.Id, CancellationToken.None)).Value!.Status.Should().Be(existingStatus);
    }

    [Fact]
    public async Task ItShouldRejectCompletingAPlannedTripWithoutStartingIt()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var planned = NewTrip(ownerUserId, status: TripStatusEnum.Planned, plannedStartOn: PlannedStartOn);
        await Sut.UpsertAsync(planned, CancellationToken.None);
        var completed = NewTrip(
            ownerUserId,
            planned.Id,
            TripStatusEnum.Completed,
            startedOn: PlannedStartOn,
            endedOn: PlannedStartOn.AddHours(3));

        // Act
        var result = await Sut.UpsertAsync(completed, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripTransitionInvalidError>();
        (await Sut.GetByIdAsync(planned.Id, CancellationToken.None)).Value!.Status.Should().Be(TripStatusEnum.Planned);
    }

    [Theory]
    [InlineData(TripStatusEnum.Planned)]
    [InlineData(TripStatusEnum.Active)]
    [InlineData(TripStatusEnum.Completed)]
    public async Task ItShouldNotRevivePlanOrStartACancelledTrip(TripStatusEnum next)
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var planned = NewTrip(ownerUserId, status: TripStatusEnum.Planned, plannedStartOn: PlannedStartOn);
        await Sut.UpsertAsync(planned, CancellationToken.None);
        await Sut.UpsertAsync(
            NewTrip(ownerUserId, planned.Id, TripStatusEnum.Cancelled, plannedStartOn: PlannedStartOn),
            CancellationToken.None);
        var attempt = next == TripStatusEnum.Planned
            ? NewTrip(ownerUserId, planned.Id, next, plannedStartOn: PlannedStartOn)
            : NewTrip(
                ownerUserId,
                planned.Id,
                next,
                startedOn: StartedOn,
                endedOn: next == TripStatusEnum.Completed ? StartedOn.AddHours(1) : null);

        // Act
        var result = await Sut.UpsertAsync(attempt, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripTransitionInvalidError>();
        (await Sut.GetByIdAsync(planned.Id, CancellationToken.None)).Value!.Status.Should().Be(TripStatusEnum.Cancelled);
    }

    [Fact]
    public async Task ItShouldAcceptAReplayOfTheCancellation()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var planned = NewTrip(ownerUserId, status: TripStatusEnum.Planned, plannedStartOn: PlannedStartOn);
        await Sut.UpsertAsync(planned, CancellationToken.None);
        var cancelled = NewTrip(ownerUserId, planned.Id, TripStatusEnum.Cancelled, plannedStartOn: PlannedStartOn);
        await Sut.UpsertAsync(cancelled, CancellationToken.None);

        // Act
        var replay = await Sut.UpsertAsync(cancelled, CancellationToken.None);

        // Assert
        replay.IsSuccess.Should().BeTrue();
        replay.Value.Status.Should().Be(TripStatusEnum.Cancelled);
        replay.Value.PlannedStartOn.Should().Be(PlannedStartOn);
    }

    [Fact]
    public async Task ItShouldCancelAPlannedTripAndKeepItsPlannedStart()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var planned = NewTrip(ownerUserId, status: TripStatusEnum.Planned, plannedStartOn: PlannedStartOn, title: "Corrib");
        await Sut.UpsertAsync(planned, CancellationToken.None);
        var cancelled = NewTrip(
            ownerUserId,
            planned.Id,
            TripStatusEnum.Cancelled,
            plannedStartOn: PlannedStartOn.AddDays(9),
            title: "Corrib");

        // Act
        var result = await Sut.UpsertAsync(cancelled, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TripStatusEnum.Cancelled);
        result.Value.PlannedStartOn.Should().Be(PlannedStartOn);
        result.Value.EndedOn.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldAllowAPlannedTripToBeEdited()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var planned = NewTrip(ownerUserId, status: TripStatusEnum.Planned, plannedStartOn: PlannedStartOn, title: "Corrib");
        await Sut.UpsertAsync(planned, CancellationToken.None);
        var edited = NewTrip(
            ownerUserId,
            planned.Id,
            TripStatusEnum.Planned,
            plannedStartOn: PlannedStartOn.AddHours(5),
            title: "Mask",
            placeName: "Lough Mask");

        // Act
        var result = await Sut.UpsertAsync(edited, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TripStatusEnum.Planned);
        result.Value.Title.Should().Be("Mask");
        result.Value.PlaceName.Should().Be("Lough Mask");
        result.Value.PlannedStartOn.Should().Be(PlannedStartOn.AddHours(5));
        result.Value.StartedOn.Should().Be(PlannedStartOn.AddHours(5));
    }

    [Fact]
    public async Task ItShouldStoreAPlannedTripWithoutDemotingTheActiveTrip()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var active = NewTrip(ownerUserId, startedOn: StartedOn);
        await Sut.UpsertAsync(active, CancellationToken.None);
        var planned = NewTrip(ownerUserId, status: TripStatusEnum.Planned, plannedStartOn: StartedOn.AddDays(2));

        // Act
        var result = await Sut.UpsertAsync(planned, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        (await Sut.GetByIdAsync(active.Id, CancellationToken.None)).Value!.Status.Should().Be(TripStatusEnum.Active);
        (await Sut.GetByIdAsync(planned.Id, CancellationToken.None)).Value!.Status.Should().Be(TripStatusEnum.Planned);
    }

    [Fact]
    public async Task ItShouldRecordTheActualStartWhenAPlannedTripIsActivated()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var planned = NewTrip(ownerUserId, status: TripStatusEnum.Planned, plannedStartOn: PlannedStartOn, title: "Corrib");
        await Sut.UpsertAsync(planned, CancellationToken.None);
        var activated = NewTrip(
            ownerUserId,
            planned.Id,
            TripStatusEnum.Active,
            startedOn: PlannedStartOn.AddMinutes(25),
            title: "Corrib",
            placeName: "Inver Bay");

        // Act
        var result = await Sut.UpsertAsync(activated, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TripStatusEnum.Active);
        result.Value.StartedOn.Should().Be(PlannedStartOn.AddMinutes(25));
        result.Value.PlannedStartOn.Should().Be(PlannedStartOn);
        result.Value.Title.Should().Be("Corrib");
        result.Value.PlaceName.Should().Be("Inver Bay");
    }

    [Fact]
    public async Task ItShouldKeepTheStoredPlannedStartWhenAnActivationClaimsAnotherOne()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var planned = NewTrip(ownerUserId, status: TripStatusEnum.Planned, plannedStartOn: PlannedStartOn);
        await Sut.UpsertAsync(planned, CancellationToken.None);
        var activated = NewTrip(ownerUserId, planned.Id, TripStatusEnum.Active, startedOn: PlannedStartOn.AddHours(1));
        activated = new Trip
        {
            Id = activated.Id,
            OwnerUserId = activated.OwnerUserId,
            Status = activated.Status,
            StartedOn = activated.StartedOn,
            PlannedStartOn = PlannedStartOn.AddDays(30)
        };

        // Act
        var result = await Sut.UpsertAsync(activated, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PlannedStartOn.Should().Be(PlannedStartOn);
    }

    [Fact]
    public async Task ItShouldRefuseToActivateWhenTheOwnerAlreadyHasAnActiveTripAndChangeNeitherTrip()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var active = NewTrip(ownerUserId, startedOn: StartedOn);
        await Sut.UpsertAsync(active, CancellationToken.None);
        var planned = NewTrip(ownerUserId, status: TripStatusEnum.Planned, plannedStartOn: StartedOn.AddDays(1));
        await Sut.UpsertAsync(planned, CancellationToken.None);
        var activation = NewTrip(ownerUserId, planned.Id, TripStatusEnum.Active, startedOn: StartedOn.AddDays(1));

        // Act
        var result = await Sut.UpsertAsync(activation, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripAlreadyActiveError>();
        (await Sut.GetByIdAsync(active.Id, CancellationToken.None)).Value!.Status.Should().Be(TripStatusEnum.Active);
        (await Sut.GetByIdAsync(planned.Id, CancellationToken.None)).Value!.Status.Should().Be(TripStatusEnum.Planned);
    }

    [Fact]
    public async Task ItShouldNotRecordASecondActualStartWhenActivatedAgain()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var planned = NewTrip(ownerUserId, status: TripStatusEnum.Planned, plannedStartOn: PlannedStartOn);
        await Sut.UpsertAsync(planned, CancellationToken.None);
        await Sut.UpsertAsync(
            NewTrip(ownerUserId, planned.Id, TripStatusEnum.Active, startedOn: PlannedStartOn.AddMinutes(10)),
            CancellationToken.None);

        // Act
        var replay = await Sut.UpsertAsync(
            NewTrip(ownerUserId, planned.Id, TripStatusEnum.Active, startedOn: PlannedStartOn.AddMinutes(45)),
            CancellationToken.None);

        // Assert
        replay.IsSuccess.Should().BeTrue();
        replay.Value.StartedOn.Should().Be(PlannedStartOn.AddMinutes(10));
        replay.Value.PlannedStartOn.Should().Be(PlannedStartOn);
    }

    [Fact]
    public async Task ItShouldActivateOnceWhenTwoActivationsRunAtTheSameTime()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var planned = NewTrip(ownerUserId, status: TripStatusEnum.Planned, plannedStartOn: PlannedStartOn);
        await Sut.UpsertAsync(planned, CancellationToken.None);
        var first = NewTrip(ownerUserId, planned.Id, TripStatusEnum.Active, startedOn: PlannedStartOn.AddMinutes(5));
        var second = NewTrip(ownerUserId, planned.Id, TripStatusEnum.Active, startedOn: PlannedStartOn.AddMinutes(9));

        // Act
        var results = await Task.WhenAll(
            Sut.UpsertAsync(first, CancellationToken.None),
            Sut.UpsertAsync(second, CancellationToken.None));

        // Assert
        results.Should().OnlyContain(result => result.IsSuccess);
        results[0].Value.StartedOn.Should().Be(results[1].Value.StartedOn);
        var stored = (await Sut.GetByIdAsync(planned.Id, CancellationToken.None)).Value!;
        stored.Status.Should().Be(TripStatusEnum.Active);
        stored.StartedOn.Should().Be(results[0].Value.StartedOn);
        stored.PlannedStartOn.Should().Be(PlannedStartOn);
        var summaries = await Sut.GetSummariesForUserAsync(
            new GetMyTripsArgs { UserId = ownerUserId }, CancellationToken.None);
        summaries.Value.Count(summary => summary.Status == TripStatusEnum.Active).Should().Be(1);
    }

    [Fact]
    public async Task ItShouldRefuseAnotherUsersAttemptToActivateAPlannedTrip()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var planned = NewTrip(ownerUserId, status: TripStatusEnum.Planned, plannedStartOn: PlannedStartOn);
        await Sut.UpsertAsync(planned, CancellationToken.None);
        var hijack = NewTrip(otherUserId, planned.Id, TripStatusEnum.Active, startedOn: PlannedStartOn);

        // Act
        var result = await Sut.UpsertAsync(hijack, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripOwnershipConflictError>();
        (await Sut.GetByIdAsync(planned.Id, CancellationToken.None)).Value!.Status.Should().Be(TripStatusEnum.Planned);
    }

    [Fact]
    public async Task ItShouldKeepTheLifecycleOfExistingActiveAndCompletedTripsUnchanged()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var trip = NewTrip(ownerUserId, startedOn: StartedOn);
        await Sut.UpsertAsync(trip, CancellationToken.None);
        var completed = NewTrip(ownerUserId, trip.Id, TripStatusEnum.Completed, startedOn: StartedOn, endedOn: StartedOn.AddHours(4));

        // Act
        var result = await Sut.UpsertAsync(completed, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TripStatusEnum.Completed);
        result.Value.StartedOn.Should().Be(StartedOn);
        result.Value.EndedOn.Should().Be(StartedOn.AddHours(4));
        result.Value.PlannedStartOn.Should().BeNull();
    }
}
