using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchList;
using FishingLogBook.Web.Features.Trips.Modals.StartPlannedTrip;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchListTests;

public class WhenTestingStartPlannedTrip : BaseCatchListTest
{
    private static readonly Guid PlannedTripId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly DateTimeOffset PlannedStart = DateTimeOffset.Parse("2026-09-12T06:30:00Z");

    [Fact]
    public async Task ItShouldStartANewTripWithoutAModalWhenNothingIsPlanned()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = ActiveTripService();
        var modals = Substitute.For<IModalService>();
        await using var context = CreateContext(EmptyStore(), modalService: modals);
        context.Services.AddSingleton(activeTrip);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#trip-start-link").Should().NotBeNull());

        // Act
        await cut.Find("#trip-start-link").ClickAsync();

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
        var modals = ModalReturning(null);
        await using var context = CreateContext(EmptyStore(), modalService: modals, plannedTrips: planned);
        context.Services.AddSingleton(activeTrip);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#trip-start-link").Should().NotBeNull());

        // Act
        await cut.Find("#trip-start-link").ClickAsync();

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
        var modals = ModalReturning(new StartPlannedTripModalResult(Selected: null));
        await using var context = CreateContext(EmptyStore(), modalService: modals, plannedTrips: planned);
        context.Services.AddSingleton(activeTrip);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#trip-start-link").Should().NotBeNull());

        // Act
        await cut.Find("#trip-start-link").ClickAsync();

        // Assert
        await activeTrip.Received(1).StartAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await planned.DidNotReceive().ActivateAsync(
            Arg.Any<Guid>(),
            Arg.Any<PlannedTripOptionModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheModalWithTheEligibleTripsAndActivateTheChosenOne()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = ActiveTripService();
        var option = Option();
        var planned = PlannedTrips(option);
        planned.ActivateAsync(OwnerUserId, option, Arg.Any<CancellationToken>())
            .Returns(new TripModel(PlannedTripId, OwnerUserId, TripConstants.Active, PlannedStart.AddMinutes(5)));
        var modals = ModalReturning(new StartPlannedTripModalResult(option));
        await using var context = CreateContext(EmptyStore(), modalService: modals, plannedTrips: planned);
        context.Services.AddSingleton(activeTrip);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#trip-start-link").Should().NotBeNull());

        // Act
        await cut.Find("#trip-start-link").ClickAsync();

        // Assert
        await modals.Received(1).ShowAsync<StartPlannedTripModal, StartPlannedTripModalModel, StartPlannedTripModalResult>(
            Arg.Is<StartPlannedTripModalModel>(model => model.Trips.Count == 1 && model.Trips[0] == option),
            Arg.Any<CancellationToken>());
        await planned.Received(1).ActivateAsync(OwnerUserId, option, Arg.Any<CancellationToken>());
        await activeTrip.DidNotReceive().StartAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        context.Services.GetRequiredService<NavigationManager>().Uri
            .Should().EndWith($"/trips/{PlannedTripId:D}");
    }

    [Fact]
    public async Task ItShouldReportAFailureToActivateAPlannedTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var activeTrip = ActiveTripService();
        var option = Option();
        var failure = new HttpRequestException("conflict");
        var planned = PlannedTrips(option);
        planned.ActivateAsync(OwnerUserId, option, Arg.Any<CancellationToken>()).ThrowsAsync(failure);
        var modals = ModalReturning(new StartPlannedTripModalResult(option));
        var logging = QuietLogging();
        await using var context = CreateContext(
            EmptyStore(),
            modalService: modals,
            plannedTrips: planned,
            logging: logging);
        context.Services.AddSingleton(activeTrip);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#trip-start-link").Should().NotBeNull());

        // Act
        await cut.Find("#trip-start-link").ClickAsync();

        // Assert
        await modals.Received(1).ShowMessageAsync(
            Arg.Is<MessageModalModel>(model => model.Message == "The planned trip could not be started."
                && model.Severity == ModalSeverity.Error),
            Arg.Any<CancellationToken>());
        await logging.Received(1).LogErrorAsync("starting a planned trip", failure, Arg.Any<CancellationToken>());
        await activeTrip.DidNotReceive().StartAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().NotContain("/trips/");
    }

    [Fact]
    public async Task ItShouldRecoverToTheRunningTripWhenAnotherTripIsAlreadyActive()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var runningId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var started = false;
        var activeTrip = Substitute.For<IActiveTripService>();
        activeTrip.GetActiveAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<TripModel?>(started
                ? new TripModel(runningId, OwnerUserId, TripConstants.Active, PlannedStart)
                : null));
        var option = Option();
        var planned = PlannedTrips(option);
        planned.ActivateAsync(OwnerUserId, option, Arg.Any<CancellationToken>())
            .Returns<Task<TripModel>>(_ =>
            {
                started = true;
                throw new Web.Features.Trips.Offline.TripAlreadyActiveException();
            });
        var modals = ModalReturning(new StartPlannedTripModalResult(option));
        await using var context = CreateContext(EmptyStore(), modalService: modals, plannedTrips: planned);
        context.Services.AddSingleton(activeTrip);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#trip-start-link").Should().NotBeNull());

        // Act
        await cut.Find("#trip-start-link").ClickAsync();

        // Assert
        context.Services.GetRequiredService<NavigationManager>().Uri
            .Should().EndWith($"/trips/{runningId:D}");
        await modals.DidNotReceive().ShowMessageAsync(Arg.Any<MessageModalModel>(), Arg.Any<CancellationToken>());
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

    private static ICatchStore EmptyStore()
    {
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Web.Features.Catch.Models.CatchModel>());
        return store;
    }
}
