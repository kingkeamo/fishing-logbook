using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;
using FishingLogBook.Web.Features.TestCatch.Pages.TestCatchLog;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.TestCatch.Pages.TestCatchLogTests;

public class WhenTestingLocationRemove : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldClearLocationWhenRemoveIsClicked()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var existing = new TestCatchModel(
            Guid.Parse("5a2c8e14-9d70-4b31-8f6a-1c3e7b90d246"),
            "Pike",
            DateTimeOffset.Parse("2026-08-15T12:00:00Z"),
            null,
            SyncStatus.Synchronised,
            Location: new CatchLocationModel(
                53.2707,
                -9.0568,
                12,
                DateTimeOffset.Parse("2026-08-15T12:00:00Z"),
                "DeviceGps",
                "Private",
                "1"));
        var items = new List<TestCatchModel> { existing };
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<TestCatchModel>>(items.ToArray()));
        store.SaveAsync(Arg.Any<TestCatchModel>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var saved = callInfo.Arg<TestCatchModel>();
                var index = items.FindIndex(item => item.Id == saved.Id);
                if (index >= 0)
                {
                    items[index] = saved;
                }

                return Task.CompletedTask;
            });
        await using var context = CreateContext(store);
        var cut = context.Render<TestCatchLog>();

        // Act
        cut.WaitForAssertion(() => cut.Find($"#remove-test-catch-location-{existing.Id}").Should().NotBeNull());
        await cut.Find($"#remove-test-catch-location-{existing.Id}").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            items[0].Location.Should().BeNull();
            cut.Find($"#test-catch-location-missing-{existing.Id}").Should().NotBeNull();
            cut.FindAll($"#remove-test-catch-location-{existing.Id}").Should().BeEmpty();
            cut.FindAll($"#test-catch-location-saved-{existing.Id}").Should().BeEmpty();
        });
        await store.Received(1).SaveAsync(
            Arg.Is<TestCatchModel>(testCatch =>
                testCatch.Id == existing.Id &&
                testCatch.Location == null &&
                testCatch.SyncStatus == SyncStatus.SavedLocally),
            Arg.Any<CancellationToken>());
    }
}
