using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;
using FishingLogBook.Web.Features.TestCatch.Pages.TestCatchLog;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.TestCatch.Pages.TestCatchLogTests;

public class WhenTestingLocationCaptureHang : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldSaveCatchWithoutLocation_WhenCaptureNeverCompletes()
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
            .Returns(_ => Task.FromResult<IReadOnlyList<TestCatchModel>>(saved.ToArray()));
        var synchroniser = Substitute.For<ITestCatchSynchroniser>();
        await using var context = CreateContext(
            store,
            synchroniser,
            Substitute.For<ITestCatchPhotoStore>(),
            location: HangingLocation());
        var cut = context.Render<TestCatchLog>();
        cut.WaitForAssertion(() => cut.Find("#test-catch-species").Should().NotBeNull());

        // Act
        cut.Find("#test-catch-species").Input("Pike");
        await cut.Find("#save-test-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            saved.Should().ContainSingle();
            saved[0].Location.Should().BeNull();
            saved[0].SpeciesName.Should().Be("Pike");
            saved[0].SyncStatus.Should().Be(SyncStatus.SavedLocally);
            cut.Find($"#test-catch-species-{saved[0].Id}").TextContent.Should().Contain("Pike");
            cut.FindAll("#save-test-catch-spinner").Should().BeEmpty();
            cut.Find("#save-test-catch-button").TextContent.Should().Contain("Save catch");
        });
        await store.Received(1).SaveAsync(
            Arg.Is<TestCatchModel>(testCatch =>
                testCatch.SpeciesName == "Pike" &&
                testCatch.Location == null &&
                testCatch.SyncStatus == SyncStatus.SavedLocally),
            Arg.Any<CancellationToken>());
        await synchroniser.Received(2).SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }
}
