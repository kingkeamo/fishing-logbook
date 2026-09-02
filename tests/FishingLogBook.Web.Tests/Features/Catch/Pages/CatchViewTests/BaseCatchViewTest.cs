using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchViewTests;

public class BaseCatchViewTest
{
    protected static readonly Guid CatchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    protected static readonly Guid PhotographId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid CaughtByUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid RecorderUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    protected static readonly DateTimeOffset CaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z");

    protected static BunitContext CreateContext(
        ICatchClient catchClient,
        IAnglerPreferencesProvider? preferences = null,
        ITimeService? time = null,
        ILoggingService? logging = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(catchClient);
        context.Services.AddSingleton(preferences ?? QuietPreferences());
        context.Services.AddSingleton(time ?? TestTimeService.WithOffset(TimeSpan.Zero));
        context.Services.AddSingleton(logging ?? QuietLogging());
        context.Services.AddSingleton<IMeasurementService>(new MeasurementService());
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        var authorization = context.AddAuthorization();
        authorization.SetAuthorized("tester@example.test");
        return context;
    }

    protected static IAnglerPreferencesProvider QuietPreferences(
        WeightUnitEnum weightUnit = WeightUnitEnum.Kg,
        LengthUnitEnum lengthUnit = LengthUnitEnum.Cm)
    {
        var provider = Substitute.For<IAnglerPreferencesProvider>();
        provider.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new AnglerPreferencesModel(
                new FishingCatalogueDto([], []),
                new FishingPreferencesDto([]),
                weightUnit,
                lengthUnit));
        return provider;
    }

    protected static ILoggingService QuietLogging()
    {
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return logging;
    }

    protected static ICatchClient ClientReturning(CatchViewDto? catchRecord)
    {
        var client = Substitute.For<ICatchClient>();
        client.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(catchRecord);
        return client;
    }

    protected static CatchViewDto ViewDto(
        string? speciesName = "Brown Trout",
        decimal? weight = 1.02m,
        decimal? length = 48m,
        string? method = "Fly",
        string? baitOrLure = "Mayfly",
        string? notes = "Took on the drift.",
        string? anglerName = "Mark",
        string? recordedByName = "Eamonn",
        CatchLocationExposureDto? location = null,
        IReadOnlyList<CatchPhotographViewDto>? photographs = null)
    {
        return new CatchViewDto(CatchId, OwnerUserId, CaughtOn, location)
        {
            AnglerName = anglerName,
            RecordedByUserId = RecorderUserId,
            RecordedByName = recordedByName,
            SpeciesName = speciesName,
            Weight = weight,
            Length = length,
            Method = method,
            BaitOrLure = baitOrLure,
            Notes = notes,
            Photographs = photographs ??
            [
                new CatchPhotographViewDto(
                    PhotographId,
                    PhotographContentTypeConstants.Jpeg,
                    "https://storage.test/catch.jpg")
            ]
        };
    }
}
