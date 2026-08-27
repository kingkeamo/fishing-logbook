using AwesomeAssertions;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.TripRepositoryTests;

public class WhenTestingGetByOwnerUserId : BaseTripRepositoryTest
{
    public WhenTestingGetByOwnerUserId(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldReturnAnEmptyListWhenTheAnglerHasNoTrips()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();

        // Act
        var result = await Sut.GetByOwnerUserIdAsync(ownerUserId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotReturnAnotherAnglersTrip()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var owned = NewTrip(ownerUserId, placeName: "Lough Corrib");
        var foreign = NewTrip(otherUserId, placeName: "Lough Mask");
        await Sut.UpsertAsync(owned, CancellationToken.None);
        await Sut.UpsertAsync(foreign, CancellationToken.None);

        // Act
        var result = await Sut.GetByOwnerUserIdAsync(ownerUserId, CancellationToken.None);

        // Assert
        result.Value.Should().ContainSingle();
        result.Value[0].Id.Should().Be(owned.Id);
        result.Value[0].PlaceName.Should().Be("Lough Corrib");
    }

    [Fact]
    public async Task ItShouldReturnNewestTripsFirst()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var older = NewTrip(
            ownerUserId,
            status: TripStatusEnum.Completed,
            startedOn: StartedOn.AddDays(-2),
            endedOn: StartedOn.AddDays(-2).AddHours(4));
        var newer = NewTrip(
            ownerUserId,
            status: TripStatusEnum.Completed,
            startedOn: StartedOn,
            endedOn: StartedOn.AddHours(4));
        await Sut.UpsertAsync(older, CancellationToken.None);
        await Sut.UpsertAsync(newer, CancellationToken.None);

        // Act
        var result = await Sut.GetByOwnerUserIdAsync(ownerUserId, CancellationToken.None);

        // Assert
        result.Value.Should().HaveCount(2);
        result.Value[0].Id.Should().Be(newer.Id);
        result.Value[1].Id.Should().Be(older.Id);
    }

    [Fact]
    public async Task ItShouldReturnActiveAndCompletedTrips()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var completed = NewTrip(
            ownerUserId,
            status: TripStatusEnum.Completed,
            startedOn: StartedOn.AddDays(-1),
            endedOn: StartedOn.AddDays(-1).AddHours(3));
        var active = NewTrip(ownerUserId);
        await Sut.UpsertAsync(completed, CancellationToken.None);
        await Sut.UpsertAsync(active, CancellationToken.None);

        // Act
        var result = await Sut.GetByOwnerUserIdAsync(ownerUserId, CancellationToken.None);

        // Assert
        result.Value.Select(trip => trip.Status)
            .Should()
            .Contain([TripStatusEnum.Active, TripStatusEnum.Completed]);
    }
}
