using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.CatchRepositoryTests;

public class WhenTestingAssociateTrip : BaseCatchRepositoryTest
{
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");

    private readonly TripRepository _trips;

    public WhenTestingAssociateTrip(PostgresFixture fixture)
        : base(fixture)
    {
        _trips = new TripRepository(
            ConnectionFactory,
            NullLogger<TripRepository>.Instance,
            TestMapper.Create());
    }

    [Fact]
    public async Task ItShouldReturnFalseWhenTheCatchIsMissing()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);

        // Act
        var result = await Sut.AssociateTripAsync(
            Args(Guid.NewGuid(), userId, trip.Id),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldFailWhenTheTripDoesNotExist()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var catchRecord = NewCatch(userId);
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);
        var missingTripId = Guid.NewGuid();

        // Act
        var result = await Sut.AssociateTripAsync(
            Args(catchRecord.Id, userId, missingTripId),
            CancellationToken.None);
        var loaded = await Sut.GetByIdAsync(catchRecord.Id, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to save the catch.");
        loaded.Value!.TripId.Should().BeNull();
        Logger.Records.Should().ContainSingle();
        Logger.Records[0].Level.Should().Be(LogLevel.Error);
        Logger.Records[0].Exception.Should().NotBeNull();
        Logger.Records[0].Message.Should().Contain(catchRecord.Id.ToString("D"));
    }

    [Fact]
    public async Task ItShouldReturnFalseWhenAnotherAnglerOwnsTheCatch()
    {
        // Arrange
        var ownerId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var trip = await CreateTripAsync(otherUserId);
        var catchRecord = NewCatch(ownerId);
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Act
        var result = await Sut.AssociateTripAsync(
            Args(catchRecord.Id, otherUserId, trip.Id),
            CancellationToken.None);
        var loaded = await Sut.GetByIdAsync(catchRecord.Id, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        loaded.Value!.TripId.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldReturnFalseWhenTheCatchAlreadyHasATrip()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var first = await CreateTripAsync(userId);
        var second = await CreateTripAsync(userId);
        var catchRecord = NewCatch(userId);
        await Sut.UpsertAsync(
            WithTrip(catchRecord, first.Id),
            CancellationToken.None);

        // Act
        var result = await Sut.AssociateTripAsync(
            Args(catchRecord.Id, userId, second.Id),
            CancellationToken.None);
        var loaded = await Sut.GetByIdAsync(catchRecord.Id, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        loaded.Value!.TripId.Should().Be(first.Id);
    }

    [Fact]
    public async Task ItShouldAssociateAnUnlinkedOwnedCatch()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);
        var catchRecord = NewCatch(userId);
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Act
        var result = await Sut.AssociateTripAsync(
            Args(catchRecord.Id, userId, trip.Id),
            CancellationToken.None);
        var loaded = await Sut.GetByIdAsync(catchRecord.Id, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        loaded.Value!.TripId.Should().Be(trip.Id);
    }

    private static PersistCatchTripArgs Args(Guid catchId, Guid userId, Guid tripId)
    {
        return new PersistCatchTripArgs
        {
            CatchId = catchId,
            UserId = userId,
            TripId = tripId
        };
    }

    private static Catch WithTrip(Catch seed, Guid tripId)
    {
        return new Catch
        {
            Id = seed.Id,
            UserId = seed.UserId,
            AnglerUserId = seed.AnglerUserId,
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
