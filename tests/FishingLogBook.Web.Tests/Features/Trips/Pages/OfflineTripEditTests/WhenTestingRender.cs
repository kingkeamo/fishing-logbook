using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.OfflineAccess.Models;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.Features.Trips.Pages.TripEditTests;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OfflineTripEditPage = FishingLogBook.Web.Features.Trips.Pages.OfflineTripEdit.OfflineTripEdit;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.OfflineTripEditTests;

public class WhenTestingRender : BaseTripEditTest
{
    [Fact]
    public async Task ItShouldShowTheFailureWhenTheOfflineOwnerIsLocked()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        var lockedOwner = Substitute.For<IOfflineOwnerContextService>();
        lockedOwner.Owner.Returns((OfflineOwnerModel?)null);
        await using var context = CreateContext(store, offlineOwner: lockedOwner);

        // Act
        var cut = context.Render<OfflineTripEditPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#offline-trip-edit-load-failed").Should().NotBeNull());
        await store.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRenderTheEditorFromTheLocalStore()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var trip = ActiveTrip(title: "Morning session", placeName: "Lough Corrib");
        var store = StoreWithTrip(trip);
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<OfflineTripEditPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-editor").Should().NotBeNull());
        cut.Find("#trip-editor-title").GetAttribute("value").Should().Be("Morning session");
    }

    [Fact]
    public async Task ItShouldReturnToTheOfflineDiaryWhenTheEditorCloses()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var trip = ActiveTrip();
        var store = StoreWithTrip(trip);
        await using var context = CreateContext(store);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var cut = context.Render<OfflineTripEditPage>(parameters => parameters.Add(p => p.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find("#trip-editor-cancel").Should().NotBeNull());

        // Act
        await cut.Find("#trip-editor-cancel").ClickAsync();

        // Assert
        navigation.Uri.Should().EndWith($"/offline/trips/{TripId:D}");
    }
}
