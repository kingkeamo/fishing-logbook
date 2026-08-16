using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;
using FishingLogBook.Web.Features.TestCatch.Pages.TestCatchLog;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.TestCatch.Pages.TestCatchLogTests;

public class WhenTestingDiagnosticLoggerHang : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldLoadExistingCatchesWhenDiagnosticLoggingNeverCompletes()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var existing = new TestCatchModel(
            Guid.Parse("8c0e91a2-4d77-4b18-a6f1-0c3d5e7a9b21"),
            "Roach",
            DateTimeOffset.Parse("2026-08-15T18:10:00Z"),
            null,
            SyncStatus.SavedLocally);
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TestCatchModel>>([existing]));
        await using var context = CreateContext(
            store,
            Substitute.For<ITestCatchSynchroniser>(),
            Substitute.For<ITestCatchPhotoStore>(),
            HangingDiagnostics());

        // Act
        var cut = context.Render<TestCatchLog>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#test-catch-item-{existing.Id}").Should().NotBeNull();
            cut.Find($"#test-catch-species-{existing.Id}").TextContent.Should().Contain("Roach");
        });
        await store.Received(2).GetAllAsync(Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync(Arg.Any<TestCatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveTheCatchWhenDiagnosticLoggingNeverCompletes()
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
        await using var context = CreateContext(
            store,
            Substitute.For<ITestCatchSynchroniser>(),
            Substitute.For<ITestCatchPhotoStore>(),
            HangingDiagnostics());
        var cut = context.Render<TestCatchLog>();
        cut.WaitForAssertion(() => cut.Find("#test-catch-species").Should().NotBeNull());

        // Act
        cut.Find("#test-catch-species").Input("Bream");
        await cut.Find("#save-test-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            saved.Should().ContainSingle();
            saved[0].SpeciesName.Should().Be("Bream");
            cut.Find($"#test-catch-species-{saved[0].Id}").TextContent.Should().Contain("Bream");
            cut.FindAll("#save-test-catch-spinner").Should().BeEmpty();
            cut.Find("#save-test-catch-button").TextContent.Should().Contain("Save catch");
        });
        await store.Received(1).SaveAsync(
            Arg.Is<TestCatchModel>(testCatch =>
                testCatch.SpeciesName == "Bream" &&
                testCatch.SyncStatus == SyncStatus.SavedLocally),
            Arg.Any<CancellationToken>());
    }
}
