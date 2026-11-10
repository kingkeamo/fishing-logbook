using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Pages.DiagnosticsInspector;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Storage.Stores;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Diagnostics.Pages.DiagnosticsInspectorTests;

public class WhenTestingStatusLabels : BaseDiagnosticsInspectorTest
{
    [Fact]
    public async Task ItShouldShowOfflineWhenTheDeviceIsOffline()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var network = Substitute.For<INetworkService>();
        network.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);
        await using var context = CreateContext(CreateStore(), network: network);

        // Act
        var cut = context.Render<DiagnosticsInspector>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#diagnostics-online").TextContent.Should().Contain("Offline"));
        await network.Received(1).IsOnlineAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowUnknownWhenConnectivityCannotBeChecked()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var network = Substitute.For<INetworkService>();
        network.IsOnlineAsync(Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new InvalidOperationException("js unavailable"));
        var logging = SilentLogging();
        await using var context = CreateContext(CreateStore(), network: network, logging: logging);

        // Act
        var cut = context.Render<DiagnosticsInspector>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#diagnostics-online").TextContent.Should().Contain("Unknown"));
        await network.Received(1).IsOnlineAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowUnavailableForAStorageEstimateWithNoUsageOrQuota()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = CreateStore();
        store.GetStorageEstimateAsync(Arg.Any<CancellationToken>()).Returns(new StorageEstimate());
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<DiagnosticsInspector>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#diagnostics-storage-usage").TextContent.Should().Contain("Unavailable");
            cut.Find("#diagnostics-storage-quota").TextContent.Should().Contain("Unavailable");
        });
        await store.Received(1).GetStorageEstimateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheStoredUsageAndQuota()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = CreateStore();
        store.GetStorageEstimateAsync(Arg.Any<CancellationToken>()).Returns(new StorageEstimate { Usage = 1234, Quota = 98765 });
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<DiagnosticsInspector>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#diagnostics-storage-usage").TextContent.Should().Contain("1234");
            cut.Find("#diagnostics-storage-quota").TextContent.Should().Contain("98765");
        });
    }

    [Fact]
    public async Task ItShouldReportAMissingObjectStoreAsAFailedReadingStage()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = CreateStore();
        store.InspectExistingAsync(Arg.Any<CancellationToken>())
            .Returns(new DiagnosticDatabaseInspectionModel { Exists = true, HasStore = false });
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<DiagnosticsInspector>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#diagnostics-probe-production-stage").TextContent.Should()
            .Contain(DiagnosticsInspector.StageDatabaseOpened));
        cut.Find("#diagnostics-probe-production-error").TextContent.Should().Contain("object store was not found");
        await store.Received(1).InspectExistingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTreatAMissingInspectionAsAnUninitialisedDatabase()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = CreateStore();
        store.InspectExistingAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<DiagnosticDatabaseInspectionModel?>(null));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<DiagnosticsInspector>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#diagnostics-probe-production-stage").Should().NotBeNull());
        await store.Received(1).InspectExistingAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null, "storage failed")]
    [InlineData("", "storage failed")]
    [InlineData("QuotaExceededError", "QuotaExceededError: storage failed")]
    public async Task ItShouldShowTheLastLoggedErrorWithItsTypeWhenOneIsKnown(string? errorType, string expected)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var logging = SilentLogging();
        logging.GetLastErrorAsync(Arg.Any<CancellationToken>()).Returns(new LastErrorLog
        {
            ErrorType = errorType,
            Message = "storage failed",
            Source = "test"
        });
        await using var context = CreateContext(CreateStore(), logging: logging);

        // Act
        var cut = context.Render<DiagnosticsInspector>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#diagnostics-last-error").TextContent.Should().Contain(expected));
        await logging.Received().GetLastErrorAsync(Arg.Any<CancellationToken>());
    }
}
