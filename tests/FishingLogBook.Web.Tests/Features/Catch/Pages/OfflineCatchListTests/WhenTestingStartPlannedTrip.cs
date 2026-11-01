using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Pages.OfflineCatchList;
using FishingLogBook.Web.Features.Trips.Modals.StartPlannedTrip;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.OfflineCatchListTests;

public class WhenTestingStartPlannedTrip : BaseOfflineCatchListTest
{
    private static readonly Guid PlannedTripId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly DateTimeOffset PlannedStart = DateTimeOffset.Parse("2026-09-12T06:30:00Z");

    [Fact]
    public async Task ItShouldShowAnErrorAndNeverStartANewTripWhenEligibilityFails()
    {
        // Arrange
        var activeTrip = ActiveTripService();
        var planned = PlannedTrips();
        planned.GetEligibleAsync(OwnerUserId, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("lookup"));
        var modals = Substitute.For<IModalService>();
        await using var context = CreateContext(EmptyCatchStore(), modalService: modals, plannedTrips: planned);
        context.Services.AddSingleton(activeTrip);
        var cut = context.Render<OfflineCatchList>();
        cut.WaitForAssertion(() => cut.Find("#offline-trip-start-link").Should().NotBeNull());

        // Act
        await cut.Find("#offline-trip-start-link").ClickAsync();

        // Assert
        await activeTrip.DidNotReceive().StartAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await modals.Received(1).ShowMessageAsync(Arg.Is<MessageModalModel>(message => message.Severity == ModalSeverity.Error), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStartANewTripWithoutAModalWhenNothingIsPlanned()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = ActiveTripService();
        var modals = Substitute.For<IModalService>();
        await using var context = CreateContext(EmptyCatchStore(), modalService: modals);
        context.Services.AddSingleton(activeTrip);
        var cut = context.Render<OfflineCatchList>();
        cut.WaitForAssertion(() => cut.Find("#offline-trip-start-link").Should().NotBeNull());

        // Act
        await cut.Find("#offline-trip-start-link").ClickAsync();

        // Assert
        await activeTrip.Received(1).StartAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await modals.DidNotReceive().ShowAsync<StartPlannedTripModal, StartPlannedTripModalModel, StartPlannedTripModalResult>(
            Arg.Any<StartPlannedTripModalModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDoNothingWhenThePlannedTripModalIsDismissed()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = ActiveTripService();
        var planned = PlannedTrips(Option());
        await using var context = CreateContext(
            EmptyCatchStore(),
            plannedTrips: planned,
            modalService: ModalReturning(null));
        context.Services.AddSingleton(activeTrip);
        var cut = context.Render<OfflineCatchList>();
        cut.WaitForAssertion(() => cut.Find("#offline-trip-start-link").Should().NotBeNull());

        // Act
        await cut.Find("#offline-trip-start-link").ClickAsync();

        // Assert
        await activeTrip.DidNotReceive().StartAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await planned.DidNotReceive().ActivateAsync(
            Arg.Any<Guid>(),
            Arg.Any<PlannedTripOptionModel>(),
            Arg.Any<CancellationToken>());
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().NotContain("/trips/");
    }

    [Fact]
    public async Task ItShouldStartANewTripWhenStartNewTripIsChosenInTheModal()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = ActiveTripService();
        var planned = PlannedTrips(Option());
        await using var context = CreateContext(
            EmptyCatchStore(),
            plannedTrips: planned,
            modalService: ModalReturning(new StartPlannedTripModalResult(Selected: null)));
        context.Services.AddSingleton(activeTrip);
        var cut = context.Render<OfflineCatchList>();
        cut.WaitForAssertion(() => cut.Find("#offline-trip-start-link").Should().NotBeNull());

        // Act
        await cut.Find("#offline-trip-start-link").ClickAsync();

        // Assert
        await activeTrip.Received(1).StartAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await planned.DidNotReceive().ActivateAsync(
            Arg.Any<Guid>(),
            Arg.Any<PlannedTripOptionModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldActivateTheChosenPlannedTripAndNavigateToTheOfflineRoute()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = ActiveTripService();
        var option = Option();
        var planned = PlannedTrips(option);
        planned.ActivateAsync(OwnerUserId, option, Arg.Any<CancellationToken>())
            .Returns(new TripModel(PlannedTripId, OwnerUserId, TripConstants.Active, PlannedStart.AddMinutes(5)));
        var modals = ModalReturning(new StartPlannedTripModalResult(option));
        await using var context = CreateContext(EmptyCatchStore(), plannedTrips: planned, modalService: modals);
        context.Services.AddSingleton(activeTrip);
        var cut = context.Render<OfflineCatchList>();
        cut.WaitForAssertion(() => cut.Find("#offline-trip-start-link").Should().NotBeNull());

        // Act
        await cut.Find("#offline-trip-start-link").ClickAsync();

        // Assert
        await modals.Received(1).ShowAsync<StartPlannedTripModal, StartPlannedTripModalModel, StartPlannedTripModalResult>(
            Arg.Is<StartPlannedTripModalModel>(model => model.Trips.Count == 1 && model.Trips[0] == option),
            Arg.Any<CancellationToken>());
        await planned.Received(1).ActivateAsync(OwnerUserId, option, Arg.Any<CancellationToken>());
        await activeTrip.DidNotReceive().StartAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        context.Services.GetRequiredService<NavigationManager>().Uri
            .Should().EndWith($"/offline/trips/{PlannedTripId:D}");
    }

    [Fact]
    public async Task ItShouldReportAFailureToActivateAPlannedTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = ActiveTripService();
        var option = Option();
        var failure = new InvalidOperationException("not the organiser");
        var planned = PlannedTrips(option);
        planned.ActivateAsync(OwnerUserId, option, Arg.Any<CancellationToken>()).ThrowsAsync(failure);
        var modals = ModalReturning(new StartPlannedTripModalResult(option));
        var logging = QuietLogging();
        await using var context = CreateContext(
            EmptyCatchStore(),
            logging: logging,
            plannedTrips: planned,
            modalService: modals);
        context.Services.AddSingleton(activeTrip);
        var cut = context.Render<OfflineCatchList>();
        cut.WaitForAssertion(() => cut.Find("#offline-trip-start-link").Should().NotBeNull());

        // Act
        await cut.Find("#offline-trip-start-link").ClickAsync();

        // Assert
        await modals.Received(1).ShowMessageAsync(
            Arg.Is<MessageModalModel>(model => model.Message == "The trip could not be started. Check your connection and try Start fishing again."
                && model.Severity == ModalSeverity.Error),
            Arg.Any<CancellationToken>());
        await logging.Received(1).LogErrorAsync("starting a planned offline trip", failure, Arg.Any<CancellationToken>());
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().NotContain("/trips/");
    }

    private static PlannedTripOptionModel Option()
    {
        return new PlannedTripOptionModel(PlannedTripId, PlannedStart, OwnerUserId, "Alex", IsOrganiser: true)
        {
            Title = "Corrib"
        };
    }

    private static IPlannedTripService PlannedTrips(params PlannedTripOptionModel[] options)
    {
        var service = Substitute.For<IPlannedTripService>();
        service.GetEligibleAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<PlannedTripOptionModel>)options);
        return service;
    }

    private static IModalService ModalReturning(StartPlannedTripModalResult? result)
    {
        var modals = Substitute.For<IModalService>();
        modals.ShowAsync<StartPlannedTripModal, StartPlannedTripModalModel, StartPlannedTripModalResult>(
                Arg.Any<StartPlannedTripModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return modals;
    }

    private static IActiveTripService ActiveTripService()
    {
        var service = Substitute.For<IActiveTripService>();
        service.GetActiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TripModel?)null);
        service.StartAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(new TripModel(Guid.NewGuid(), OwnerUserId, TripConstants.Active, PlannedStart));
        return service;
    }

    private static FishingLogBook.Web.Features.Catch.Offline.Stores.ICatchStore EmptyCatchStore()
    {
        var store = Substitute.For<FishingLogBook.Web.Features.Catch.Offline.Stores.ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<FishingLogBook.Web.Features.Catch.Models.CatchModel>());
        return store;
    }
}
