using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchEdit;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchEditTests;

public class WhenTestingProvenance : BaseCatchEditTest
{
    [Fact]
    public async Task ItShouldShowCaughtByAndRecordedByWhenTheyDiffer()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(
                catchId,
                SyncStatus.Synchronised,
                SyncStatus.Synchronised,
                anglerUserId: OwnerUserId,
                recordedByUserId: OtherUserId));
        var catchClient = Substitute.For<ICatchClient>();
        catchClient.GetAsync(catchId, Arg.Any<CancellationToken>())
            .Returns(new CatchViewDto(catchId, OwnerUserId, StoredCaughtOn)
            {
                AnglerUserId = OwnerUserId,
                RecordedByUserId = OtherUserId,
                AnglerName = "Patrick Connolly",
                RecordedByName = "Myles Costello"
            });
        await using var context = CreateContext(store, catchClient: catchClient, network: OnlineNetwork());

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-edit-angler-name").TextContent.Should().Contain("Patrick Connolly");
            cut.Find("#catch-edit-recorder-name").TextContent.Should().Contain("Myles Costello");
        });
        await catchClient.Received(1).GetAsync(catchId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOmitRecordedByWhenTheAnglerAndRecorderAreTheSame()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(catchId, SyncStatus.Synchronised, SyncStatus.Synchronised));
        var catchClient = Substitute.For<ICatchClient>();
        catchClient.GetAsync(catchId, Arg.Any<CancellationToken>())
            .Returns(new CatchViewDto(catchId, OwnerUserId, StoredCaughtOn)
            {
                AnglerUserId = OwnerUserId,
                RecordedByUserId = OwnerUserId,
                AnglerName = "Myles Costello",
                RecordedByName = "Myles Costello"
            });
        await using var context = CreateContext(store, catchClient: catchClient, network: OnlineNetwork());

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-angler-name").TextContent.Should().Contain("Myles Costello"));
        cut.FindAll("#catch-edit-recorder-name").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotShowProvenanceWhenOffline()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(catchId, SyncStatus.Synchronised, SyncStatus.Synchronised));
        var catchClient = Substitute.For<ICatchClient>();
        await using var context = CreateContext(store, catchClient: catchClient, network: OnlineNetwork(isOnline: false));

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-title").Should().NotBeNull());
        cut.FindAll("#catch-edit-provenance").Should().BeEmpty();
        cut.FindAll("#catch-edit-load-failed").Should().BeEmpty();
        await catchClient.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotFetchProvenanceForAPendingLocalEdit()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(catchId, SyncStatus.WaitingToSynchronise, SyncStatus.WaitingToSynchronise));
        var catchClient = Substitute.For<ICatchClient>();
        await using var context = CreateContext(store, catchClient: catchClient, network: OnlineNetwork());

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-title").Should().NotBeNull());
        cut.FindAll("#catch-edit-provenance").Should().BeEmpty();
        await catchClient.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
