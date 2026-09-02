using AwesomeAssertions;
using Dapper;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.TripPhotographRepositoryTests;

public class WhenTestingUpsert : BaseTripPhotographRepositoryTest
{
    public WhenTestingUpsert(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldRejectAPhotographForATripThatDoesNotExist()
    {
        // Arrange
        var userId = await CreateUserAsync();

        // Act
        var saved = await Sut.UpsertAsync(
            NewPhotograph(userId, Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        saved.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldNotMoveAPhotographToAnotherTrip()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var first = await CreateTripAsync(userId, TripStatusEnum.Completed);
        var second = await CreateTripAsync(userId);
        var photograph = NewPhotograph(userId, first.Id);
        await Sut.UpsertAsync(photograph, CancellationToken.None);

        // Act
        await Sut.UpsertAsync(
            NewPhotograph(userId, second.Id, photograph.Id),
            CancellationToken.None);

        // Assert
        var stored = await Sut.GetByIdAsync(photograph.Id, CancellationToken.None);
        stored.Value!.TripId.Should().Be(first.Id);
    }

    [Fact]
    public async Task ItShouldStoreAPhotographWithNoTrustworthyCaptureTime()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);
        var addedOn = StartedOn.AddHours(2);

        // Act
        var saved = await Sut.UpsertAsync(
            NewPhotograph(userId, trip.Id, capturedOn: null, addedOn: addedOn),
            CancellationToken.None);

        // Assert
        saved.IsSuccess.Should().BeTrue();
        saved.Value.CapturedOn.Should().BeNull();
        saved.Value.AddedOn.Should().Be(addedOn);
        saved.Value.OrderedOn.Should().Be(addedOn);
    }

    [Fact]
    public async Task ItShouldRoundTripTheCaptureAndAddedTimes()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);
        var capturedOn = StartedOn.AddMinutes(30);
        var addedOn = StartedOn.AddHours(2);

        // Act
        var saved = await Sut.UpsertAsync(
            NewPhotograph(userId, trip.Id, capturedOn: capturedOn, addedOn: addedOn),
            CancellationToken.None);

        // Assert
        saved.Value.CapturedOn.Should().Be(capturedOn);
        saved.Value.AddedOn.Should().Be(addedOn);
        saved.Value.OrderedOn.Should().Be(capturedOn);
        var reloaded = await Sut.GetByIdAsync(saved.Value.Id, CancellationToken.None);
        reloaded.Value!.CapturedOn.Should().Be(capturedOn);
        reloaded.Value.AddedOn.Should().Be(addedOn);
    }

    [Fact]
    public async Task ItShouldStoreTheTripPrefixedObjectKey()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);
        var photograph = NewPhotograph(userId, trip.Id);

        // Act
        var saved = await Sut.UpsertAsync(photograph, CancellationToken.None);

        // Assert
        saved.Value.ObjectKey.Should().Be($"trip-photographs/{trip.Id:D}/{photograph.Id:D}");
    }

    [Fact]
    public async Task ItShouldReplayTheSamePhotographIdempotently()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);
        var photograph = NewPhotograph(userId, trip.Id);
        await Sut.UpsertAsync(photograph, CancellationToken.None);

        // Act
        var replay = await Sut.UpsertAsync(photograph, CancellationToken.None);

        // Assert
        replay.IsSuccess.Should().BeTrue();
        var stored = await Sut.GetByTripIdAsync(trip.Id, CancellationToken.None);
        stored.Value.Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldNotCreateAnyCatchPhotographRows()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);
        var photograph = NewPhotograph(userId, trip.Id);

        // Act
        await Sut.UpsertAsync(photograph, CancellationToken.None);

        // Assert
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        var borrowedPhotographRow = await connection.ExecuteScalarAsync<int>(
            """SELECT COUNT(*) FROM catchphotographs WHERE id = @Id;""",
            new { photograph.Id });
        borrowedPhotographRow.Should().Be(0);
        var catchesForThisAngler = await connection.ExecuteScalarAsync<int>(
            """SELECT COUNT(*) FROM catches WHERE caughtbyuserid = @UserId OR tripid = @TripId;""",
            new { UserId = userId, TripId = trip.Id });
        catchesForThisAngler.Should().Be(0);
        var storedForTrip = await connection.ExecuteScalarAsync<int>(
            """SELECT COUNT(*) FROM tripphotographs WHERE tripid = @TripId;""",
            new { TripId = trip.Id });
        storedForTrip.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldStoreManyPhotographsForOneTripInTimelineOrder()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var trip = await CreateTripAsync(userId);
        var later = NewPhotograph(userId, trip.Id, capturedOn: StartedOn.AddHours(4));
        var earlier = NewPhotograph(userId, trip.Id, capturedOn: StartedOn.AddHours(1));
        var undated = NewPhotograph(userId, trip.Id, addedOn: StartedOn.AddHours(9));

        // Act
        await Sut.UpsertAsync(later, CancellationToken.None);
        await Sut.UpsertAsync(undated, CancellationToken.None);
        await Sut.UpsertAsync(earlier, CancellationToken.None);

        // Assert
        var stored = await Sut.GetByTripIdAsync(trip.Id, CancellationToken.None);
        stored.Value.Select(photograph => photograph.Id)
            .Should().Equal(earlier.Id, later.Id, undated.Id);
    }
}
