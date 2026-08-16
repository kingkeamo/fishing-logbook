using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;
using FishingLogBook.Web.Features.TestCatch.Pages.TestCatchLog;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.TestCatch.Pages.TestCatchLogTests;

public class WhenTestingReloadAfterSave : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldKeepTheSaveSuccessful()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var saved = new List<TestCatchModel>();
        var store = Substitute.For<ITestCatchStore>();
        store.SaveAsync(Arg.Any<TestCatchModel>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                saved.Add(callInfo.Arg<TestCatchModel>());
                return Task.CompletedTask;
            });
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (saved.Count == 0)
                {
                    return Task.FromResult<IReadOnlyList<TestCatchModel>>([]);
                }

                return Task.FromException<IReadOnlyList<TestCatchModel>>(new TimeoutException("reload timed out"));
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
            Arg.Is<TestCatchModel>(testCatch =>
                testCatch.SpeciesName == "Pike" &&
                testCatch.SyncStatus == SyncStatus.SavedLocally),
            Arg.Any<CancellationToken>());
    }
}
