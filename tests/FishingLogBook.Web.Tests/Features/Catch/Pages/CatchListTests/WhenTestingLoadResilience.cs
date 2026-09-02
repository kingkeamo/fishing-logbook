using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Offline.Synchronisers;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchList;
using FishingLogBook.Web.Localization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchListTests;

public class WhenTestingLoadResilience : BaseCatchListTest
{
    private static readonly Guid ServerCatchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task ItShouldRenderServerCatchesWhenTheLocalReadTimesOut()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("read timed out after 5000ms."));
        var catchClient = ServerCatchClient();
        await using var context = CreateContext(store, catchClient: catchClient);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-card-species-{ServerCatchId:D}").TextContent.Should().Contain("Perch"));
        cut.FindAll("#catch-list-load-failed").Should().BeEmpty();
        await catchClient.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRenderServerCatchesWhenTheLocalReadThrows()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("IndexedDB is unavailable."));
        var catchClient = ServerCatchClient();
        await using var context = CreateContext(store, catchClient: catchClient);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-card-species-{ServerCatchId:D}").TextContent.Should().Contain("Perch"));
        cut.FindAll("#catch-list-load-failed").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRenderServerCatchesBeforeASlowLocalReadCompletes()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var localRead = new TaskCompletionSource<IReadOnlyList<CatchModel>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(localRead.Task);
        await using var context = CreateContext(store, catchClient: ServerCatchClient());

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-card-species-{ServerCatchId:D}").TextContent.Should().Contain("Perch"));
        localRead.Task.IsCompleted.Should().BeFalse();

        localRead.SetResult([]);
        cut.WaitForAssertion(() => cut.FindAll("#catch-list-loading").Should().BeEmpty());
    }

    [Fact]
    public async Task ItShouldRenderLocalCatchesWhenTheServerFetchFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var localId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var local = StoredCatch(localId, DateTimeOffset.UtcNow, speciesName: "Local Pike");
        var store = LocalStore(local);
        var catchClient = Substitute.For<ICatchClient>();
        catchClient.GetAllAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("The API is unreachable."));
        await using var context = CreateContext(store, catchClient: catchClient);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-card-species-{localId:D}").TextContent.Should().Contain("Local Pike"));
        cut.FindAll("#catch-list-load-failed").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldKeepAnUnsynchronisedLocalCatchVisibleWhileOnline()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var localId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var local = StoredCatch(
            localId,
            DateTimeOffset.UtcNow,
            SyncStatus.WaitingToSynchronise,
            speciesName: "Unsent Pike");
        var store = LocalStore(local);
        await using var context = CreateContext(store, catchClient: ServerCatchClient());

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-card-species-{localId:D}").TextContent.Should().Contain("Unsent Pike"));
        cut.Find($"#catch-card-species-{ServerCatchId:D}").TextContent.Should().Contain("Perch");
    }

    [Fact]
    public async Task ItShouldNotReadLocalPhotographBytesWhenTheServerSuppliesTheirUrls()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var sharedId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var photographId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var local = new CatchModel(
            sharedId,
            DateTimeOffset.UtcNow,
            [new CatchPhotographModel(
                photographId,
                sharedId,
                PhotographContentTypeConstants.Jpeg,
                Bytes: null,
                SyncStatus.Synchronised)],
            SpeciesName: "Synced Pike",
            CaughtByUserId: OwnerUserId,
            SyncStatus: SyncStatus.Synchronised,
            MetadataSyncStatus: SyncStatus.Synchronised,
            RecordedByUserId: OwnerUserId);
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<CatchModel>)[local]);
        var catchClient = Substitute.For<ICatchClient>();
        catchClient.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<CatchViewDto>)
            [
                new CatchViewDto(sharedId, OwnerUserId, DateTimeOffset.UtcNow)
                {
                    SpeciesName = "Synced Pike",
                    CaughtByUserId = OwnerUserId,
                    RecordedByUserId = OwnerUserId,
                    Photographs =
                    [
                        new CatchPhotographViewDto(
                            photographId,
                            PhotographContentTypeConstants.Jpeg,
                            "https://storage.test/photo.jpg")
                    ]
                }
            ]);
        await using var context = CreateContext(store, catchClient: catchClient);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-card-species-{sharedId:D}").TextContent.Should().Contain("Synced Pike"));
        await store.Received(1).GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await store.DidNotReceive().GetAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await store.DidNotReceive().GetAllAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPreferNewerServerMetadataOverAFullySynchronisedLocalSnapshot()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var sharedId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var local = StoredCatch(sharedId, DateTimeOffset.UtcNow, speciesName: "Old local name") with
        {
            SyncStatus = SyncStatus.Synchronised,
            MetadataSyncStatus = SyncStatus.Synchronised,
            Photographs = StoredCatch(sharedId, DateTimeOffset.UtcNow).Photographs
                .Select(photograph => photograph with { SyncStatus = SyncStatus.Synchronised })
                .ToArray()
        };
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<CatchModel>)[local]);
        var catchClient = Substitute.For<ICatchClient>();
        catchClient.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<CatchViewDto>)
            [
                new CatchViewDto(sharedId, OwnerUserId, DateTimeOffset.UtcNow)
                {
                    SpeciesName = "Fresh server name",
                    CaughtByUserId = OwnerUserId,
                    RecordedByUserId = OwnerUserId
                }
            ]);
        await using var context = CreateContext(store, catchClient: catchClient);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-card-species-{sharedId:D}").TextContent.Should().Contain("Fresh server name"));
        await store.DidNotReceive().GetAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReadOnlyTheUnsynchronisedCatchPhotographBytes()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var syncedId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var pendingId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var syncedPhotographId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var synced = new CatchModel(
            syncedId,
            DateTimeOffset.UtcNow,
            [new CatchPhotographModel(
                syncedPhotographId,
                syncedId,
                PhotographContentTypeConstants.Jpeg,
                Bytes: null,
                SyncStatus.Synchronised)],
            SpeciesName: "Synced Pike",
            CaughtByUserId: OwnerUserId,
            SyncStatus: SyncStatus.Synchronised,
            MetadataSyncStatus: SyncStatus.Synchronised,
            RecordedByUserId: OwnerUserId);
        var pending = StoredCatch(
            pendingId,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            SyncStatus.WaitingToSynchronise,
            speciesName: "Unsent Perch");
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<CatchModel>)[synced, pending]);
        store.GetAsync(OwnerUserId, pendingId, Arg.Any<CancellationToken>()).Returns(pending);
        var catchClient = Substitute.For<ICatchClient>();
        catchClient.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<CatchViewDto>)
            [
                new CatchViewDto(syncedId, OwnerUserId, DateTimeOffset.UtcNow)
                {
                    SpeciesName = "Synced Pike",
                    CaughtByUserId = OwnerUserId,
                    RecordedByUserId = OwnerUserId,
                    Photographs =
                    [
                        new CatchPhotographViewDto(
                            syncedPhotographId,
                            PhotographContentTypeConstants.Jpeg,
                            "https://storage.test/photo.jpg")
                    ]
                }
            ]);
        await using var context = CreateContext(store, catchClient: catchClient);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-card-species-{pendingId:D}").TextContent.Should().Contain("Unsent Perch"));
        await store.Received(1).GetAsync(OwnerUserId, pendingId, Arg.Any<CancellationToken>());
        await store.DidNotReceive().GetAsync(OwnerUserId, syncedId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReloadWhenRetryIsPressedAfterBothSourcesFail()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var localId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var recovered = StoredCatch(localId, DateTimeOffset.UtcNow, speciesName: "Recovered Pike");
        var store = Substitute.For<ICatchStore>();
        var reads = 0;
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                reads += 1;
                return reads == 1
                    ? throw new TimeoutException("read timed out after 5000ms.")
                    : Task.FromResult<IReadOnlyList<CatchModel>>([recovered]);
            });
        store.GetAsync(OwnerUserId, localId, Arg.Any<CancellationToken>()).Returns(recovered);
        var catchClient = Substitute.For<ICatchClient>();
        catchClient.GetAllAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("The API is unreachable."));
        await using var context = CreateContext(store, catchClient: catchClient);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#catch-list-load-retry").Should().NotBeNull());

        // Act
        await cut.Find("#catch-list-load-retry").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-card-species-{localId:D}").TextContent.Should().Contain("Recovered Pike"));
        cut.FindAll("#catch-list-load-failed").Should().BeEmpty();
        await store.Received(2).GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotStartOverlappingLoadsWhenRetryIsPressedRepeatedly()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var gate = new TaskCompletionSource();
        var concurrent = 0;
        var peak = 0;
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                concurrent += 1;
                peak = Math.Max(peak, concurrent);
                await gate.Task;
                concurrent -= 1;
                return (IReadOnlyList<CatchModel>)[];
            });
        await using var context = CreateContext(store, catchClient: ServerCatchClient());

        // Act
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-card-species-{ServerCatchId:D}").Should().NotBeNull());
        gate.SetResult();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#catch-list-loading").Should().BeEmpty());
        peak.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldCoalesceRepeatedSynchronisationStateChangesIntoASingleReload()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var gate = new TaskCompletionSource();
        var concurrent = 0;
        var peak = 0;
        var reads = 0;
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                reads += 1;
                concurrent += 1;
                peak = Math.Max(peak, concurrent);
                if (reads == 1)
                {
                    await gate.Task;
                }

                concurrent -= 1;
                return (IReadOnlyList<CatchModel>)[];
            });
        var synchroniser = Substitute.For<ILogbookSynchroniser>();
        var catchClient = ServerCatchClient();
        await using var context = CreateContext(
            store,
            synchroniser: synchroniser,
            catchClient: catchClient);
        var cut = context.Render<CatchList>();

        // Act
        for (var raise = 0; raise < 5; raise++)
        {
            synchroniser.StateChanged += Raise.Event<EventHandler>(synchroniser, EventArgs.Empty);
        }

        gate.SetResult();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#catch-list-loading").Should().BeEmpty());
        peak.Should().Be(1);
        reads.Should().BeLessThan(6);
        await catchClient.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    private static ICatchClient ServerCatchClient()
    {
        var catchClient = Substitute.For<ICatchClient>();
        catchClient.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<CatchViewDto>)
            [
                new CatchViewDto(ServerCatchId, OwnerUserId, DateTimeOffset.UtcNow.AddDays(-1))
                {
                    SpeciesName = "Perch",
                    CaughtByUserId = OwnerUserId,
                    RecordedByUserId = OwnerUserId
                }
            ]);
        return catchClient;
    }

    private static ICatchStore LocalStore(params CatchModel[] catches)
    {
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<CatchModel>)[.. catches]);
        foreach (var catchRecord in catches)
        {
            store.GetAsync(OwnerUserId, catchRecord.Id, Arg.Any<CancellationToken>())
                .Returns(catchRecord);
        }

        return store;
    }
}
