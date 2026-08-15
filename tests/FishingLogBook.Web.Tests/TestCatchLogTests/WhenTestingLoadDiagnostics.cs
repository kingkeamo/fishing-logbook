using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Diagnostics;
using FishingLogBook.Web.Models;
using FishingLogBook.Web.Offline;
using FishingLogBook.Web.Pages.TestCatchLog;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.TestCatchLogTests;

public class WhenTestingLoadDiagnostics : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldLogWarningLoadStartedAndCompleted_WhenPageLoads()
    {
        // Arrange
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TestCatch>>([]));
        var diagnostics = Substitute.For<IDiagnosticLogger>();
        await using var context = CreateContext(
            store,
            Substitute.For<ITestCatchSynchroniser>(),
            Substitute.For<ITestCatchPhotoStore>(),
            diagnostics);

        // Act
        var cut = context.Render<TestCatchLog>();
        cut.WaitForAssertion(() => cut.Find("#test-catch-empty").Should().NotBeNull());

        // Assert
        await diagnostics.Received().LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.CatchOfflineLoadStarted,
            "Catch offline load started.",
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
        await diagnostics.Received().LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.CatchOfflineLoadCompleted,
            "Catch offline load completed.",
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotLogLoadCompleted_WhenLocalReadFails()
    {
        // Arrange
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("read timed out"));
        var diagnostics = Substitute.For<IDiagnosticLogger>();
        await using var context = CreateContext(
            store,
            Substitute.For<ITestCatchSynchroniser>(),
            Substitute.For<ITestCatchPhotoStore>(),
            diagnostics);

        // Act
        var cut = context.Render<TestCatchLog>();
        cut.WaitForAssertion(() => cut.Find("#test-catch-load-error").Should().NotBeNull());

        // Assert
        await diagnostics.Received().LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.CatchOfflineLoadStarted,
            "Catch offline load started.",
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
        await diagnostics.DidNotReceive().LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.CatchOfflineLoadCompleted,
            "Catch offline load completed.",
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }
}
