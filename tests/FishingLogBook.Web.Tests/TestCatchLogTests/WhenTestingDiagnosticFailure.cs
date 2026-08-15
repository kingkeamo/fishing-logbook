using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Diagnostics;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Models;
using FishingLogBook.Web.Offline;
using FishingLogBook.Web.Pages.TestCatchLog;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.TestCatchLogTests;

public class WhenTestingDiagnosticFailure : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldStillSaveTheCatch_WhenDiagnosticLoggingThrows()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var saved = new List<TestCatch>();
        var store = Substitute.For<ITestCatchStore>();
        store.SaveAsync(Arg.Any<TestCatch>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                saved.Add(callInfo.Arg<TestCatch>());
                return Task.CompletedTask;
            });
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<TestCatch>>(saved.ToArray()));
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
    }

    [Fact]
    public async Task ItShouldReleaseBusyState_WhenLocalReadFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("read timed out"));
        store.SaveAsync(Arg.Any<TestCatch>(), Arg.Any<CancellationToken>())
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
        await store.Received().SaveAsync(Arg.Any<TestCatch>(), Arg.Any<CancellationToken>());
    }
}
