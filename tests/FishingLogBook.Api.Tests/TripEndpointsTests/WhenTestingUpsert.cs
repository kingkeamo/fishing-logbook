using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.TripEndpointsTests;

public class WhenTestingUpsert : IClassFixture<SystemApiFactory>
{
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");

    private readonly SystemApiFactory _factory;

    public WhenTestingUpsert(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectTheRequestWhenAuthorizationIsMissing()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/trips", NewTrip());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.TripRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Trip>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnEmptyTripId()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/trips", NewTrip(tripId: Guid.Empty));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.TripRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Trip>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnUnsupportedStatus()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/trips", NewTrip(status: "Planned"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.TripRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Trip>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAStartInTheFuture()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/trips",
            NewTrip(startedOn: DateTimeOffset.UtcNow.AddDays(1)));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.TripRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Trip>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnInvalidLocation()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/trips",
            NewTrip(location: Location(latitude: 200)));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.TripRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Trip>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportServiceUnavailableWhenPersistenceFails()
    {
        // Arrange
        ResetRepositories();
        _factory.TripRepository
            .UpsertAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Trip>("Failed to save the trip."));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/trips", NewTrip());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task ItShouldPersistABlankActiveTrip()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "trip-blank"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        current.Should().NotBeNull();
        var trip = NewTrip();

        // Act
        var response = await client.PostAsJsonAsync("/api/trips", trip);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await response.Content.ReadFromJsonAsync<TripDto>();
        saved.Should().NotBeNull();
        saved!.Id.Should().Be(trip.Id);
        saved.Status.Should().Be(TripConstants.Active);
        saved.Title.Should().BeNull();
        saved.PlaceName.Should().BeNull();
        saved.Location.Should().BeNull();
        await _factory.TripRepository.Received(1).UpsertAsync(
            Arg.Is<Trip>(item =>
                item.Id == trip.Id
                && item.OwnerUserId == current!.UserId
                && item.Status == TripStatusEnum.Active
                && item.Title == null
                && item.PlaceName == null
                && item.Location == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistATripWithATitlePlaceAndPrivateLocation()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "trip-located"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        current.Should().NotBeNull();
        var trip = NewTrip(
            title: "Day with Dad",
            placeName: "Lough Corrib",
            location: Location());

        // Act
        var response = await client.PostAsJsonAsync("/api/trips", trip);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await response.Content.ReadFromJsonAsync<TripDto>();
        saved!.Title.Should().Be("Day with Dad");
        saved.PlaceName.Should().Be("Lough Corrib");
        saved.Location!.Visibility.Should().Be(LocationDefaults.Private);
        await _factory.TripRepository.Received(1).UpsertAsync(
            Arg.Is<Trip>(item =>
                item.OwnerUserId == current!.UserId
                && item.Title == "Day with Dad"
                && item.PlaceName == "Lough Corrib"
                && item.Location != null
                && item.Location.Visibility == LocationDefaults.Private
                && item.Location.Source == LocationDefaults.DeviceGps),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFinishATrip()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "trip-finish"));
        var trip = NewTrip(
            status: TripConstants.Completed,
            endedOn: StartedOn.AddHours(6));

        // Act
        var response = await client.PostAsJsonAsync("/api/trips", trip);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await response.Content.ReadFromJsonAsync<TripDto>();
        saved!.Status.Should().Be(TripConstants.Completed);
        saved.EndedOn.Should().Be(StartedOn.AddHours(6));
        await _factory.TripRepository.Received(1).UpsertAsync(
            Arg.Is<Trip>(item =>
                item.Status == TripStatusEnum.Completed
                && item.EndedOn == StartedOn.AddHours(6)),
            Arg.Any<CancellationToken>());
    }

    private void ResetRepositories()
    {
        _factory.TripRepository.ClearReceivedCalls();
        _factory.TripRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(null));
        _factory.TripRepository
            .UpsertAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Trip>(0)));
    }

    private static TripDto NewTrip(
        Guid? tripId = null,
        string status = TripConstants.Active,
        DateTimeOffset? startedOn = null,
        DateTimeOffset? endedOn = null,
        string? title = null,
        string? placeName = null,
        TripLocationDto? location = null)
    {
        return new TripDto(
            tripId ?? Guid.NewGuid(),
            status,
            startedOn ?? StartedOn,
            endedOn,
            location)
        {
            Title = title,
            PlaceName = placeName
        };
    }

    private static TripLocationDto Location(double latitude = 53.4419, double longitude = -9.2531)
    {
        return new TripLocationDto(
            latitude,
            longitude,
            8,
            StartedOn,
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
    }
}
