using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.OfflineAccess.Models;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Offline.Stores;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.OfflineCatchListTests;

public class BaseOfflineCatchListTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected static BunitContext CreateContext(ICatchStore store, IAnglerPreferencesStore? preferences = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(store);
        context.Services.AddSingleton(preferences ?? EmptyPreferences());
        context.Services.AddSingleton<IMeasurementService, MeasurementService>();
        context.Services.AddSingleton<ICatchDateGroupingService, CatchDateGroupingService>();
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        var owner = new OfflineOwnerContextService();
        owner.Unlock(new OfflineOwnerModel(OwnerUserId, 1));
        context.Services.AddSingleton<IOfflineOwnerContextService>(owner);
        return context;
    }

    protected static IAnglerPreferencesStore EmptyPreferences()
    {
        var store = Substitute.For<IAnglerPreferencesStore>();
        store.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(
            new AnglerPreferencesModel(new FishingCatalogueDto([], []), new FishingPreferencesDto([]), WeightUnitEnum.Kg, LengthUnitEnum.Cm));
        return store;
    }

    protected static CatchModel Catch(Guid userId, string species) => new(
        Guid.NewGuid(),
        DateTimeOffset.UtcNow,
        [new CatchPhotographModel(Guid.NewGuid(), Guid.Empty, "image/jpeg", [1, 2, 3])],
        species,
        UserId: userId,
        AnglerUserId: userId,
        RecordedByUserId: userId);
}
