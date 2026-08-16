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

public class WhenTestingDiagnosticSyncFailure : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldStillCompleteSave()
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
        var diagnosticSynchroniser = Substitute.For<IDiagnosticSynchroniser>();
        diagnosticSynchroniser.SynchronisePendingAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("diagnostic sync timed out"));
        await using var context = CreateContext(
            store,
            Substitute.For<ITestCatchSynchroniser>(),
            Substitute.For<ITestCatchPhotoStore>(),
            diagnosticSynchroniser: diagnosticSynchroniser);
        var cut = context.Render<TestCatchLog>();

        // Act
        cut.Find("#test-catch-species").Input("Pike");
        await cut.Find("#save-test-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            saved.Should().ContainSingle();
            cut.Find($"#test-catch-species-{saved[0].Id}").TextContent.Should().Contain("Pike");
            cut.FindAll("#save-test-catch-spinner").Should().BeEmpty();
        });
        await store.Received(1).SaveAsync(
            Arg.Is<TestCatchModel>(testCatch => testCatch.SpeciesName == "Pike"),
            Arg.Any<CancellationToken>());
    }
}
