using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Photographs.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripEditorTests;

public class BaseTripEditorTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid PikeCatchId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    protected static readonly Guid TroutCatchId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    protected static readonly Guid CorribId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    protected static readonly Guid MoyId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-27T06:00:00Z");

    protected static BunitContext CreateContext(
        IActiveTripService? activeTrip = null,
        ICatchStore? catchStore = null,
        IModalService? modalService = null,
        IAnglerPreferencesProvider? anglerPreferences = null,
        ITripNoteStore? noteStore = null,
        ILoggingService? logging = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(activeTrip ?? TripServiceThatSaves());
        context.Services.AddSingleton(catchStore ?? QuietCatchStore());
        context.Services.AddSingleton(modalService ?? ConfirmingModalService());
        context.Services.AddSingleton(anglerPreferences ?? QuietAnglerPreferences());
        context.Services.AddSingleton(noteStore ?? Substitute.For<ITripNoteStore>());
        context.Services.AddSingleton(logging ?? QuietLogging());
        context.Services.AddSingleton(Substitute.For<ITripNoteWriteService>());
        context.Services.AddSingleton(Substitute.For<ITripPhotographStore>());
        context.Services.AddSingleton(Substitute.For<ITripClient>());
        context.Services.AddSingleton(Substitute.For<IPhotographPreparationService>());
        context.Services.AddSingleton<IMeasurementService>(new MeasurementService());
        context.Services.AddSingleton<ITimeService>(TestTimeService.WithOffset(TimeSpan.Zero));
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static IActiveTripService TripServiceThatSaves()
    {
        var service = Substitute.For<IActiveTripService>();
        service.UpdateDetailsAsync(
                Arg.Any<TripModel>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<TripModel>(0) with
            {
                Title = call.ArgAt<string?>(1),
                PlaceName = call.ArgAt<string?>(2)
            });
        return service;
    }

    protected static ICatchStore QuietCatchStore(params CatchModel[] unassigned)
    {
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(unassigned);
        store.UpdateTripAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return store;
    }

    protected static IModalService ConfirmingModalService(bool confirm = true)
    {
        var modalService = Substitute.For<IModalService>();
        modalService.ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>())
            .Returns(confirm);
        return modalService;
    }

    protected static IAnglerPreferencesProvider QuietAnglerPreferences(
        params FishingLocationPreferenceDto[] locations)
    {
        var provider = Substitute.For<IAnglerPreferencesProvider>();
        provider.GetAsync(Arg.Any<CancellationToken>())
            .Returns(AnglerPreferencesModel.Empty with { Locations = locations });
        return provider;
    }

    protected static IAnglerPreferencesProvider PreferencesWith(params FishingLocationPreferenceDto[] locations)
    {
        return QuietAnglerPreferences(locations);
    }

    protected static ILoggingService QuietLogging()
    {
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return logging;
    }

    protected static TripModel ActiveTrip(string? title = null, string? placeName = null)
    {
        return new TripModel(
            TripId,
            OwnerUserId,
            TripConstants.Active,
            StartedOn,
            Title: title,
            PlaceName: placeName);
    }

    protected static CatchModel AssociatedCatch(
        string? speciesName = "Brown Trout",
        decimal? weight = null,
        decimal? length = null,
        Guid? catchId = null)
    {
        return new CatchModel(
            catchId ?? PikeCatchId,
            StartedOn.AddMinutes(30),
            [],
            speciesName,
            CaughtByUserId: OwnerUserId,
            Weight: weight,
            Length: length,
            TripId: TripId);
    }

    protected static FishingLocationPreferenceDto Corrib(bool isDefault = true)
    {
        return new FishingLocationPreferenceDto(CorribId, "Lough Corrib", isDefault);
    }

    protected static FishingLocationPreferenceDto Moy()
    {
        return new FishingLocationPreferenceDto(MoyId, "River Moy", false);
    }
}
