using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Application.Args;
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
        await _factory.TripRepository.DidNotReceive().GetSummariesForUserAsync(
            Arg.Any<GetMyTripsArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportServiceUnavailableWhenTheReadFails()
    {
        // Arrange
        ResetRepositories();
        _factory.TripRepository
            .GetSummariesForUserAsync(Arg.Any<GetMyTripsArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<TripSummary>>("Failed to save the trip."));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/trips");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.TripRepository.Received(1).GetSummariesForUserAsync(
            Arg.Any<GetMyTripsArgs>(),
            Arg.Any<CancellationToken>());
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
        var trips = await response.Content.ReadFromJsonAsync<IReadOnlyList<TripSummaryDto>>();
        trips.Should().BeEmpty();
        await _factory.TripRepository.Received(1).GetSummariesForUserAsync(
            Arg.Is<GetMyTripsArgs>(args => args.UserId == current!.UserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheAnglersOwnTripsWithTheirCounts()
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
            .GetSummariesForUserAsync(
                Arg.Is<GetMyTripsArgs>(args => args.UserId == current!.UserId),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripSummary>>(
            [
                new TripSummary
                {
                    Id = activeId,
                    Status = TripStatusEnum.Active,
                    StartedOn = StartedOn
                },
                new TripSummary
                {
                    Id = completedId,
                    PlaceName = "Lough Corrib",
                    Status = TripStatusEnum.Completed,
                    StartedOn = StartedOn.AddDays(-1),
                    EndedOn = StartedOn.AddDays(-1).AddHours(5),
                    CatchCount = 4,
                    PhotographCount = 2,
                    NoteCount = 1
                }
            ]));

        // Act
        var response = await client.GetAsync("/api/trips");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trips = await response.Content.ReadFromJsonAsync<IReadOnlyList<TripSummaryDto>>();
        trips.Should().HaveCount(2);
        trips!.Single(trip => trip.Id == activeId).Status.Should().Be(TripConstants.Active);
        var completed = trips.Single(trip => trip.Id == completedId);
        completed.Status.Should().Be(TripConstants.Completed);
        completed.PlaceName.Should().Be("Lough Corrib");
        completed.CatchCount.Should().Be(4);
        completed.PhotographCount.Should().Be(2);
        completed.NoteCount.Should().Be(1);
        await _factory.TripRepository.Received(1).GetSummariesForUserAsync(
            Arg.Is<GetMyTripsArgs>(args => args.UserId == current.UserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotReadEveryTripsCatchesPhotographsOrNotesToBuildTheList()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "trip-list-no-amplification"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        _factory.TripRepository
            .GetSummariesForUserAsync(
                Arg.Is<GetMyTripsArgs>(args => args.UserId == current!.UserId),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripSummary>>(
            [
                new TripSummary { Id = Guid.NewGuid(), Status = TripStatusEnum.Completed, StartedOn = StartedOn },
                new TripSummary { Id = Guid.NewGuid(), Status = TripStatusEnum.Completed, StartedOn = StartedOn }
            ]));

        // Act
        var response = await client.GetAsync("/api/trips");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.TripRepository.DidNotReceive().GetCatchSummariesByTripIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await _factory.TripPhotographRepository.DidNotReceive().GetByTripIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await _factory.TripNoteRepository.DidNotReceive().GetByTripIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    private void ResetRepositories()
    {
        _factory.TripRepository.ClearReceivedCalls();
        _factory.TripNoteRepository.ClearReceivedCalls();
        _factory.TripPhotographRepository.ClearReceivedCalls();
        _factory.TripRepository
            .GetSummariesForUserAsync(Arg.Any<GetMyTripsArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripSummary>>([]));
    }
}
