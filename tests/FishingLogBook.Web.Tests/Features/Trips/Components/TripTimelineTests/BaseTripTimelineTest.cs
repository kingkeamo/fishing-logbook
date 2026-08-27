using Bunit;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripTimelineTests;

public class BaseTripTimelineTest
{
    protected static readonly Guid CatchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-27T06:00:00Z");

    protected static BunitContext CreateContext(ITimeService? time = null, ILoggingService? logging = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(time ?? TestTimeService.WithOffset(TimeSpan.Zero));
        context.Services.AddSingleton(logging ?? QuietLogging());
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

    protected static TripTimelineItemModel Item(
        TripTimelineKindEnum kind,
        DateTimeOffset occurredOn,
        string? speciesName = null,
        string? text = null,
        Guid? catchId = null)
    {
        return new TripTimelineItemModel(kind, occurredOn)
        {
            SpeciesName = speciesName,
            Text = text,
            CatchId = catchId
        };
    }
}
