using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Offline.Synchronisers;
using FishingLogBook.Web.Features.Catch.Pages.CatchEdit;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components.Forms;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchEditTests;

public class WhenTestingPhotographs : BaseCatchEditTest
{
    private static readonly Guid CatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid FirstPhotographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid SecondPhotographId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task ItShouldRejectAnUnsupportedPhotographFormat()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(CatchId));
        await using var context = CreateContext(store);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo-camera").Should().NotBeNull());

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([0x00, 0x01], "photo.heic", contentType: "image/heic"));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-photo-unsupported").TextContent
                .Should().Contain("This photo format isn't supported"));
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAddAValidPhotographAndSynchroniseImmediately()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(CatchId, SyncStatus.Synchronised, SyncStatus.Synchronised, SyncStatus.Synchronised));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var synchroniser = QuietSynchroniser();
        await using var context = CreateContext(store, synchroniser: synchroniser);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo-camera").Should().NotBeNull());

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary(
                [0xFF, 0xD8, 0xFF],
                "photo.jpg",
                contentType: PhotographContentTypeConstants.Jpeg));

        // Assert
        cut.WaitForAssertion(() =>
        {
            var call = store.ReceivedCalls().Last();
            var saved = (CatchModel)call.GetArguments()[0]!;
            saved.Photographs.Should().HaveCount(2);
            saved.SyncStatus.Should().Be(SyncStatus.WaitingToSynchronise);
        });
        await synchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepAnAddedPhotographWhenSharedDetailsAreSaved()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(CatchId));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        await using var context = CreateContext(store);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo-camera").Should().NotBeNull());
        cut.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary(
                [0xFF, 0xD8, 0xFF],
                "photo.jpg",
                contentType: PhotographContentTypeConstants.Jpeg));
        cut.WaitForAssertion(() => store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.Photographs.Count == 2),
            Arg.Any<CancellationToken>()));
        cut.Find("#catch-edit-notes").Input("Updated after adding a photo");

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        await store.Received(2).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.Photographs.Count == 2),
            Arg.Any<CancellationToken>());
        var lastSaved = (CatchModel)store.ReceivedCalls().Last().GetArguments()[0]!;
        lastSaved.Notes.Should().Be("Updated after adding a photo");
    }

    [Fact]
    public async Task ItShouldPreventRemovingTheLastPhotograph()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(CatchId));
        var modalService = Substitute.For<IModalService>();
        await using var context = CreateContext(store, modalService: modalService);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo-remove").Should().NotBeNull());

        // Act
        await cut.Find("#catch-edit-photo-remove").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-photo-last").TextContent
                .Should().Contain("A catch needs at least one photograph"));
        await modalService.DidNotReceive().ConfirmAsync(
            Arg.Any<ConfirmModalModel>(),
            Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRemoveThePhotographWhenTheUserCancelsTheConfirmation()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .Returns(TwoPhotographCatch());
        var modalService = Substitute.For<IModalService>();
        modalService.ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>())
            .Returns(false);
        await using var context = CreateContext(store, modalService: modalService);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo-remove").Should().NotBeNull());

        // Act
        await cut.Find("#catch-edit-photo-remove").ClickAsync();

        // Assert
        await modalService.Received(1).ConfirmAsync(
            Arg.Is<ConfirmModalModel>(model => model.IsDestructive),
            Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMarkTheCurrentPhotographPendingDeletionWhenConfirmed()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .Returns(TwoPhotographCatch());
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var modalService = Substitute.For<IModalService>();
        modalService.ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var synchroniser = QuietSynchroniser();
        await using var context = CreateContext(store, synchroniser: synchroniser, modalService: modalService);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo-remove").Should().NotBeNull());

        // Act
        await cut.Find("#catch-edit-photo-remove").ClickAsync();

        // Assert
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Photographs.Single(item => item.Id == FirstPhotographId).SyncStatus
                    == SyncStatus.PendingDeletion
                && catchRecord.Photographs.Single(item => item.Id == SecondPhotographId).SyncStatus
                    == SyncStatus.Synchronised
                && catchRecord.SyncStatus == SyncStatus.WaitingToSynchronise),
            Arg.Any<CancellationToken>());
        await synchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
        cut.WaitForAssertion(() =>
            cut.FindAll("#catch-edit-photo-count").Should().BeEmpty());
    }

    [Fact]
    public async Task ItShouldShowAnErrorWhenSavingTheRemovalFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .Returns(TwoPhotographCatch());
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("offline"));
        var modalService = Substitute.For<IModalService>();
        modalService.ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>())
            .Returns(true);
        await using var context = CreateContext(store, modalService: modalService);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo-remove").Should().NotBeNull());

        // Act
        await cut.Find("#catch-edit-photo-remove").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-photo-action-failed").Should().NotBeNull());
    }

    private static CatchModel TwoPhotographCatch()
    {
        var stored = StoredCatch(CatchId, SyncStatus.Synchronised, SyncStatus.Synchronised, SyncStatus.Synchronised);
        return stored with
        {
            Photographs =
            [
                stored.Photographs[0] with { Id = FirstPhotographId },
                new CatchPhotographModel(
                    SecondPhotographId,
                    CatchId,
                    PhotographContentTypeConstants.Jpeg,
                    [4, 5, 6],
                    SyncStatus.Synchronised)
            ]
        };
    }
}
