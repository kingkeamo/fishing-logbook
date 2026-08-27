using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripLocationPickerTests;

public class BaseTripLocationPickerTest
{
    protected static readonly Guid CorribId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    protected static readonly Guid MoyId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    protected static BunitContext CreateContext(
        IAnglerPreferencesProvider? anglerPreferences = null,
        ILoggingService? logging = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
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

    protected static ILoggingService QuietLogging()
    {
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return logging;
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
