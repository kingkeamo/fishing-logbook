using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Modals.LocationPrivacy;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchEdit;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components.Forms;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchEditTests;

public class WhenTestingPhotographFailures : BaseCatchEditTest
{
    private static readonly Guid CatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid FirstPhotographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid SecondPhotographId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

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

    [Fact]
    public async Task ItShouldLogAndWarnWhenAddingAPhotographCannotBeSaved()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(CatchId, SyncStatus.Synchronised, SyncStatus.Synchronised, SyncStatus.Synchronised));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("quota"));
        var synchroniser = QuietSynchroniser();
        var logging = Substitute.For<ILoggingService>();
        await using var context = CreateContext(store, synchroniser: synchroniser, logging: logging);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo-camera").Should().NotBeNull());

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary(
                [0xFF, 0xD8, 0xFF],
                "photo.jpg",
                contentType: PhotographContentTypeConstants.Jpeg));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo-action-failed").Should().NotBeNull());
        await logging.Received(1).LogErrorAsync(
            "adding photographs to a catch",
            Arg.Is<Exception>(exception => exception.Message == "quota"),
            CancellationToken.None);
        await synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLogWhenRemovingAPhotographCannotBeSaved()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>()).Returns(TwoPhotographCatch());
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("offline"));
        var modalService = Substitute.For<IModalService>();
        modalService.ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>()).Returns(true);
        var logging = Substitute.For<ILoggingService>();
        await using var context = CreateContext(store, modalService: modalService, logging: logging);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo-remove").Should().NotBeNull());

        // Act
        await cut.Find("#catch-edit-photo-remove").ClickAsync();

        // Assert
        await logging.Received(1).LogErrorAsync(
            "removing a catch photograph",
            Arg.Is<Exception>(exception => exception.Message == "offline"),
            CancellationToken.None);
        await modalService.Received(1).ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotReloadTheCatchWhenTheLocationPrivacyModalIsDismissed()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var location = new CatchLocationModel(
            53.2707,
            -9.0568,
            12,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(CatchId, location: location));
        var modalService = Substitute.For<IModalService>();
        modalService.ShowAsync<LocationPrivacyModal, LocationPrivacyModalModel, LocationPrivacyModalResult>(
                Arg.Any<LocationPrivacyModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(new LocationPrivacyModalResult(false));
        await using var context = CreateContext(store, modalService: modalService);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-location-manage").Should().NotBeNull());

        // Act
        await cut.Find("#catch-edit-location-manage").ClickAsync();

        // Assert
        await modalService.Received(1)
            .ShowAsync<LocationPrivacyModal, LocationPrivacyModalModel, LocationPrivacyModalResult>(
                Arg.Is<LocationPrivacyModalModel>(model => model.CatchId == CatchId),
                Arg.Any<CancellationToken>());
        await store.Received(1).GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>());
    }
}
