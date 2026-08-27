using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Domain.Users;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.CatchEndpointsTests;

public class WhenTestingUpsertWithATrip : IClassFixture<SystemApiFactory>
{
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");

    private readonly SystemApiFactory _factory;

    public WhenTestingUpsertWithATrip(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectACatchForAnUnknownTrip()
    {
        // Arrange
        Reset();
        _factory.TripRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(null));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/catches", TrippedDto(Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.CatchRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Catch>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectACatchForAnotherAnglersTrip()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, Guid.NewGuid())));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/catches", TrippedDto(tripId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.CatchRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Catch>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRevealWhetherAnotherAnglersTripExists()
    {
        // Arrange
        Reset();
        var knownTripId = Guid.NewGuid();
        var unknownTripId = Guid.NewGuid();
        _factory.TripRepository.GetByIdAsync(knownTripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(knownTripId, Guid.NewGuid())));
        _factory.TripRepository.GetByIdAsync(unknownTripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(null));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var known = await client.PostAsJsonAsync("/api/catches", TrippedDto(knownTripId));
        var unknown = await client.PostAsJsonAsync("/api/catches", TrippedDto(unknownTripId));

        // Assert
        known.StatusCode.Should().Be(unknown.StatusCode);
        var knownBody = await known.Content.ReadAsStringAsync();
        var unknownBody = await unknown.Content.ReadAsStringAsync();
        knownBody.Should().Be(unknownBody);
        knownBody.Should().NotContain(knownTripId.ToString("D"));
    }

    [Fact]
    public async Task ItShouldStillAcceptACatchWithNoTrip()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();
        var dto = TrippedDto(null);

        // Act
        var response = await client.PostAsJsonAsync("/api/catches", dto);
        var body = await response.Content.ReadFromJsonAsync<CatchDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.TripId.Should().BeNull();
        await _factory.TripRepository.DidNotReceive().GetByIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await _factory.CatchRepository.Received(1).UpsertAsync(
            Arg.Is<Catch>(item => item.TripId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAcceptACatchForTheAnglersOwnCompletedTrip()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var tripId = Guid.NewGuid();
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, current!.UserId, TripStatusEnum.Completed)));
        var dto = TrippedDto(tripId);

        // Act
        var response = await client.PostAsJsonAsync("/api/catches", dto);
        var body = await response.Content.ReadFromJsonAsync<CatchDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.TripId.Should().Be(tripId);
        await _factory.CatchRepository.Received(1).UpsertAsync(
            Arg.Is<Catch>(item =>
                item.Id == dto.Id
                && item.TripId == tripId
                && item.UserId == current.UserId
                && item.Location == null),
            Arg.Any<CancellationToken>());
    }

    private static Trip Trip(
        Guid tripId,
        Guid ownerUserId,
        TripStatusEnum status = TripStatusEnum.Active)
    {
        return new Trip
        {
            Id = tripId,
            OwnerUserId = ownerUserId,
            Status = status,
            StartedOn = StartedOn,
            EndedOn = status == TripStatusEnum.Completed ? StartedOn.AddHours(3) : null
        };
    }

    private static CatchDto TrippedDto(Guid? tripId)
    {
        var catchId = Guid.NewGuid();
        return new CatchDto(
            catchId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographDto(Guid.NewGuid(), catchId, PhotographContentTypeConstants.Jpeg)])
        {
            TripId = tripId
        };
    }

    private void Reset()
    {
        _factory.CatchRepository.ClearReceivedCalls();
        _factory.TripRepository.ClearReceivedCalls();
        _factory.CatchRepository
            .UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Catch>(0)));
    }
}
