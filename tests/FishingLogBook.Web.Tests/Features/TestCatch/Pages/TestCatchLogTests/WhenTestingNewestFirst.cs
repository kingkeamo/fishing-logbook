using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;
using FishingLogBook.Web.Features.TestCatch.Pages.TestCatchLog;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.TestCatch.Pages.TestCatchLogTests;

public class WhenTestingNewestFirst : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldShowTheNewestCatchFirst()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var older = new TestCatchModel(
            Guid.Parse("3a1f2c8e-7b44-4d1a-9c3e-2f8a6b0d1e55"),
            "Perch",
            DateTimeOffset.Parse("2026-08-14T10:00:00Z"),
            null,
            SyncStatus.SavedLocally);
        var newer = new TestCatchModel(
            Guid.Parse("8c0e91a2-4d77-4b18-a6f1-0c3d5e7a9b21"),
            "Pike",
            DateTimeOffset.Parse("2026-08-15T08:30:00Z"),
            null,
            SyncStatus.SavedLocally);
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TestCatchModel>>([older, newer]));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TestCatchLog>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            var items = cut.FindAll("#test-catch-list [id^='test-catch-item-']");
            items.Should().HaveCount(2);
            items[0].Id.Should().Be($"test-catch-item-{newer.Id}");
            items[1].Id.Should().Be($"test-catch-item-{older.Id}");
            cut.Find($"#test-catch-species-{newer.Id}").TextContent.Should().Contain("Pike");
            cut.Find($"#test-catch-species-{older.Id}").TextContent.Should().Contain("Perch");
        });
        await store.Received().GetAllAsync(Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync(Arg.Any<TestCatchModel>(), Arg.Any<CancellationToken>());
    }
}
