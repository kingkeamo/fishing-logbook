using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Storage;
using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;
using FishingLogBook.Web.Features.TestCatch.Pages.TestCatchLog;
using FishingLogBook.Web.Localization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.TestCatch.Pages.TestCatchLogTests;

public class WhenTestingDiagnosticFailure : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldStillSaveTheCatchWhenDiagnosticLoggingThrows()
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
        var diagnostics = Substitute.For<IDiagnosticLogger>();
        diagnostics.LogAsync(
                Arg.Any<FishingLogBook.Shared.Diagnostics.DiagnosticLevel>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(),
                Arg.Any<Exception?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("diagnostics"));
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
        saved[0].SpeciesName.Should().Be("Pike");
        await store.Received(1).SaveAsync(
            Arg.Is<TestCatchModel>(testCatch =>
                testCatch.SpeciesName == "Pike" &&
                testCatch.SyncStatus == SyncStatus.SavedLocally),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReleaseBusyStateWhenLocalReadFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("read timed out"));
        store.SaveAsync(Arg.Any<TestCatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        await using var context = CreateContext(store);
        var cut = context.Render<TestCatchLog>();

        // Act
        cut.Find("#test-catch-species").Input("Pike");
        await cut.Find("#save-test-catch-button").ClickAsync();
        cut.Find("#test-catch-species").Input("Perch");

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#save-test-catch-button").HasAttribute("disabled").Should().BeFalse();
            cut.Find("#test-catch-load-error").Should().NotBeNull();
        });
        await store.Received(1).SaveAsync(
            Arg.Is<TestCatchModel>(testCatch => testCatch.SpeciesName == "Pike"),
            Arg.Any<CancellationToken>());
    }
}
