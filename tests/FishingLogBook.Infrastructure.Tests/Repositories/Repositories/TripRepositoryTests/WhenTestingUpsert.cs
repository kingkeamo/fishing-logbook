using AwesomeAssertions;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.TripRepositoryTests;

public class WhenTestingUpsert : BaseTripRepositoryTest
{
    public WhenTestingUpsert(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldRejectATripWhoseOwnerDoesNotExist()
    {
        // Arrange
        var trip = NewTrip(Guid.NewGuid());

        // Act
        var result = await Sut.UpsertAsync(trip, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        Logger.Records.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ItShouldRejectASecondActiveTripForTheSameOwner()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var first = await Sut.UpsertAsync(NewTrip(ownerUserId), CancellationToken.None);
        first.IsSuccess.Should().BeTrue();

        // Act
        var second = await Sut.UpsertAsync(NewTrip(ownerUserId), CancellationToken.None);

        // Assert
        second.IsFailed.Should().BeTrue();
        second.Errors[0].Should().BeOfType<TripAlreadyActiveError>();
    }

    [Fact]
    public async Task ItShouldAllowAnActiveTripForEachOwner()
    {
        // Arrange
        var firstOwner = await CreateUserAsync();
        var secondOwner = await CreateUserAsync();

        // Act
        var first = await Sut.UpsertAsync(NewTrip(firstOwner), CancellationToken.None);
        var second = await Sut.UpsertAsync(NewTrip(secondOwner), CancellationToken.None);

        // Assert
        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldAllowANewActiveTripOnceTheEarlierOneIsCompleted()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var active = NewTrip(ownerUserId);
        await Sut.UpsertAsync(active, CancellationToken.None);
        var completed = NewTrip(
            ownerUserId,
            tripId: active.Id,
            status: TripStatusEnum.Completed,
            endedOn: StartedOn.AddHours(6));
        await Sut.UpsertAsync(completed, CancellationToken.None);

        // Act
        var next = await Sut.UpsertAsync(NewTrip(ownerUserId), CancellationToken.None);

        // Assert
        next.IsSuccess.Should().BeTrue();
        next.Value.Status.Should().Be(TripStatusEnum.Active);
    }

    [Fact]
    public async Task ItShouldRejectAChangeOfOwner()
    {
        // Arrange
        var firstOwner = await CreateUserAsync();
        var secondOwner = await CreateUserAsync();
        var trip = NewTrip(firstOwner);
        await Sut.UpsertAsync(trip, CancellationToken.None);

        // Act
        var result = await Sut.UpsertAsync(
            NewTrip(secondOwner, tripId: trip.Id),
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripOwnershipConflictError>();
        var stored = await Sut.GetByIdAsync(trip.Id, CancellationToken.None);
        stored.Value!.OwnerUserId.Should().Be(firstOwner);
    }

    [Fact]
    public async Task ItShouldRejectAnEndBeforeTheStart()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var trip = NewTrip(
            ownerUserId,
            status: TripStatusEnum.Completed,
            endedOn: StartedOn.AddHours(-1));

        // Act
        var result = await Sut.UpsertAsync(trip, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        Logger.Records.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ItShouldPersistABlankTripWithNoTitlePlaceOrLocation()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var trip = NewTrip(ownerUserId);

        // Act
        var result = await Sut.UpsertAsync(trip, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().BeNull();
        result.Value.PlaceName.Should().BeNull();
        result.Value.Location.Should().BeNull();
        result.Value.EndedOn.Should().BeNull();
        result.Value.Status.Should().Be(TripStatusEnum.Active);
        result.Value.CreatedOn.Should().NotBe(default);
        result.Value.UpdatedOn.Should().NotBe(default);
    }

    [Fact]
    public async Task ItShouldPersistAPlaceNameWithoutCoordinates()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var trip = NewTrip(ownerUserId, placeName: "Lough Corrib");

        // Act
        var result = await Sut.UpsertAsync(trip, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PlaceName.Should().Be("Lough Corrib");
        result.Value.Location.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldPersistTheLocationAndItsProvenance()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var trip = NewTrip(
            ownerUserId,
            title: "Day with Dad",
            placeName: "Lough Corrib",
            location: PrivateLocation());

        // Act
        var result = await Sut.UpsertAsync(trip, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Location.Should().NotBeNull();
        result.Value.Location!.Latitude.Should().Be(53.4419);
        result.Value.Location.Longitude.Should().Be(-9.2531);
        result.Value.Location.AccuracyMetres.Should().Be(8);
        result.Value.Location.CapturedOn.Should().Be(StartedOn);
        result.Value.Location.Source.Should().Be(LocationDefaults.DeviceGps);
        result.Value.Location.Visibility.Should().Be(LocationDefaults.Private);
        result.Value.Location.ConsentVersion.Should().Be(LocationDefaults.ConsentVersion);
    }

    [Fact]
    public async Task ItShouldClearTheLocationWhenUpsertedWithoutOne()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var trip = NewTrip(ownerUserId, location: PrivateLocation());
        await Sut.UpsertAsync(trip, CancellationToken.None);

        // Act
        var result = await Sut.UpsertAsync(
            NewTrip(ownerUserId, tripId: trip.Id),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Location.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldFinishATripWithoutCreatingAnother()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var trip = NewTrip(ownerUserId);
        await Sut.UpsertAsync(trip, CancellationToken.None);

        // Act
        var result = await Sut.UpsertAsync(
            NewTrip(
                ownerUserId,
                tripId: trip.Id,
                status: TripStatusEnum.Completed,
                endedOn: StartedOn.AddHours(6)),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(trip.Id);
        result.Value.Status.Should().Be(TripStatusEnum.Completed);
        result.Value.EndedOn.Should().Be(StartedOn.AddHours(6));
        var all = await Sut.GetByOwnerUserIdAsync(ownerUserId, CancellationToken.None);
        all.Value.Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldBeIdempotentWhenTheSamePayloadIsReplayed()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var trip = NewTrip(ownerUserId, title: "Day with Dad", location: PrivateLocation());
        var first = await Sut.UpsertAsync(trip, CancellationToken.None);

        // Act
        var second = await Sut.UpsertAsync(trip, CancellationToken.None);

        // Assert
        second.IsSuccess.Should().BeTrue();
        second.Value.Id.Should().Be(first.Value.Id);
        second.Value.Title.Should().Be(first.Value.Title);
        second.Value.StartedOn.Should().Be(first.Value.StartedOn);
        second.Value.Location!.Latitude.Should().Be(first.Value.Location!.Latitude);
        second.Value.CreatedOn.Should().Be(first.Value.CreatedOn);
        var all = await Sut.GetByOwnerUserIdAsync(ownerUserId, CancellationToken.None);
        all.Value.Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldPersistAHistoricalCompletedTrip()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var startedOn = DateTimeOffset.Parse("2019-06-14T05:32:00Z");
        var trip = NewTrip(
            ownerUserId,
            status: TripStatusEnum.Completed,
            startedOn: startedOn,
            endedOn: startedOn.AddHours(10));

        // Act
        var result = await Sut.UpsertAsync(trip, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.StartedOn.Should().Be(startedOn);
        result.Value.EndedOn.Should().Be(startedOn.AddHours(10));
    }

    [Fact]
    public async Task ItShouldPersistACompletedTripWithNoEnd()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var trip = NewTrip(ownerUserId, status: TripStatusEnum.Completed);

        // Act
        var result = await Sut.UpsertAsync(trip, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TripStatusEnum.Completed);
        result.Value.EndedOn.Should().BeNull();
    }
}
