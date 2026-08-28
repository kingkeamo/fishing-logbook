using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Modals.AddTripCatches;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Modals.AddTripCatchesModalTests;

public class BaseAddTripCatchesModalTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid PikeCatchId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    protected static readonly Guid TroutCatchId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");
    protected static readonly DateTimeOffset EndedOn = DateTimeOffset.Parse("2026-08-17T16:00:00Z");

    protected static BunitContext CreateContext(
        ITripCatchService tripCatches,
        ILoggingService? logging = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(tripCatches);
        context.Services.AddSingleton(logging ?? Substitute.For<ILoggingService>());
        context.Services.AddSingleton(Substitute.For<ICatchStore>());
        context.Services.AddSingleton<ITimeService>(TestTimeService.WithOffset(TimeSpan.Zero));
        context.Services.AddSingleton<IMeasurementService>(new MeasurementService());
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static ITripCatchService TripCatchesOffering(params CatchModel[] candidates)
    {
        var service = Substitute.For<ITripCatchService>();
        service.GetEligibleAsync(
                Arg.Any<TripCatchScopeModel>(),
                Arg.Any<TripStorageEnum>(),
                Arg.Any<CancellationToken>())
            .Returns(candidates);
        service.AssociateAsync(
                Arg.Any<TripCatchScopeModel>(),
                Arg.Any<IReadOnlyList<Guid>>(),
                Arg.Any<TripStorageEnum>(),
                Arg.Any<CancellationToken>())
            .Returns(call => new TripCatchAssociationModel(
                call.ArgAt<IReadOnlyList<Guid>>(1),
                []));
        return service;
    }

    protected static async Task<(IRenderedComponent<MudDialogProvider> Cut, IDialogReference Dialog)> ShowModalAsync(
        BunitContext context,
        TripStorageEnum storage = TripStorageEnum.LocalFirst,
        DateTimeOffset? endedOn = null)
    {
        var cut = context.Render<MudDialogProvider>();
        var dialogs = context.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<AddTripCatchesModal>
        {
            {
                modal => modal.Model,
                new AddTripCatchesModalModel(
                    new TripCatchScopeModel(TripId, OwnerUserId, StartedOn, endedOn ?? EndedOn),
                    storage)
            }
        };
        var dialog = await dialogs.ShowAsync<AddTripCatchesModal>(
            parameters,
            new DialogOptions
            {
                CloseButton = true,
                CloseOnEscapeKey = true,
                FullWidth = true,
                MaxWidth = MaxWidth.Small
            });
        return (cut, dialog);
    }

    protected static CatchModel Catch(Guid catchId, string speciesName, DateTimeOffset? caughtOn = null)
    {
        return new CatchModel(
            catchId,
            caughtOn ?? StartedOn.AddHours(2),
            [new CatchPhotographModel(Guid.NewGuid(), catchId, PhotographContentTypeConstants.Jpeg)],
            speciesName,
            UserId: OwnerUserId);
    }
}
