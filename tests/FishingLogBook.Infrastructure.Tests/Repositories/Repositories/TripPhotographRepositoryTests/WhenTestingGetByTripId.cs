using AwesomeAssertions;
using Dapper;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.TripPhotographRepositoryTests;

public class WhenTestingGetByTripId : BaseTripPhotographRepositoryTest
{
    public WhenTestingGetByTripId(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldReturnNothingForATripWithNoPhotographs()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);

        // Act
        var stored = await Sut.GetByTripIdAsync(trip.Id, CancellationToken.None);

        // Assert
        stored.IsSuccess.Should().BeTrue();
        stored.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotReturnAnotherTripsPhotographs()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var mine = await CreateTripAsync(ownerUserId);
        var theirs = await CreateTripAsync(otherUserId);
        await Sut.UpsertAsync(NewPhotograph(ownerUserId, mine.Id), CancellationToken.None);
        var theirPhotograph = NewPhotograph(otherUserId, theirs.Id);
        await Sut.UpsertAsync(theirPhotograph, CancellationToken.None);

        // Act
        var stored = await Sut.GetByTripIdAsync(mine.Id, CancellationToken.None);

        // Assert
        stored.Value.Should().ContainSingle();
        stored.Value.Should().NotContain(photograph => photograph.Id == theirPhotograph.Id);
    }

    [Fact]
    public async Task ItShouldSurviveTheServerReconcilingTwoActiveTrips()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var earlier = await CreateTripAsync(userId);
        var photograph = NewPhotograph(userId, earlier.Id);
        await Sut.UpsertAsync(photograph, CancellationToken.None);

        // Act
        await Trips.UpsertAsync(
            new Trip
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                Status = TripStatusEnum.Active,
                StartedOn = StartedOn.AddHours(2)
            },
            CancellationToken.None);

        // Assert
        var reconciled = await Trips.GetByIdAsync(earlier.Id, CancellationToken.None);
        reconciled.Value!.Status.Should().Be(TripStatusEnum.Completed);
        var stored = await Sut.GetByTripIdAsync(earlier.Id, CancellationToken.None);
        stored.Value.Should().ContainSingle();
        stored.Value[0].Id.Should().Be(photograph.Id);
        stored.Value[0].ObjectKey.Should().Be(photograph.ObjectKey);
    }

    [Fact]
    public async Task ItShouldRemoveOnlyTheDeletedPhotograph()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);
        var kept = NewPhotograph(userId, trip.Id, capturedOn: StartedOn.AddHours(1));
        var removed = NewPhotograph(userId, trip.Id, capturedOn: StartedOn.AddHours(2));
        await Sut.UpsertAsync(kept, CancellationToken.None);
        await Sut.UpsertAsync(removed, CancellationToken.None);

        // Act
        var deleted = await Sut.DeleteAsync(removed.Id, CancellationToken.None);

        // Assert
        deleted.IsSuccess.Should().BeTrue();
        var stored = await Sut.GetByTripIdAsync(trip.Id, CancellationToken.None);
        stored.Value.Should().ContainSingle();
        stored.Value[0].Id.Should().Be(kept.Id);
    }

    [Fact]
    public async Task ItShouldLeaveTheTripInPlaceWhenAPhotographIsDeleted()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);
        var photograph = NewPhotograph(userId, trip.Id);
        await Sut.UpsertAsync(photograph, CancellationToken.None);

        // Act
        await Sut.DeleteAsync(photograph.Id, CancellationToken.None);

        // Assert
        var storedTrip = await Trips.GetByIdAsync(trip.Id, CancellationToken.None);
        storedTrip.Value.Should().NotBeNull();
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        var remaining = await connection.ExecuteScalarAsync<int>(
            """SELECT COUNT(*) FROM tripphotographs WHERE tripid = @TripId;""",
            new { TripId = trip.Id });
        remaining.Should().Be(0);
    }
}
