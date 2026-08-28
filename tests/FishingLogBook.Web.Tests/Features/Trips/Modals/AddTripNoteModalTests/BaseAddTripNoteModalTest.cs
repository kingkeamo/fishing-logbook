using Bunit;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Modals.AddTripNote;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
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
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");

    protected static BunitContext CreateContext(
        ITripNoteStore store,
        ILoggingService? logging = null,
        ITimeService? time = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(store);
        context.Services.AddSingleton(logging ?? Substitute.For<ILoggingService>());
        context.Services.AddSingleton(time ?? TestTimeService.WithOffset(TimeSpan.Zero));
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static async Task<(IRenderedComponent<MudDialogProvider> Cut, IDialogReference Dialog)> ShowModalAsync(
        BunitContext context,
        DateTimeOffset? startedOn = null)
    {
        var cut = context.Render<MudDialogProvider>();
        var dialogs = context.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<AddTripNoteModal>
        {
            {
                modal => modal.Model,
                new AddTripNoteModalModel(TripId, OwnerUserId, startedOn ?? StartedOn)
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
}
