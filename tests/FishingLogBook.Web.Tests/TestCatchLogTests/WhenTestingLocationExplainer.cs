using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Offline;
using FishingLogBook.Web.Pages.TestCatchLog;
using FishingLogBook.Web.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.TestCatchLogTests;

public class WhenTestingLocationExplainer : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldExplainLocationBeforeRequestingPermission()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TestCatch>>([]));
        var location = Substitute.For<ILocationService>();
        location.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new LocationPromptStatus(true, false, false));
        await using var context = CreateContext(
            store,
            Substitute.For<ITestCatchSynchroniser>(),
            Substitute.For<ITestCatchPhotoStore>(),
            location: location);

        // Act
        var cut = context.Render<TestCatchLog>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#test-catch-location-explainer").TextContent.Should()
                .Contain("Keep a record of where you caught this fish");
            cut.Find("#test-catch-location-allow").TextContent.Should().Contain("Allow location");
            cut.Find("#test-catch-location-not-now").TextContent.Should().Contain("Not now");
            cut.Find("#test-catch-location-not-now").ClassList.Should().Contain(c => c.Contains("outlined"));
        });
        await location.DidNotReceive().TryCaptureAsync(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopShowingExplainer_WhenNotNowIsChosen()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TestCatch>>([]));
        var location = Substitute.For<ILocationService>();
        location.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(
                new LocationPromptStatus(true, false, false),
                new LocationPromptStatus(false, true, false));
        await using var context = CreateContext(
            store,
            Substitute.For<ITestCatchSynchroniser>(),
            Substitute.For<ITestCatchPhotoStore>(),
            location: location);
        var cut = context.Render<TestCatchLog>();

        // Act
        await cut.Find("#test-catch-location-not-now").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.FindAll("#test-catch-location-explainer").Should().BeEmpty();
            cut.Find("#test-catch-location-enable").Should().NotBeNull();
        });
        await location.Received(1).DismissPromptAsync(Arg.Any<CancellationToken>());
        await location.DidNotReceive().TryCaptureAsync(true, Arg.Any<CancellationToken>());
    }
}
