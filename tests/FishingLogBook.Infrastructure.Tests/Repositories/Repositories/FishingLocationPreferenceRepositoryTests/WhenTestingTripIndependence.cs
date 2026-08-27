using AwesomeAssertions;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.FishingLocationPreferenceRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingTripIndependence : BaseFishingLocationPreferenceRepositoryTest
{
    private readonly TripRepository _trips;

    public WhenTestingTripIndependence(PostgresFixture fixture)
        : base(fixture)
    {
        _trips = new TripRepository(
            ConnectionFactory,
            NullLogger<TripRepository>.Instance,
            TestMapper.Create());
    }

    [Fact]
    public async Task ItShouldLeaveTheTripPlaceUnchangedWhenTheSavedLocationIsRenamed()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var corrib = Location(userId, "Lough Corrib", true);
        await Sut.ReplaceAsync(userId, [corrib], CancellationToken.None);
        var trip = await CreateTripAsync(userId, "Lough Corrib");

        // Act
        var renamed = await Sut.ReplaceAsync(
            userId,
            [Location(userId, "Lough Corrib West", true, corrib.Id)],
            CancellationToken.None);

        // Assert
        renamed.IsSuccess.Should().BeTrue();
        var stored = await _trips.GetByIdAsync(trip.Id, CancellationToken.None);
        stored.Value!.PlaceName.Should().Be("Lough Corrib");
    }

    [Fact]
    public async Task ItShouldLeaveTheTripPlaceUnchangedWhenTheSavedLocationIsDeleted()
    {
        // Arrange
        var userId = await CreateUserAsync();
        await Sut.ReplaceAsync(
            userId,
            [Location(userId, "Lough Corrib", true)],
            CancellationToken.None);
        var trip = await CreateTripAsync(userId, "Lough Corrib");

        // Act
        var cleared = await Sut.ReplaceAsync(userId, [], CancellationToken.None);

        // Assert
        cleared.IsSuccess.Should().BeTrue();
        var stored = await _trips.GetByIdAsync(trip.Id, CancellationToken.None);
        stored.Value!.PlaceName.Should().Be("Lough Corrib");
        var locations = await Sut.GetByUserIdAsync(userId, CancellationToken.None);
        locations.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldLeaveTheTripPlaceUnchangedWhenTheDefaultMovesToAnotherLocation()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var corrib = Location(userId, "Lough Corrib", true);
        var moy = Location(userId, "River Moy");
        await Sut.ReplaceAsync(userId, [corrib, moy], CancellationToken.None);
        var trip = await CreateTripAsync(userId, "Lough Corrib");

        // Act
        await Sut.ReplaceAsync(
            userId,
            [
                Location(userId, "Lough Corrib", false, corrib.Id),
                Location(userId, "River Moy", true, moy.Id)
            ],
            CancellationToken.None);

        // Assert
        var stored = await _trips.GetByIdAsync(trip.Id, CancellationToken.None);
        stored.Value!.PlaceName.Should().Be("Lough Corrib");
        var locations = await Sut.GetByUserIdAsync(userId, CancellationToken.None);
        locations.Value.Single(location => location.IsDefault).Name.Should().Be("River Moy");
    }

    [Fact]
    public async Task ItShouldLeaveTheSavedLocationsUnchangedWhenTheTripPlaceIsEdited()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var corrib = Location(userId, "Lough Corrib", true);
        await Sut.ReplaceAsync(userId, [corrib], CancellationToken.None);
        var trip = await CreateTripAsync(userId, "Lough Corrib");

        // Act
        var moved = await _trips.UpsertAsync(
            new Trip
            {
                Id = trip.Id,
                OwnerUserId = userId,
                Status = TripStatusEnum.Completed,
                StartedOn = trip.StartedOn,
                EndedOn = trip.StartedOn.AddHours(3),
                PlaceName = "Small lake near Clifden"
            },
            CancellationToken.None);

        // Assert
        moved.Value.PlaceName.Should().Be("Small lake near Clifden");
        var locations = await Sut.GetByUserIdAsync(userId, CancellationToken.None);
        locations.Value.Should().ContainSingle();
        locations.Value[0].Name.Should().Be("Lough Corrib");
        locations.Value[0].IsDefault.Should().BeTrue();
    }

    private async Task<Trip> CreateTripAsync(Guid ownerUserId, string placeName)
    {
        var trip = new Trip
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            Status = TripStatusEnum.Completed,
            StartedOn = DateTimeOffset.Parse("2026-08-27T06:00:00Z"),
            EndedOn = DateTimeOffset.Parse("2026-08-27T10:00:00Z"),
            PlaceName = placeName
        };
        var saved = await _trips.UpsertAsync(trip, CancellationToken.None);
        if (saved.IsFailed)
        {
            throw new InvalidOperationException(saved.Errors[0].Message);
        }

        return saved.Value;
    }
}
