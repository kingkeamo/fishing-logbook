using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Models;
using FishingLogBook.Web.Offline;
using FishingLogBook.Web.Pages.TestCatchLog;
using NSubstitute;

namespace FishingLogBook.Web.Tests.TestCatchLogTests;

public class WhenTestingReloadAfterSave : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldKeepTheSaveSuccessful()
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
            .Returns(_ =>
            {
                if (saved.Count == 0)
                {
                    return Task.FromResult<IReadOnlyList<TestCatch>>([]);
                }

                return Task.FromException<IReadOnlyList<TestCatch>>(new TimeoutException("reload timed out"));
            });
        await using var context = CreateContext(store);
        var cut = context.Render<TestCatchLog>();

        // Act
        cut.Find("#test-catch-species").Input("Pike");
        await cut.Find("#save-test-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            saved.Should().ContainSingle();
            cut.FindAll("#save-test-catch-spinner").Should().BeEmpty();
            cut.Find("#save-test-catch-button").TextContent.Should().Contain("Save catch");
            cut.Find("#test-catch-load-error").Should().NotBeNull();
            cut.Find("#test-catch-species").GetAttribute("value").Should().BeNullOrEmpty();
        });
        await store.Received(1).SaveAsync(
            Arg.Is<TestCatch>(testCatch =>
                testCatch.SpeciesName == "Pike" &&
                testCatch.SyncStatus == SyncStatus.SavedLocally),
            Arg.Any<CancellationToken>());
    }
}
