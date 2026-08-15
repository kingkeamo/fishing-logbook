using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Models;
using FishingLogBook.Web.Offline;
using FishingLogBook.Web.Pages.TestCatchLog;
using NSubstitute;

namespace FishingLogBook.Web.Tests.TestCatchLogTests;

public class WhenTestingSave : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldStoreAndListCatchImmediately_WhenSaved()
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
        await using var context = CreateContext(store);
        var cut = context.Render<TestCatchLog>();

        // Act
        cut.Find("#test-catch-species").Input("Pike");
        await cut.Find("#save-test-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            saved.Should().ContainSingle();
            cut.Find($"#test-catch-species-{saved[0].Id}").TextContent.Should().Contain("Pike");
            cut.Find($"#test-catch-sync-status-{saved[0].Id}").TextContent.Should()
                .Contain("Saved locally — not synchronised");
            cut.FindAll("#save-test-catch-spinner").Should().BeEmpty();
            cut.Find("#save-test-catch-button").TextContent.Should().Contain("Save catch");
        });
        await store.Received(1).SaveAsync(
            Arg.Is<TestCatch>(testCatch =>
                testCatch.SpeciesName == "Pike" &&
                testCatch.SyncStatus == SyncStatus.SavedLocally),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotSave_WhenSpeciesIsMissing()
    {
        // Arrange
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TestCatch>>([]));
        await using var context = CreateContext(store);
        var cut = context.Render<TestCatchLog>();

        // Act
        await cut.Find("#save-test-catch-button").ClickAsync();

        // Assert
        await store.DidNotReceive().SaveAsync(Arg.Any<TestCatch>(), Arg.Any<CancellationToken>());
        cut.Find("#test-catch-empty").TextContent.Should().NotBeNullOrWhiteSpace();
    }
}
