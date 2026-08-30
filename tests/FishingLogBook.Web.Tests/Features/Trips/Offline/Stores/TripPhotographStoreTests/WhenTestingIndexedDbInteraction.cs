using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using Microsoft.JSInterop;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripPhotographStoreTests;

public class WhenTestingIndexedDbInteraction
{
    private static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PhotographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset AddedOn = DateTimeOffset.Parse("2026-08-26T06:00:00Z");

    [Fact]
    public async Task ItShouldRejectAPhotographWithNoOwner()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        var act = () => sut.SaveAsync(
            NewPhotograph() with { ContributedByUserId = Guid.Empty },
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        js.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRejectAPhotographWithNoBytes()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        var act = () => sut.SaveAsync(NewPhotograph() with { Bytes = null }, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        js.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRejectAnEmptyOwnerForEveryOwnedOperation()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        var bytes = () => sut.GetBytesAsync(Guid.Empty, TripId, PhotographId, CancellationToken.None);
        var delete = () => sut.DeleteAsync(Guid.Empty, TripId, PhotographId, CancellationToken.None);
        var pending = () => sut.GetPendingAsync(Guid.Empty, CancellationToken.None);

        // Assert
        await bytes.Should().ThrowAsync<InvalidOperationException>();
        await delete.Should().ThrowAsync<InvalidOperationException>();
        await pending.Should().ThrowAsync<InvalidOperationException>();
        js.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldSendTheMetadataAndBytesSeparatelyOnSave()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        await sut.SaveAsync(NewPhotograph(), CancellationToken.None);

        // Assert
        js.Identifiers.Should().Equal("putTripPhotograph");
        js.Invocations[0].Arguments.Should().HaveCount(2);
        var json = js.Invocations[0].Arguments[0].Should().BeOfType<string>().Subject;
        json.Should().Contain($"\"{PhotographId:D}\"");
        json.Should().Contain($"\"{TripId:D}\"");
        json.Should().Contain("\"savedLocally\"");
        json.Should().NotContain("\"bytes\"");
        js.Invocations[0].Arguments[1].Should().BeOfType<byte[]>()
            .Which.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ItShouldReadBytesForOneOwnedPhotographOnly()
    {
        // Arrange
        var js = new RecordingJsRuntime { Bytes = [4, 5, 6] };
        var sut = CreateStore(js);

        // Act
        var bytes = await sut.GetBytesAsync(OwnerUserId, TripId, PhotographId, CancellationToken.None);

        // Assert
        bytes.Should().Equal(4, 5, 6);
        js.Identifiers.Should().Equal("getTripPhotographBytes");
        js.Invocations[0].Arguments.Should().Equal(
            OwnerUserId.ToString("D"),
            TripId.ToString("D"),
            PhotographId.ToString("D"));
    }

    [Fact]
    public async Task ItShouldNotReturnAPhotographOwnedByAnotherAngler()
    {
        // Arrange
        var js = new RecordingJsRuntime
        {
            PendingRecords = [StoredRecord(OtherUserId)]
        };
        var sut = CreateStore(js);

        // Act
        var pending = await sut.GetPendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        pending.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReadPendingPhotographsWithoutTheirBytes()
    {
        // Arrange
        var js = new RecordingJsRuntime { PendingRecords = [StoredRecord(OwnerUserId)] };
        var sut = CreateStore(js);

        // Act
        var pending = await sut.GetPendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        pending.Should().ContainSingle();
        pending[0].Id.Should().Be(PhotographId);
        pending[0].Bytes.Should().BeNull();
        js.Identifiers.Should().Equal("getPendingTripPhotographs");
        js.Identifiers.Should().NotContain("getTripPhotographBytes");
    }

    [Fact]
    public async Task ItShouldReadTheTripsHoldingPendingPhotographs()
    {
        // Arrange
        var js = new RecordingJsRuntime { PendingTripIds = [TripId] };
        var sut = CreateStore(js);

        // Act
        var tripIds = await sut.GetTripsWithPendingPhotographsAsync(OwnerUserId, CancellationToken.None);

        // Assert
        tripIds.Should().Equal(TripId);
        js.Identifiers.Should().Equal("getTripsWithPendingPhotographs");
        js.Invocations[0].Arguments.Should().Equal(OwnerUserId.ToString("D"));
    }

    [Fact]
    public async Task ItShouldReturnNothingForAnUnknownOwnerWhenResolvingDependencies()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        var tripIds = await sut.GetTripsWithPendingPhotographsAsync(Guid.Empty, CancellationToken.None);

        // Assert
        tripIds.Should().BeEmpty();
        js.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldDeleteAnOwnedPhotograph()
    {
        // Arrange
        var js = new RecordingJsRuntime { Deleted = true };
        var sut = CreateStore(js);

        // Act
        var deleted = await sut.DeleteAsync(OwnerUserId, TripId, PhotographId, CancellationToken.None);

        // Assert
        deleted.Should().BeTrue();
        js.Identifiers.Should().Equal("deleteTripPhotograph");
        js.Invocations[0].Arguments.Should().Equal(
            OwnerUserId.ToString("D"),
            TripId.ToString("D"),
            PhotographId.ToString("D"));
    }

    [Fact]
    public async Task ItShouldTimeOutRatherThanHangWhenTheModuleNeverLoads()
    {
        // Arrange
        var sut = new IndexedDbTripPhotographStore(
            new HangingJsRuntime(),
            Substitute.For<IDiagnosticLogger>(),
            Substitute.For<ILoggingService>(),
            new DiagnosticsClientConfig { OperationTimeoutMilliseconds = 250 });

        // Act
        var act = () => sut.GetPendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
    }

    private static IndexedDbTripPhotographStore CreateStore(IJSRuntime js)
    {
        return new IndexedDbTripPhotographStore(
            js,
            Substitute.For<IDiagnosticLogger>(),
            Substitute.For<ILoggingService>(),
            new DiagnosticsClientConfig { OperationTimeoutMilliseconds = 5000 });
    }

    private static TripPhotographModel NewPhotograph()
    {
        return new TripPhotographModel(
            PhotographId,
            TripId,
            OwnerUserId,
            PhotographContentTypeConstants.Jpeg,
            AddedOn,
            Bytes: [1, 2, 3]);
    }

    private static StoredTripRecord StoredRecord(Guid ownerUserId)
    {
        return new StoredTripRecord
        {
            Json = TripJson.SerializePhotograph(new TripPhotographModel(
                PhotographId,
                TripId,
                ownerUserId,
                PhotographContentTypeConstants.Jpeg,
                AddedOn))
        };
    }

    private sealed record JsInvocation(string Identifier, IReadOnlyList<object?> Arguments);

    private sealed class RecordingJsRuntime : IJSRuntime, IJSObjectReference
    {
        private readonly List<JsInvocation> _invocations = [];

        public byte[]? Bytes { get; set; }

        public bool Deleted { get; set; }

        public StoredTripRecord[] PendingRecords { get; set; } = [];

        public Guid[] PendingTripIds { get; set; } = [];

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
                "putTripPhotograph" => true,
                "getTripPhotographBytes" => Bytes,
                "deleteTripPhotograph" => Deleted,
                "getPendingTripPhotographs" => PendingRecords,
                "getTripsWithPendingPhotographs" => PendingTripIds,
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
