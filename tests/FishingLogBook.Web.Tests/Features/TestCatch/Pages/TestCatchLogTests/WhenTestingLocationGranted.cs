using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.SystemStatus.Services;
using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;
using FishingLogBook.Web.Features.TestCatch.Pages.TestCatchLog;
using FishingLogBook.Web.Features.TestCatch.Services;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.TestCatch.Pages.TestCatchLogTests;

public class WhenTestingLocationGranted : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldStoreLocationWithTheCatch_WhenPermissionWasGranted()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var captured = new TestCatchLocationModel(
            53.2707,
            -9.0568,
            12,
            DateTimeOffset.Parse("2026-08-15T12:00:00Z"),
            "DeviceGps",
            "Private",
            "1");
        var saved = new List<TestCatchModel>();
        var store = Substitute.For<ITestCatchStore>();
        store.SaveAsync(Arg.Any<TestCatchModel>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                saved.Add(callInfo.Arg<TestCatchModel>());
                return Task.CompletedTask;
            });
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<TestCatchModel>>(saved.ToArray()));
        await using var context = CreateContext(
            store,
            Substitute.For<ITestCatchSynchroniser>(),
            Substitute.For<ITestCatchPhotoStore>(),
            location: GrantedLocation(captured));
        var cut = context.Render<TestCatchLog>();

        // Act
        cut.Find("#test-catch-species").Input("Pike");
        await cut.Find("#save-test-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            saved.Should().ContainSingle();
            saved[0].Location.Should().Be(captured);
            cut.Find($"#test-catch-location-check-{saved[0].Id}").Should().NotBeNull();
            cut.Find($"#test-catch-location-saved-{saved[0].Id}").TextContent.Should().Contain("Saved");
            cut.Find($"#remove-test-catch-location-{saved[0].Id}").TextContent.Should().Contain("Remove");
            cut.Find($"#remove-test-catch-location-{saved[0].Id}").ClassList.Should().Contain(c => c.Contains("error"));
            cut.Find($"#test-catch-location-check-{saved[0].Id}").ClassList.Should().Contain(c => c.Contains("success"));
            cut.FindAll($"#test-catch-location-missing-{saved[0].Id}").Should().BeEmpty();
            cut.FindAll("#test-catch-location-explainer").Should().BeEmpty();
        });
        await store.Received(1).SaveAsync(
            Arg.Is<TestCatchModel>(testCatch =>
                testCatch.Location == captured &&
                testCatch.SyncStatus == SyncStatus.SavedLocally),
            Arg.Any<CancellationToken>());
    }
}
