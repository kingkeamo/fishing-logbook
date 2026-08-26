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

public class WhenTestingList : IClassFixture<SystemApiFactory>
{
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");

    private readonly SystemApiFactory _factory;

    public WhenTestingList(SystemApiFactory factory)
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
        var response = await client.GetAsync("/api/trips");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.TripRepository.DidNotReceive().GetByOwnerUserIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportServiceUnavailableWhenTheReadFails()
    {
        // Arrange
        ResetRepositories();
        _factory.TripRepository
            .GetByOwnerUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<Trip>>("Failed to save the trip."));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/trips");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task ItShouldReturnAnEmptyListWhenTheAnglerHasNoTrips()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "trip-list-empty"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        current.Should().NotBeNull();

        // Act
        var response = await client.GetAsync("/api/trips");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trips = await response.Content.ReadFromJsonAsync<IReadOnlyList<TripViewDto>>();
        trips.Should().BeEmpty();
        await _factory.TripRepository.Received(1).GetByOwnerUserIdAsync(
            current!.UserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheAnglersOwnTrips()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "trip-list-owned"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        current.Should().NotBeNull();
        var activeId = Guid.NewGuid();
        var completedId = Guid.NewGuid();
        _factory.TripRepository
            .GetByOwnerUserIdAsync(current!.UserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<Trip>>(
            [
                new Trip
                {
                    Id = activeId,
                    OwnerUserId = current.UserId,
                    Status = TripStatusEnum.Active,
                    StartedOn = StartedOn
                },
                new Trip
                {
                    Id = completedId,
                    OwnerUserId = current.UserId,
                    PlaceName = "Lough Corrib",
                    Status = TripStatusEnum.Completed,
                    StartedOn = StartedOn.AddDays(-1),
                    EndedOn = StartedOn.AddDays(-1).AddHours(5)
                }
            ]));

        // Act
        var response = await client.GetAsync("/api/trips");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trips = await response.Content.ReadFromJsonAsync<IReadOnlyList<TripViewDto>>();
        trips.Should().HaveCount(2);
        trips!.Single(trip => trip.Id == activeId).Status.Should().Be(TripConstants.Active);
        var completed = trips.Single(trip => trip.Id == completedId);
        completed.Status.Should().Be(TripConstants.Completed);
        completed.PlaceName.Should().Be("Lough Corrib");
        await _factory.TripRepository.Received(1).GetByOwnerUserIdAsync(
            current.UserId,
            Arg.Any<CancellationToken>());
    }

    private void ResetRepositories()
    {
        _factory.TripRepository.ClearReceivedCalls();
        _factory.TripRepository
            .GetByOwnerUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<Trip>>([]));
    }
}
