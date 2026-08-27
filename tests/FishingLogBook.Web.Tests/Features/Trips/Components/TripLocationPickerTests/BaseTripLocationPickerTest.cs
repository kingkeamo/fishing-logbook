using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripLocationPickerTests;

public class BaseTripLocationPickerTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid CorribId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    protected static readonly Guid MoyId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-27T05:32:00Z");

    protected static BunitContext CreateContext(
        IActiveTripService activeTrip,
        IAnglerPreferencesProvider? anglerPreferences = null,
        ILoggingService? logging = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(activeTrip);
        context.Services.AddSingleton(anglerPreferences ?? PreferencesWith());
        context.Services.AddSingleton(logging ?? QuietLogging());
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static IAnglerPreferencesProvider PreferencesWith(params FishingLocationPreferenceDto[] locations)
    {
        var provider = Substitute.For<IAnglerPreferencesProvider>();
        provider.GetAsync(Arg.Any<CancellationToken>())
            .Returns(AnglerPreferencesModel.Empty with { Locations = locations });
        return provider;
    }

    protected static IActiveTripService TripServiceThatSaves()
    {
        var service = Substitute.For<IActiveTripService>();
        service.UpdatePlaceAsync(Arg.Any<TripModel>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<TripModel>(0) with { PlaceName = call.ArgAt<string?>(1) });
        return service;
    }

    protected static ILoggingService QuietLogging()
    {
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return logging;
    }

    protected static TripModel Trip(string? placeName = null)
    {
        return new TripModel(
            TripId,
            OwnerUserId,
            TripConstants.Active,
            StartedOn,
            PlaceName: placeName);
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
