using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Models;
using FishingLogBook.Web.Offline;
using FishingLogBook.Web.Pages.TestCatchLog;
using NSubstitute;

namespace FishingLogBook.Web.Tests.TestCatchLogTests;

public class WhenTestingBackgroundSyncFailure : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldKeepTheCatchPersisted()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var saved = new List<TestCatch>();
        var store = Substitute.For<ITestCatchStore>();
        store.SaveAsync(Arg.Any<TestCatch>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                saved.Add(callInfo.Arg<TestCatch>());
                return Task.CompletedTask;
            });
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<TestCatch>>(saved.ToArray()));
        var synchroniser = Substitute.For<ITestCatchSynchroniser>();
        synchroniser.SynchronisePendingAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask, Task.FromException(new HttpRequestException("sync failed")));
        await using var context = CreateContext(store, synchroniser);
        var cut = context.Render<TestCatchLog>();

        // Act
        cut.Find("#test-catch-species").Input("Pike");
        await cut.Find("#save-test-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            saved.Should().ContainSingle();
            saved[0].SpeciesName.Should().Be("Pike");
            saved[0].SyncStatus.Should().Be(SyncStatus.SavedLocally);
            cut.Find($"#test-catch-species-{saved[0].Id}").TextContent.Should().Contain("Pike");
            cut.FindAll("#save-test-catch-spinner").Should().BeEmpty();
        });
        await store.Received(1).SaveAsync(
            Arg.Is<TestCatch>(testCatch =>
                testCatch.SpeciesName == "Pike" &&
                testCatch.SyncStatus == SyncStatus.SavedLocally),
            Arg.Any<CancellationToken>());
    }
}
