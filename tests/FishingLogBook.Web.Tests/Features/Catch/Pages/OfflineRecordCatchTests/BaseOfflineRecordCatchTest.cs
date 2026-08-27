using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.OfflineAccess.Models;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Features.Photographs.Models;
using FishingLogBook.Web.Features.Photographs.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.OfflineRecordCatchTests;

public class BaseOfflineRecordCatchTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected static BunitContext CreateContext(
        ICatchStore catchStore,
        IAnglerPreferencesStore preferencesStore,
        ILoggingService? logging = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(catchStore);
        context.Services.AddSingleton(preferencesStore);
        var loggingService = logging ?? QuietLogging();
        var timeService = TestTimeService.WithOffset(TimeSpan.Zero);
        var metadata = NoPhotoMetadata();
        context.Services.AddSingleton(loggingService);
        context.Services.AddSingleton(Substitute.For<IModalService>());
        context.Services.AddSingleton(timeService);
        context.Services.AddSingleton(metadata);
        context.Services.AddSingleton<IPhotographPreparationService>(
            new PhotographPreparationService(metadata, timeService, loggingService));
        context.Services.AddSingleton<ICatchPhotographProposalService, CatchPhotographProposalService>();
        context.Services.AddSingleton<IMeasurementService, MeasurementService>();
        context.Services.AddSingleton(Substitute.For<ITripStore>());
        var activeTrip = Substitute.For<IActiveTripService>();
        activeTrip.GetActiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TripModel?)null);
        context.Services.AddSingleton(activeTrip);
        context.Services.AddSingleton<ITripDisplayService>(new TripDisplayService(timeService));
        var location = Substitute.For<ILocationService>();
        location.GetPromptStatusAsync(Arg.Any<CancellationToken>()).Returns(new LocationPromptStatus(false, false, false));
        context.Services.AddSingleton(location);
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        var owner = new OfflineOwnerContextService();
        owner.Unlock(new OfflineOwnerModel(OwnerUserId, 1));
        context.Services.AddSingleton<IOfflineOwnerContextService>(owner);
        return context;
    }

    protected static IPhotographMetadataService NoPhotoMetadata()
    {
        var photoMetadata = Substitute.For<IPhotographMetadataService>();
        photoMetadata.ReadAsync(
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(PhotographMetadataModel.Empty);
        photoMetadata.Sanitise(Arg.Any<byte[]>(), Arg.Any<string>())
            .Returns(call => call.ArgAt<byte[]>(0));
        return photoMetadata;
    }

    protected static ILoggingService QuietLogging()
    {
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return logging;
    }

    protected static AnglerPreferencesModel Preferences() => new(
        new FishingCatalogueDto(
            [new FishingMethodDto(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), "Fly", "Fly")],
            [new SpeciesDto(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"), "BrownTrout", "Brown Trout")]),
        new FishingPreferencesDto([
            new FishingMethodPreferenceDto(
                Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
                "Fly",
                "Fly",
                true,
                [new FishingSpeciesPreferenceDto(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"), "BrownTrout", "Brown Trout", true)])
        ]),
        WeightUnitEnum.Kg,
        LengthUnitEnum.Cm);
}
