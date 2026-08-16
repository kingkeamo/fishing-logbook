using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Storage;
using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;
using FishingLogBook.Web.Features.TestCatch.Pages.TestCatchLog;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.TestCatch.Pages.TestCatchLogTests;

public class WhenTestingSave : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldNotSaveWhenSpeciesIsMissing()
    {
        // Arrange
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TestCatchModel>>([]));
        await using var context = CreateContext(store);
        var cut = context.Render<TestCatchLog>();

        // Act
        await cut.Find("#save-test-catch-button").ClickAsync();

        // Assert
        await store.DidNotReceive().SaveAsync(Arg.Any<TestCatchModel>(), Arg.Any<CancellationToken>());
        cut.Find("#test-catch-empty").TextContent.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ItShouldLogWarningSaveStartedAndCompletedWhenSaved()
    {
        // Arrange
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
        var diagnostics = Substitute.For<IDiagnosticLogger>();
        await using var context = CreateContext(
            store,
            Substitute.For<ITestCatchSynchroniser>(),
            Substitute.For<ITestCatchPhotoStore>(),
            diagnostics);
        var cut = context.Render<TestCatchLog>();

        // Act
        cut.Find("#test-catch-species").Input("Pike");
        await cut.Find("#save-test-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => saved.Should().ContainSingle());
        await diagnostics.Received(1).LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.CatchOfflineSaveStarted,
            "Catch offline save started.",
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
        await diagnostics.Received(1).LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.CatchOfflineSaveCompleted,
            "Catch offline save completed.",
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStoreAndListCatchImmediatelyWhenSaved()
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
        await using var context = CreateContext(store);
        var cut = context.Render<TestCatchLog>();

        // Act
        cut.Find("#test-catch-species").Input("Pike");
        await cut.Find("#save-test-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            saved.Should().ContainSingle();
            cut.Find($"#test-catch-species-{saved[0].Id}").TextContent.Should().Contain("Pike");
            cut.Find($"#test-catch-sync-status-{saved[0].Id}").TextContent.Should()
                .Contain("Saved locally — not synchronised");
            cut.FindAll("#save-test-catch-spinner").Should().BeEmpty();
            cut.Find("#save-test-catch-button").TextContent.Should().Contain("Save catch");
        });
        await store.Received(1).SaveAsync(
            Arg.Is<TestCatchModel>(testCatch =>
                testCatch.SpeciesName == "Pike" &&
                testCatch.SyncStatus == SyncStatus.SavedLocally),
            Arg.Any<CancellationToken>());
    }
}
