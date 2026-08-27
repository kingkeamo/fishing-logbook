using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.Synchronisers.TripNoteSynchroniserTests;

public class WhenTestingSynchronisePending : BaseTripNoteSynchroniserTest
{
    [Fact]
    public async Task ItShouldDoNothingWhenTheOwnerIsUnknown()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateNote());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(Guid.Empty, CancellationToken.None);

        // Assert
        store.PendingCalls.Should().Be(0);
        await MockTripClient.DidNotReceive().RecordNoteAsync(
            Arg.Any<Guid>(),
            Arg.Any<RecordTripNoteDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotContactTheServerWhenNothingIsPending()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateNote(syncStatus: SyncStatus.Synchronised));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockNetworkService.DidNotReceive().IsOnlineAsync(Arg.Any<CancellationToken>());
        await MockTripClient.DidNotReceive().RecordNoteAsync(
            Arg.Any<Guid>(),
            Arg.Any<RecordTripNoteDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLeaveWorkPendingWhileOffline()
    {
        // Arrange
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);
        var store = await CreateStoreAsync(CreateNote());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripClient.DidNotReceive().RecordNoteAsync(
            Arg.Any<Guid>(),
            Arg.Any<RecordTripNoteDto>(),
            Arg.Any<CancellationToken>());
        store.Stored(NoteId)!.SyncStatus.Should().Be(SyncStatus.SavedLocally);
    }

    [Fact]
    public async Task ItShouldWaitWhenTheParentTripHasNotReachedTheServer()
    {
        // Arrange
        MockTripDependency.IsTripReadyForServerAsync(
                OwnerUserId,
                TripId,
                Arg.Any<CancellationToken>())
            .Returns(false);
        var store = await CreateStoreAsync(CreateNote());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripClient.DidNotReceive().RecordNoteAsync(
            Arg.Any<Guid>(),
            Arg.Any<RecordTripNoteDto>(),
            Arg.Any<CancellationToken>());
        store.Stored(NoteId)!.SyncStatus.Should().Be(SyncStatus.SavedLocally);
        await MockDiagnostics.Received(1).LogAsync(
            DiagnosticLevel.Information,
            DiagnosticEventNames.TripNoteSyncWaitingForTrip,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillSendNotesForAReadyTrip()
    {
        // Arrange
        MockTripDependency.IsTripReadyForServerAsync(
                OwnerUserId,
                TripId,
                Arg.Any<CancellationToken>())
            .Returns(false);
        var store = await CreateStoreAsync(
            CreateNote(),
            CreateNote(SecondNoteId, OtherTripId));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripClient.Received(1).RecordNoteAsync(
            OtherTripId,
            Arg.Is<RecordTripNoteDto>(request => request.NoteId == SecondNoteId),
            Arg.Any<CancellationToken>());
        store.Stored(SecondNoteId)!.SyncStatus.Should().Be(SyncStatus.Synchronised);
        store.Stored(NoteId)!.SyncStatus.Should().Be(SyncStatus.SavedLocally);
    }

    [Fact]
    public async Task ItShouldKeepTheNotePendingWhenTheServerRejectsIt()
    {
        // Arrange
        MockTripClient.RecordNoteAsync(
                Arg.Any<Guid>(),
                Arg.Any<RecordTripNoteDto>(),
                Arg.Any<CancellationToken>())
            .Returns<TripNoteDto?>(_ => throw new HttpRequestException("The API is unavailable."));
        var store = await CreateStoreAsync(CreateNote());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        store.Stored(NoteId)!.SyncStatus.Should().Be(SyncStatus.SavedLocally);
        store.Stored(NoteId)!.SyncedAt.Should().BeNull();
        await MockDiagnostics.Received(1).LogAsync(
            DiagnosticLevel.Error,
            DiagnosticEventNames.TripNoteSyncFailed,
            Arg.Any<string>(),
            Arg.Is<IReadOnlyDictionary<string, string>>(metadata =>
                metadata[DiagnosticMetadata.ErrorType] == nameof(HttpRequestException)),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNeverPutTheNoteTextIntoDiagnostics()
    {
        // Arrange
        const string secret = "met Sarah at the bailiff hut about the lease";
        MockTripClient.RecordNoteAsync(
                Arg.Any<Guid>(),
                Arg.Any<RecordTripNoteDto>(),
                Arg.Any<CancellationToken>())
            .Returns<TripNoteDto?>(_ => throw new HttpRequestException("The API is unavailable."));
        var store = await CreateStoreAsync(CreateNote(text: secret));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockDiagnostics.DidNotReceive().LogAsync(
            Arg.Any<DiagnosticLevel>(),
            Arg.Any<string>(),
            Arg.Is<string>(message => message.Contains(secret)),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
        await MockDiagnostics.DidNotReceive().LogAsync(
            Arg.Any<DiagnosticLevel>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<IReadOnlyDictionary<string, string>>(metadata =>
                metadata.Values.Any(value => value.Contains(secret))),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRetryAFailedNoteAndSendItExactlyOnce()
    {
        // Arrange
        var attempts = 0;
        MockTripClient.RecordNoteAsync(
                Arg.Any<Guid>(),
                Arg.Any<RecordTripNoteDto>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new HttpRequestException("The API is unavailable.");
                }

                var request = call.ArgAt<RecordTripNoteDto>(1);
                return new TripNoteDto(
                    request.NoteId,
                    call.ArgAt<Guid>(0),
                    request.Text,
                    request.RecordedOn);
            });
        var store = await CreateStoreAsync(CreateNote());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        attempts.Should().Be(2);
        store.Stored(NoteId)!.SyncStatus.Should().Be(SyncStatus.Synchronised);
    }

    [Fact]
    public async Task ItShouldNotResurrectANoteRemovedWhileItWasSyncing()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateNote());
        var sut = CreateSut(store);
        MockTripClient.RecordNoteAsync(
                Arg.Any<Guid>(),
                Arg.Any<RecordTripNoteDto>(),
                Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await store.DeleteAsync(OwnerUserId, TripId, NoteId, CancellationToken.None);
                var request = call.ArgAt<RecordTripNoteDto>(1);
                return new TripNoteDto(
                    request.NoteId,
                    call.ArgAt<Guid>(0),
                    request.Text,
                    request.RecordedOn);
            });

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        store.Count.Should().Be(0);
        store.Stored(NoteId).Should().BeNull();
    }

    [Fact]
    public async Task ItShouldNotSendTheSameNoteTwiceConcurrently()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateNote());
        var sut = CreateSut(store);
        var release = new TaskCompletionSource();
        MockTripClient.RecordNoteAsync(
                Arg.Any<Guid>(),
                Arg.Any<RecordTripNoteDto>(),
                Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await release.Task;
                var request = call.ArgAt<RecordTripNoteDto>(1);
                return new TripNoteDto(
                    request.NoteId,
                    call.ArgAt<Guid>(0),
                    request.Text,
                    request.RecordedOn);
            });

        // Act
        var first = sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);
        var second = sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);
        release.SetResult();
        await Task.WhenAll(first, second);

        // Assert
        await MockTripClient.Received(1).RecordNoteAsync(
            TripId,
            Arg.Is<RecordTripNoteDto>(request => request.NoteId == NoteId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSendEachNoteWithItsOwnRecordedInstant()
    {
        // Arrange
        var later = RecordedOn.AddHours(2);
        var store = await CreateStoreAsync(
            CreateNote(text: "fish rising near the reeds"),
            CreateNote(SecondNoteId, text: "wind picked up", recordedOn: later));
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripClient.Received(1).RecordNoteAsync(
            TripId,
            Arg.Is<RecordTripNoteDto>(request =>
                request.NoteId == NoteId
                && request.Text == "fish rising near the reeds"
                && request.RecordedOn == RecordedOn),
            Arg.Any<CancellationToken>());
        await MockTripClient.Received(1).RecordNoteAsync(
            TripId,
            Arg.Is<RecordTripNoteDto>(request =>
                request.NoteId == SecondNoteId
                && request.Text == "wind picked up"
                && request.RecordedOn == later),
            Arg.Any<CancellationToken>());
        store.All().Should().AllSatisfy(note =>
        {
            note.SyncStatus.Should().Be(SyncStatus.Synchronised);
            note.SyncedAt.Should().NotBeNull();
        });
    }
}
