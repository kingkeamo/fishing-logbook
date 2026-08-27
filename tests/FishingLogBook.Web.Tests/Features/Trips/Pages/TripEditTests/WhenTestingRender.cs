using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TripEditPage = FishingLogBook.Web.Features.Trips.Pages.TripEdit.TripEdit;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.TripEditTests;

public class WhenTestingRender : BaseTripEditTest
{
    [Fact]
    public async Task ItShouldShowTheFailureWithRetryWhenTheLocalReadThrows()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("single-read timed out."));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TripEditPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-edit-load-failed").TextContent.Should().Contain("could not be loaded"));
    }

    [Fact]
    public async Task ItShouldReloadWhenRetryIsPressed()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        var reads = 0;
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                reads += 1;
                return reads == 1
                    ? throw new TimeoutException("single-read timed out.")
                    : Task.FromResult<TripModel?>(ActiveTrip());
            });
        await using var context = CreateContext(store);
        var cut = context.Render<TripEditPage>(parameters => parameters.Add(p => p.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find("#trip-edit-retry").Should().NotBeNull());

        // Act
        await cut.Find("#trip-edit-retry").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-editor").Should().NotBeNull());
        await store.Received(2).GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheUnavailableMessageWhenTheTripIsNotStored()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWithTrip(null);
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TripEditPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-edit-not-found").TextContent.Should().Contain("no longer available"));
        cut.FindAll("#trip-editor").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRenderTheEditorWithTheStoredTripAndOnlyItsOwnCatches()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var trip = ActiveTrip(title: "Morning session", placeName: "Lough Corrib");
        var store = StoreWithTrip(trip);
        var catchStore = QuietCatchStore(
            CatchFor(TripId, "Brown Trout"),
            CatchFor(Guid.NewGuid(), "Pike"),
            CatchFor(null, "Perch"));
        await using var context = CreateContext(store, catchStore: catchStore);

        // Act
        var cut = context.Render<TripEditPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-editor").Should().NotBeNull());
        cut.Find("#trip-editor-title").GetAttribute("value").Should().Be("Morning session");
        cut.Markup.Should().Contain("Brown Trout");
        cut.Markup.Should().NotContain("Pike");
        cut.Markup.Should().NotContain("Perch");
    }

    [Fact]
    public async Task ItShouldReturnToTheDiaryWhenTheEditorCloses()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var trip = ActiveTrip();
        var store = StoreWithTrip(trip);
        await using var context = CreateContext(store);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var cut = context.Render<TripEditPage>(parameters => parameters.Add(p => p.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find("#trip-editor-cancel").Should().NotBeNull());

        // Act
        await cut.Find("#trip-editor-cancel").ClickAsync();

        // Assert
        navigation.Uri.Should().EndWith($"/trips/{TripId:D}");
    }
}
