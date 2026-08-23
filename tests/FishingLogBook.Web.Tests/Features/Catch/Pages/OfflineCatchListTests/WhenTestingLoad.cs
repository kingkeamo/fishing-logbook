using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.OfflineCatchList;
using FishingLogBook.Web.Features.Diagnostics.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.OfflineCatchListTests;

public class WhenTestingLoad : BaseOfflineCatchListTest
{
    [Fact]
    public async Task ItShouldLogTheOriginalExceptionWhenLocalCatchLoadingFails()
    {
        // Arrange
        var exception = new InvalidOperationException("IndexedDB read failed.");
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>()).ThrowsAsync(exception);
        var logging = QuietLogging();
        await using var context = CreateContext(store, logging: logging);

        // Act
        var cut = context.Render<OfflineCatchList>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#offline-catch-list-load-failed").Should().NotBeNull());
        await logging.Received(1).LogErrorAsync(
            "loading offline catches",
            exception,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLoadOnlyTheUnlockedOwnersLocalCatches()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns([
            Catch(OwnerUserId, "Brown Trout"),
            Catch(OtherUserId, "Pike")
        ]);
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<OfflineCatchList>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#offline-catch-list").TextContent.Should().Contain("Brown Trout"));
        cut.Markup.Should().NotContain("Pike");
        cut.Find("#offline-catch-list").QuerySelectorAll(".catch-card").Should().HaveCount(1);
        await store.Received(1).GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }
}
