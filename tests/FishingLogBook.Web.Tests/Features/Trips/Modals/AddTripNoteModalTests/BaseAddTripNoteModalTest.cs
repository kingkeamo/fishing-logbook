using AngleSharp.Html.Dom;
using Bunit;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Modals.AddTripNote;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Modals.AddTripNoteModalTests;

public class BaseAddTripNoteModalTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid NoteId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");
    protected static readonly DateTimeOffset EndedOn = DateTimeOffset.Parse("2026-08-17T16:00:00Z");

    protected static BunitContext CreateContext(
        ITripNoteWriteService? writer = null,
        ILoggingService? logging = null,
        ITimeService? time = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(writer ?? WriterThatSaves());
        context.Services.AddSingleton(logging ?? Substitute.For<ILoggingService>());
        context.Services.AddSingleton(time ?? TestTimeService.WithOffset(TimeSpan.Zero));
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static ITripNoteWriteService WriterThatSaves()
    {
        var writer = Substitute.For<ITripNoteWriteService>();
        writer.AddAsync(
                Arg.Any<TripNoteDraftModel>(),
                Arg.Any<TripNoteStorageEnum>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var draft = call.ArgAt<TripNoteDraftModel>(0);
                return new TripNoteModel(
                    NoteId,
                    draft.TripId,
                    draft.OwnerUserId,
                    draft.Text,
                    draft.RecordedOn);
            });
        return writer;
    }

    protected static ITripNoteWriteService WriterThatCannotReachTheServer()
    {
        var writer = Substitute.For<ITripNoteWriteService>();
        writer.AddAsync(
                Arg.Any<TripNoteDraftModel>(),
                Arg.Any<TripNoteStorageEnum>(),
                Arg.Any<CancellationToken>())
            .Returns<TripNoteModel>(_ => throw new HttpRequestException("offline"));
        return writer;
    }

    protected static ITripNoteWriteService WriterThatFails()
    {
        var writer = Substitute.For<ITripNoteWriteService>();
        writer.AddAsync(
                Arg.Any<TripNoteDraftModel>(),
                Arg.Any<TripNoteStorageEnum>(),
                Arg.Any<CancellationToken>())
            .Returns<TripNoteModel>(_ => throw new InvalidOperationException("write failed"));
        return writer;
    }

    protected static async Task<(IRenderedComponent<MudDialogProvider> Cut, IDialogReference Dialog)> ShowModalAsync(
        BunitContext context,
        DateTimeOffset? startedOn = null,
        DateTimeOffset? endedOn = null,
        TripNoteStorageEnum storage = TripNoteStorageEnum.LocalFirst)
    {
        var cut = context.Render<MudDialogProvider>();
        var dialogs = context.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<AddTripNoteModal>
        {
            {
                modal => modal.Model,
                new AddTripNoteModalModel(
                    TripId,
                    OwnerUserId,
                    startedOn ?? StartedOn,
                    endedOn,
                    storage)
            }
        };
        var dialog = await dialogs.ShowAsync<AddTripNoteModal>(
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

    protected static TimeSpan OffsetPuttingLocalTimeAt(TimeSpan localTimeOfDay)
    {
        return localTimeOfDay - DateTimeOffset.UtcNow.UtcDateTime.TimeOfDay;
    }

    protected static string DateValue(IRenderedComponent<MudDialogProvider> cut)
    {
        return ((IHtmlInputElement)cut.Find("#trip-note-date")).Value;
    }

    protected static string TimeValue(IRenderedComponent<MudDialogProvider> cut)
    {
        return ((IHtmlInputElement)cut.Find("#trip-note-time")).Value;
    }
}
