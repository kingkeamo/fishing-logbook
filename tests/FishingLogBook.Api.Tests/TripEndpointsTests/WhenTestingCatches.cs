using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Domain.Users;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.TripEndpointsTests;

public class WhenTestingCatches : IClassFixture<SystemApiFactory>
{
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");
    private static readonly DateTimeOffset CaughtOn = DateTimeOffset.Parse("2026-08-17T09:12:00Z");

    private readonly SystemApiFactory _factory;

    public WhenTestingCatches(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectAnUnauthenticatedRequest()
    {
        // Arrange
        Reset();
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{Guid.NewGuid():D}/catches",
            new AssociateTripCatchesDto([Guid.NewGuid()]));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.CatchRepository.DidNotReceive().AssociateTripAsync(
            Arg.Any<PersistCatchTripArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnEmptyCatchList()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{Guid.NewGuid():D}/catches",
            new AssociateTripCatchesDto([]));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.CatchRepository.DidNotReceive().AssociateTripAsync(
            Arg.Any<PersistCatchTripArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotFindAnotherAnglersTrip()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, Guid.NewGuid())));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{tripId:D}/catches",
            new AssociateTripCatchesDto([Guid.NewGuid()]));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _factory.CatchRepository.DidNotReceive().AssociateTripAsync(
            Arg.Any<PersistCatchTripArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectACatchBelongingToAnotherAngler()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var tripId = Guid.NewGuid();
        var catchId = Guid.NewGuid();
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, current!.UserId)));
        _factory.CatchRepository.GetByIdAsync(catchId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(CatchRecord(catchId, Guid.NewGuid())));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{tripId:D}/catches",
            new AssociateTripCatchesDto([catchId]));
        var body = await response.Content.ReadFromJsonAsync<TripCatchAssociationDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.AssociatedCatchIds.Should().BeEmpty();
        body.RejectedCatchIds.Should().ContainSingle().Which.Should().Be(catchId);
        await _factory.CatchRepository.DidNotReceive().AssociateTripAsync(
            Arg.Any<PersistCatchTripArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectACatchAlreadyAssociatedWithAnotherTrip()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var tripId = Guid.NewGuid();
        var catchId = Guid.NewGuid();
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, current!.UserId)));
        _factory.CatchRepository.GetByIdAsync(catchId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(CatchRecord(catchId, current.UserId, tripId: Guid.NewGuid())));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{tripId:D}/catches",
            new AssociateTripCatchesDto([catchId]));
        var body = await response.Content.ReadFromJsonAsync<TripCatchAssociationDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.RejectedCatchIds.Should().ContainSingle().Which.Should().Be(catchId);
        await _factory.CatchRepository.DidNotReceive().AssociateTripAsync(
            Arg.Any<PersistCatchTripArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectACatchOutsideTheTripTimeframe()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var tripId = Guid.NewGuid();
        var catchId = Guid.NewGuid();
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, current!.UserId, TripStatusEnum.Completed)));
        _factory.CatchRepository.GetByIdAsync(catchId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(CatchRecord(catchId, current.UserId, StartedOn.AddHours(4))));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{tripId:D}/catches",
            new AssociateTripCatchesDto([catchId]));
        var body = await response.Content.ReadFromJsonAsync<TripCatchAssociationDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.RejectedCatchIds.Should().ContainSingle().Which.Should().Be(catchId);
        await _factory.CatchRepository.DidNotReceive().AssociateTripAsync(
            Arg.Any<PersistCatchTripArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportServiceUnavailableWhenPersistenceFails()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var tripId = Guid.NewGuid();
        var catchId = Guid.NewGuid();
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, current!.UserId)));
        _factory.CatchRepository.GetByIdAsync(catchId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(CatchRecord(catchId, current.UserId)));
        _factory.CatchRepository
            .AssociateTripAsync(Arg.Any<PersistCatchTripArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<bool>("Failed to save the catch."));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{tripId:D}/catches",
            new AssociateTripCatchesDto([catchId]));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.CatchRepository.Received(1).AssociateTripAsync(
            Arg.Is<PersistCatchTripArgs>(args =>
                args.CatchId == catchId
                && args.TripId == tripId
                && args.CaughtByUserId == current.UserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAssociateAnEligibleCatchWithTheAnglersOwnTrip()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var tripId = Guid.NewGuid();
        var catchId = Guid.NewGuid();
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, current!.UserId)));
        _factory.CatchRepository.GetByIdAsync(catchId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(CatchRecord(catchId, current.UserId)));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{tripId:D}/catches",
            new AssociateTripCatchesDto([catchId]));
        var body = await response.Content.ReadFromJsonAsync<TripCatchAssociationDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.AssociatedCatchIds.Should().ContainSingle().Which.Should().Be(catchId);
        body.RejectedCatchIds.Should().BeEmpty();
        await _factory.CatchRepository.Received(1).AssociateTripAsync(
            Arg.Is<PersistCatchTripArgs>(args =>
                args.CatchId == catchId
                && args.TripId == tripId
                && args.CaughtByUserId == current.UserId),
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

    private static Catch CatchRecord(
        Guid catchId,
        Guid userId,
        DateTimeOffset? caughtOn = null,
        Guid? tripId = null)
    {
        return new Catch
        {
            Id = catchId,
            CaughtByUserId = userId,
            RecordedByUserId = userId,
            CaughtOn = caughtOn ?? CaughtOn,
            TripId = tripId
        };
    }

    private void Reset()
    {
        _factory.TripRepository.ClearReceivedCalls();
        _factory.CatchRepository.ClearReceivedCalls();
        _factory.CatchRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(null));
        _factory.CatchRepository
            .AssociateTripAsync(Arg.Any<PersistCatchTripArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(true));
    }
}
