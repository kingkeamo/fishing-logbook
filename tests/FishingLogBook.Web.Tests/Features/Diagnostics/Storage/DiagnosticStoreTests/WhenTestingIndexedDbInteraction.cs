using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Storage.Stores;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Tests.Features.Diagnostics.Storage.DiagnosticStoreTests;

public class WhenTestingIndexedDbInteraction
{
    private static readonly Guid EventId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherEventId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task ItShouldQueueTheSerialisedEventWithTheConfiguredQueueLimit()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js, maxQueueSize: 42);

        // Act
        await sut.EnqueueAsync(NewEvent(EventId), CancellationToken.None);

        // Assert
        js.Identifiers.Should().Equal("putDiagnosticEvent");
        var arguments = js.Invocations[0].Arguments;
        arguments.Should().HaveCount(2);
        var json = arguments[0].Should().BeOfType<string>().Subject;
        json.Should().Contain("\"eventName\":\"offline.read.completed\"");
        json.Should().Contain(EventId.ToString("D"));
        arguments[1].Should().Be(42);
    }

    [Fact]
    public async Task ItShouldSaveByQueueingTheEvent()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        await sut.SaveAsync(NewEvent(EventId), CancellationToken.None);

        // Assert
        js.Identifiers.Should().Equal("putDiagnosticEvent");
    }

    [Fact]
    public async Task ItShouldReturnNoPendingEventsWhenTheStoreReturnsNothing()
    {
        // Arrange
        var js = new RecordingJsRuntime { Pending = null };
        var sut = CreateStore(js);

        // Act
        var pending = await sut.GetPendingAsync(10, CancellationToken.None);

        // Assert
        pending.Should().BeEmpty();
        js.Identifiers.Should().Equal("getPendingDiagnosticEvents");
        js.Invocations[0].Arguments.Should().Equal(10);
    }

    [Fact]
    public async Task ItShouldDeserialiseEveryPendingEventAndSkipNullPayloads()
    {
        // Arrange
        var js = new RecordingJsRuntime
        {
            Pending =
            [
                JsonSerializer.Serialize(NewEvent(EventId), new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                "null",
                JsonSerializer.Serialize(NewEvent(OtherEventId), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ]
        };
        var sut = CreateStore(js);

        // Act
        var pending = await sut.GetPendingAsync(10, CancellationToken.None);

        // Assert
        pending.Select(item => item.Id).Should().Equal(EventId, OtherEventId);
        pending[0].EventName.Should().Be("offline.read.completed");
    }

    [Fact]
    public async Task ItShouldDeleteTheGivenEventIdentifiers()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        await sut.RemoveAsync([EventId, OtherEventId], CancellationToken.None);

        // Assert
        js.Identifiers.Should().Equal("deleteDiagnosticEvents");
        var json = js.Invocations[0].Arguments.Should().ContainSingle().Subject.Should().BeOfType<string>().Subject;
        JsonSerializer.Deserialize<string[]>(json).Should().Equal(EventId.ToString("D"), OtherEventId.ToString("D"));
    }

    [Fact]
    public async Task ItShouldReadTheQueueCount()
    {
        // Arrange
        var js = new RecordingJsRuntime { Count = 7 };
        var sut = CreateStore(js);

        // Act
        var count = await sut.GetCountAsync(CancellationToken.None);

        // Assert
        count.Should().Be(7);
        js.Identifiers.Should().Equal("getDiagnosticQueueCount");
    }

    [Fact]
    public async Task ItShouldReportAnEmptyInspectionWhenTheBrowserReturnsNothing()
    {
        // Arrange
        var js = new RecordingJsRuntime { Inspection = null };
        var sut = CreateStore(js);

        // Act
        var inspection = await sut.InspectExistingAsync(CancellationToken.None);

        // Assert
        inspection.Exists.Should().BeFalse();
        inspection.HasStore.Should().BeFalse();
        inspection.Count.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldReturnTheInspectionTheBrowserReports()
    {
        // Arrange
        var js = new RecordingJsRuntime
        {
            Inspection = new DiagnosticDatabaseInspectionModel { Exists = true, HasStore = true, Count = 3 }
        };
        var sut = CreateStore(js);

        // Act
        var inspection = await sut.InspectExistingAsync(CancellationToken.None);

        // Assert
        inspection.Count.Should().Be(3);
        inspection.Exists.Should().BeTrue();
        js.Identifiers.Should().Equal("inspectExistingDiagnosticDatabase");
    }

    [Fact]
    public async Task ItShouldReportAnUnknownStorageEstimateWhenTheBrowserReturnsNothing()
    {
        // Arrange
        var js = new RecordingJsRuntime { Estimate = null };
        var sut = CreateStore(js);

        // Act
        var estimate = await sut.GetStorageEstimateAsync(CancellationToken.None);

        // Assert
        estimate.Quota.Should().BeNull();
        estimate.Usage.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldReturnTheStorageEstimateTheBrowserReports()
    {
        // Arrange
        var js = new RecordingJsRuntime { Estimate = new StorageEstimate { Quota = 1000, Usage = 250 } };
        var sut = CreateStore(js);

        // Act
        var estimate = await sut.GetStorageEstimateAsync(CancellationToken.None);

        // Assert
        estimate.Quota.Should().Be(1000);
        estimate.Usage.Should().Be(250);
        js.Identifiers.Should().Equal("getStorageEstimate");
    }

    [Fact]
    public async Task ItShouldLoadTheModuleOnlyOnceAcrossOperations()
    {
        // Arrange
        var js = new RecordingJsRuntime();
        var sut = CreateStore(js);

        // Act
        await sut.GetCountAsync(CancellationToken.None);
        await sut.GetCountAsync(CancellationToken.None);

        // Assert
        js.ImportCount.Should().Be(1);
        js.Identifiers.Should().Equal("getDiagnosticQueueCount", "getDiagnosticQueueCount");
    }

    private static IndexedDbDiagnosticEventStore CreateStore(IJSRuntime js, int maxQueueSize = 500)
    {
        return new IndexedDbDiagnosticEventStore(
            js,
            new DiagnosticsClientConfig { MaxQueueSize = maxQueueSize, OperationTimeoutMilliseconds = 5000 });
    }

    private static DiagnosticEventModel NewEvent(Guid id)
    {
        return new DiagnosticEventModel
        {
            Id = id,
            TimestampUtc = DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            Level = DiagnosticLevel.Information,
            EventName = "offline.read.completed",
            Message = "Read completed."
        };
    }

    private sealed record JsInvocation(string Identifier, IReadOnlyList<object?> Arguments);

    private sealed class RecordingJsRuntime : IJSRuntime, IJSObjectReference
    {
        private readonly List<JsInvocation> _invocations = [];

        public string[]? Pending { get; set; } = [];

        public int Count { get; set; }

        public DiagnosticDatabaseInspectionModel? Inspection { get; set; }

        public StorageEstimate? Estimate { get; set; }

        public int ImportCount { get; private set; }

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
                ImportCount++;
                return new ValueTask<TValue>((TValue)(object)this);
            }

            _invocations.Add(new JsInvocation(identifier, args ?? []));
            object? result = identifier switch
            {
                "getPendingDiagnosticEvents" => Pending,
                "getDiagnosticQueueCount" => Count,
                "inspectExistingDiagnosticDatabase" => Inspection,
                "getStorageEstimate" => Estimate,
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
