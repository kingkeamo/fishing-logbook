using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.OfflineCatchList;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.OfflineCatchListTests;

public class WhenTestingLoad : BaseOfflineCatchListTest
{
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
