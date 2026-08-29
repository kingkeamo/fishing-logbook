using System.Globalization;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Common.Offline.Synchronisers;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Components.MeasurementEditor;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Photographs.Models;
using FishingLogBook.Web.Features.Photographs.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchEditTests;

public class BaseCatchEditTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly DateTimeOffset StoredCaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z");
    protected static readonly DateTimeOffset UtcPlusFourCaughtOn = DateTimeOffset.Parse("2026-08-17T10:00:00Z");

    protected static readonly Guid FlyMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    protected static readonly Guid SpinningMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    protected static readonly Guid BrownTroutSpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    protected static readonly Guid PikeSpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");

    protected static BunitContext CreateContext(
        ICatchStore store,
        ILocalCatchOwnerService? owner = null,
        ILogbookSynchroniser? synchroniser = null,
        ILoggingService? logging = null,
        ITimeService? time = null,
        IAnglerPreferencesProvider? anglerPreferences = null,
        IModalService? modalService = null,
        ICatchClient? catchClient = null,
        IPhotographMetadataService? photoMetadata = null,
        IPhotographPreparationService? preparation = null,
        INetworkService? network = null,
        ITripClient? tripClient = null,
        ITripParticipantClient? participantClient = null)
    {
        var metadata = photoMetadata ?? PassThroughPhotoMetadata();
        var timeService = time ?? UtcTime();
        var loggingService = logging ?? QuietLogging();
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(store);
        context.Services.AddSingleton(owner ?? SignedInOwner());
        context.Services.AddSingleton(synchroniser ?? QuietSynchroniser());
        context.Services.AddSingleton(loggingService);
        context.Services.AddSingleton(timeService);
        context.Services.AddSingleton(anglerPreferences ?? QuietAnglerPreferences());
        context.Services.AddSingleton(modalService ?? QuietModalService());
        context.Services.AddSingleton(catchClient ?? QuietCatchClient());
        context.Services.AddSingleton(network ?? OnlineNetwork());
        context.Services.AddSingleton(tripClient ?? QuietTripClient());
        context.Services.AddSingleton(participantClient ?? QuietParticipantClient());
        context.Services.AddSingleton(metadata);
        context.Services.AddSingleton(preparation
            ?? new PhotographPreparationService(metadata, timeService, loggingService));
        context.Services.AddSingleton<IMeasurementService, MeasurementService>();
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static IPhotographMetadataService PassThroughPhotoMetadata()
    {
        var photoMetadata = Substitute.For<IPhotographMetadataService>();
        photoMetadata.Sanitise(Arg.Any<byte[]>(), Arg.Any<string>())
            .Returns(call => call.ArgAt<byte[]>(0));
        return photoMetadata;
    }

    protected static IPhotographMetadataService SanitisingPhotoMetadata(byte[] sanitised)
    {
        var photoMetadata = Substitute.For<IPhotographMetadataService>();
        photoMetadata.Sanitise(Arg.Any<byte[]>(), Arg.Any<string>()).Returns(sanitised);
        return photoMetadata;
    }

    protected static IPhotographMetadataService UnsanitisablePhotoMetadata()
    {
        var photoMetadata = Substitute.For<IPhotographMetadataService>();
        photoMetadata.Sanitise(Arg.Any<byte[]>(), Arg.Any<string>()).Returns((byte[]?)null);
        return photoMetadata;
    }

    protected static ICatchClient QuietCatchClient()
    {
        var client = Substitute.For<ICatchClient>();
        client.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((CatchViewDto?)null);
        return client;
    }

    protected static INetworkService OnlineNetwork(bool isOnline = true)
    {
        var network = Substitute.For<INetworkService>();
        network.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(isOnline);
        return network;
    }

    protected static ITripClient QuietTripClient()
    {
        var client = Substitute.For<ITripClient>();
        client.GetDetailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TripDetailDto?)null);
        return client;
    }

    protected static ITripParticipantClient QuietParticipantClient()
    {
        var client = Substitute.For<ITripParticipantClient>();
        client.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TripParticipantsDto?)null);
        return client;
    }

    protected static IAnglerPreferencesProvider QuietAnglerPreferences(
        FishingPreferencesDto? preferences = null,
        FishingCatalogueDto? catalogue = null,
        WeightUnitEnum weightUnit = WeightUnitEnum.Kg,
        LengthUnitEnum lengthUnit = LengthUnitEnum.Cm)
    {
        var provider = Substitute.For<IAnglerPreferencesProvider>();
        provider.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new AnglerPreferencesModel(
                catalogue ?? new FishingCatalogueDto([], []),
                preferences ?? new FishingPreferencesDto([]),
                weightUnit,
                lengthUnit));
        return provider;
    }

    protected static IModalService QuietModalService()
    {
        return Substitute.For<IModalService>();
    }

    protected static void AnswerMeasurement(IModalService modal, bool isWeight, decimal? canonicalValue)
    {
        modal.ShowAsync<MeasurementEditorModal, MeasurementEditorModel, MeasurementEditorResult>(
                Arg.Is<MeasurementEditorModel>(model => model.IsWeight == isWeight),
                Arg.Any<CancellationToken>())
            .Returns(new MeasurementEditorResult(canonicalValue));
    }

    protected static FishingCatalogueDto SampleCatalogue()
    {
        return new FishingCatalogueDto(
            [
                new FishingMethodDto(FlyMethodId, "Fly", "Fly"),
                new FishingMethodDto(SpinningMethodId, "Spinning", "Spinning")
            ],
            [
                new SpeciesDto(BrownTroutSpeciesId, "BrownTrout", "Brown Trout"),
                new SpeciesDto(PikeSpeciesId, "Pike", "Pike")
            ]);
    }

    protected static FishingPreferencesDto SamplePreferences()
    {
        return new FishingPreferencesDto(
        [
            new FishingMethodPreferenceDto(
                FlyMethodId,
                "Fly",
                "Fly",
                true,
                [new FishingSpeciesPreferenceDto(BrownTroutSpeciesId, "BrownTrout", "Brown Trout", true)]),
            new FishingMethodPreferenceDto(
                SpinningMethodId,
                "Spinning",
                "Spinning",
                false,
                [new FishingSpeciesPreferenceDto(PikeSpeciesId, "Pike", "Pike", true)])
        ]);
    }

    protected static ILocalCatchOwnerService SignedInOwner()
    {
        var owner = Substitute.For<ILocalCatchOwnerService>();
        owner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(OwnerUserId);
        return owner;
    }

    protected static ILogbookSynchroniser QuietSynchroniser()
    {
        var synchroniser = Substitute.For<ILogbookSynchroniser>();
        synchroniser.SynchronisePendingAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return synchroniser;
    }

    protected static ILoggingService QuietLogging()
    {
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return logging;
    }

    protected static ITimeService UtcTime()
    {
        return OffsetTime(TimeSpan.Zero);
    }

    protected static ITimeService PlusFourTime()
    {
        return OffsetTime(TimeSpan.FromHours(4));
    }

    protected static CatchModel StoredCatch(
        Guid catchId,
        SyncStatus syncStatus = SyncStatus.SavedLocally,
        SyncStatus metadataStatus = SyncStatus.SavedLocally,
        SyncStatus photographStatus = SyncStatus.SavedLocally,
        string? objectKey = null,
        CatchLocationModel? location = null,
        DateTimeOffset? caughtOn = null,
        string? speciesName = null,
        string? method = null,
        decimal? weight = null,
        decimal? length = null)
    {
        return new CatchModel(
            catchId,
            caughtOn ?? StoredCaughtOn,
            [
                new CatchPhotographModel(
                    Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    catchId,
                    PhotographContentTypeConstants.Jpeg,
                    [1, 2, 3],
                    photographStatus,
                    objectKey)
            ],
            SpeciesName: speciesName,
            Location: location,
            UserId: OwnerUserId,
            SyncStatus: syncStatus,
            MetadataSyncStatus: metadataStatus,
            AnglerUserId: OwnerUserId,
            RecordedByUserId: OwnerUserId,
            Weight: weight,
            Length: length,
            Method: method);
    }

    private static ITimeService OffsetTime(TimeSpan offset)
    {
        return TestTimeService.WithOffset(offset);
    }
}
