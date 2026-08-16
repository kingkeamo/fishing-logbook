using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Models;
using FishingLogBook.Web.Offline;
using FishingLogBook.Web.Pages.TestCatchLog;
using NSubstitute;

namespace FishingLogBook.Web.Tests.TestCatchLogTests;

public class WhenTestingSynchroniseHang : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldResetSavingBeforeSyncCompletes()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var saved = new List<TestCatch>();
        var store = CreateSavingStore(saved);
        var synchroniser = Substitute.For<ITestCatchSynchroniser>();
        synchroniser.SynchronisePendingAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask, Hang());
        await using var context = CreateContext(store, synchroniser);
        var cut = context.Render<TestCatchLog>();
        cut.WaitForAssertion(() => cut.Find("#test-catch-species").Should().NotBeNull());

        // Act
        cut.Find("#test-catch-species").Input("Pike");
        await cut.Find("#save-test-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            saved.Should().ContainSingle();
            cut.FindAll("#save-test-catch-spinner").Should().BeEmpty();
            cut.Find("#save-test-catch-button").TextContent.Should().Contain("Save catch");
            cut.Find($"#test-catch-species-{saved[0].Id}").TextContent.Should().Contain("Pike");
        });
        await store.Received(1).SaveAsync(
            Arg.Is<TestCatch>(testCatch =>
                testCatch.SpeciesName == "Pike" &&
                testCatch.SyncStatus == SyncStatus.SavedLocally),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCompleteSave()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var saved = new List<TestCatch>();
        var store = CreateSavingStore(saved);
        var syncStarted = new TaskCompletionSource();
        var synchroniser = Substitute.For<ITestCatchSynchroniser>();
        var syncCalls = 0;
        synchroniser.SynchronisePendingAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                syncCalls++;
                if (syncCalls == 1)
                {
                    return Task.CompletedTask;
                }

                syncStarted.TrySetResult();
                return Hang();
            });
        await using var context = CreateContext(store, synchroniser);
        var cut = context.Render<TestCatchLog>();
        cut.WaitForAssertion(() => cut.Find("#test-catch-species").Should().NotBeNull());

        // Act
        cut.Find("#test-catch-species").Input("Bream");
        await cut.Find("#save-test-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            saved.Should().ContainSingle();
            cut.FindAll("#save-test-catch-spinner").Should().BeEmpty();
            cut.Find("#save-test-catch-button").HasAttribute("disabled").Should().BeTrue();
        });
        await syncStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await synchroniser.Received(2).SynchronisePendingAsync(Arg.Any<CancellationToken>());
        await store.Received(1).SaveAsync(
            Arg.Is<TestCatch>(testCatch => testCatch.SpeciesName == "Bream"),
            Arg.Any<CancellationToken>());
    }

    private static ITestCatchStore CreateSavingStore(List<TestCatch> saved)
    {
        var store = Substitute.For<ITestCatchStore>();
        store.SaveAsync(Arg.Any<TestCatch>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                saved.Add(callInfo.Arg<TestCatch>());
                return Task.CompletedTask;
            });
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<TestCatch>>(saved.ToArray()));
        return store;
    }
}
