using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;
using FishingLogBook.Web.Features.TestCatch.Pages.TestCatchLog;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.TestCatch.Pages.TestCatchLogTests;

public class WhenTestingReopen : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldShowSavedCatch_WhenPageIsCreatedAgain()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var existing = new TestCatchModel(
            Guid.Parse("3a1f2c8e-7b44-4d1a-9c3e-2f8a6b0d1e55"),
            "Perch",
            DateTimeOffset.Parse("2026-08-14T10:30:00Z"),
            "Reed margin",
            SyncStatus.SavedLocally);
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TestCatchModel>>([existing]));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TestCatchLog>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#test-catch-item-{existing.Id}").Should().NotBeNull();
            cut.Find($"#test-catch-species-{existing.Id}").TextContent.Should().Contain("Perch");
            cut.Find($"#test-catch-sync-status-{existing.Id}").TextContent.Should()
                .Contain("Saved locally — not synchronised");
        });
        await store.Received(2).GetAllAsync(Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync(Arg.Any<TestCatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchLocalStatus_WhenUiCultureIsFrench()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var existing = new TestCatchModel(
            Guid.Parse("8c0e91a2-4d77-4b18-a6f1-0c3d5e7a9b21"),
            "Brochet",
            DateTimeOffset.Parse("2026-08-14T11:00:00Z"),
            null,
            SyncStatus.SavedLocally);
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TestCatchModel>>([existing]));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TestCatchLog>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#test-catch-sync-status-{existing.Id}").TextContent.Should()
                .Contain("Enregistrée localement — pas encore synchronisée");
        });
        await store.Received(2).GetAllAsync(Arg.Any<CancellationToken>());
    }
}
