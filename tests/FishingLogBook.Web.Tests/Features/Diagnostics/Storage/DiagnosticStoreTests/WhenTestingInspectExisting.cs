using AwesomeAssertions;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Storage;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Tests.Features.Diagnostics.Storage.DiagnosticStoreTests;

public class WhenTestingInspectExisting
{
    [Fact]
    public async Task ItShouldNotOpenTheQueueWhenTheDatabaseIsMissing()
    {
        // Arrange
        var js = new RecordingStoreJsRuntime
        {
            Inspection = new DiagnosticDatabaseInspectionModel
            {
                Exists = false,
                HasStore = false,
                Count = 0
            }
        };
        var sut = new IndexedDbDiagnosticEventStore(
            js,
            new DiagnosticsClientConfig { OperationTimeoutMilliseconds = 1000 });

        // Act
        var result = await sut.InspectExistingAsync(CancellationToken.None);

        // Assert
        result.Exists.Should().BeFalse();
        result.HasStore.Should().BeFalse();
        result.Count.Should().Be(0);
        js.ImportPaths.Should().Equal("./js/diagnostic-store.js");
        js.Invocations.Should().Equal("inspectExistingDiagnosticDatabase");
        js.Invocations.Should().NotContain("getDiagnosticQueueCount");
        js.Invocations.Should().NotContain("putDiagnosticEvent");
        js.Invocations.Should().NotContain("getPendingDiagnosticEvents");
        js.Invocations.Should().NotContain("openProbeDatabase");
    }

    [Fact]
    public void ItShouldAbortUpgradeOfAMissingDatabase()
    {
        // Arrange
        var script = ReadDiagnosticStoreScript();

        // Act
        // Assert
        script.Should().Contain("inspectExistingDiagnosticDatabase");
        script.Should().Contain("event.oldVersion === 0");
        script.Should().Contain("event.target.transaction.abort()");
        script.Should().Contain("function openExistingDatabase");
    }

    private static string ReadDiagnosticStoreScript()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "FishingLogBook.Web",
                "wwwroot",
                "js",
                "storage",
                "diagnostic-store.js");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find wwwroot file 'js/storage/diagnostic-store.js'.");
    }

    private sealed class RecordingStoreJsRuntime : IJSRuntime, IJSObjectReference
    {
        public List<string> ImportPaths { get; } = [];

        public List<string> Invocations { get; } = [];

        public DiagnosticDatabaseInspectionModel Inspection { get; init; } = new();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "import")
            {
                ImportPaths.Add(args?[0] as string ?? string.Empty);
                return ValueTask.FromResult((TValue)(object)this);
            }

            Invocations.Add(identifier);
            if (identifier == "inspectExistingDiagnosticDatabase")
            {
                return ValueTask.FromResult((TValue)(object)Inspection);
            }

            throw new InvalidOperationException($"Unexpected invocation '{identifier}'.");
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
