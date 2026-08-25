using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.OfflineAccess.Models;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Offline.Stores;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.OfflineRecordCatchTests;

public class BaseOfflineRecordCatchTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected static BunitContext CreateContext(
        ICatchStore catchStore,
        IAnglerPreferencesStore preferencesStore,
        ILoggingService? logging = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(catchStore);
        context.Services.AddSingleton(preferencesStore);
        context.Services.AddSingleton(logging ?? QuietLogging());
        context.Services.AddSingleton(Substitute.For<IModalService>());
        context.Services.AddSingleton<IMeasurementService, MeasurementService>();
        var location = Substitute.For<ILocationService>();
        location.GetPromptStatusAsync(Arg.Any<CancellationToken>()).Returns(new LocationPromptStatus(false, false, false));
        context.Services.AddSingleton(location);
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        var owner = new OfflineOwnerContextService();
        owner.Unlock(new OfflineOwnerModel(OwnerUserId, 1));
        context.Services.AddSingleton<IOfflineOwnerContextService>(owner);
        return context;
    }

    protected static ILoggingService QuietLogging()
    {
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return logging;
    }

    protected static AnglerPreferencesModel Preferences() => new(
        new FishingCatalogueDto(
            [new FishingMethodDto(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), "Fly", "Fly")],
            [new SpeciesDto(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"), "BrownTrout", "Brown Trout")]),
        new FishingPreferencesDto([
            new FishingMethodPreferenceDto(
                Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
                "Fly",
                "Fly",
                true,
                [new FishingSpeciesPreferenceDto(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"), "BrownTrout", "Brown Trout", true)])
        ]),
        WeightUnitEnum.Kg,
        LengthUnitEnum.Cm);
}
