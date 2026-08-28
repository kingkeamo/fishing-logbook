using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TripListPage = FishingLogBook.Web.Features.Trips.Pages.TripList.TripList;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.TripListTests;

public class WhenTestingRender : BaseTripListTest
{
    [Fact]
    public async Task ItShouldShowTheFailureWithRetryWhenNeitherSourceCanBeRead()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("IndexedDB unavailable."));
        var client = Substitute.For<ITripClient>();
        client.GetMyAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("offline"));
        var logging = QuietLogging();
        await using var context = CreateContext(store, client, logging: logging);

        // Act
        var cut = context.Render<TripListPage>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-list-load-failed").TextContent.Should()
                .Contain("Your trips could not be loaded."));
        cut.Find("#trip-list-retry").Should().NotBeNull();
        await logging.Received(1).LogErrorAsync(
            "reading local trips",
            Arg.Any<InvalidOperationException>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillShowTheLocalTripsWhenTheServerCannotBeReached()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWith(LocalTrip(placeName: "Lough Corrib"));
        var client = Substitute.For<ITripClient>();
        client.GetMyAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("offline"));
        await using var context = CreateContext(store, client);

        // Act
        var cut = context.Render<TripListPage>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-list-item-{LocalTripId:D}").Should().NotBeNull());
        cut.Find($"#trip-list-place-{LocalTripId:D}").TextContent.Should().Contain("Lough Corrib");
        cut.Find("#trip-list-load-failed").Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldSayTheAnglerHasNoTripsYet()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWith();
        var client = ClientWith();
        await using var context = CreateContext(store, client);

        // Act
        var cut = context.Render<TripListPage>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-list-empty").TextContent.Should()
                .Contain("You have not started a trip yet."));
        cut.FindAll("#trip-list").Should().BeEmpty();
        await client.Received(1).GetMyAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheServerCountsForAHistoricalTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWith();
        var client = ClientWith(RemoteTrip(
            placeName: "Lough Mask",
            catchCount: 3,
            photographCount: 2,
            noteCount: 1));
        await using var context = CreateContext(store, client);

        // Act
        var cut = context.Render<TripListPage>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-list-catches-{RemoteTripId:D}").TextContent.Should().Contain("3 catches"));
        cut.Find($"#trip-list-photographs-{RemoteTripId:D}").TextContent.Should().Contain("2 photos");
        cut.Find($"#trip-list-notes-{RemoteTripId:D}").TextContent.Should().Contain("1 note");
        cut.Find($"#trip-list-place-{RemoteTripId:D}").TextContent.Should().Contain("Lough Mask");
        cut.Find($"#trip-list-view-{RemoteTripId:D}").GetAttribute("href")
            .Should().Be($"/trips/{RemoteTripId:D}");
        cut.FindAll($"#trip-list-active-{RemoteTripId:D}").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldPreferTheLocalCopyOfATripTheServerAlsoKnows()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWith(LocalTrip(tripId: RemoteTripId, placeName: "River Moy"));
        var client = ClientWith(RemoteTrip(
            tripId: RemoteTripId,
            placeName: "Lough Mask",
            catchCount: 5));
        var catchStore = QuietCatchStore(Catch(RemoteTripId));
        await using var context = CreateContext(store, client, catchStore);

        // Act
        var cut = context.Render<TripListPage>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-list-place-{RemoteTripId:D}").TextContent.Should().Contain("River Moy"));
        cut.Find($"#trip-list-catches-{RemoteTripId:D}").TextContent.Should().Contain("1 catch");
        cut.Find($"#trip-list-active-{RemoteTripId:D}").Should().NotBeNull();
        cut.FindAll("#trip-list .trip-list-card").Should().HaveCount(1);
    }

    [Fact]
    public async Task ItShouldListTheActiveTripFirstThenNewestCompletedTrips()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var olderId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
        var store = StoreWith(LocalTrip(startedOn: StartedOn.AddDays(-5)));
        var client = ClientWith(
            RemoteTrip(startedOn: StartedOn.AddDays(-1)),
            RemoteTrip(tripId: olderId, startedOn: StartedOn.AddDays(-4)));
        await using var context = CreateContext(store, client);

        // Act
        var cut = context.Render<TripListPage>();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#trip-list .trip-list-card").Should().HaveCount(3));
        var ids = cut.FindAll("#trip-list .trip-list-card").Select(card => card.Id).ToArray();
        ids.Should().Equal(
            $"trip-list-item-{LocalTripId:D}",
            $"trip-list-item-{RemoteTripId:D}",
            $"trip-list-item-{olderId:D}");
        cut.Find($"#trip-list-view-{LocalTripId:D}").TextContent.Should().Contain("Update trip");
        cut.Find($"#trip-list-view-{RemoteTripId:D}").TextContent.Should().Contain("View trip");
    }

    [Fact]
    public async Task ItShouldCountTheLocalPhotographsAndNotesOfATripThatIsStillOnTheDevice()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWith(LocalTrip(
            photographs:
            [
                new Web.Features.Trips.Models.TripPhotographModel(
                    Guid.NewGuid(),
                    LocalTripId,
                    OwnerUserId,
                    PhotographContentTypeConstants.Jpeg,
                    StartedOn.AddMinutes(20))
            ],
            notes:
            [
                new Web.Features.Trips.Models.TripNoteModel(
                    Guid.NewGuid(),
                    LocalTripId,
                    OwnerUserId,
                    "The wind dropped.",
                    StartedOn.AddMinutes(10))
            ]));
        var client = ClientWith();
        await using var context = CreateContext(store, client, QuietCatchStore(Catch(LocalTripId)));

        // Act
        var cut = context.Render<TripListPage>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-list-photographs-{LocalTripId:D}").TextContent.Should().Contain("1 photo"));
        cut.Find($"#trip-list-notes-{LocalTripId:D}").TextContent.Should().Contain("1 note");
        cut.Find($"#trip-list-catches-{LocalTripId:D}").TextContent.Should().Contain("1 catch");
    }

    [Fact]
    public async Task ItShouldShowTheStartedDateWhenTheTripHasNoTitle()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWith(LocalTrip(placeName: "Ballynahinch"));
        var client = ClientWith();
        await using var context = CreateContext(store, client);

        // Act
        var cut = context.Render<TripListPage>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-list-date-{LocalTripId:D}").TextContent.Should().Contain("27 Aug 2026"));
        cut.FindAll($"#trip-list-title-{LocalTripId:D}").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldKeepTheStartedDateWhenTheTripHasATitle()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWith(LocalTrip(title: "First day of mayfly", placeName: "Lough Corrib"));
        var client = ClientWith();
        await using var context = CreateContext(store, client);

        // Act
        var cut = context.Render<TripListPage>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-list-date-{LocalTripId:D}").TextContent.Should().Contain("27 Aug 2026"));
        var heading = cut.Find($"#trip-list-item-{LocalTripId:D} .trip-list-heading-group").TextContent;
        heading.Should().Contain("27 Aug 2026");
        heading.Should().Contain("First day of mayfly");
        heading.IndexOf("27 Aug 2026", StringComparison.Ordinal)
            .Should().BeLessThan(heading.IndexOf("First day of mayfly", StringComparison.Ordinal));
        var title = cut.Find($"#trip-list-title-{LocalTripId:D}");
        var date = cut.Find($"#trip-list-date-{LocalTripId:D}");
        title.TextContent.Should().Contain("First day of mayfly");
        title.ClassName.Should().NotContain("mud-text-secondary");
        date.ClassName.Should().NotContain("mud-text-secondary");
        title.ClassName.Should().Contain("mud-typography-subtitle1");
        date.ClassName.Should().Contain("mud-typography-subtitle1");
        cut.Find($"#trip-list-item-{LocalTripId:D} .trip-list-heading-row").ClassName.Should().Contain("flex-row");
        cut.Find($"#trip-list-place-{LocalTripId:D}").TextContent.Should().Contain("Lough Corrib");
        cut.Find($"#trip-list-place-{LocalTripId:D}").ClassName.Should().Contain("mud-text-secondary");
    }

    [Fact]
    public async Task ItShouldShowFrenchCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var store = StoreWith();
        var client = ClientWith();
        await using var context = CreateContext(store, client);

        // Act
        var cut = context.Render<TripListPage>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-list-heading").TextContent.Should().Contain("Vos sorties"));
        cut.Find("#trip-list-empty").TextContent.Should()
            .Contain("Vous n'avez pas encore commencé de sortie.");
    }
}
