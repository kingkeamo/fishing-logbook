using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Components.TripCatches;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripCatchesTests;

public class WhenTestingAddExistingCatches : BaseTripCatchesTest
{
    [Fact]
    public async Task ItShouldOfferRecordCatchForThisTripWithoutReadingAnyCatches()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWith();
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TripCatches>(parameters => parameters
            .Add(component => component.Trip, Trip())
            .Add(component => component.RecordCatchBaseHref, "/offline/record"));

        // Assert
        cut.Find("#trip-catches-record").GetAttribute("href")
            .Should().Be($"/offline/record?tripId={TripId:D}");
        await store.DidNotReceive().GetMetadataAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheFailureWhenTheUnassignedCatchesCannotBeRead()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("IndexedDB unavailable."));
        var logging = QuietLogging();
        await using var context = CreateContext(store, logging);
        var cut = context.Render<TripCatches>(parameters => parameters
            .Add(component => component.Trip, Trip()));

        // Act
        cut.Find("#trip-catches-add").Click();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-catches-failed").Should().NotBeNull());
        await logging.Received(1).LogErrorAsync(
            "reading catches that are not on a trip",
            Arg.Any<InvalidOperationException>(),
            Arg.Any<CancellationToken>());
        await store.DidNotReceive().UpdateTripAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOnlyOfferCatchesThatAreNotAlreadyOnATrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWith(
            Catch(PikeCatchId, "Pike"),
            Catch(TrippedCatchId, "Brown Trout", Guid.NewGuid()));
        await using var context = CreateContext(store);
        var cut = context.Render<TripCatches>(parameters => parameters
            .Add(component => component.Trip, Trip()));

        // Act
        cut.Find("#trip-catches-add").Click();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-selector-option-{PikeCatchId:D}").Should().NotBeNull());
        cut.FindAll($"#catch-selector-option-{TrippedCatchId:D}").Should().BeEmpty();
        await store.Received(1).GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSayWhenEveryCatchIsAlreadyOnATrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWith(Catch(TrippedCatchId, "Brown Trout", Guid.NewGuid()));
        await using var context = CreateContext(store);
        var cut = context.Render<TripCatches>(parameters => parameters
            .Add(component => component.Trip, Trip()));

        // Act
        cut.Find("#trip-catches-add").Click();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-selector-empty").TextContent.Should()
                .Contain("Every catch is already on a trip."));
        cut.FindAll("#catch-selector-confirm").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldCloseThePickerWithoutAttachingAnything()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWith(Catch(PikeCatchId, "Pike"));
        await using var context = CreateContext(store);
        var cut = context.Render<TripCatches>(parameters => parameters
            .Add(component => component.Trip, Trip()));
        cut.Find("#trip-catches-add").Click();
        cut.WaitForAssertion(() => cut.Find("#trip-catches-cancel").Should().NotBeNull());

        // Act
        cut.Find("#trip-catches-cancel").Click();

        // Assert
        cut.FindAll("#trip-catches-picker").Should().BeEmpty();
        cut.Find("#trip-catches-add").Should().NotBeNull();
        await store.DidNotReceive().UpdateTripAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheFailureWhenAttachingACatchFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWith(Catch(PikeCatchId, "Pike"));
        store.UpdateTripAsync(
                OwnerUserId,
                PikeCatchId,
                TripId,
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("IndexedDB unavailable."));
        var logging = QuietLogging();
        var attached = 0;
        await using var context = CreateContext(store, logging);
        var cut = context.Render<TripCatches>(parameters => parameters
            .Add(component => component.Trip, Trip())
            .Add(component => component.OnCatchesAttached, () => attached++));
        cut.Find("#trip-catches-add").Click();
        cut.WaitForAssertion(() => cut.Find($"#catch-selector-option-{PikeCatchId:D}").Should().NotBeNull());
        cut.Find($"#catch-selector-option-{PikeCatchId:D}").Change(true);

        // Act
        cut.Find("#catch-selector-confirm").Click();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-catches-failed").Should().NotBeNull());
        attached.Should().Be(0);
        await logging.Received(1).LogErrorAsync(
            "adding catches to a trip",
            Arg.Any<InvalidOperationException>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAttachEverySelectedCatchToThisTripAndTellTheParent()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWith(Catch(PikeCatchId, "Pike"), Catch(TroutCatchId, "Brown Trout"));
        var attached = 0;
        await using var context = CreateContext(store);
        var cut = context.Render<TripCatches>(parameters => parameters
            .Add(component => component.Trip, Trip())
            .Add(component => component.OnCatchesAttached, () => attached++));
        cut.Find("#trip-catches-add").Click();
        cut.WaitForAssertion(() => cut.Find($"#catch-selector-option-{PikeCatchId:D}").Should().NotBeNull());
        cut.Find($"#catch-selector-option-{PikeCatchId:D}").Change(true);
        cut.Find($"#catch-selector-option-{TroutCatchId:D}").Change(true);

        // Act
        cut.Find("#catch-selector-confirm").Click();

        // Assert
        cut.WaitForAssertion(() => attached.Should().Be(1));
        cut.FindAll("#trip-catches-picker").Should().BeEmpty();
        await store.Received(1).UpdateTripAsync(
            OwnerUserId,
            PikeCatchId,
            TripId,
            Arg.Any<CancellationToken>());
        await store.Received(1).UpdateTripAsync(
            OwnerUserId,
            TroutCatchId,
            TripId,
            Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
        await store.DidNotReceive().GetAllAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
