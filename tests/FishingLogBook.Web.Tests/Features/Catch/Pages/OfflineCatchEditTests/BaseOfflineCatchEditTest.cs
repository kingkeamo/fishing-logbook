using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Common.Modals;
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

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.OfflineCatchEditTests;

public class BaseOfflineCatchEditTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid CatchId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    protected static BunitContext CreateContext(ICatchStore catchStore)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(catchStore);
        context.Services.AddSingleton<IMeasurementService, MeasurementService>();
        context.Services.AddSingleton(Substitute.For<IModalService>());
        var preferences = Substitute.For<IAnglerPreferencesStore>();
        preferences.GetAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns(
            new AnglerPreferencesModel(new FishingCatalogueDto([], []), new FishingPreferencesDto([]), WeightUnitEnum.Kg, LengthUnitEnum.Cm));
        context.Services.AddSingleton(preferences);
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        var owner = new OfflineOwnerContextService();
        owner.Unlock(new OfflineOwnerModel(OwnerUserId, 1));
        context.Services.AddSingleton<IOfflineOwnerContextService>(owner);
        return context;
    }

    protected static CatchModel Catch(Guid ownerUserId) => new(
        CatchId,
        DateTimeOffset.UtcNow,
        [new CatchPhotographModel(Guid.Parse("44444444-4444-4444-4444-444444444444"), CatchId, "image/jpeg", [1, 2, 3])],
        "Brown Trout",
        UserId: ownerUserId,
        AnglerUserId: ownerUserId,
        RecordedByUserId: ownerUserId,
        Notes: "Before");
}
