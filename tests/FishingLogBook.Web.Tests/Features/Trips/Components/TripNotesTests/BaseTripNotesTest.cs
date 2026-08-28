using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Modals.AddTripNote;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripNotesTests;

public class BaseTripNotesTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");

    protected static IModalService ConfirmingModalService(bool confirm = true)
    {
        var modalService = Substitute.For<IModalService>();
        modalService.ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>())
            .Returns(confirm);
        return modalService;
    }

    protected static IModalService ModalServiceAdding(TripNoteModel note)
    {
        var modalService = ConfirmingModalService();
        modalService
            .ShowAsync<AddTripNoteModal, AddTripNoteModalModel, AddTripNoteModalResult>(
                Arg.Any<AddTripNoteModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(new AddTripNoteModalResult(note));
        return modalService;
    }

    protected static IModalService ModalServiceEditing(TripNoteModel edited)
    {
        var modalService = ConfirmingModalService();
        modalService
            .ShowAsync<AddTripNoteModal, AddTripNoteModalModel, AddTripNoteModalResult>(
                Arg.Any<AddTripNoteModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(new AddTripNoteModalResult(edited));
        return modalService;
    }

    protected static BunitContext CreateContext(
        ITripNoteStore store,
        ITripClient? tripClient = null,
        ILoggingService? logging = null,
        ITimeService? time = null,
        IModalService? modalService = null,
        ITripNoteWriteService? noteWriter = null)
    {
        var client = tripClient ?? Substitute.For<ITripClient>();
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(store);
        context.Services.AddSingleton(client);
        context.Services.AddSingleton(logging ?? Substitute.For<ILoggingService>());
        context.Services.AddSingleton(time ?? TestTimeService.WithOffset(TimeSpan.Zero));
        context.Services.AddSingleton(noteWriter ?? new TripNoteWriteService(store, client));
        context.Services.AddSingleton(modalService ?? ConfirmingModalService());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static TripModel Trip(params TripNoteModel[] notes)
    {
        return new TripModel(
            TripId,
            OwnerUserId,
            TripConstants.Active,
            StartedOn,
            Notes: notes.Length == 0 ? null : notes);
    }

    protected static TripModel CompletedTrip(params TripNoteModel[] notes)
    {
        return new TripModel(
            TripId,
            OwnerUserId,
            TripConstants.Completed,
            StartedOn,
            StartedOn.AddHours(4),
            Notes: notes.Length == 0 ? null : notes);
    }

    protected static TripNoteModel Note(
        Guid noteId,
        string text = "water dropped about a foot",
        DateTimeOffset? recordedOn = null,
        SyncStatus syncStatus = SyncStatus.SavedLocally)
    {
        return new TripNoteModel(
            noteId,
            TripId,
            OwnerUserId,
            text,
            recordedOn ?? StartedOn.AddMinutes(45),
            syncStatus);
    }
}
