using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Domain.Users;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.TripEndpointsTests;

public class WhenTestingNotes : IClassFixture<SystemApiFactory>
{
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");
    private static readonly DateTimeOffset RecordedOn = DateTimeOffset.Parse("2026-08-17T09:12:00Z");

    private readonly SystemApiFactory _factory;

    public WhenTestingNotes(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectAnUnauthenticatedNote()
    {
        // Arrange
        Reset();
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{Guid.NewGuid():D}/notes",
            new RecordTripNoteDto(Guid.NewGuid(), "water dropped", RecordedOn));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.TripNoteRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripNote>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAWhitespaceOnlyNote()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{Guid.NewGuid():D}/notes",
            new RecordTripNoteDto(Guid.NewGuid(), "   ", RecordedOn));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.TripNoteRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripNote>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectANoteOverTheLengthCap()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{Guid.NewGuid():D}/notes",
            new RecordTripNoteDto(
                Guid.NewGuid(),
                new string('a', TripConstants.MaxNoteTextLength + 1),
                RecordedOn));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.TripNoteRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripNote>(),
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
            $"/api/trips/{tripId:D}/notes",
            new RecordTripNoteDto(Guid.NewGuid(), "water dropped", RecordedOn));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _factory.TripNoteRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripNote>(),
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
        var known = await client.PostAsJsonAsync(
            $"/api/trips/{knownTripId:D}/notes",
            new RecordTripNoteDto(Guid.NewGuid(), "water dropped", RecordedOn));
        var unknown = await client.PostAsJsonAsync(
            $"/api/trips/{unknownTripId:D}/notes",
            new RecordTripNoteDto(Guid.NewGuid(), "water dropped", RecordedOn));

        // Assert
        known.StatusCode.Should().Be(unknown.StatusCode);
        (await known.Content.ReadAsStringAsync())
            .Should().Be(await unknown.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ItShouldRecordANoteForTheAnglersOwnTrip()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var tripId = Guid.NewGuid();
        var noteId = Guid.NewGuid();
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, current!.UserId, TripStatusEnum.Completed)));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{tripId:D}/notes",
            new RecordTripNoteDto(noteId, "  a good day  ", RecordedOn));
        var body = await response.Content.ReadFromJsonAsync<TripNoteDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Id.Should().Be(noteId);
        body.TripId.Should().Be(tripId);
        body.Text.Should().Be("a good day");
        body.RecordedOn.Should().Be(RecordedOn);
        body.CreatedByUserId.Should().Be(current.UserId);
        await _factory.TripNoteRepository.Received(1).UpsertAsync(
            Arg.Is<TripNote>(note =>
                note.Id == noteId
                && note.TripId == tripId
                && note.Text == "a good day"
                && note.RecordedOn == RecordedOn
                && note.CreatedByUserId == current.UserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotDuplicateANoteWhenTheSameRequestIsReplayed()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var tripId = Guid.NewGuid();
        var noteId = Guid.NewGuid();
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, current!.UserId)));
        var request = new RecordTripNoteDto(noteId, "wind picked up", RecordedOn);

        // Act
        var first = await client.PostAsJsonAsync($"/api/trips/{tripId:D}/notes", request);
        _factory.TripNoteRepository.GetByIdAsync(noteId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripNote?>(new TripNote
            {
                Id = noteId,
                TripId = tripId,
                Text = "wind picked up",
                RecordedOn = RecordedOn
            }));
        var second = await client.PostAsJsonAsync($"/api/trips/{tripId:D}/notes", request);

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.TripNoteRepository.Received(2).UpsertAsync(
            Arg.Is<TripNote>(note => note.Id == noteId && note.TripId == tripId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDeleteANoteFromTheAnglersOwnTrip()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var tripId = Guid.NewGuid();
        var noteId = Guid.NewGuid();
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, current!.UserId)));
        _factory.TripNoteRepository.GetByIdAsync(noteId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripNote?>(new TripNote
            {
                Id = noteId,
                TripId = tripId,
                Text = "wind picked up",
                RecordedOn = RecordedOn
            }));

        // Act
        var response = await client.DeleteAsync($"/api/trips/{tripId:D}/notes/{noteId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.TripNoteRepository.Received(1).DeleteAsync(
            noteId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotDeleteANoteBelongingToAnotherTrip()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var tripId = Guid.NewGuid();
        var noteId = Guid.NewGuid();
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, current!.UserId)));
        _factory.TripNoteRepository.GetByIdAsync(noteId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripNote?>(new TripNote
            {
                Id = noteId,
                TripId = Guid.NewGuid(),
                Text = "someone else's day",
                RecordedOn = RecordedOn
            }));

        // Act
        var response = await client.DeleteAsync($"/api/trips/{tripId:D}/notes/{noteId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _factory.TripNoteRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAttributeTheNoteToTheAuthenticatedAnglerNotTheRequest()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var tripId = Guid.NewGuid();
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, current!.UserId)));
        var impostor = Guid.NewGuid();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{tripId:D}/notes",
            new
            {
                noteId = Guid.NewGuid(),
                text = "wind picked up",
                recordedOn = RecordedOn,
                createdByUserId = impostor
            });
        var body = await response.Content.ReadFromJsonAsync<TripNoteDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.CreatedByUserId.Should().Be(current.UserId);
        body.CreatedByUserId.Should().NotBe(impostor);
        await _factory.TripNoteRepository.Received(1).UpsertAsync(
            Arg.Is<TripNote>(note => note.CreatedByUserId == current.UserId),
            Arg.Any<CancellationToken>());
        await _factory.TripNoteRepository.DidNotReceive().UpsertAsync(
            Arg.Is<TripNote>(note => note.CreatedByUserId == impostor),
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

    private void Reset()
    {
        _factory.TripRepository.ClearReceivedCalls();
        _factory.TripNoteRepository.ClearReceivedCalls();
        _factory.TripNoteRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripNote?>(null));
        _factory.TripNoteRepository
            .UpsertAsync(Arg.Any<TripNote>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<TripNote>(0)));
        _factory.TripNoteRepository
            .DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
    }
}
