using AwesomeAssertions;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripServiceTests;

public class WhenTestingUpsert : BaseTripServiceTest
{
    [Fact]
    public async Task ItShouldRejectAnUnknownStatus()
    {
        // Arrange
        var args = UpsertArgs(status: "Abandoned");

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripLifecycleInvalidError>();
        await MockTripRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Trip>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnActiveTripThatAlreadyEnded()
    {
        // Arrange
        var args = UpsertArgs(endedOn: StartedOn.AddHours(3));

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripLifecycleInvalidError>();
        await MockTripRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Trip>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnEndBeforeTheStart()
    {
        // Arrange
        var args = UpsertArgs(
            status: TripConstants.Completed,
            endedOn: StartedOn.AddHours(-1));

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripLifecycleInvalidError>();
        await MockTripRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Trip>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnInvalidLocation()
    {
        // Arrange
        var args = UpsertArgs(location: PrivateLocation(latitude: 200));

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripLocationInvalidError>();
        await MockTripRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Trip>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnUnsupportedLocationVisibility()
    {
        // Arrange
        var args = UpsertArgs(location: PrivateLocation(visibility: "Everyone"));

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripLocationInvalidError>();
    }

    [Fact]
    public async Task ItShouldRejectASecondActiveTripForTheSameOwner()
    {
        // Arrange
        MockTripRepository.GetActiveAsync(CurrentUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(StoredTrip(
                tripId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"))));
        var args = UpsertArgs();

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripAlreadyActiveError>();
        await MockTripRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Trip>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAllowReplayingTheSameActiveTrip()
    {
        // Arrange
        MockTripRepository.GetActiveAsync(CurrentUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(StoredTrip()));
        var args = UpsertArgs();

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(TripId);
        await MockTripRepository.Received(1).UpsertAsync(
            Arg.Is<Trip>(trip => trip.Id == TripId && trip.Status == TripStatusEnum.Active),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotCheckForAnActiveTripWhenCompletingOne()
    {
        // Arrange
        var args = UpsertArgs(
            status: TripConstants.Completed,
            endedOn: StartedOn.AddHours(6));

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockTripRepository.DidNotReceive().GetActiveAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistATripWithNoTitleOrPlaceOrLocation()
    {
        // Arrange
        var args = UpsertArgs();

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().BeNull();
        result.Value.PlaceName.Should().BeNull();
        result.Value.Location.Should().BeNull();
        await MockTripRepository.Received(1).UpsertAsync(
            Arg.Is<Trip>(trip =>
                trip.Title == null
                && trip.PlaceName == null
                && trip.Location == null
                && trip.OwnerUserId == CurrentUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTrimTitleAndPlaceToNullWhenBlank()
    {
        // Arrange
        var args = UpsertArgs(title: "   ", placeName: "\t");

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockTripRepository.Received(1).UpsertAsync(
            Arg.Is<Trip>(trip => trip.Title == null && trip.PlaceName == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistAPlaceNameWithoutCoordinates()
    {
        // Arrange
        var args = UpsertArgs(placeName: "  Lough Corrib  ");

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PlaceName.Should().Be("Lough Corrib");
        result.Value.Location.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldPersistAPrivateLocationWithItsProvenance()
    {
        // Arrange
        var args = UpsertArgs(title: "Day with Dad", location: PrivateLocation());

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Day with Dad");
        result.Value.Location.Should().NotBeNull();
        result.Value.Location!.Latitude.Should().Be(53.4419);
        result.Value.Location.Longitude.Should().Be(-9.2531);
        result.Value.Location.AccuracyMetres.Should().Be(8);
        result.Value.Location.Source.Should().Be(LocationDefaults.DeviceGps);
        result.Value.Location.Visibility.Should().Be(LocationDefaults.Private);
        result.Value.Location.ConsentVersion.Should().Be(LocationDefaults.ConsentVersion);
    }

    [Fact]
    public async Task ItShouldTakeOwnershipFromTheAuthenticatedCallerRatherThanThePayload()
    {
        // Arrange
        var args = UpsertArgs(userId: OtherUserId);

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockTripRepository.Received(1).UpsertAsync(
            Arg.Is<Trip>(trip => trip.OwnerUserId == OtherUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheRepositoryFailure()
    {
        // Arrange
        MockTripRepository.UpsertAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Trip>(new TripAlreadyActiveError()));
        var args = UpsertArgs();

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripAlreadyActiveError>();
    }
}
