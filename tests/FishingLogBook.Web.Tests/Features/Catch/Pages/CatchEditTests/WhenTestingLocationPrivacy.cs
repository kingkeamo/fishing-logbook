using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Modals.LocationPrivacy;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchEdit;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchEditTests;

public class WhenTestingLocationPrivacy : BaseCatchEditTest
{
    private static readonly Guid CatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task ItShouldShowTheNoLocationMessageWhenTheCatchHasNoLocation()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(CatchId, location: null));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-location-none").TextContent
                .Should().Contain("no saved location"));
        cut.FindAll("#catch-edit-location-manage").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowTheVisibilityAndOpenTheLocationPrivacyModal()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var location = new CatchLocationModel(
            53.2707,
            -9.0568,
            12,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            "DeviceGps",
            "Private",
            "1");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(CatchId, location: location));
        var modalService = Substitute.For<IModalService>();
        modalService
            .ShowAsync<LocationPrivacyModal, LocationPrivacyModalModel, LocationPrivacyModalResult>(
                Arg.Any<LocationPrivacyModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(new LocationPrivacyModalResult(true));
        await using var context = CreateContext(store, modalService: modalService);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-location-visibility").TextContent.Should().Contain("Only me"));

        // Act
        await cut.Find("#catch-edit-location-manage").ClickAsync();

        // Assert
        await modalService.Received(1)
            .ShowAsync<LocationPrivacyModal, LocationPrivacyModalModel, LocationPrivacyModalResult>(
                Arg.Is<LocationPrivacyModalModel>(model => model.CatchId == CatchId),
                Arg.Any<CancellationToken>());
        await store.Received(2).GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>());
    }
}
