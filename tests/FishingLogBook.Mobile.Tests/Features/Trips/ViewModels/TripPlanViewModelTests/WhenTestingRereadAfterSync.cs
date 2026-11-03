using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.ViewModels;
using FishingLogBook.Mobile.Tests.Support;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripPlanViewModelTests;

public sealed class WhenTestingRereadAfterSync : BaseTripPlanViewModelTest
{
    public WhenTestingRereadAfterSync()
        : base(TimeZoneInfo.CreateCustomTimeZone("Plus2", TimeSpan.FromHours(2), "Plus2", "Plus2"))
    {
    }

    [Fact]
    public async Task ItShouldIgnoreARereadThatCompletesAfterDispose()
    {
        // Arrange
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingRead = new TaskCompletionSource<LocalTripRecordModel?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = await SavePendingAsync(Sut);
        var confirmed = pending with { SyncedRevision = 2 };
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(
            _ =>
            {
                started.TrySetResult();
                return pendingRead.Task;
            });
        var reread = Sut.RereadAfterSyncAsync(CancellationToken.None);
        await started.Task;

        // Act
        Sut.Dispose();
        pendingRead.SetResult(confirmed);
        await reread;

        // Assert
        Sut.HasPendingSync.Should().BeTrue();
        Sut.Trip!.SyncedRevision.Should().Be(1);
        await MockTrips.Received(2).GetAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotLetAnOlderRereadOverwriteANewerTrip()
    {
        // Arrange
        var pending = await SavePendingAsync(Sut);
        var stale = pending with
        {
            Detail = pending.Detail with
            {
                Trip = pending.Detail.Trip with { Title = "Stale" }
            }
        };
        var confirmed = pending with
        {
            SyncedRevision = 2,
            Detail = pending.Detail with
            {
                Trip = pending.Detail.Trip with { Title = "Fresh" }
            }
        };
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new TaskCompletionSource<LocalTripRecordModel?>(TaskCreationOptions.RunContinuationsAsynchronously);
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(
            _ =>
            {
                firstStarted.TrySetResult();
                return first.Task;
            },
            _ => Task.FromResult<LocalTripRecordModel?>(confirmed));

        // Act
        var older = Sut.RereadAfterSyncAsync(CancellationToken.None);
        await firstStarted.Task;
        var newer = Sut.RereadAfterSyncAsync(CancellationToken.None);
        await newer;
        first.SetResult(stale);
        await older;

        // Assert
        Sut.HasPendingSync.Should().BeFalse();
        Sut.EditTitle.Should().Be("Fresh");
        Sut.Trip!.Detail.Trip.Title.Should().Be("Fresh");
        await MockTrips.Received(3).GetAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepPendingSyncWhenTheRereadIsStillLocal()
    {
        // Arrange
        var pending = await SavePendingAsync(Sut);
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(pending);

        // Act
        await Sut.RereadAfterSyncAsync(CancellationToken.None);

        // Assert
        Sut.HasPendingSync.Should().BeTrue();
        Sut.EditTitle.Should().Be("Corrib");
        await MockTrips.Received(2).GetAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepPendingSyncWhenTheRereadFails()
    {
        // Arrange
        var logger = new CapturingLogger<TripPlanViewModel>();
        Sut.Dispose();
        var sut = CreateSut(logger: logger);
        await SavePendingAsync(sut);
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("store"));

        // Act
        await sut.RereadAfterSyncAsync(CancellationToken.None);

        // Assert
        sut.HasPendingSync.Should().BeTrue();
        sut.EditTitle.Should().Be("Corrib");
        logger.Entries.Should().Contain(entry => entry.Exception is InvalidOperationException);
        await MockTrips.Received(2).GetAsync(TripId, Arg.Any<CancellationToken>());
        sut.Dispose();
    }

    [Fact]
    public async Task ItShouldApplySynchronisationResultsOnTheUiThread()
    {
        // Arrange
        var ui = new QueueSynchronizationContext();
        Sut.Dispose();
        var sut = CreateSut(ui);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingRead = new TaskCompletionSource<LocalTripRecordModel?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = await SavePendingAsync(sut);
        var confirmed = pending with { SyncedRevision = 2 };
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(
            _ =>
            {
                started.TrySetResult();
                return pendingRead.Task;
            });
        var appliedOnUiContext = false;
        var notifiedBeforeDrain = false;
        sut.PropertyChanged += (_, _) =>
        {
            notifiedBeforeDrain = !ui.IsDraining;
            appliedOnUiContext = ReferenceEquals(SynchronizationContext.Current, ui);
        };

        // Act
        await RaiseSyncOnBackgroundAsync();
        await started.Task;
        pendingRead.SetResult(confirmed);
        await ui.Posted;
        var beforeDrain = sut.HasPendingSync;
        ui.Drain();

        // Assert
        beforeDrain.Should().BeTrue();
        notifiedBeforeDrain.Should().BeFalse();
        sut.HasPendingSync.Should().BeFalse();
        appliedOnUiContext.Should().BeTrue();
        ui.PostCount.Should().BeGreaterThan(0);
        await MockTrips.Received(2).GetAsync(TripId, Arg.Any<CancellationToken>());
        sut.Dispose();
    }

    [Fact]
    public async Task ItShouldClearPendingSyncWhenTheAuthoritativeRereadIsConfirmed()
    {
        // Arrange
        var pending = await SavePendingAsync(Sut);
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(pending with { SyncedRevision = 2 });

        // Act
        await Sut.RereadAfterSyncAsync(CancellationToken.None);

        // Assert
        Sut.HasPendingSync.Should().BeFalse();
        await MockTrips.Received(2).GetAsync(TripId, Arg.Any<CancellationToken>());
    }

    private async Task<LocalTripRecordModel> SavePendingAsync(TripPlanViewModel sut)
    {
        var plan = Plan();
        var pending = Plan(revision: 2, syncedRevision: 1);
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(plan);
        await sut.LoadAsync(TripId, CancellationToken.None);
        MockPlanned.UpdatePlanAsync(plan, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(pending);
        (await sut.SaveAsync(CancellationToken.None)).Should().BeTrue();
        sut.HasPendingSync.Should().BeTrue();
        return pending;
    }

    private Task RaiseSyncOnBackgroundAsync()
    {
        return Task.Run(() => MockNotifier.SyncRunCompleted += Raise.Event());
    }
}
