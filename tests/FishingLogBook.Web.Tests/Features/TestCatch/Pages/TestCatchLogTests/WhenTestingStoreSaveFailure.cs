using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;
using FishingLogBook.Web.Features.TestCatch.Pages.TestCatchLog;
using FishingLogBook.Web.Localization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.TestCatch.Pages.TestCatchLogTests;

public class WhenTestingStoreSaveFailure : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldKeepFormValuesAndResetSaving()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TestCatchModel>>([]));
        store.SaveAsync(Arg.Any<TestCatchModel>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("write timed out"));
        var photos = Substitute.For<ITestCatchPhotoStore>();
        await using var context = CreateContext(store, Substitute.For<ITestCatchSynchroniser>(), photos);
        var cut = context.Render<TestCatchLog>();

        // Act
        cut.Find("#test-catch-species").Input("Pike");
        cut.Find("#test-catch-notes").Change("Near the reeds");
        await cut.Find("#save-test-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.FindAll("#save-test-catch-spinner").Should().BeEmpty();
            cut.Find("#save-test-catch-button").TextContent.Should().Contain("Save catch");
            cut.Find("#save-test-catch-button").HasAttribute("disabled").Should().BeFalse();
            cut.Find("#test-catch-species").GetAttribute("value").Should().Be("Pike");
            cut.Find("#test-catch-notes").GetAttribute("value").Should().Contain("Near the reeds");
        });
        await store.Received(1).SaveAsync(Arg.Any<TestCatchModel>(), Arg.Any<CancellationToken>());
        await photos.DidNotReceive().PutAsync(
            Arg.Any<Guid>(),
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
