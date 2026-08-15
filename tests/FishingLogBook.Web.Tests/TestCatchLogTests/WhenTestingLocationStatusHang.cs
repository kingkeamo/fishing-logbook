using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Models;
using FishingLogBook.Web.Offline;
using FishingLogBook.Web.Pages.TestCatchLog;
using NSubstitute;

namespace FishingLogBook.Web.Tests.TestCatchLogTests;

public class WhenTestingLocationStatusHang : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldLoadExistingCatches_WhenLocationStatusNeverCompletes()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var existing = new TestCatch(
            Guid.Parse("3a1f2c8e-7b44-4d1a-9c3e-2f8a6b0d1e55"),
            "Perch",
            DateTimeOffset.Parse("2026-08-15T18:00:00Z"),
            null,
            SyncStatus.SavedLocally);
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TestCatch>>([existing]));
        await using var context = CreateContext(
            store,
            Substitute.For<ITestCatchSynchroniser>(),
            Substitute.For<ITestCatchPhotoStore>(),
            location: HangingLocation());

        // Act
        var cut = context.Render<TestCatchLog>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#test-catch-item-{existing.Id}").Should().NotBeNull();
            cut.Find($"#test-catch-species-{existing.Id}").TextContent.Should().Contain("Perch");
        });
        await store.Received().GetAllAsync(Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync(Arg.Any<TestCatch>(), Arg.Any<CancellationToken>());
    }
}
