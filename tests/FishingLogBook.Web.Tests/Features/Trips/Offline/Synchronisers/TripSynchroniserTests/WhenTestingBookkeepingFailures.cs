using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.Synchronisers.TripSynchroniserTests;

public class WhenTestingBookkeepingFailures : BaseTripSynchroniserTest
{
    private void GivenTheServerIsUnavailable()
    {
        MockTripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>())
            .Returns<TripDto?>(_ => throw new HttpRequestException("The API is unavailable."));
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
        GivenTheServerIsUnavailable();
        var store = await CreateStoreAsync(CreateTrip());
        store.FailWrite = true;
        var sut = CreateSut(store);

        // Act
        var act = () => sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        await MockLogging.Received(1).LogErrorAsync(
            "recording a failed trip synchronisation",
            Arg.Any<Exception>(),
            CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldFallBackToTheErrorLogWhenDiagnosticsFail()
    {
        // Arrange
        GivenTheServerIsUnavailable();
        var failure = new InvalidOperationException("diagnostics store unavailable");
        GivenDiagnosticsFail(failure);
        var store = await CreateStoreAsync(CreateTrip());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        (await store.GetAsync(OwnerUserId, TripId, CancellationToken.None))!.SyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        await MockLogging.Received().LogErrorAsync("trip synchronisation diagnostic", failure, CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldNeverLetFailingDiagnosticsAndLoggingControlSynchronisation()
    {
        // Arrange
        GivenTheServerIsUnavailable();
        GivenDiagnosticsFail(new InvalidOperationException("diagnostics"));
        MockLogging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("logging")));
        var store = await CreateStoreAsync(CreateTrip());
        var sut = CreateSut(store);

        // Act
        var act = () => sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        (await store.GetAsync(OwnerUserId, TripId, CancellationToken.None))!.SyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
    }
}
