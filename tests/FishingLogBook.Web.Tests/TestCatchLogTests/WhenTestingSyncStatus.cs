using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Models;
using FishingLogBook.Web.Offline;
using FishingLogBook.Web.Pages.TestCatchLog;
using NSubstitute;

namespace FishingLogBook.Web.Tests.TestCatchLogTests;

public class WhenTestingSyncStatus : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldShowRetry_WhenSynchronisationFailed()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var existing = new TestCatch(
            Guid.Parse("5a2c8e14-9d70-4b31-8f6a-1c3e7b90d246"),
            "Carp",
            DateTimeOffset.Parse("2026-08-14T17:00:00Z"),
            null,
            SyncStatus.FailedToSynchronise);
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TestCatch>>([existing]));
        var synchroniser = Substitute.For<ITestCatchSynchroniser>();
        await using var context = CreateContext(store, synchroniser);
        var cut = context.Render<TestCatchLog>();

        // Act
        cut.WaitForAssertion(() => cut.Find($"#retry-test-catch-{existing.Id}").Should().NotBeNull());
        await cut.Find($"#retry-test-catch-{existing.Id}").ClickAsync();

        // Assert
        cut.Find($"#test-catch-sync-status-{existing.Id}").TextContent.Should()
            .Contain("Failed to synchronise");
        await synchroniser.Received(1).RetryAsync(existing.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowSynchronisedStatus()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var existing = new TestCatch(
            Guid.Parse("0e7b3c91-5a24-4d86-b1f0-8c2d9e4a6b17"),
            "Tench",
            DateTimeOffset.Parse("2026-08-14T18:00:00Z"),
            null,
            SyncStatus.Synchronised);
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TestCatch>>([existing]));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TestCatchLog>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#test-catch-sync-status-{existing.Id}").TextContent.Should()
                .Contain("Synchronised");
        });
        cut.FindAll($"#retry-test-catch-{existing.Id}").Should().BeEmpty();
    }

    [Theory]
    [InlineData(SyncStatus.WaitingToSynchronise, "Waiting to synchronise")]
    [InlineData(SyncStatus.Synchronising, "Synchronising")]
    [InlineData(SyncStatus.SavedLocally, "Saved locally — not synchronised")]
    public async Task ItShouldShowTheSyncState(SyncStatus status, string expected)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var existing = new TestCatch(
            Guid.Parse("8c1d4a70-2e59-4b16-9f83-6a0c7d5e1b42"),
            "Rudd",
            DateTimeOffset.Parse("2026-08-14T19:00:00Z"),
            null,
            status);
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TestCatch>>([existing]));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TestCatchLog>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#test-catch-sync-status-{existing.Id}").TextContent.Should()
                .Contain(expected);
        });
        cut.FindAll($"#retry-test-catch-{existing.Id}").Should().BeEmpty();
    }
}
