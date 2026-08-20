using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Modals.LocationPrivacy;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchList;
using FishingLogBook.Web.Localization;
using MudBlazor;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchListTests;

public class WhenTestingActions : BaseCatchListTest
{
    [Fact]
    public async Task ItShouldLinkTheCardToTheEditPage()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<CatchModel>>([StoredCatch(catchId, DateTimeOffset.Parse("2026-08-17T08:00:00Z"))]);
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-card-link-{catchId:D}").GetAttribute("href").Should().Be($"/catches/{catchId:D}/edit"));
    }

    [Fact]
    public async Task ItShouldOmitLocationPrivacyWhenTheCatchHasNoLocation()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<CatchModel>>([StoredCatch(catchId, DateTimeOffset.Parse("2026-08-17T08:00:00Z"))]);
        await using var context = CreateContext(store);
        var popover = context.Render<MudPopoverProvider>();
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find($"#catch-card-menu-{catchId:D}").Should().NotBeNull());

        // Act
        await cut.Find($"#catch-card-menu-{catchId:D}").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => popover.Find($"#catch-card-edit-{catchId:D}").Should().NotBeNull());
        popover.FindAll($"#catch-card-location-privacy-{catchId:D}").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldOpenLocationPrivacyThroughTheModalServiceAndRefreshOnSave()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var location = new CatchLocationModel(
            53.2707,
            -9.0568,
            12,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<CatchModel>>(
                [StoredCatch(catchId, DateTimeOffset.Parse("2026-08-17T08:00:00Z"), location: location)]);
        var modalService = Substitute.For<IModalService>();
        modalService.ShowAsync<LocationPrivacyModal, LocationPrivacyModalModel, LocationPrivacyModalResult>(
                Arg.Any<LocationPrivacyModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(new LocationPrivacyModalResult(true));
        await using var context = CreateContext(store, modalService: modalService);
        var popover = context.Render<MudPopoverProvider>();
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find($"#catch-card-menu-{catchId:D}").Should().NotBeNull());
        await cut.Find($"#catch-card-menu-{catchId:D}").ClickAsync();
        cut.WaitForAssertion(() => popover.Find($"#catch-card-location-privacy-{catchId:D}").Should().NotBeNull());

        // Act
        await popover.Find($"#catch-card-location-privacy-{catchId:D}").ClickAsync();

        // Assert
        await modalService.Received(1).ShowAsync<LocationPrivacyModal, LocationPrivacyModalModel, LocationPrivacyModalResult>(
            Arg.Is<LocationPrivacyModalModel>(model => model.CatchId == catchId),
            Arg.Any<CancellationToken>());
        await store.Received(2).GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }
}
