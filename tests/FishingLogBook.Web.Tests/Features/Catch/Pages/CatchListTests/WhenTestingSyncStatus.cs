using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Pages.CatchList;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchListTests;

public class WhenTestingSyncStatus : BaseCatchListTest
{
    [Fact]
    public async Task ItShouldShowTheFailedStatusAndRetryTheExactCatch()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var catchRecord = CatchWithStatus(catchId, SyncStatus.FailedToSynchronise);
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<CatchModel>>([catchRecord]);
        var synchroniser = Substitute.For<ICatchSynchroniser>();
        await using var context = CreateContext(store, synchroniser: synchroniser);

        // Act
        var cut = context.Render<CatchList>();
        await cut.Find($"#catch-sync-retry-{catchId:D}").ClickAsync(new());

        // Assert
        cut.Find($"#catch-sync-status-{catchId:D}").TextContent
            .Should().Contain("Failed to synchronise");
        cut.Find($"#catch-sync-reassurance-{catchId:D}").TextContent
            .Should().Contain("Your catch is still saved on this device.");
        await synchroniser.Received(1).RetryAsync(
            catchId,
            Arg.Any<CancellationToken>());
        await store.Received(2).GetAllAsync(
            OwnerUserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheSynchronisedStatus()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<CatchModel>>(
                [CatchWithStatus(catchId, SyncStatus.Synchronised)]);
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.Find($"#catch-sync-status-{catchId:D}").TextContent
            .Should().Contain("Synchronised");
        cut.FindAll($"#catch-sync-retry-{catchId:D}").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotOfferManualSynchronisationWhileWaitingToSynchronise()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<CatchModel>>(
                [CatchWithStatus(catchId, SyncStatus.WaitingToSynchronise)]);
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.Find($"#catch-sync-status-{catchId:D}").TextContent
            .Should().Contain("Waiting to synchronise");
        cut.FindAll($"#catch-sync-retry-{catchId:D}").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotOfferManualSynchronisationWhileSynchronising()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<CatchModel>>(
                [CatchWithStatus(catchId, SyncStatus.Synchronising)]);
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.Find($"#catch-sync-status-{catchId:D}").TextContent
            .Should().Contain("Synchronising");
        cut.FindAll($"#catch-sync-retry-{catchId:D}").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldOfferManualSynchronisationForALocallySavedCatch()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var catchRecord = CatchWithStatus(catchId, SyncStatus.SavedLocally);
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<CatchModel>>([catchRecord]);
        var synchroniser = Substitute.For<ICatchSynchroniser>();
        await using var context = CreateContext(store, synchroniser: synchroniser);
        var cut = context.Render<CatchList>();

        // Act
        await cut.Find($"#catch-sync-retry-{catchId:D}").ClickAsync(new());

        // Assert
        cut.Find($"#catch-sync-retry-{catchId:D}").TextContent
            .Should().Contain("Synchronise now");
        await synchroniser.Received(1).RetryAsync(
            catchId,
            Arg.Any<CancellationToken>());
        await store.Received(2).GetAllAsync(
            OwnerUserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchSyncActions()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var catchId = Guid.NewGuid();
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<CatchModel>>(
                [CatchWithStatus(catchId, SyncStatus.FailedToSynchronise)]);
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.Find($"#catch-sync-retry-{catchId:D}").TextContent
            .Should().Contain("Réessayer");
        cut.Find($"#catch-sync-reassurance-{catchId:D}").TextContent
            .Should().Contain("Votre prise reste enregistrée sur cet appareil.");
    }

    [Fact]
    public async Task ItShouldRefreshWhenBackgroundSynchronisationChangesState()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(
                _ => [CatchWithStatus(catchId, SyncStatus.SavedLocally)],
                _ => [CatchWithStatus(catchId, SyncStatus.Synchronised)]);
        var synchroniser = Substitute.For<ICatchSynchroniser>();
        await using var context = CreateContext(store, synchroniser: synchroniser);
        var cut = context.Render<CatchList>();

        // Act
        synchroniser.StateChanged += Raise.Event<EventHandler>(
            synchroniser,
            EventArgs.Empty);

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-sync-status-{catchId:D}").TextContent
                .Should().Contain("Synchronised"));
        await store.Received(2).GetAllAsync(
            OwnerUserId,
            Arg.Any<CancellationToken>());
    }

    private static CatchModel CatchWithStatus(Guid catchId, SyncStatus status)
    {
        return new CatchModel(
            catchId,
            DateTimeOffset.Parse("2026-08-17T12:00:00Z"),
            [
                new CatchPhotographModel(
                    Guid.NewGuid(),
                    catchId,
                    "image/jpeg",
                    [1, 2, 3],
                    status)
            ],
            UserId: OwnerUserId,
            SyncStatus: status,
            MetadataSyncStatus: status);
    }
}
