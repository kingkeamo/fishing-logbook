using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using Microsoft.JSInterop;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripStoreTests;

public class WhenTestingIndexedDbInteraction
{
    private static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");

    [Fact]
    public async Task ItShouldRejectATripWithNoOwner()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        var act = () => sut.SaveAsync(NewTrip() with { OwnerUserId = Guid.Empty }, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        js.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRejectATripWithNoIdentifier()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        var act = () => sut.SaveAsync(NewTrip() with { Id = Guid.Empty }, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        js.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRejectAnEmptyOwnerForEveryRead()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        var all = () => sut.GetAllAsync(Guid.Empty, CancellationToken.None);
        var single = () => sut.GetAsync(Guid.Empty, TripId, CancellationToken.None);
        var active = () => sut.GetActiveAsync(Guid.Empty, CancellationToken.None);
        var pending = () => sut.GetPendingAsync(Guid.Empty, CancellationToken.None);
        var cleanup = () => sut.CleanupSyncedAsync(Guid.Empty, StartedOn, [], CancellationToken.None);

        // Assert
        await all.Should().ThrowAsync<InvalidOperationException>();
        await single.Should().ThrowAsync<InvalidOperationException>();
        await active.Should().ThrowAsync<InvalidOperationException>();
        await pending.Should().ThrowAsync<InvalidOperationException>();
        await cleanup.Should().ThrowAsync<InvalidOperationException>();
        js.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldSurfaceALocalActiveTripConflict()
    {
        // Arrange
        var js = new RecordingJsRuntime { WriteOutcome = "activeConflict" };
        var sut = CreateStore(js);

        // Act
        var act = () => sut.SaveAsync(NewTrip(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TripAlreadyActiveException>();
        js.Identifiers.Should().Equal("putTrip");
    }

    [Fact]
    public async Task ItShouldSaveATripAsSerialisedMetadata()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        await sut.SaveAsync(NewTrip() with { PlaceName = "Lough Corrib" }, CancellationToken.None);

        // Assert
        js.Identifiers.Should().Equal("putTrip");
        js.Invocations[0].Arguments.Should().ContainSingle();
        var json = js.Invocations[0].Arguments[0].Should().BeOfType<string>().Subject;
        json.Should().Contain("\"placeName\":\"Lough Corrib\"");
        json.Should().Contain("\"status\":\"Active\"");
        json.Should().Contain(OwnerUserId.ToString("D"));
    }

    [Fact]
    public async Task ItShouldReadASingleTripDirectlyWithoutListingEveryTrip()
    {
        // Arrange
        var js = new RecordingJsRuntime { SingleRecord = StoredRecord(OwnerUserId) };
        var sut = CreateStore(js);

        // Act
        var loaded = await sut.GetAsync(OwnerUserId, TripId, CancellationToken.None);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(TripId);
        js.Identifiers.Should().Equal("getTrip");
        js.Identifiers.Should().NotContain("getTrips");
        js.Invocations[0].Arguments.Should().Equal(OwnerUserId.ToString("D"), TripId.ToString("D"));
    }

    [Fact]
    public async Task ItShouldNotReturnASingleTripOwnedByAnotherAngler()
    {
        // Arrange
        var js = new RecordingJsRuntime { SingleRecord = StoredRecord(OtherUserId) };
        var sut = CreateStore(js);

        // Act
        var loaded = await sut.GetAsync(OwnerUserId, TripId, CancellationToken.None);

        // Assert
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldReturnNullWhenTheTripIsNotStored()
    {
        // Arrange
        var js = new RecordingJsRuntime { SingleRecord = null };
        var sut = CreateStore(js);

        // Act
        var loaded = await sut.GetAsync(OwnerUserId, TripId, CancellationToken.None);

        // Assert
        loaded.Should().BeNull();
        js.Identifiers.Should().Equal("getTrip");
    }

    [Fact]
    public async Task ItShouldReadTheActiveTripDirectly()
    {
        // Arrange
        var js = new RecordingJsRuntime { SingleRecord = StoredRecord(OwnerUserId) };
        var sut = CreateStore(js);

        // Act
        var loaded = await sut.GetActiveAsync(OwnerUserId, CancellationToken.None);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be(TripConstants.Active);
        js.Identifiers.Should().Equal("getActiveTrip");
        js.Identifiers.Should().NotContain("getTrips");
        js.Invocations[0].Arguments.Should().Equal(OwnerUserId.ToString("D"));
    }

    [Fact]
    public async Task ItShouldNotReturnAnotherAnglersActiveTrip()
    {
        // Arrange
        var js = new RecordingJsRuntime { SingleRecord = StoredRecord(OtherUserId) };
        var sut = CreateStore(js);

        // Act
        var loaded = await sut.GetActiveAsync(OwnerUserId, CancellationToken.None);

        // Assert
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldListOnlyTheOwnersTrips()
    {
        // Arrange
        var js = new RecordingJsRuntime
        {
            ListRecords = [StoredRecord(OwnerUserId), StoredRecord(OtherUserId, Guid.NewGuid())]
        };
        var sut = CreateStore(js);

        // Act
        var loaded = await sut.GetAllAsync(OwnerUserId, CancellationToken.None);

        // Assert
        loaded.Should().ContainSingle();
        loaded[0].OwnerUserId.Should().Be(OwnerUserId);
        js.Identifiers.Should().Equal("getTrips");
    }

    [Fact]
    public async Task ItShouldTimeOutRatherThanHangWhenTheModuleNeverLoads()
    {
        // Arrange
        var sut = new IndexedDbTripStore(
            new HangingJsRuntime(),
            Substitute.For<IDiagnosticLogger>(),
            Substitute.For<ILoggingService>(),
            new DiagnosticsClientConfig { OperationTimeoutMilliseconds = 250 });

        // Act
        var act = () => sut.GetActiveAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
    }


    [Fact]
    public async Task ItShouldNotReturnAnotherAnglersPendingTrip()
    {
        // Arrange
        var js = new RecordingJsRuntime
        {
            ListRecords = [StoredRecord(OwnerUserId), StoredRecord(OtherUserId, Guid.NewGuid())]
        };
        var sut = CreateStore(js);

        // Act
        var loaded = await sut.GetPendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        loaded.Should().ContainSingle();
        loaded[0].OwnerUserId.Should().Be(OwnerUserId);
    }

    [Fact]
    public async Task ItShouldReadPendingTripsDirectlyWithoutListingEveryTrip()
    {
        // Arrange
        var js = new RecordingJsRuntime { ListRecords = [StoredRecord(OwnerUserId)] };
        var sut = CreateStore(js);

        // Act
        var loaded = await sut.GetPendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        loaded.Should().ContainSingle();
        js.Identifiers.Should().Equal("getPendingTrips");
        js.Identifiers.Should().NotContain("getTrips");
        js.Invocations[0].Arguments.Should().Equal(OwnerUserId.ToString("D"));
    }

    [Fact]
    public async Task ItShouldPassTheRetentionCutoffAsUniversalTime()
    {
        // Arrange
        var js = new RecordingJsRuntime { RemovedCount = 2 };
        var sut = CreateStore(js);
        var cutoff = new DateTimeOffset(2026, 8, 26, 7, 32, 0, TimeSpan.FromHours(2));

        // Act
        var removed = await sut.CleanupSyncedAsync(OwnerUserId, cutoff, [], CancellationToken.None);

        // Assert
        removed.Should().Be(2);
        js.Identifiers.Should().Equal("cleanupSyncedTrips");
        js.Invocations[0].Arguments.Should().HaveCount(3);
        js.Invocations[0].Arguments[0].Should().Be(OwnerUserId.ToString("D"));
        js.Invocations[0].Arguments[1].Should().Be(cutoff.ToUniversalTime().ToString("O"));
        js.Invocations[0].Arguments[2].Should().BeAssignableTo<IEnumerable<string>>()
            .Which.Should().BeEmpty();
    }

    private static IndexedDbTripStore CreateStore(IJSRuntime js)
    {
        return new IndexedDbTripStore(
            js,
            Substitute.For<IDiagnosticLogger>(),
            Substitute.For<ILoggingService>(),
            new DiagnosticsClientConfig { OperationTimeoutMilliseconds = 5000 });
    }

    private static TripModel NewTrip()
    {
        return new TripModel(TripId, OwnerUserId, TripConstants.Active, StartedOn);
    }

    private static StoredTripRecord StoredRecord(Guid ownerUserId, Guid? tripId = null)
    {
        return new StoredTripRecord
        {
            Json = TripJson.Serialize(new TripModel(
                tripId ?? TripId,
                ownerUserId,
                TripConstants.Active,
                StartedOn))
        };
    }

    private sealed record JsInvocation(string Identifier, IReadOnlyList<object?> Arguments);

    private sealed class RecordingJsRuntime : IJSRuntime, IJSObjectReference
    {
        private readonly List<JsInvocation> _invocations = [];

        public string WriteOutcome { get; set; } = "saved";

        public StoredTripRecord? SingleRecord { get; set; }

        public StoredTripRecord[] ListRecords { get; set; } = [];

        public int RemovedCount { get; set; }

        public IReadOnlyList<JsInvocation> Invocations => _invocations;

        public IReadOnlyList<string> Identifiers =>
            [.. _invocations.Select(invocation => invocation.Identifier)];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            if (identifier == "import")
            {
                return new ValueTask<TValue>((TValue)(object)this);
            }

            _invocations.Add(new JsInvocation(identifier, Flatten(args)));
            object? result = identifier switch
            {
                "putTrip" => WriteOutcome,
                "getTrip" => SingleRecord,
                "getActiveTrip" => SingleRecord,
                "getTrips" => ListRecords,
                "getPendingTrips" => ListRecords,
                "cleanupSyncedTrips" => RemovedCount,
                _ => null
            };
            return new ValueTask<TValue>((TValue)result!);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        private static IReadOnlyList<object?> Flatten(object?[]? args)
        {
            if (args is null)
            {
                return [];
            }

            if (args.Length == 1 && args[0] is object?[] nested)
            {
                return nested;
            }

            return args;
        }
    }

    private sealed class HangingJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            return new ValueTask<TValue>(Hang<TValue>(cancellationToken));
        }

        private static async Task<TValue> Hang<TValue>(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return default!;
        }
    }
}
