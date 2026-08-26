using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Diagnostics.Services;
using Microsoft.JSInterop;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.Stores.CatchStoreTests;

public class WhenTestingIndexedDbReads
{
    private static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PhotographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task ItShouldRejectAnEmptyOwnerForASingleCatchRead()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        var act = () => sut.GetAsync(Guid.Empty, CatchId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        js.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRejectAnEmptyOwnerForAMetadataRead()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        var act = () => sut.GetMetadataAsync(Guid.Empty, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        js.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReadASingleCatchWithoutReadingEveryCatch()
    {
        // Arrange
        var js = new RecordingJsRuntime
        {
            SingleRecord = StoredRecord(OwnerUserId, withBytes: true)
        };
        var sut = CreateStore(js);

        // Act
        var loaded = await sut.GetAsync(OwnerUserId, CatchId, CancellationToken.None);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(CatchId);
        loaded.Photographs.Should().HaveCount(1);
        loaded.Photographs[0].Bytes.Should().Equal([1, 2, 3]);
        js.Invocations.Should().ContainSingle();
        js.Invocations[0].Identifier.Should().Be("getCatchWithPhotographs");
        js.Invocations[0].Arguments.Should().Equal(
            OwnerUserId.ToString("D"),
            CatchId.ToString("D"));
        js.Identifiers.Should().NotContain("getAllCatchesWithPhotographs");
    }

    [Fact]
    public async Task ItShouldNotReturnASingleCatchOwnedByAnotherAngler()
    {
        // Arrange
        var js = new RecordingJsRuntime
        {
            SingleRecord = StoredRecord(OtherUserId, withBytes: true)
        };
        var sut = CreateStore(js);

        // Act
        var loaded = await sut.GetAsync(OwnerUserId, CatchId, CancellationToken.None);

        // Assert
        loaded.Should().BeNull();
        js.Identifiers.Should().Equal("getCatchWithPhotographs");
    }

    [Fact]
    public async Task ItShouldReturnNullWhenTheSingleCatchIsNotStored()
    {
        // Arrange
        var js = new RecordingJsRuntime { SingleRecord = null };
        var sut = CreateStore(js);

        // Act
        var loaded = await sut.GetAsync(OwnerUserId, CatchId, CancellationToken.None);

        // Assert
        loaded.Should().BeNull();
        js.Identifiers.Should().Equal("getCatchWithPhotographs");
    }

    [Fact]
    public async Task ItShouldReadMetadataWithoutPhotographBytesButKeepPhotographReferences()
    {
        // Arrange
        var js = new RecordingJsRuntime
        {
            ListRecords = [StoredRecord(OwnerUserId, withBytes: false)]
        };
        var sut = CreateStore(js);

        // Act
        var loaded = await sut.GetMetadataAsync(OwnerUserId, CancellationToken.None);

        // Assert
        loaded.Should().ContainSingle();
        loaded[0].Photographs.Should().ContainSingle();
        loaded[0].Photographs[0].Id.Should().Be(PhotographId);
        loaded[0].Photographs[0].ContentType.Should().Be(PhotographContentTypeConstants.Jpeg);
        loaded[0].Photographs[0].Bytes.Should().BeNull();
        js.Identifiers.Should().Equal("getCatchMetadata");
        js.Identifiers.Should().NotContain("getAllCatchesWithPhotographs");
    }

    [Fact]
    public async Task ItShouldNotReturnAnotherAnglersCatchFromAMetadataRead()
    {
        // Arrange
        var js = new RecordingJsRuntime
        {
            ListRecords = [StoredRecord(OtherUserId, withBytes: false)]
        };
        var sut = CreateStore(js);

        // Act
        var loaded = await sut.GetMetadataAsync(OwnerUserId, CancellationToken.None);

        // Assert
        loaded.Should().BeEmpty();
        js.Identifiers.Should().Equal("getCatchMetadata");
    }

    [Fact]
    public async Task ItShouldStillReadEveryCatchWithBytesForTheOfflineLogbook()
    {
        // Arrange
        var js = new RecordingJsRuntime
        {
            ListRecords = [StoredRecord(OwnerUserId, withBytes: true)]
        };
        var sut = CreateStore(js);

        // Act
        var loaded = await sut.GetAllAsync(OwnerUserId, CancellationToken.None);

        // Assert
        loaded.Should().ContainSingle();
        loaded[0].Photographs[0].Bytes.Should().Equal([1, 2, 3]);
        js.Identifiers.Should().Equal("getAllCatchesWithPhotographs");
    }

    private static IndexedDbCatchStore CreateStore(IJSRuntime js)
    {
        return new IndexedDbCatchStore(
            js,
            Substitute.For<IDiagnosticLogger>(),
            Substitute.For<ILoggingService>(),
            new DiagnosticsClientConfig { OperationTimeoutMilliseconds = 5000 });
    }

    private static StoredCatchRecord StoredRecord(Guid ownerUserId, bool withBytes)
    {
        var model = new CatchModel(
            CatchId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographModel(
                PhotographId,
                CatchId,
                PhotographContentTypeConstants.Jpeg,
                withBytes ? [1, 2, 3] : null)],
            UserId: ownerUserId);
        return new StoredCatchRecord
        {
            Json = CatchJson.SerializeMetadata(model),
            Photographs = withBytes
                ?
                [
                    new StoredCatchPhotographRecord
                    {
                        Id = PhotographId.ToString("D"),
                        CatchId = CatchId.ToString("D"),
                        ContentType = PhotographContentTypeConstants.Jpeg,
                        BytesBase64 = Convert.ToBase64String([1, 2, 3])
                    }
                ]
                : []
        };
    }

    private sealed record JsInvocation(string Identifier, IReadOnlyList<object?> Arguments);

    private sealed class RecordingJsRuntime : IJSRuntime, IJSObjectReference
    {
        private readonly List<JsInvocation> _invocations = [];

        public StoredCatchRecord? SingleRecord { get; set; }

        public StoredCatchRecord[] ListRecords { get; set; } = [];

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

            _invocations.Add(new JsInvocation(identifier, args ?? []));
            object? result = identifier switch
            {
                "getCatchWithPhotographs" => SingleRecord,
                "getCatchMetadata" => ListRecords,
                "getAllCatchesWithPhotographs" => ListRecords,
                _ => null
            };
            return new ValueTask<TValue>((TValue)result!);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
