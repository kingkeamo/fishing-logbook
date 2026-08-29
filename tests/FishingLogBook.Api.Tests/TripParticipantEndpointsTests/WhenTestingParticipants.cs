using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.TripParticipantEndpointsTests;

public class WhenTestingParticipants : IClassFixture<SystemApiFactory>
{
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");

    private readonly SystemApiFactory _factory;

    public WhenTestingParticipants(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectAnUnauthenticatedRead()
    {
        // Arrange
        Reset();
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/trips/{Guid.NewGuid():D}/participants");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.TripParticipantRepository.DidNotReceive().GetByTripIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnUnauthenticatedInvite()
    {
        // Arrange
        Reset();
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{Guid.NewGuid():D}/participants",
            new InviteTripParticipantDto(Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.TripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNotFoundForATripTheAnglerIsNotOn()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "participants-stranger"));
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, Guid.NewGuid())));

        // Act
        var response = await client.GetAsync($"/api/trips/{tripId:D}/participants");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _factory.TripParticipantRepository.DidNotReceive().GetByTripIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldForbidAParticipantInvitingAnotherAngler()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "participants-not-owner"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, ownerUserId)));
        GivenAcceptedParticipant(tripId, current!.UserId, ownerUserId);

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{tripId:D}/participants",
            new InviteTripParticipantDto(Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _factory.TripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldForbidAParticipantRemovingAnotherAngler()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "participants-remove-not-owner"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, ownerUserId)));
        GivenAcceptedParticipant(tripId, current!.UserId, ownerUserId);

        // Act
        var response = await client.DeleteAsync(
            $"/api/trips/{tripId:D}/participants/{Guid.NewGuid():D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _factory.TripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectTheOwnerInvitingThemselves()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "participants-self-invite"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, current!.UserId)));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{tripId:D}/participants",
            new InviteTripParticipantDto(current.UserId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.TripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportAConflictForADuplicateInvitation()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "participants-duplicate"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, current!.UserId)));
        _factory.TripParticipantRepository
            .FindAsync(
                Arg.Is<FindTripParticipantArgs>(args =>
                    args.TripId == tripId && args.UserId == invitedUserId),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripParticipant?>(Participant(
                tripId,
                invitedUserId,
                current.UserId,
                TripParticipantStatusEnum.Pending)));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{tripId:D}/participants",
            new InviteTripParticipantDto(invitedUserId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await _factory.TripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLetTheOwnerInviteAnExistingAngler()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "participants-invite"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, current!.UserId)));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{tripId:D}/participants",
            new InviteTripParticipantDto(invitedUserId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TripParticipantsDto>();
        body!.TripId.Should().Be(tripId);
        body.Role.Should().Be(TripParticipantConstants.Owner);
        await _factory.TripParticipantRepository.Received(1).UpsertAsync(
            Arg.Is<TripParticipant>(participant =>
                participant.TripId == tripId
                && participant.UserId == invitedUserId
                && participant.InvitedByUserId == current.UserId
                && participant.Status == TripParticipantStatusEnum.Pending),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLetTheOwnerRemoveAParticipantWithoutTouchingTheirContributions()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        var participantUserId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "participants-remove"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, current!.UserId)));
        GivenAcceptedParticipant(tripId, participantUserId, current.UserId);

        // Act
        var response = await client.DeleteAsync(
            $"/api/trips/{tripId:D}/participants/{participantUserId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.TripParticipantRepository.Received(1).UpsertAsync(
            Arg.Is<TripParticipant>(participant =>
                participant.UserId == participantUserId
                && participant.RemovedOn != null
                && !participant.IsContributing),
            Arg.Any<CancellationToken>());
        await _factory.TripNoteRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await _factory.TripPhotographRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    private void GivenAcceptedParticipant(Guid tripId, Guid userId, Guid ownerUserId)
    {
        _factory.TripParticipantRepository
            .FindAsync(
                Arg.Is<FindTripParticipantArgs>(args =>
                    args.TripId == tripId && args.UserId == userId),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripParticipant?>(Participant(
                tripId,
                userId,
                ownerUserId,
                TripParticipantStatusEnum.Accepted)));
    }

    private static TripParticipant Participant(
        Guid tripId,
        Guid userId,
        Guid invitedByUserId,
        TripParticipantStatusEnum status)
    {
        return new TripParticipant
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            UserId = userId,
            Status = status,
            InvitedByUserId = invitedByUserId,
            InvitedOn = StartedOn.AddDays(-1),
            RespondedOn = status == TripParticipantStatusEnum.Pending ? null : StartedOn.AddHours(-1)
        };
    }

    private static Trip Trip(Guid tripId, Guid ownerUserId)
    {
        return new Trip
        {
            Id = tripId,
            OwnerUserId = ownerUserId,
            Status = TripStatusEnum.Active,
            StartedOn = StartedOn
        };
    }

    private void Reset()
    {
        _factory.TripRepository.ClearReceivedCalls();
        _factory.TripNoteRepository.ClearReceivedCalls();
        _factory.TripPhotographRepository.ClearReceivedCalls();
        _factory.TripParticipantRepository.ClearReceivedCalls();
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.TripRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(null));
        _factory.TripParticipantRepository
            .FindAsync(Arg.Any<FindTripParticipantArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripParticipant?>(null));
        _factory.TripParticipantRepository
            .GetByTripIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripParticipant>>([]));
        _factory.TripParticipantRepository
            .UpsertAsync(Arg.Any<TripParticipant>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<TripParticipant>(0)));
        _factory.ProfileRepository
            .UserExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(true));
        _factory.ProfileRepository
            .GetByUserIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<Profile>>([]));
    }
}
