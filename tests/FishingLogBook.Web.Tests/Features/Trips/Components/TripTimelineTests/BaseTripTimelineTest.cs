using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripTimelineTests;

public class BaseTripTimelineTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid CatchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    protected static readonly Guid NoteId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    protected static readonly Guid PhotographId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-27T06:00:00Z");

    protected static BunitContext CreateContext(
        ITimeService? time = null,
        ILoggingService? logging = null,
        ITripPhotographStore? tripPhotographStore = null,
        ICatchStore? catchStore = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(time ?? TestTimeService.WithOffset(TimeSpan.Zero));
        context.Services.AddSingleton(logging ?? QuietLogging());
        context.Services.AddSingleton(tripPhotographStore ?? Substitute.For<ITripPhotographStore>());
        context.Services.AddSingleton(catchStore ?? Substitute.For<ICatchStore>());
        context.Services.AddSingleton<IMeasurementService>(new MeasurementService());
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static ILoggingService QuietLogging()
    {
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return logging;
    }

    protected static ITripPhotographStore StoreWithPhotographBytes(params byte[] bytes)
    {
        var store = Substitute.For<ITripPhotographStore>();
        store.GetBytesAsync(
                OwnerUserId,
                TripId,
                PhotographId,
                Arg.Any<CancellationToken>())
            .Returns(bytes);
        return store;
    }

    protected static ICatchStore CatchStoreWithPhotographBytes(params byte[] bytes)
    {
        var store = Substitute.For<ICatchStore>();
        store.GetPhotographBytesAsync(
                OwnerUserId,
                CatchId,
                PhotographId,
                Arg.Any<CancellationToken>())
            .Returns(bytes);
        return store;
    }

    protected static TripTimelineItemModel Item(
        TripTimelineKindEnum kind,
        DateTimeOffset occurredOn,
        string? speciesName = null,
        string? text = null,
        Guid? catchId = null,
        Guid? noteId = null,
        Guid? photographId = null,
        string? photographUrl = null,
        decimal? weight = null,
        decimal? length = null)
    {
        return new TripTimelineItemModel(kind, occurredOn)
        {
            SpeciesName = speciesName,
            Text = text,
            CatchId = catchId,
            NoteId = noteId,
            PhotographId = photographId,
            PhotographUrl = photographUrl,
            ContentType = PhotographContentTypeConstants.Jpeg,
            Weight = weight,
            Length = length
        };
    }
}
