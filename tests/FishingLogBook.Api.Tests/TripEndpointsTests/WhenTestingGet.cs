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

public class WhenTestingGet : IClassFixture<SystemApiFactory>
{
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");

    private readonly SystemApiFactory _factory;

    public WhenTestingGet(SystemApiFactory factory)
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
        var response = await client.GetAsync($"/api/trips/{Guid.NewGuid():D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.TripRepository.DidNotReceive().GetByIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNotFoundWhenTheTripIsMissing()
    {
        // Arrange
        var tripId = Guid.NewGuid();
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync($"/api/trips/{tripId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _factory.TripRepository.Received(1).GetByIdAsync(tripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNotFoundForAnotherAnglersTrip()
    {
        // Arrange
        var tripId = Guid.NewGuid();
        ResetRepositories();
        _factory.TripRepository
            .GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(new Trip
            {
                Id = tripId,
                OwnerUserId = Guid.NewGuid(),
                Status = TripStatusEnum.Active,
                StartedOn = StartedOn
            }));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync($"/api/trips/{tripId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _factory.TripRepository.Received(1).GetByIdAsync(tripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportServiceUnavailableWhenTheReadFails()
    {
        // Arrange
        var tripId = Guid.NewGuid();
        ResetRepositories();
        _factory.TripRepository
            .GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Trip?>("Failed to save the trip."));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync($"/api/trips/{tripId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task ItShouldReturnABlankTripWithNoTitlePlaceOrLocation()
    {
        // Arrange
        var tripId = Guid.NewGuid();
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "trip-get-blank"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        current.Should().NotBeNull();
        _factory.TripRepository
            .GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(new Trip
            {
                Id = tripId,
                OwnerUserId = current!.UserId,
                Status = TripStatusEnum.Active,
                StartedOn = StartedOn
            }));

        // Act
        var response = await client.GetAsync($"/api/trips/{tripId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trip = await response.Content.ReadFromJsonAsync<TripViewDto>();
        trip.Should().NotBeNull();
        trip!.Id.Should().Be(tripId);
        trip.OwnerUserId.Should().Be(current.UserId);
        trip.Status.Should().Be(TripConstants.Active);
        trip.Title.Should().BeNull();
        trip.PlaceName.Should().BeNull();
        trip.Location.Should().BeNull();
        trip.EndedOn.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldReturnACompletedTripWithItsPlaceAndLocation()
    {
        // Arrange
        var tripId = Guid.NewGuid();
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "trip-get-located"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        current.Should().NotBeNull();
        _factory.TripRepository
            .GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(new Trip
            {
                Id = tripId,
                OwnerUserId = current!.UserId,
                Title = "Day with Dad",
                PlaceName = "Lough Corrib",
                Status = TripStatusEnum.Completed,
                StartedOn = StartedOn,
                EndedOn = StartedOn.AddHours(6),
                Location = TripLocation.TryCreate(
                    53.4419,
                    -9.2531,
                    8,
                    StartedOn,
                    LocationDefaults.DeviceGps,
                    LocationDefaults.Private,
                    LocationDefaults.ConsentVersion)
            }));

        // Act
        var response = await client.GetAsync($"/api/trips/{tripId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trip = await response.Content.ReadFromJsonAsync<TripViewDto>();
        trip!.Status.Should().Be(TripConstants.Completed);
        trip.Title.Should().Be("Day with Dad");
        trip.PlaceName.Should().Be("Lough Corrib");
        trip.EndedOn.Should().Be(StartedOn.AddHours(6));
        trip.Location!.Latitude.Should().Be(53.4419);
        trip.Location.Visibility.Should().Be(LocationDefaults.Private);
        await _factory.TripRepository.Received(1).GetByIdAsync(tripId, Arg.Any<CancellationToken>());
    }

    private void ResetRepositories()
    {
        _factory.TripRepository.ClearReceivedCalls();
        _factory.TripRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(null));
    }
}
