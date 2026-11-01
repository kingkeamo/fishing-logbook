using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.TripEndpointsTests;

public class WhenTestingActivate : IClassFixture<SystemApiFactory>
{
    private static readonly DateTimeOffset PlannedStartOn = DateTimeOffset.UtcNow.AddHours(1);

    private readonly SystemApiFactory _factory;

    public WhenTestingActivate(SystemApiFactory factory)
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
        var response = await client.PostAsync($"/api/trips/{Guid.NewGuid():D}/activate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.TripRepository.DidNotReceive().ActivateAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotFindATripTheCallerIsNotOn()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Planned(tripId, Guid.NewGuid())));
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "activate-stranger"));

        // Act
        var response = await client.PostAsync($"/api/trips/{tripId:D}/activate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _factory.TripRepository.DidNotReceive().ActivateAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldActivateAPlannedTripForItsOrganiser()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "activate-owner"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var trip = Planned(Guid.NewGuid(), current!.UserId);
        _factory.TripRepository.GetByIdAsync(trip.Id, Arg.Any<CancellationToken>()).Returns(Result.Ok<Trip?>(trip));

        // Act
        var response = await client.PostAsync($"/api/trips/{trip.Id:D}/activate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await response.Content.ReadFromJsonAsync<TripDto>();
        saved!.Status.Should().Be(TripConstants.Active);
        saved.PlannedStartOn.Should().Be(PlannedStartOn);
        await _factory.TripRepository.Received(1).ActivateAsync(
            Arg.Is<Trip>(item => item.Id == trip.Id && item.Status == TripStatusEnum.Active),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldActivateAPlannedTripForAnAcceptedParticipant()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "activate-guest"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var trip = Planned(Guid.NewGuid(), Guid.NewGuid());
        _factory.TripRepository.GetByIdAsync(trip.Id, Arg.Any<CancellationToken>()).Returns(Result.Ok<Trip?>(trip));
        _factory.TripParticipantRepository
            .FindAsync(
                Arg.Is<FishingLogBook.Application.Args.FindTripParticipantArgs>(args =>
                    args.TripId == trip.Id && args.UserId == current!.UserId),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripParticipant?>(new TripParticipant
            {
                Id = Guid.NewGuid(),
                TripId = trip.Id,
                UserId = current!.UserId,
                Status = TripParticipantStatusEnum.Accepted,
                InvitedByUserId = trip.OwnerUserId,
                InvitedOn = DateTimeOffset.UtcNow.AddDays(-1),
                RespondedOn = DateTimeOffset.UtcNow.AddHours(-2)
            }));

        // Act
        var response = await client.PostAsync($"/api/trips/{trip.Id:D}/activate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.TripRepository.Received(1).ActivateAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportAConflictWhenTheTripWasCancelled()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "activate-cancelled"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var trip = Planned(Guid.NewGuid(), current!.UserId, TripStatusEnum.Cancelled);
        _factory.TripRepository.GetByIdAsync(trip.Id, Arg.Any<CancellationToken>()).Returns(Result.Ok<Trip?>(trip));

        // Act
        var response = await client.PostAsync($"/api/trips/{trip.Id:D}/activate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await _factory.TripRepository.DidNotReceive().ActivateAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportAConflictWhenAnotherTripIsAlreadyActive()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "activate-busy"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var trip = Planned(Guid.NewGuid(), current!.UserId);
        _factory.TripRepository.GetByIdAsync(trip.Id, Arg.Any<CancellationToken>()).Returns(Result.Ok<Trip?>(trip));
        _factory.TripRepository.ActivateAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Trip>(new TripAlreadyActiveError()));

        // Act
        var response = await client.PostAsync($"/api/trips/{trip.Id:D}/activate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ItShouldReportServiceUnavailableWhenPersistenceFails()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "activate-failure"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var trip = Planned(Guid.NewGuid(), current!.UserId);
        _factory.TripRepository.GetByIdAsync(trip.Id, Arg.Any<CancellationToken>()).Returns(Result.Ok<Trip?>(trip));
        _factory.TripRepository.ActivateAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Trip>("Failed to save the trip."));

        // Act
        var response = await client.PostAsync($"/api/trips/{trip.Id:D}/activate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    private static Trip Planned(Guid tripId, Guid ownerUserId, TripStatusEnum status = TripStatusEnum.Planned)
    {
        return new Trip
        {
            Id = tripId,
            OwnerUserId = ownerUserId,
            Status = status,
            StartedOn = PlannedStartOn,
            PlannedStartOn = PlannedStartOn
        };
    }

    private void Reset()
    {
        _factory.TripRepository.ClearReceivedCalls();
        _factory.TripRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(null));
        _factory.TripRepository
            .ActivateAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Trip>(0)));
        _factory.TripParticipantRepository
            .FindAsync(Arg.Any<FishingLogBook.Application.Args.FindTripParticipantArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripParticipant?>(null));
    }
}
