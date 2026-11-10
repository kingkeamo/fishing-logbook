using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.Features.Trips.Pages.TripEditTests;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OfflineTripEditPage = FishingLogBook.Web.Features.Trips.Pages.OfflineTripEdit.OfflineTripEdit;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.OfflineTripEditTests;

public class WhenTestingResilience : BaseTripEditTest
{
    [Fact]
    public async Task ItShouldSayTheTripWasNotFoundWhenItIsNotInTheLocalStore()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWithTrip(null);
        var catchStore = QuietCatchStore();
        await using var context = CreateContext(store, catchStore);

        // Act
        var cut = context.Render<OfflineTripEditPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#offline-trip-edit-not-found").Should().NotBeNull());
        await store.Received(1).GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>());
        await catchStore.DidNotReceive().GetMetadataAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillEditTheTripAndLogWhenTheLocalCatchesCannotBeRead()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchStore = Substitute.For<ICatchStore>();
        catchStore.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("catches timed out"));
        var logging = Substitute.For<ILoggingService>();
        await using var context = CreateContext(StoreWithTrip(ActiveTrip()), catchStore, logging: logging);

        // Act
        var cut = context.Render<OfflineTripEditPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-editor").Should().NotBeNull());
        cut.Find("#trip-editor-catches-empty").Should().NotBeNull();
        await logging.Received(1).LogErrorAsync(
            "reading the catches of a trip for editing offline",
            Arg.Is<Exception>(exception => exception.Message == "catches timed out"),
            CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldStillEditTheTripAndLogWhenThePreferencesCannotBeRead()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var preferences = Substitute.For<IAnglerPreferencesProvider>();
        preferences.GetAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("no prefs"));
        var logging = Substitute.For<ILoggingService>();
        await using var context = CreateContext(
            StoreWithTrip(ActiveTrip()),
            logging: logging,
            anglerPreferences: preferences);

        // Act
        var cut = context.Render<OfflineTripEditPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-editor").Should().NotBeNull());
        await logging.Received(1).LogErrorAsync(
            "reading angler preferences for a trip offline",
            Arg.Is<Exception>(exception => exception.Message == "no prefs"),
            CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldListOnlyTheCatchesOfThisTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var linked = CatchFor(TripId);
        var unrelated = CatchFor(Guid.NewGuid());
        var standalone = CatchFor(null);
        await using var context = CreateContext(
            StoreWithTrip(ActiveTrip()),
            QuietCatchStore(linked, unrelated, standalone));

        // Act
        var cut = context.Render<OfflineTripEditPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find($"#trip-editor-catch-{linked.Id:D}").Should().NotBeNull());
        cut.FindAll($"#trip-editor-catch-{unrelated.Id:D}").Should().BeEmpty();
        cut.FindAll($"#trip-editor-catch-{standalone.Id:D}").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReloadTheTripWhenTheAnglerRetriesAfterAFailedLoad()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException<TripModel?>(new TimeoutException("first")),
                _ => Task.FromResult<TripModel?>(ActiveTrip()));
        await using var context = CreateContext(store);
        var cut = context.Render<OfflineTripEditPage>(parameters => parameters.Add(p => p.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find("#offline-trip-edit-retry").Should().NotBeNull());

        // Act
        await cut.Find("#offline-trip-edit-retry").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-editor").Should().NotBeNull());
        cut.FindAll("#offline-trip-edit-load-failed").Should().BeEmpty();
        await store.Received(2).GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>());
    }
}
