using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Offline.Synchronisers;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchList;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchListTests;

public class WhenTestingSyncStatus : BaseCatchListTest
{
    [Fact]
    public async Task ItShouldBeQuietForAFullySynchronisedCatch()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<CatchModel>>(
                [StoredCatch(catchId, DateTimeOffset.Parse("2026-08-17T08:00:00Z"), SyncStatus.Synchronised)]);
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() => cut.Find($"#catch-card-{catchId:D}").Should().NotBeNull());
        cut.FindAll($"#catch-card-attention-{catchId:D}").Should().BeEmpty();
        cut.FindAll($"#catch-sync-retry-{catchId:D}").Should().BeEmpty();
    }

    [Theory]
    [InlineData(SyncStatus.SavedLocally)]
    [InlineData(SyncStatus.WaitingToSynchronise)]
    [InlineData(SyncStatus.FailedToSynchronise)]
    public async Task ItShouldShowAttentionAndRetryForNonHealthyStates(SyncStatus status)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<CatchModel>>(
                [StoredCatch(catchId, DateTimeOffset.Parse("2026-08-17T08:00:00Z"), status)]);
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-sync-retry-{catchId:D}").Should().NotBeNull());
    }

    [Fact]
    public async Task ItShouldShowAQuietSynchronisingIndicatorWithoutRetry()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<CatchModel>>(
                [StoredCatch(catchId, DateTimeOffset.Parse("2026-08-17T08:00:00Z"), SyncStatus.Synchronising)]);
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-card-synchronising-{catchId:D}").Should().NotBeNull());
        cut.FindAll($"#catch-sync-retry-{catchId:D}").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRetryTheExactCatchAndReloadTheList()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(
                _ => [StoredCatch(catchId, DateTimeOffset.Parse("2026-08-17T08:00:00Z"), SyncStatus.FailedToSynchronise)],
                _ => [StoredCatch(catchId, DateTimeOffset.Parse("2026-08-17T08:00:00Z"), SyncStatus.Synchronised)]);
        var synchroniser = Substitute.For<ILogbookSynchroniser>();
        var catchClient = EmptyCatchClient();
        await using var context = CreateContext(
            store,
            synchroniser: synchroniser,
            catchClient: catchClient);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find($"#catch-sync-retry-{catchId:D}").Should().NotBeNull());

        // Act
        await cut.Find($"#catch-sync-retry-{catchId:D}").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.FindAll($"#catch-sync-retry-{catchId:D}").Should().BeEmpty());
        await synchroniser.Received(1).RetryAsync(catchId, Arg.Any<CancellationToken>());
        await store.Received(2).GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefreshWhenBackgroundSynchronisationChangesState()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(
                _ => [StoredCatch(catchId, DateTimeOffset.Parse("2026-08-17T08:00:00Z"), SyncStatus.SavedLocally)],
                _ => [StoredCatch(catchId, DateTimeOffset.Parse("2026-08-17T08:00:00Z"), SyncStatus.Synchronised)]);
        var synchroniser = Substitute.For<ILogbookSynchroniser>();
        var catchClient = EmptyCatchClient();
        await using var context = CreateContext(
            store,
            synchroniser: synchroniser,
            catchClient: catchClient);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find($"#catch-sync-retry-{catchId:D}").Should().NotBeNull());

        // Act
        synchroniser.StateChanged += Raise.Event<EventHandler>(synchroniser, EventArgs.Empty);

        // Assert
        cut.WaitForAssertion(() =>
            cut.FindAll($"#catch-sync-retry-{catchId:D}").Should().BeEmpty());
        await store.Received(2).GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await catchClient.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchSyncReassurance()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var catchId = Guid.NewGuid();
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<CatchModel>>(
                [StoredCatch(catchId, DateTimeOffset.Parse("2026-08-17T08:00:00Z"), SyncStatus.FailedToSynchronise)]);
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-sync-reassurance-{catchId:D}").TextContent
                .Should().Contain("Votre prise reste enregistrée sur cet appareil."));
        cut.Find($"#catch-sync-retry-{catchId:D}").TextContent.Should().Contain("Réessayer");
    }
}
