using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Modals.AddTripCatches;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripCatchesTests;

public class BaseTripCatchesTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid PikeCatchId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    protected static readonly Guid TroutCatchId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-27T06:00:00Z");
    protected static readonly DateTimeOffset EndedOn = DateTimeOffset.Parse("2026-08-27T14:00:00Z");

    protected static BunitContext CreateContext(
        IModalService modalService,
        ILoggingService? logging = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(modalService);
        context.Services.AddSingleton(logging ?? QuietLogging());
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static IModalService ModalServiceAdding(AddTripCatchesModalResult? result = null)
    {
        var modalService = Substitute.For<IModalService>();
        modalService
            .ShowAsync<AddTripCatchesModal, AddTripCatchesModalModel, AddTripCatchesModalResult>(
                Arg.Any<AddTripCatchesModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return modalService;
    }

    protected static ILoggingService QuietLogging()
    {
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return logging;
    }

    protected static TripModel Trip()
    {
        return new TripModel(TripId, OwnerUserId, TripConstants.Active, StartedOn);
    }

    protected static TripModel CompletedTrip()
    {
        return new TripModel(TripId, OwnerUserId, TripConstants.Completed, StartedOn, EndedOn);
    }
}
