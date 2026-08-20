using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Components.CatchCard;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace FishingLogBook.Web.Tests.Features.Catch.Components.CatchCardTests;

public class BaseCatchCardTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly DateTime LocalToday = new(2026, 8, 17, 0, 0, 0, DateTimeKind.Unspecified);

    protected static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton<IMeasurementService, MeasurementService>();
        context.Services.AddSingleton<ICatchDateGroupingService, CatchDateGroupingService>();
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static (IRenderedComponent<CatchCard> Card, IRenderedComponent<MudPopoverProvider> Popover) RenderCard(
        BunitContext context,
        Action<ComponentParameterCollectionBuilder<CatchCard>> parameterBuilder)
    {
        var popover = context.Render<MudPopoverProvider>();
        var card = context.Render<CatchCard>(parameterBuilder);
        return (card, popover);
    }

    protected static CatchModel StoredCatch(
        Guid catchId,
        SyncStatus syncStatus = SyncStatus.Synchronised,
        string? speciesName = "Brown Trout",
        string? method = null,
        decimal? weight = null,
        decimal? length = null,
        string? baitOrLure = null,
        string? notes = null,
        CatchLocationModel? location = null,
        Guid? anglerUserId = null,
        Guid? recordedByUserId = null,
        bool withPhotograph = true,
        int photographCount = 1)
    {
        IReadOnlyList<CatchPhotographModel> photographs = photographCount > 1
            ? [.. Enumerable.Range(0, photographCount)
                .Select(index => new CatchPhotographModel(
                    Guid.NewGuid(),
                    catchId,
                    PhotographContentTypeConstants.Jpeg,
                    [(byte)index, 2, 3]))]
            : withPhotograph
                ? [new CatchPhotographModel(Guid.NewGuid(), catchId, PhotographContentTypeConstants.Jpeg, [1, 2, 3])]
                : [];

        return new CatchModel(
            catchId,
            DateTimeOffset.Parse("2026-08-17T16:50:00Z"),
            photographs,
            SpeciesName: speciesName,
            Location: location,
            UserId: OwnerUserId,
            SyncStatus: syncStatus,
            MetadataSyncStatus: syncStatus,
            AnglerUserId: anglerUserId ?? OwnerUserId,
            RecordedByUserId: recordedByUserId ?? OwnerUserId,
            Weight: weight,
            Length: length,
            Method: method,
            BaitOrLure: baitOrLure,
            Notes: notes);
    }
}
