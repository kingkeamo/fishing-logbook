using Bunit;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.OfflineAccess.Models;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Layouts.OfflineLayoutTests;

public class BaseOfflineLayoutTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected static BunitContext CreateContext(
        out OfflineOwnerContextService owner,
        IOfflineReconnectService? reconnect = null,
        IActiveTripService? activeTrip = null,
        ILocalCatchOwnerService? localOwner = null,
        ILoggingService? logging = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        owner = new OfflineOwnerContextService();
        owner.Unlock(new OfflineOwnerModel(OwnerUserId, 1));
        context.Services.AddSingleton<IOfflineOwnerContextService>(owner);
        context.Services.AddSingleton(reconnect ?? Substitute.For<IOfflineReconnectService>());
        context.Services.AddSingleton(activeTrip ?? NoActiveTrip());
        context.Services.AddSingleton(localOwner ?? Substitute.For<ILocalCatchOwnerService>());
        context.Services.AddSingleton(logging ?? Substitute.For<ILoggingService>());
        return context;
    }

    protected static IActiveTripService NoActiveTrip()
    {
        var activeTrip = Substitute.For<IActiveTripService>();
        activeTrip.GetActiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((FishingLogBook.Web.Features.Trips.Models.TripModel?)null);
        return activeTrip;
    }
}
