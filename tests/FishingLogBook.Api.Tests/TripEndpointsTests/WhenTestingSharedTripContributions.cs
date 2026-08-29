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

namespace FishingLogBook.Api.Tests.TripEndpointsTests;

public class WhenTestingSharedTripContributions : IClassFixture<SystemApiFactory>
{
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");
    private static readonly DateTimeOffset RecordedOn = DateTimeOffset.Parse("2026-08-26T06:12:00Z");

    private readonly SystemApiFactory _factory;

    public WhenTestingSharedTripContributions(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectANoteFromANonParticipant()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "shared-note-stranger"));
        GivenTrip(tripId, Guid.NewGuid());

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{tripId:D}/notes",
            new RecordTripNoteDto(Guid.NewGuid(), "sneaking in", RecordedOn));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _factory.TripNoteRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripNote>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectANoteFromARemovedParticipant()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "shared-note-removed"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        GivenTrip(tripId, ownerUserId);
        GivenParticipant(
            tripId,
            current!.UserId,
            ownerUserId,
            removedOn: StartedOn.AddHours(1));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{tripId:D}/notes",
            new RecordTripNoteDto(Guid.NewGuid(), "after being removed", RecordedOn));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _factory.TripNoteRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripNote>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldForbidAParticipantDeletingAnotherAnglersNote()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        var noteId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "shared-note-foreign-delete"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        GivenTrip(tripId, ownerUserId);
        GivenParticipant(tripId, current!.UserId, ownerUserId);
        _factory.TripNoteRepository.GetByIdAsync(noteId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripNote?>(new TripNote
            {
                Id = noteId,
                TripId = tripId,
                CreatedByUserId = ownerUserId,
                Text = "the owners note",
                RecordedOn = RecordedOn
            }));

        // Act
        var response = await client.DeleteAsync($"/api/trips/{tripId:D}/notes/{noteId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _factory.TripNoteRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldForbidAParticipantDeletingAnotherAnglersPhotograph()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "shared-photo-foreign-delete"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        GivenTrip(tripId, ownerUserId);
        GivenParticipant(tripId, current!.UserId, ownerUserId);
        _factory.TripPhotographRepository.GetByIdAsync(photographId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripPhotograph?>(new TripPhotograph
            {
                Id = photographId,
                TripId = tripId,
                ContributedByUserId = ownerUserId,
                ObjectKey = $"trip-photographs/{tripId:D}/{photographId:D}",
                ContentType = PhotographContentTypeConstants.Jpeg,
                AddedOn = RecordedOn
            }));

        // Act
        var response = await client.DeleteAsync(
            $"/api/trips/{tripId:D}/photographs/{photographId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _factory.TripPhotographRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLetAnAcceptedParticipantAddANoteToTheSharedTrip()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        var noteId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "shared-note-participant"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        GivenTrip(tripId, ownerUserId);
        GivenParticipant(tripId, current!.UserId, ownerUserId);

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{tripId:D}/notes",
            new RecordTripNoteDto(noteId, "fish moving on the shallows", RecordedOn));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TripNoteDto>();
        body!.TripId.Should().Be(tripId);
        body.CreatedByUserId.Should().Be(current.UserId);
        body.CreatedByUserId.Should().NotBe(ownerUserId);
        await _factory.TripNoteRepository.Received(1).UpsertAsync(
            Arg.Is<TripNote>(note =>
                note.TripId == tripId
                && note.CreatedByUserId == current.UserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDeriveAParticipantPhotographUploadKeyFromTheTripNotAnyUserIdentity()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "shared-photo-participant"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        GivenTrip(tripId, ownerUserId);
        GivenParticipant(tripId, current!.UserId, ownerUserId);

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{tripId:D}/photographs/upload-url",
            new PhotographUploadRequestDto(photographId, PhotographContentTypeConstants.Jpeg));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var upload = await response.Content.ReadFromJsonAsync<PhotographUploadDto>();
        upload!.ObjectKey.Should().Be($"trip-photographs/{tripId:D}/{photographId:D}");
        upload.ObjectKey.Should().NotContain(ownerUserId.ToString("D"));
        upload.ObjectKey.Should().NotContain(current.UserId.ToString("D"));
    }

    [Fact]
    public async Task ItShouldGiveAnAcceptedParticipantTheSameTripIdAndTimeline()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "shared-detail-participant"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        GivenTrip(tripId, ownerUserId);
        GivenParticipant(tripId, current!.UserId, ownerUserId);
        _factory.TripNoteRepository.GetByTripIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripNote>>(
            [
                new TripNote
                {
                    Id = Guid.NewGuid(),
                    TripId = tripId,
                    CreatedByUserId = ownerUserId,
                    Text = "the owners note",
                    RecordedOn = StartedOn.AddMinutes(5)
                },
                new TripNote
                {
                    Id = Guid.NewGuid(),
                    TripId = tripId,
                    CreatedByUserId = current.UserId,
                    Text = "my note",
                    RecordedOn = StartedOn.AddMinutes(20)
                }
            ]));

        // Act
        var response = await client.GetAsync($"/api/trips/{tripId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<TripDetailDto>();
        detail!.Trip.Id.Should().Be(tripId);
        detail.Trip.OwnerUserId.Should().Be(ownerUserId);
        detail.Role.Should().Be(TripParticipantConstants.Participant);
        detail.Notes.Should().HaveCount(2);
        detail.Notes.Select(note => note.CreatedByUserId)
            .Should().Equal([ownerUserId, current.UserId]);
        await _factory.TripNoteRepository.Received(1).GetByTripIdAsync(
            tripId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldListTheSharedTripForTheParticipantUnderTheSameId()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "shared-list-participant"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        _factory.TripRepository
            .GetSummariesForUserAsync(
                Arg.Is<GetMyTripsArgs>(args => args.UserId == current!.UserId),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripSummary>>(
            [
                new TripSummary
                {
                    Id = tripId,
                    OwnerUserId = ownerUserId,
                    Status = TripStatusEnum.Active,
                    StartedOn = StartedOn,
                    ParticipantCount = 1
                }
            ]));

        // Act
        var response = await client.GetAsync("/api/trips");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trips = await response.Content.ReadFromJsonAsync<IReadOnlyList<TripSummaryDto>>();
        trips.Should().ContainSingle();
        trips![0].Id.Should().Be(tripId);
        trips[0].OwnerUserId.Should().Be(ownerUserId);
        trips[0].Role.Should().Be(TripParticipantConstants.Participant);
        trips[0].IsShared.Should().BeTrue();
    }

    private void GivenTrip(Guid tripId, Guid ownerUserId)
    {
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(new Trip
            {
                Id = tripId,
                OwnerUserId = ownerUserId,
                Status = TripStatusEnum.Active,
                StartedOn = StartedOn
            }));
    }

    private void GivenParticipant(
        Guid tripId,
        Guid userId,
        Guid ownerUserId,
        DateTimeOffset? removedOn = null)
    {
        _factory.TripParticipantRepository
            .FindAsync(
                Arg.Is<FindTripParticipantArgs>(args =>
                    args.TripId == tripId && args.UserId == userId),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripParticipant?>(new TripParticipant
            {
                Id = Guid.NewGuid(),
                TripId = tripId,
                UserId = userId,
                Status = TripParticipantStatusEnum.Accepted,
                InvitedByUserId = ownerUserId,
                InvitedOn = StartedOn.AddDays(-1),
                RespondedOn = StartedOn.AddHours(-1),
                RemovedOn = removedOn
            }));
    }

    private void Reset()
    {
        _factory.ObjectStorage.ClearReceivedCalls();
        _factory.ObjectStorage.IsConfigured.Returns(true);
        _factory.ObjectStorage.CreateUploadUrlAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(new Uri("https://storage.test/upload"));
        _factory.TripRepository.ClearReceivedCalls();
        _factory.TripNoteRepository.ClearReceivedCalls();
        _factory.TripPhotographRepository.ClearReceivedCalls();
        _factory.TripParticipantRepository.ClearReceivedCalls();
        _factory.TripRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(null));
        _factory.TripRepository
            .GetCatchSummariesByTripIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripCatchSummary>>([]));
        _factory.TripNoteRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripNote?>(null));
        _factory.TripNoteRepository
            .GetByTripIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripNote>>([]));
        _factory.TripNoteRepository
            .UpsertAsync(Arg.Any<TripNote>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<TripNote>(0)));
        _factory.TripPhotographRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripPhotograph?>(null));
        _factory.TripPhotographRepository
            .GetByTripIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripPhotograph>>([]));
        _factory.TripParticipantRepository
            .FindAsync(Arg.Any<FindTripParticipantArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripParticipant?>(null));
        _factory.ProfileRepository
            .GetByUserIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<Profile>>([]));
    }
}
