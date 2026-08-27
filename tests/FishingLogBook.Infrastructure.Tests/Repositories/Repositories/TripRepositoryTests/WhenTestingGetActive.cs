using AwesomeAssertions;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.TripRepositoryTests;

public class WhenTestingGetActive : BaseTripRepositoryTest
{
    public WhenTestingGetActive(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldReturnNothingWhenTheAnglerHasNoTrips()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();

        // Act
        var result = await Sut.GetActiveAsync(ownerUserId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldReturnNothingWhenEveryTripIsCompleted()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        await Sut.UpsertAsync(
            NewTrip(
                ownerUserId,
                status: TripStatusEnum.Completed,
                endedOn: StartedOn.AddHours(3)),
            CancellationToken.None);

        // Act
        var result = await Sut.GetActiveAsync(ownerUserId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldNotReturnAnotherAnglersActiveTrip()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        await Sut.UpsertAsync(NewTrip(otherUserId), CancellationToken.None);

        // Act
        var result = await Sut.GetActiveAsync(ownerUserId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldReturnTheActiveTripWithItsLocation()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var trip = NewTrip(ownerUserId, placeName: "Lough Corrib", location: PrivateLocation());
        await Sut.UpsertAsync(trip, CancellationToken.None);

        // Act
        var result = await Sut.GetActiveAsync(ownerUserId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(trip.Id);
        result.Value.Status.Should().Be(TripStatusEnum.Active);
        result.Value.PlaceName.Should().Be("Lough Corrib");
        result.Value.Location!.Latitude.Should().Be(53.4419);
    }

    [Fact]
    public async Task ItShouldIgnoreCompletedTripsWhenAnActiveOneExists()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var completed = NewTrip(
            ownerUserId,
            status: TripStatusEnum.Completed,
            endedOn: StartedOn.AddHours(2));
        await Sut.UpsertAsync(completed, CancellationToken.None);
        var active = NewTrip(ownerUserId, startedOn: StartedOn.AddDays(1));
        await Sut.UpsertAsync(active, CancellationToken.None);

        // Act
        var result = await Sut.GetActiveAsync(ownerUserId, CancellationToken.None);

        // Assert
        result.Value!.Id.Should().Be(active.Id);
    }
}
