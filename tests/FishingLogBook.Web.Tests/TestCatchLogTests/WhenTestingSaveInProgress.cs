using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Models;
using FishingLogBook.Web.Offline;
using FishingLogBook.Web.Pages.TestCatchLog;
using NSubstitute;

namespace FishingLogBook.Web.Tests.TestCatchLogTests;

public class WhenTestingSaveInProgress : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldShowASpinnerAndSavingText()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var saveStarted = new TaskCompletionSource();
        var saveContinue = new TaskCompletionSource();
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TestCatch>>([]));
        store.SaveAsync(Arg.Any<TestCatch>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                saveStarted.TrySetResult();
                await saveContinue.Task;
            });
        await using var context = CreateContext(store);
        var cut = context.Render<TestCatchLog>();
        cut.Find("#test-catch-species").Input("Pike");

        // Act
        var click = cut.Find("#save-test-catch-button").ClickAsync();
        await saveStarted.Task;

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#save-test-catch-spinner").Should().NotBeNull();
            cut.Find("#save-test-catch-button").TextContent.Should().Contain("Saving catch");
            cut.Find("#save-test-catch-button").HasAttribute("disabled").Should().BeTrue();
        });
        saveContinue.SetResult();
        await click;
        await store.Received(1).SaveAsync(
            Arg.Is<TestCatch>(testCatch => testCatch.SpeciesName == "Pike"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchSavingText()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var saveStarted = new TaskCompletionSource();
        var saveContinue = new TaskCompletionSource();
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TestCatch>>([]));
        store.SaveAsync(Arg.Any<TestCatch>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                saveStarted.TrySetResult();
                await saveContinue.Task;
            });
        await using var context = CreateContext(store);
        var cut = context.Render<TestCatchLog>();
        cut.Find("#test-catch-species").Input("Brochet");

        // Act
        var click = cut.Find("#save-test-catch-button").ClickAsync();
        await saveStarted.Task;

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#save-test-catch-spinner").Should().NotBeNull();
            cut.Find("#save-test-catch-button").TextContent.Should().Contain("Enregistrement de la prise");
        });
        saveContinue.SetResult();
        await click;
        await store.Received(1).SaveAsync(
            Arg.Is<TestCatch>(testCatch => testCatch.SpeciesName == "Brochet"),
            Arg.Any<CancellationToken>());
    }
}
