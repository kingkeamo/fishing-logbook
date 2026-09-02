using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.RecordCatch;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.RecordCatchTests;

public class WhenTestingTripAssociation : BaseRecordCatchTest
{
    private static readonly Guid TripId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid OtherTripId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T09:00:00Z");

    [Fact]
    public async Task ItShouldShowNoTripUiAndSaveStandaloneWhenNoTripIsActive()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        await using var context = CreateContext(store);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.FindAll("#catch-trip-association").Should().BeEmpty();
        cut.FindAll("#catch-trip-standalone").Should().BeEmpty();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.TripId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStayUsableWhenTheActiveTripLookupFails()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        var logging = QuietLogging();
        var activeTrip = Substitute.For<IActiveTripService>();
        activeTrip.GetActiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("IndexedDB unavailable."));
        await using var context = CreateContext(store, logging: logging, activeTrip: activeTrip);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.FindAll("#catch-trip-association").Should().BeEmpty();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.TripId == null),
            Arg.Any<CancellationToken>());
        await logging.Received(1).LogErrorAsync(
            "resolving the active trip",
            Arg.Is<Exception>(exception => exception.Message == "IndexedDB unavailable."),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheActiveTripAndSaveTheCatchIntoIt()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        await using var context = CreateContext(
            store,
            tripStore: TripStoreWith(Trip(title: "Evening session")),
            activeTrip: ActiveTrip(Trip(title: "Evening session")));
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.TripId == TripId
                && catchRecord.CaughtByUserId == OwnerUserId
                && catchRecord.Location == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRenderTheTripNameAndOptOutBeforeSaving()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        await using var context = CreateContext(
            store,
            activeTrip: ActiveTrip(Trip(title: "Evening session")));

        // Act
        var cut = context.Render<RecordCatch>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-trip-association").TextContent.Should().Contain("Recording in");
            cut.Find("#catch-trip-name").TextContent.Should().Contain("Evening session");
            cut.Find("#catch-trip-leave").TextContent.Should().Contain("Remove from this trip");
        });
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheGeneratedNameAndPlaceWhenTheTripHasNoTitle()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        await using var context = CreateContext(
            store,
            activeTrip: ActiveTrip(Trip(placeName: "Corrib shoreline")));

        // Act
        var cut = context.Render<RecordCatch>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            var name = cut.Find("#catch-trip-name").TextContent;
            name.Should().Contain("Corrib shoreline");
            name.Should().Contain("2026");
            name.Should().NotContain("53.2");
            name.Should().NotContain("-9.0");
        });
    }

    [Fact]
    public async Task ItShouldNeverRenderTheTripCoordinates()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        await using var context = CreateContext(
            store,
            activeTrip: ActiveTrip(Trip(withLocation: true)));

        // Act
        var cut = context.Render<RecordCatch>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-trip-association").Should().NotBeNull());
        cut.Markup.Should().NotContain("53.2707");
        cut.Markup.Should().NotContain("-9.0568");
    }

    [Fact]
    public async Task ItShouldSaveStandaloneAfterOptingOut()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        await using var context = CreateContext(store, activeTrip: ActiveTrip(Trip()));
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));
        cut.WaitForAssertion(() => cut.Find("#catch-trip-leave").Should().NotBeNull());

        // Act
        await cut.Find("#catch-trip-leave").ClickAsync();

        // Assert
        cut.FindAll("#catch-trip-association").Should().BeEmpty();
        cut.Find("#catch-trip-standalone").TextContent
            .Should().Contain("Not part of a trip");
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.TripId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheOptOutForTheNextCatchInTheSameSession()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        var activeTrip = ActiveTrip(Trip());
        await using var context = CreateContext(store, activeTrip: activeTrip);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("first.jpg", 0xFF, 0xD8, 0xFF));
        cut.WaitForAssertion(() => cut.Find("#catch-trip-leave").Should().NotBeNull());
        await cut.Find("#catch-trip-leave").ClickAsync();
        await cut.Find("#save-catch-button").ClickAsync();
        cut.WaitForAssertion(() => cut.Find("#catch-record-another").Should().NotBeNull());

        // Act
        await cut.Find("#catch-record-another").ClickAsync();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("second.jpg", 0xFF, 0xD8, 0xFF));
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.FindAll("#catch-trip-association").Should().BeEmpty();
        await store.Received(2).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.TripId == null),
            Arg.Any<CancellationToken>());
        await activeTrip.Received(1).GetActiveAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotReattachWhenTheActiveTripChangesWhileTheEditorIsOpen()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        var tripStore = Substitute.For<ITripStore>();
        tripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(Trip());
        var activeTrip = ActiveTrip(Trip());
        await using var context = CreateContext(store, tripStore: tripStore, activeTrip: activeTrip);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));
        cut.WaitForAssertion(() => cut.Find("#catch-trip-leave").Should().NotBeNull());

        // Act
        activeTrip.GetActiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Trip(OtherTripId));
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.TripId == TripId),
            Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.TripId == OtherTripId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheAssociationWhenTheTripFinishesWhileTheEditorIsOpen()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        var tripStore = Substitute.For<ITripStore>();
        tripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(Trip(status: TripConstants.Completed));
        await using var context = CreateContext(
            store,
            tripStore: tripStore,
            activeTrip: ActiveTrip(Trip()));
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));
        cut.WaitForAssertion(() => cut.Find("#catch-trip-leave").Should().NotBeNull());

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.FindAll("#catch-trip-unavailable").Should().BeEmpty();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.TripId == TripId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseToSaveWhenTheSelectedTripHasVanished()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var tripStore = Substitute.For<ITripStore>();
        tripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns((TripModel?)null);
        await using var context = CreateContext(
            store,
            tripStore: tripStore,
            activeTrip: ActiveTrip(Trip()));
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));
        cut.WaitForAssertion(() => cut.Find("#catch-trip-leave").Should().NotBeNull());

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.Find("#catch-trip-unavailable").TextContent
            .Should().Contain("no longer on this device");
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
        cut.FindAll("#catch-saved").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRecoverFromAVanishedTripByOptingOut()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        var tripStore = Substitute.For<ITripStore>();
        tripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns((TripModel?)null);
        await using var context = CreateContext(
            store,
            tripStore: tripStore,
            activeTrip: ActiveTrip(Trip()));
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));
        cut.WaitForAssertion(() => cut.Find("#catch-trip-leave").Should().NotBeNull());
        await cut.Find("#save-catch-button").ClickAsync();

        // Act
        await cut.Find("#catch-trip-leave").ClickAsync();
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.FindAll("#catch-trip-unavailable").Should().BeEmpty();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.TripId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldUseTheRequestedTripRatherThanTheActiveOne()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        var tripStore = Substitute.For<ITripStore>();
        tripStore.GetAsync(OwnerUserId, OtherTripId, Arg.Any<CancellationToken>())
            .Returns(Trip(OtherTripId, status: TripConstants.Completed));
        var activeTrip = ActiveTrip(Trip());
        await using var context = CreateContext(store, tripStore: tripStore, activeTrip: activeTrip);
        NavigateToTrip(context, OtherTripId);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.TripId == OtherTripId),
            Arg.Any<CancellationToken>());
        await activeTrip.DidNotReceive().GetActiveAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreARequestedTripOwnedByAnotherAngler()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        var tripStore = Substitute.For<ITripStore>();
        tripStore.GetAsync(OwnerUserId, OtherTripId, Arg.Any<CancellationToken>())
            .Returns((TripModel?)null);
        await using var context = CreateContext(store, tripStore: tripStore);
        NavigateToTrip(context, OtherTripId);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.FindAll("#catch-trip-association").Should().BeEmpty();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.TripId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchTripCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var store = Substitute.For<ICatchStore>();
        await using var context = CreateContext(store, activeTrip: ActiveTrip(Trip()));

        // Act
        var cut = context.Render<RecordCatch>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-trip-association").TextContent.Should().Contain("Sortie en cours");
            cut.Find("#catch-trip-leave").TextContent.Should().Contain("Retirer de cette sortie");
        });
    }

    [Fact]
    public async Task ItShouldOfferTheOptOutAsAButtonRatherThanALabel()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        await using var context = CreateContext(store, activeTrip: ActiveTrip(Trip()));

        // Act
        var cut = context.Render<RecordCatch>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            var leave = cut.Find("#catch-trip-leave");
            leave.TagName.Should().Be("BUTTON");
            leave.ClassList.Should().Contain("mud-button-outlined");
            leave.QuerySelector(".mud-icon-root").Should().NotBeNull();
        });
    }

    [Fact]
    public async Task ItShouldLetTheAnglerPutTheCatchBackOnTheTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        await using var context = CreateContext(
            store,
            tripStore: TripStoreWith(Trip()),
            activeTrip: ActiveTrip(Trip()));
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));
        cut.WaitForAssertion(() => cut.Find("#catch-trip-leave").Should().NotBeNull());
        await cut.Find("#catch-trip-leave").ClickAsync();

        // Act
        var rejoin = cut.Find("#catch-trip-rejoin");
        rejoin.TagName.Should().Be("BUTTON");
        await rejoin.ClickAsync();

        // Assert
        cut.Find("#catch-trip-association").Should().NotBeNull();
        cut.FindAll("#catch-trip-standalone").Should().BeEmpty();
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.TripId == TripId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotOfferPuttingTheCatchBackWhenThereWasNoTrip()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<RecordCatch>();

        // Assert
        cut.FindAll("#catch-trip-rejoin").Should().BeEmpty();
        cut.FindAll("#catch-trip-standalone-row").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotOfferViewingATripAfterSavingAStandaloneCatch()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        await using var context = CreateContext(store);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-view-catches").Should().NotBeNull());
        cut.FindAll("#catch-view-trip").Should().BeEmpty();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.TripId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOfferViewingTheTripAfterSavingACatchOntoIt()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        var trip = new TripModel(TripId, OwnerUserId, TripConstants.Active, StartedOn);
        await using var context = CreateContext(
            store,
            tripStore: TripStoreWith(trip),
            activeTrip: ActiveTrip(trip));
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-view-trip").GetAttribute("href").Should().Be($"/trips/{TripId:D}"));
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.TripId == TripId),
            Arg.Any<CancellationToken>());
    }

    private static void NavigateToTrip(BunitContext context, Guid tripId)
    {
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(
            navigation.GetUriWithQueryParameter("tripId", tripId.ToString("D")));
    }

    private static IActiveTripService ActiveTrip(TripModel trip)
    {
        var activeTrip = Substitute.For<IActiveTripService>();
        activeTrip.GetActiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(trip);
        return activeTrip;
    }

    private static ITripStore TripStoreWith(TripModel trip)
    {
        var tripStore = Substitute.For<ITripStore>();
        tripStore.GetAsync(OwnerUserId, trip.Id, Arg.Any<CancellationToken>()).Returns(trip);
        return tripStore;
    }

    private static TripModel Trip(
        Guid? tripId = null,
        string status = TripConstants.Active,
        string? title = null,
        string? placeName = null,
        bool withLocation = false)
    {
        return new TripModel(
            tripId ?? TripId,
            OwnerUserId,
            status,
            StartedOn,
            status == TripConstants.Completed ? StartedOn.AddHours(3) : null,
            title,
            placeName,
            withLocation
                ? new TripLocationModel(
                    53.2707,
                    -9.0568,
                    7,
                    StartedOn,
                    "DeviceGps",
                    "Private",
                    "1")
                : null,
            SyncStatus.SavedLocally);
    }

    [Fact]
    public async Task ItShouldShowASharedTripAParticipantWasInvitedToAsTheActiveTrip()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        var sharedTrip = SharedTrip();
        await using var context = CreateContext(store, activeTrip: ActiveTrip(sharedTrip));
        var cut = context.Render<RecordCatch>();

        // Assert
        cut.Find("#catch-trip-association").Should().NotBeNull();
        cut.Find("#catch-trip-name").TextContent.Should().Contain(sharedTrip.PlaceName);
    }

    [Fact]
    public async Task ItShouldSaveACatchIntoASharedTripAParticipantIsRecordingInto()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        var sharedTrip = SharedTrip();
        await using var context = CreateContext(
            store,
            activeTrip: ActiveTrip(sharedTrip),
            tripStore: TripStoreWith(sharedTrip));
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.TripId == sharedTrip.Id),
            Arg.Any<CancellationToken>());
    }

    private static TripModel SharedTrip()
    {
        return Trip() with
        {
            OwnerUserId = OtherUserId,
            PlaceName = "Costello & Fermoyle",
            ParticipantUserIds = [OwnerUserId],
            Origin = TripOriginEnum.Server
        };
    }
}
