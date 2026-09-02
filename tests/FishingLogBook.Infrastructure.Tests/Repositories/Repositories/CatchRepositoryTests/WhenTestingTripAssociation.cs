using AwesomeAssertions;
using Dapper;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.CatchRepositoryTests;

public class WhenTestingTripAssociation : BaseCatchRepositoryTest
{
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");

    private readonly TripRepository _trips;

    public WhenTestingTripAssociation(PostgresFixture fixture)
        : base(fixture)
    {
        _trips = new TripRepository(
            ConnectionFactory,
            NullLogger<TripRepository>.Instance,
            TestMapper.Create());
    }

    [Fact]
    public async Task ItShouldStoreACatchWithNoTrip()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var catchRecord = NewCatch(userId);

        // Act
        var saved = await Sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Assert
        saved.IsSuccess.Should().BeTrue();
        saved.Value.TripId.Should().BeNull();
        var loaded = await Sut.GetByIdAsync(catchRecord.Id, CancellationToken.None);
        loaded.Value!.TripId.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldRoundTripTheAssociatedTrip()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);

        // Act
        var saved = await Sut.UpsertAsync(
            TrippedCatch(userId, trip.Id),
            CancellationToken.None);

        // Assert
        saved.IsSuccess.Should().BeTrue();
        saved.Value.TripId.Should().Be(trip.Id);
        var loaded = await Sut.GetByIdAsync(saved.Value.Id, CancellationToken.None);
        loaded.Value!.TripId.Should().Be(trip.Id);
        var listed = await Sut.GetActivityForUserAsync(userId, CancellationToken.None);
        listed.Value.Single(item => item.Catch.Id == saved.Value.Id).Catch.TripId.Should().Be(trip.Id);
    }

    [Fact]
    public async Task ItShouldAcceptACatchForACompletedTrip()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId, TripStatusEnum.Completed);

        // Act
        var saved = await Sut.UpsertAsync(
            TrippedCatch(userId, trip.Id),
            CancellationToken.None);

        // Assert
        saved.IsSuccess.Should().BeTrue();
        saved.Value.TripId.Should().Be(trip.Id);
    }

    [Fact]
    public async Task ItShouldChangeTheTripWhenTheCatchIsUpsertedAgain()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var first = await CreateTripAsync(userId, TripStatusEnum.Completed);
        var second = await CreateTripAsync(userId);
        var catchRecord = TrippedCatch(userId, first.Id);
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Act
        var updated = await Sut.UpsertAsync(
            TrippedCatch(userId, second.Id, catchRecord.Id),
            CancellationToken.None);

        // Assert
        updated.Value.TripId.Should().Be(second.Id);
        var loaded = await Sut.GetByIdAsync(catchRecord.Id, CancellationToken.None);
        loaded.Value!.TripId.Should().Be(second.Id);
    }

    [Fact]
    public async Task ItShouldDetachTheTripWhenTheCatchIsUpsertedWithoutOne()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);
        var catchRecord = TrippedCatch(userId, trip.Id);
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Act
        await Sut.UpsertAsync(
            TrippedCatch(userId, null, catchRecord.Id),
            CancellationToken.None);

        // Assert
        var loaded = await Sut.GetByIdAsync(catchRecord.Id, CancellationToken.None);
        loaded.Value!.TripId.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldRejectATripThatDoesNotExist()
    {
        // Arrange
        var userId = await CreateUserAsync();

        // Act
        var saved = await Sut.UpsertAsync(
            TrippedCatch(userId, Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        saved.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldKeepTheCatchWhenItsTripRowIsDeleted()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);
        var catchRecord = TrippedCatch(userId, trip.Id);
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Act
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        await connection.ExecuteAsync(
            """DELETE FROM "Trip" WHERE "Id" = @Id;""",
            new { trip.Id });

        // Assert
        var loaded = await Sut.GetByIdAsync(catchRecord.Id, CancellationToken.None);
        loaded.Value.Should().NotBeNull();
        loaded.Value!.TripId.Should().BeNull();
        loaded.Value.Photographs.Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldNotDisturbTheProvenanceOfAnAssociatedCatch()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);

        // Act
        var saved = await Sut.UpsertAsync(
            TrippedCatch(userId, trip.Id),
            CancellationToken.None);

        // Assert
        saved.Value.CaughtByUserId.Should().Be(userId);
        saved.Value.CaughtByUserId.Should().Be(userId);
        saved.Value.RecordedByUserId.Should().Be(userId);
        saved.Value.Location.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldNotMoveCatchesWhenTheServerReconcilesTwoActiveTrips()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var earlier = await CreateTripAsync(userId);
        var earlierCatch = TrippedCatch(userId, earlier.Id);
        await Sut.UpsertAsync(earlierCatch, CancellationToken.None);

        // Act
        var later = new Trip
        {
            Id = Guid.NewGuid(),
            OwnerUserId = userId,
            Status = TripStatusEnum.Active,
            StartedOn = StartedOn.AddHours(2)
        };
        await _trips.UpsertAsync(later, CancellationToken.None);

        // Assert
        var reconciled = await _trips.GetByIdAsync(earlier.Id, CancellationToken.None);
        reconciled.Value!.Status.Should().Be(TripStatusEnum.Completed);
        var stored = await Sut.GetByIdAsync(earlierCatch.Id, CancellationToken.None);
        stored.Value!.TripId.Should().Be(earlier.Id);
        stored.Value.TripId.Should().NotBe(later.Id);
    }

    private static Catch TrippedCatch(Guid userId, Guid? tripId, Guid? catchId = null)
    {
        var seed = NewCatch(userId, catchId);
        return new Catch
        {
            Id = seed.Id,
            CaughtByUserId = seed.CaughtByUserId,
            RecordedByUserId = seed.RecordedByUserId,
            TripId = tripId,
            CaughtOn = seed.CaughtOn,
            Photographs = seed.Photographs
        };
    }

    private async Task<Trip> CreateTripAsync(
        Guid ownerUserId,
        TripStatusEnum status = TripStatusEnum.Active)
    {
        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            Status = status,
            StartedOn = StartedOn,
            EndedOn = status == TripStatusEnum.Completed ? StartedOn.AddHours(3) : null
        };
        var saved = await _trips.UpsertAsync(trip, CancellationToken.None);
        if (saved.IsFailed)
        {
            throw new InvalidOperationException(saved.Errors[0].Message);
        }

        return saved.Value;
    }
}
