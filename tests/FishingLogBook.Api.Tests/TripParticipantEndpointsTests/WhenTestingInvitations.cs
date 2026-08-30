using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.TripParticipantEndpointsTests;

public class WhenTestingInvitations : IClassFixture<SystemApiFactory>
{
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");

    private readonly SystemApiFactory _factory;

    public WhenTestingInvitations(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectAnUnauthenticatedList()
    {
        // Arrange
        Reset();
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/trips/invitations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.TripParticipantRepository.DidNotReceive()
            .GetPendingInvitationsByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnUnauthenticatedAccept()
    {
        // Arrange
        Reset();
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync(
            $"/api/trips/{Guid.NewGuid():D}/invitation/accept",
            content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.TripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNotFoundWhenThereIsNoInvitation()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "invitation-missing"));

        // Act
        var response = await client.PostAsync(
            $"/api/trips/{Guid.NewGuid():D}/invitation/accept",
            content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _factory.TripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotLetAnAnglerAcceptSomebodyElsesInvitation()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "invitation-impostor"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        _factory.TripParticipantRepository
            .FindAsync(
                Arg.Is<FindTripParticipantArgs>(args =>
                    args.TripId == tripId && args.UserId != current!.UserId),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripParticipant?>(Participant(
                tripId,
                Guid.NewGuid(),
                TripParticipantStatusEnum.Pending)));

        // Act
        var response = await client.PostAsync($"/api/trips/{tripId:D}/invitation/accept", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _factory.TripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportAConflictWhenTheInvitationWasAlreadyAnswered()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "invitation-answered"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        GivenInvitation(tripId, current!.UserId, TripParticipantStatusEnum.Accepted);

        // Act
        var response = await client.PostAsync($"/api/trips/{tripId:D}/invitation/accept", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await _factory.TripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDeclineTheInvitationForTheInvitedAngler()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "invitation-decline"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        GivenInvitation(tripId, current!.UserId, TripParticipantStatusEnum.Pending);

        // Act
        var response = await client.PostAsync($"/api/trips/{tripId:D}/invitation/decline", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.TripParticipantRepository.Received(1).UpsertAsync(
            Arg.Is<TripParticipant>(participant =>
                participant.UserId == current.UserId
                && participant.Status == TripParticipantStatusEnum.Declined
                && !participant.IsContributing),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAcceptTheInvitationForTheInvitedAngler()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "invitation-accept"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        GivenInvitation(tripId, current!.UserId, TripParticipantStatusEnum.Pending);

        // Act
        var response = await client.PostAsync($"/api/trips/{tripId:D}/invitation/accept", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.TripParticipantRepository.Received(1).UpsertAsync(
            Arg.Is<TripParticipant>(participant =>
                participant.TripId == tripId
                && participant.UserId == current.UserId
                && participant.Status == TripParticipantStatusEnum.Accepted
                && participant.IsContributing),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldListThePendingInvitationsOfTheSignedInAngler()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "invitation-list"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        _factory.TripParticipantRepository
            .GetPendingInvitationsByUserIdAsync(current!.UserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripParticipant>>(
                [Participant(tripId, current.UserId, TripParticipantStatusEnum.Pending)]));
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(new Trip
            {
                Id = tripId,
                OwnerUserId = ownerUserId,
                Status = TripStatusEnum.Active,
                StartedOn = StartedOn,
                PlaceName = "Lough Corrib"
            }));

        // Act
        var response = await client.GetAsync("/api/trips/invitations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var invitations = await response.Content.ReadFromJsonAsync<IReadOnlyList<TripInvitationDto>>();
        invitations.Should().ContainSingle();
        invitations![0].TripId.Should().Be(tripId);
        invitations[0].OwnerUserId.Should().Be(ownerUserId);
        invitations[0].PlaceName.Should().Be("Lough Corrib");
        await _factory.TripParticipantRepository.Received(1)
            .GetPendingInvitationsByUserIdAsync(current.UserId, Arg.Any<CancellationToken>());
    }

    private void GivenInvitation(Guid tripId, Guid userId, TripParticipantStatusEnum status)
    {
        _factory.TripParticipantRepository
            .FindAsync(
                Arg.Is<FindTripParticipantArgs>(args =>
                    args.TripId == tripId && args.UserId == userId),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripParticipant?>(Participant(tripId, userId, status)));
    }

    private static TripParticipant Participant(
        Guid tripId,
        Guid userId,
        TripParticipantStatusEnum status)
    {
        return new TripParticipant
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            UserId = userId,
            Status = status,
            InvitedByUserId = Guid.NewGuid(),
            InvitedOn = StartedOn.AddDays(-1),
            RespondedOn = status == TripParticipantStatusEnum.Pending ? null : StartedOn.AddHours(-1)
        };
    }

    private void Reset()
    {
        _factory.TripRepository.ClearReceivedCalls();
        _factory.TripParticipantRepository.ClearReceivedCalls();
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.TripRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(null));
        _factory.TripParticipantRepository
            .FindAsync(Arg.Any<FindTripParticipantArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripParticipant?>(null));
        _factory.TripParticipantRepository
            .GetPendingInvitationsByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripParticipant>>([]));
        _factory.TripParticipantRepository
            .UpsertAsync(Arg.Any<TripParticipant>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<TripParticipant>(0)));
        _factory.ProfileRepository
            .GetByUserIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<Profile>>([]));
    }
}
