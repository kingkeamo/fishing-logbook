using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.OfflineCatchEdit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.OfflineCatchEditTests;

public class WhenTestingSave : BaseOfflineCatchEditTest
{
    [Fact]
    public async Task ItShouldFailClosedWhenTheCatchDoesNotBelongToTheUnlockedOwner()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>()).Returns(Catch(OtherUserId));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<OfflineCatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#offline-catch-edit-load-failed").Should().NotBeNull());
        await store.Received(1).GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveMetadataLocallyForTheUnlockedOwner()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>()).Returns(Catch(OwnerUserId));
        await using var context = CreateContext(store);
        var cut = context.Render<OfflineCatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#offline-catch-edit-notes").Should().NotBeNull());
        cut.Find("#offline-catch-edit-notes").Change("After");

        // Act
        await cut.Find("#offline-catch-edit-save").ClickAsync();

        // Assert
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.Id == CatchId
                && catchRecord.UserId == OwnerUserId
                && catchRecord.Notes == "After"
                && catchRecord.MetadataSyncStatus == SyncStatus.WaitingToSynchronise),
            Arg.Any<CancellationToken>());
        context.Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri.Should().EndWith("/offline/catches");
    }
}
