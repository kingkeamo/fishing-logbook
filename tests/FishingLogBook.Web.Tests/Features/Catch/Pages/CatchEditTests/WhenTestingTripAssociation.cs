using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchEdit;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchEditTests;

public class WhenTestingTripAssociation : BaseCatchEditTest
{
    private static readonly Guid CatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TripId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    [Fact]
    public async Task ItShouldNotAttachATripToAStandaloneCatch()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(CatchId, SyncStatus.Synchronised, SyncStatus.Synchronised));
        await using var context = CreateContext(store);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-save").Should().NotBeNull());

        // Act
        cut.Find("#catch-edit-notes").Input("A good fish");
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id == CatchId && catchRecord.TripId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheExistingTripWhenTheCatchIsEdited()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .Returns(TrippedCatch());
        await using var context = CreateContext(store);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-save").Should().NotBeNull());

        // Act
        cut.Find("#catch-edit-notes").Input("A good fish");
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id == CatchId
                && catchRecord.TripId == TripId
                && catchRecord.Notes == "A good fish"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotShowAnyTripControlsOnTheEditor()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .Returns(TrippedCatch());
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-edit-save").Should().NotBeNull());
        cut.FindAll("#catch-trip-leave").Should().BeEmpty();
        cut.FindAll("#catch-trip-association").Should().BeEmpty();
    }

    private static CatchModel TrippedCatch()
    {
        return StoredCatch(CatchId, SyncStatus.Synchronised, SyncStatus.Synchronised) with
        {
            TripId = TripId
        };
    }
}
