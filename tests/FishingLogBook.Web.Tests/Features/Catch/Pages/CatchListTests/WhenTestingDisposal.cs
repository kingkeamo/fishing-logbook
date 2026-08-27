using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common.Offline.Synchronisers;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchList;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchListTests;

public class WhenTestingDisposal : BaseCatchListTest
{
    [Fact]
    public async Task ItShouldIgnoreASynchronisationUpdateAfterThePageIsDisposed()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CatchModel>());
        var synchroniser = Substitute.For<ILogbookSynchroniser>();
        await using var context = CreateContext(store, synchroniser: synchroniser);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#catch-list-empty"));
        cut.Instance.Dispose();

        // Act
        var act = () => synchroniser.StateChanged += Raise.Event<EventHandler>(
            synchroniser,
            EventArgs.Empty);

        // Assert
        act.Should().NotThrow();
        await store.Received(1).GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }
}
