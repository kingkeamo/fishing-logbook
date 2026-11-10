using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Diagnostics.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.Synchronisers.TripNoteSynchroniserTests;

public class WhenTestingBookkeepingFailures : BaseTripNoteSynchroniserTest
{
    private void GivenTheServerIsUnavailable()
    {
        MockTripClient.RecordNoteAsync(
                Arg.Any<Guid>(),
                Arg.Any<RecordTripNoteDto>(),
                Arg.Any<CancellationToken>())
            .Returns<TripNoteDto?>(_ => throw new HttpRequestException("The API is unavailable."));
    }

    [Fact]
    public async Task ItShouldLogWhenTheFailureCannotBeRecordedInTheStore()
    {
        // Arrange
        GivenTheServerIsUnavailable();
        var store = await CreateStoreAsync(CreateNote());
        store.FailWrite = true;
        var sut = CreateSut(store);

        // Act
        var act = () => sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        await MockLogging.Received(1).LogErrorAsync(
            "recording a failed trip note synchronisation",
            Arg.Any<Exception>(),
            CancellationToken.None);
        store.Stored(NoteId)!.SyncStatus.Should().Be(SyncStatus.SavedLocally);
    }

    [Fact]
    public async Task ItShouldFallBackToTheErrorLogWhenDiagnosticsFail()
    {
        // Arrange
        GivenTheServerIsUnavailable();
        var failure = new InvalidOperationException("diagnostics store unavailable");
        MockDiagnostics.LogAsync(
                Arg.Any<DiagnosticLevel>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(),
                Arg.Any<Exception?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(failure));
        var store = await CreateStoreAsync(CreateNote());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        store.Stored(NoteId)!.SyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        await MockLogging.Received(2).LogErrorAsync("trip note diagnostic", failure, CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldNeverLetFailingDiagnosticsAndLoggingControlSynchronisation()
    {
        // Arrange
        GivenTheServerIsUnavailable();
        MockDiagnostics.LogAsync(
                Arg.Any<DiagnosticLevel>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(),
                Arg.Any<Exception?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("diagnostics")));
        MockLogging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("logging")));
        var store = await CreateStoreAsync(CreateNote());
        var sut = CreateSut(store);

        // Act
        var act = () => sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        await MockDiagnostics.Received(1).LogAsync(
            DiagnosticLevel.Error,
            "trip.note.sync.failed",
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
        var diagnosticCalls = MockDiagnostics.ReceivedCalls()
            .Count(call => call.GetMethodInfo().Name == nameof(IDiagnosticLogger.LogAsync));
        await MockLogging.Received(diagnosticCalls).LogErrorAsync(
            Arg.Is<string>(message => message.EndsWith("diagnostic", StringComparison.Ordinal)),
            Arg.Is<Exception>(exception => exception.Message == "diagnostics"),
            Arg.Any<CancellationToken>());
        store.Stored(NoteId)!.SyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
    }
}
