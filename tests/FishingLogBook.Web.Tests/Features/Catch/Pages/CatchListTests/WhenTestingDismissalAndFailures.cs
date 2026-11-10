using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Modals.LocationPrivacy;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchList;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchListTests;

public class WhenTestingDismissalAndFailures : BaseCatchListTest
{
    private static ICatchStore EmptyStore()
    {
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CatchModel>());
        return store;
    }

    private static ICatchStore StoreWithLocatedCatch(Guid catchId)
    {
        var location = new CatchLocationModel(
            53.2707,
            -9.0568,
            12,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<CatchModel>>(
                [StoredCatch(catchId, DateTimeOffset.Parse("2026-08-17T08:00:00Z"), location: location)]);
        return store;
    }

    private static async Task OpenLocationPrivacyAsync(
        IRenderedComponent<CatchList> cut,
        IRenderedComponent<MudPopoverProvider> popover,
        Guid catchId)
    {
        cut.WaitForAssertion(() => cut.Find($"#catch-card-menu-{catchId:D}").Should().NotBeNull());
        await cut.Find($"#catch-card-menu-{catchId:D}").ClickAsync();
        popover.WaitForAssertion(() => popover.Find($"#catch-card-location-privacy-{catchId:D}").Should().NotBeNull());
        await popover.Find($"#catch-card-location-privacy-{catchId:D}").ClickAsync();
    }

    [Fact]
    public async Task ItShouldNotReloadWhenTheLocationPrivacyModalIsDismissed()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var store = StoreWithLocatedCatch(catchId);
        var modalService = Substitute.For<IModalService>();
        modalService.ShowAsync<LocationPrivacyModal, LocationPrivacyModalModel, LocationPrivacyModalResult>(
                Arg.Any<LocationPrivacyModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<LocationPrivacyModalResult?>(null));
        await using var context = CreateContext(store, modalService: modalService);
        var popover = context.Render<MudPopoverProvider>();
        var cut = context.Render<CatchList>();

        // Act
        await OpenLocationPrivacyAsync(cut, popover, catchId);

        // Assert
        await modalService.Received(1).ShowAsync<LocationPrivacyModal, LocationPrivacyModalModel, LocationPrivacyModalResult>(
            Arg.Is<LocationPrivacyModalModel>(model => model.CatchId == catchId),
            Arg.Any<CancellationToken>());
        await store.Received(1).GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotReloadWhenTheLocationPrivacyModalClosesWithoutSaving()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var store = StoreWithLocatedCatch(catchId);
        var modalService = Substitute.For<IModalService>();
        modalService.ShowAsync<LocationPrivacyModal, LocationPrivacyModalModel, LocationPrivacyModalResult>(
                Arg.Any<LocationPrivacyModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(new LocationPrivacyModalResult(false));
        await using var context = CreateContext(store, modalService: modalService);
        var popover = context.Render<MudPopoverProvider>();
        var cut = context.Render<CatchList>();

        // Act
        await OpenLocationPrivacyAsync(cut, popover, catchId);

        // Assert
        await modalService.Received(1).ShowAsync<LocationPrivacyModal, LocationPrivacyModalModel, LocationPrivacyModalResult>(
            Arg.Is<LocationPrivacyModalModel>(model => model.CatchId == catchId),
            Arg.Any<CancellationToken>());
        await store.Received(1).GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLogAndStayOnTheLogbookWhenStartingATripFailsUnexpectedly()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = Substitute.For<IActiveTripService>();
        activeTrip.GetActiveAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns((TripModel?)null);
        activeTrip.StartAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<Task<TripModel>>(_ => throw new NullReferenceException("unexpected"));
        var logging = Substitute.For<ILoggingService>();
        await using var context = CreateContext(EmptyStore(), logging: logging);
        context.Services.AddSingleton(activeTrip);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#trip-start-link").Should().NotBeNull());

        // Act
        await cut.Find("#trip-start-link").ClickAsync();

        // Assert
        await activeTrip.Received(1).StartAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await logging.Received(1).LogErrorAsync(
            "starting a trip",
            Arg.Is<Exception>(exception => exception is NullReferenceException),
            CancellationToken.None);
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().NotContain("/trips/");
        cut.Find("#trip-start-link").Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldStillShowAndFilterCatchesWhenTheBrowserClockCannotBeRead()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<CatchModel>>([StoredCatch(catchId, DateTimeOffset.UtcNow.AddMinutes(-5))]);
        var time = Substitute.For<ITimeService>();
        time.ToDateTimeLocalValueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns("not a date");
        await using var context = CreateContext(store, time: time);
        var popover = context.Render<MudPopoverProvider>();
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#catch-filters-button").Should().NotBeNull());
        await cut.Find("#catch-filters-button").ClickAsync();
        popover.WaitForAssertion(() => popover.Find("#catch-filter-date-Today").Should().NotBeNull());

        // Act
        await popover.Find("#catch-filter-date-Today").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find($"#catch-card-{catchId:D}").Should().NotBeNull());
        await time.Received().ToDateTimeLocalValueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }
}
