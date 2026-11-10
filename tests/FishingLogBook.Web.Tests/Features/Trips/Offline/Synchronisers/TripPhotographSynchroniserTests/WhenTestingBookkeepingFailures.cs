using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Diagnostics.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.Synchronisers.TripPhotographSynchroniserTests;

public class WhenTestingBookkeepingFailures : BaseTripPhotographSynchroniserTest
{
    private void GivenTheUploadUrlRequestFails()
    {
        MockTripClient.CreatePhotographUploadAsync(
                Arg.Any<Guid>(),
                Arg.Any<PhotographUploadRequestDto>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<PhotographUploadDto>>(_ => throw new HttpRequestException("The API is unavailable."));
    }

    private void GivenDiagnosticsFail(Exception failure)
    {
        MockDiagnostics.LogAsync(
                Arg.Any<DiagnosticLevel>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(),
                Arg.Any<Exception?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(failure));
    }

    [Fact]
    public async Task ItShouldLogWhenTheFailureCannotBeRecordedInTheStore()
    {
        // Arrange
        GivenTheUploadUrlRequestFails();
        var store = await CreateStoreAsync(CreatePhotograph());
        store.FailWrite = true;
        var sut = CreateSut(store);

        // Act
        var act = () => sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        await MockLogging.Received(1).LogErrorAsync(
            "recording a failed trip photograph synchronisation",
            Arg.Any<Exception>(),
            CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldFallBackToTheErrorLogWhenDiagnosticsFail()
    {
        // Arrange
        GivenTheUploadUrlRequestFails();
        var failure = new InvalidOperationException("diagnostics store unavailable");
        GivenDiagnosticsFail(failure);
        var store = await CreateStoreAsync(CreatePhotograph());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        store.Stored(PhotographId)!.SyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        await MockLogging.Received().LogErrorAsync("trip photograph diagnostic", failure, CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldNeverLetFailingDiagnosticsAndLoggingControlSynchronisation()
    {
        // Arrange
        GivenTheUploadUrlRequestFails();
        GivenDiagnosticsFail(new InvalidOperationException("diagnostics"));
        MockLogging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("logging")));
        var store = await CreateStoreAsync(CreatePhotograph());
        var sut = CreateSut(store);

        // Act
        var act = () => sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        await MockDiagnostics.Received(1).LogAsync(
            DiagnosticLevel.Error,
            "trip.photo.sync.failed",
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
        store.Stored(PhotographId)!.SyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
    }
}
