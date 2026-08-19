using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Diagnostics.Clients;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Pages.DiagnosticsInspector;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Storage;
using FishingLogBook.Web.Features.Diagnostics.Synchronisers;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.Features.Diagnostics.TestSupport;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Diagnostics.Pages.DiagnosticsInspectorTests;

public class WhenTestingSuccessfulSync : BaseDiagnosticsInspectorTest
{
    [Fact]
    public async Task ItShouldClearTheLocalQueue()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = new MemoryDiagnosticEventStore();
        store.Items.Add(new DiagnosticEventModel
        {
            Id = Guid.Parse("3a1f2c8e-7b44-4d1a-9c3e-2f8a6b0d1e55"),
            TimestampUtc = DateTimeOffset.Parse("2026-08-14T19:48:00Z"),
            Level = DiagnosticLevel.Information,
            EventName = DiagnosticEventNames.SyncCompleted,
            Message = "Catch synchronisation completed."
        });
        var client = Substitute.For<IDiagnosticClient>();
        var network = OnlineNetwork();
        var synchroniser = new DiagnosticSynchroniser(
            store,
            client,
            network,
            new DiagnosticStatusModel(),
            new DiagnosticsClientConfig { MaxBatchSize = 50, MaxUploadAttempts = 5 });
        await using var context = CreateContext(store, synchroniser, network);

        // Act
        var cut = context.Render<DiagnosticsInspector>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#diagnostic-queue-empty").Should().NotBeNull();
            cut.Find("#diagnostics-queued-count").TextContent.Should().Contain("0");
            cut.FindAll("#diagnostic-queue").Should().BeEmpty();
        });
        store.Items.Should().BeEmpty();
        await client.Received(1).UploadBatchAsync(
            Arg.Is<IReadOnlyList<FishingLogBook.Shared.Dtos.ClientDiagnosticEventDto>>(events =>
                events.Count == 1 && events[0].EventName == DiagnosticEventNames.SyncCompleted),
            Arg.Any<CancellationToken>());
    }
}
