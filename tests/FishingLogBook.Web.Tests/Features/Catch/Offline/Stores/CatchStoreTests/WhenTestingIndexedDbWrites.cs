using System.Collections;
using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Diagnostics.Services;
using Microsoft.JSInterop;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.Stores.CatchStoreTests;

public class WhenTestingIndexedDbWrites
{
    private static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PhotographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid TripId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly DateTimeOffset CaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z");

    [Fact]
    public async Task ItShouldRejectASaveWithNoOwner()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        var act = () => sut.SaveAsync(NewCatch() with { CaughtByUserId = Guid.Empty }, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        js.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRejectASaveWithNoPhotographs()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        var act = () => sut.SaveAsync(NewCatch() with { Photographs = [] }, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        js.Invocations.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ItShouldRejectASaveWhenAPhotographHasNoBytes(bool emptyArray)
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);
        var photograph = new CatchPhotographModel(
            PhotographId,
            CatchId,
            PhotographContentTypeConstants.Jpeg,
            emptyArray ? [] : null);

        // Act
        var act = () => sut.SaveAsync(NewCatch() with { Photographs = [photograph] }, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        js.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldWriteTheMetadataAndEveryPhotographTogetherOnSave()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        await sut.SaveAsync(NewCatch(), CancellationToken.None);

        // Assert
        js.Identifiers.Should().Equal("putCatchWithPhotographs");
        var arguments = js.Invocations[0].Arguments;
        arguments.Should().HaveCount(2);
        var json = arguments[0].Should().BeOfType<string>().Subject;
        json.Should().Contain($"\"{CatchId:D}\"");
        json.Should().NotContain("\"bytes\"");
        var photographs = arguments[1].Should().BeAssignableTo<IEnumerable>().Subject.Cast<object>().ToArray();
        var written = photographs.Should().ContainSingle().Subject.Should().BeOfType<StoredCatchPhotographWrite>().Subject;
        written.Id.Should().Be(PhotographId.ToString("D"));
        written.CatchId.Should().Be(CatchId.ToString("D"));
        written.ContentType.Should().Be(PhotographContentTypeConstants.Jpeg);
        written.Bytes.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ItShouldRejectASyncStateUpdateWithNoOwner()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        var act = () => sut.UpdateSyncStateAsync(NewCatch() with { CaughtByUserId = Guid.Empty }, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        js.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldUpdateOnlyTheMetadataWhenTheSyncStateChanges()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        await sut.UpdateSyncStateAsync(NewCatch(), CancellationToken.None);

        // Assert
        js.Identifiers.Should().Equal("updateCatchMetadata");
        js.Invocations[0].Arguments.Should().ContainSingle().Which.Should().BeOfType<string>()
            .Which.Should().Contain($"\"{CatchId:D}\"");
    }

    [Fact]
    public async Task ItShouldRejectAMetadataReconciliationWithNoOwner()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        var act = () => sut.ReconcileMetadataAsync(NewCatch() with { CaughtByUserId = Guid.Empty }, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        js.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReconcileTheServerMetadataWithoutTouchingPhotographs()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        await sut.ReconcileMetadataAsync(NewCatch(), CancellationToken.None);

        // Assert
        js.Identifiers.Should().Equal("reconcileCatchMetadata");
        js.Invocations[0].Arguments.Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldRejectATripAssociationWithNoOwner()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        var act = () => sut.UpdateTripAsync(Guid.Empty, CatchId, TripId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        js.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRecordTheTripAssociationAsASavedLocallyChange()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        await sut.UpdateTripAsync(OwnerUserId, CatchId, TripId, CancellationToken.None);

        // Assert
        js.Identifiers.Should().Equal("updateCatchTrip");
        var json = js.Invocations[0].Arguments.Should().ContainSingle().Which.Should().BeOfType<string>().Subject;
        json.Should().Contain($"\"{CatchId:D}\"");
        json.Should().Contain($"\"{TripId:D}\"");
        json.Should().Contain($"\"{OwnerUserId:D}\"");
        json.Should().Contain("savedLocally");
    }

    [Fact]
    public async Task ItShouldRecordRemovingACatchFromATrip()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        await sut.UpdateTripAsync(OwnerUserId, CatchId, null, CancellationToken.None);

        // Assert
        js.Identifiers.Should().Equal("updateCatchTrip");
        var json = js.Invocations[0].Arguments.Should().ContainSingle().Which.Should().BeOfType<string>().Subject;
        json.Should().NotContain($"{TripId:D}");
    }

    [Fact]
    public async Task ItShouldNotReadPhotographBytesWithoutAnOwner()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        var bytes = await sut.GetPhotographBytesAsync(Guid.Empty, CatchId, PhotographId, CancellationToken.None);

        // Assert
        bytes.Should().BeNull();
        js.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReadPhotographBytesForTheOwnersCatchOnly()
    {
        // Arrange
        var js = new RecordingJsRuntime { PhotographBytes = [4, 5, 6] };
        var sut = CreateStore(js);

        // Act
        var bytes = await sut.GetPhotographBytesAsync(OwnerUserId, CatchId, PhotographId, CancellationToken.None);

        // Assert
        bytes.Should().Equal(4, 5, 6);
        js.Identifiers.Should().Equal("getCatchPhotographBytes");
        js.Invocations[0].Arguments.Should().Equal(
            OwnerUserId.ToString("D"),
            CatchId.ToString("D"),
            PhotographId.ToString("D"));
    }

    [Fact]
    public async Task ItShouldNotRevealAMetadataRecordThatNeitherCaughtNorRecordedByTheViewer()
    {
        // Arrange
        var js = new RecordingJsRuntime
        {
            MetadataRecord = new StoredCatchRecord
            {
                Json = CatchJson.SerializeMetadata(NewCatch() with
                {
                    CaughtByUserId = OtherUserId,
                    RecordedByUserId = OtherUserId
                })
            }
        };
        var sut = CreateStore(js);

        // Act
        var result = await sut.GetMetadataAsync(OwnerUserId, CatchId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        js.Identifiers.Should().Equal("getCatchMetadataById");
    }

    [Fact]
    public async Task ItShouldReturnNothingWhenTheMetadataRecordDoesNotExist()
    {
        // Arrange
        var js = new RecordingJsRuntime { MetadataRecord = null };
        var sut = CreateStore(js);

        // Act
        var result = await sut.GetMetadataAsync(OwnerUserId, CatchId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldRejectAnEmptyOwnerForASingleMetadataRead()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        var act = () => sut.GetMetadataAsync(Guid.Empty, CatchId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        js.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReturnAMetadataRecordRecordedByTheViewerOnSomeoneElsesBehalf()
    {
        // Arrange
        var js = new RecordingJsRuntime
        {
            MetadataRecord = new StoredCatchRecord
            {
                Json = CatchJson.SerializeMetadata(NewCatch() with
                {
                    CaughtByUserId = OtherUserId,
                    RecordedByUserId = OwnerUserId
                })
            }
        };
        var sut = CreateStore(js);

        // Act
        var result = await sut.GetMetadataAsync(OwnerUserId, CatchId, CancellationToken.None);

        // Assert
        result!.Id.Should().Be(CatchId);
        js.Invocations[0].Arguments.Should().Equal(OwnerUserId.ToString("D"), CatchId.ToString("D"));
    }

    private static IndexedDbCatchStore CreateStore(IJSRuntime js)
    {
        return new IndexedDbCatchStore(
            js,
            Substitute.For<IDiagnosticLogger>(),
            Substitute.For<ILoggingService>(),
            new DiagnosticsClientConfig { OperationTimeoutMilliseconds = 5000 });
    }

    private static CatchModel NewCatch()
    {
        return new CatchModel(
            CatchId,
            CaughtOn,
            [new CatchPhotographModel(PhotographId, CatchId, PhotographContentTypeConstants.Jpeg, [1, 2, 3])],
            CaughtByUserId: OwnerUserId,
            RecordedByUserId: OwnerUserId);
    }

    private sealed record JsInvocation(string Identifier, IReadOnlyList<object?> Arguments);

    private sealed class RecordingJsRuntime : IJSRuntime, IJSObjectReference
    {
        private readonly List<JsInvocation> _invocations = [];

        public byte[]? PhotographBytes { get; set; }

        public StoredCatchRecord? MetadataRecord { get; set; }

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
                "getCatchPhotographBytes" => PhotographBytes,
                "getCatchMetadataById" => MetadataRecord,
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
