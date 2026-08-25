using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Components.MeasurementEditor;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Offline.Synchronisers;
using FishingLogBook.Web.Features.Catch.Pages.RecordCatch;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.RecordCatchTests;

public class BaseRecordCatchTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected static readonly Guid FlyMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    protected static readonly Guid SpinningMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    protected static readonly Guid BrownTroutSpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    protected static readonly Guid PikeSpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");

    protected static BunitContext CreateContext(
        ICatchStore store,
        ILocationService? location = null,
        ILocalCatchOwnerService? owner = null,
        ICatchSynchroniser? synchroniser = null,
        ILoggingService? logging = null,
        IAnglerPreferencesProvider? anglerPreferences = null,
        IModalService? modalService = null,
        ITimeService? time = null,
        IPhotoMetadataService? photoMetadata = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(store);
        context.Services.AddSingleton(location ?? QuietLocation());
        context.Services.AddSingleton(owner ?? SignedInOwner());
        context.Services.AddSingleton(synchroniser ?? QuietSynchroniser());
        context.Services.AddSingleton(logging ?? QuietLogging());
        context.Services.AddSingleton(anglerPreferences ?? QuietAnglerPreferences());
        context.Services.AddSingleton(modalService ?? QuietModalService());
        context.Services.AddSingleton(time ?? UtcTime());
        context.Services.AddSingleton(photoMetadata ?? NoPhotoMetadata());
        context.Services.AddSingleton<IPhotoMetadataProposalService, PhotoMetadataProposalService>();
        context.Services.AddSingleton<IMeasurementService, MeasurementService>();
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static ITimeService UtcTime()
    {
        return TestTimeService.WithOffset(TimeSpan.Zero);
    }

    protected static ITimeService OffsetTime(TimeSpan offset)
    {
        return TestTimeService.WithOffset(offset);
    }

    protected static IPhotoMetadataService RealPhotoMetadata()
    {
        return new PhotoMetadataService(TestTimeService.WithOffset(TimeSpan.Zero));
    }

    protected static byte[] MinimalJpeg()
    {
        return
        [
            0xFF, 0xD8,
            0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00,
            0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
            0xFF, 0xDB, 0x00, 0x05, 0x00, 0x01, 0x02,
            0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00,
            0x9A, 0x2B, 0x7C,
            0xFF, 0xD9
        ];
    }

    protected static IPhotoMetadataService NoPhotoMetadata()
    {
        var photoMetadata = Substitute.For<IPhotoMetadataService>();
        photoMetadata.ReadAsync(
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<CancellationToken>())
            .Returns(PhotoMetadataModel.Empty);
        PassThroughSanitisation(photoMetadata);
        return photoMetadata;
    }

    protected static void PassThroughSanitisation(IPhotoMetadataService photoMetadata)
    {
        photoMetadata.Sanitise(Arg.Any<byte[]>(), Arg.Any<string>())
            .Returns(call => call.ArgAt<byte[]>(0));
    }

    protected static IPhotoMetadataService SanitisingPhotoMetadata(
        PhotoMetadataModel metadata,
        byte[] sanitised)
    {
        var photoMetadata = Substitute.For<IPhotoMetadataService>();
        photoMetadata.ReadAsync(
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<CancellationToken>())
            .Returns(metadata);
        photoMetadata.Sanitise(Arg.Any<byte[]>(), Arg.Any<string>()).Returns(sanitised);
        return photoMetadata;
    }

    protected static IPhotoMetadataService PhotoMetadataFor(
        params (byte Marker, PhotoMetadataModel Metadata)[] photographs)
    {
        var photoMetadata = Substitute.For<IPhotoMetadataService>();
        photoMetadata.ReadAsync(
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var bytes = call.ArgAt<byte[]>(0);
                var match = photographs.FirstOrDefault(photograph =>
                    bytes.Length > 0 && bytes[0] == photograph.Marker);
                return Task.FromResult(match.Metadata ?? PhotoMetadataModel.Empty);
            });
        PassThroughSanitisation(photoMetadata);
        return photoMetadata;
    }

    protected static ILoggingService QuietLogging()
    {
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return logging;
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

    protected static void AnswerCataloguePicker(
        IModalService modalService,
        string offeredOptionCode,
        CatalogueOptionModel chosen)
    {
        modalService
            .ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Is<CataloguePickerModalModel>(model =>
                    model.Options.Any(option => option.Code == offeredOptionCode)),
                Arg.Any<CancellationToken>())
            .Returns(new CataloguePickerModalResult(chosen));
    }

    protected static void AnswerMeasurement(IModalService modalService, bool isWeight, decimal? canonicalValue)
    {
        modalService
            .ShowAsync<MeasurementEditorModal, MeasurementEditorModel, MeasurementEditorResult>(
                Arg.Is<MeasurementEditorModel>(model => model.IsWeight == isWeight),
                Arg.Any<CancellationToken>())
            .Returns(new MeasurementEditorResult(canonicalValue));
    }

    protected static string SelectedMethod(IRenderedComponent<RecordCatch> cut)
    {
        return SelectedChip(cut, "record-catch-method-chips");
    }

    protected static string SelectedSpecies(IRenderedComponent<RecordCatch> cut)
    {
        return SelectedChip(cut, "record-catch-species-chips");
    }

    private static string SelectedChip(IRenderedComponent<RecordCatch> cut, string containerId)
    {
        var selected = cut.Find($"#{containerId}")
            .QuerySelectorAll(".mud-chip-filled")
            .FirstOrDefault();
        return selected?.TextContent.Trim() ?? string.Empty;
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

    protected static ICatchSynchroniser QuietSynchroniser()
    {
        var synchroniser = Substitute.For<ICatchSynchroniser>();
        synchroniser.SynchronisePendingAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        synchroniser.RetryAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return synchroniser;
    }

    protected static ILocalCatchOwnerService SignedInOwner()
    {
        var owner = Substitute.For<ILocalCatchOwnerService>();
        owner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(OwnerUserId);
        return owner;
    }

    protected static ILocationService QuietLocation()
    {
        var location = Substitute.For<ILocationService>();
        location.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new LocationPromptStatus(false, false, false));
        location.TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((CatchLocationModel?)null);
        return location;
    }

    protected static ILocationService GrantedLocation(params CatchLocationModel[] captured)
    {
        var location = Substitute.For<ILocationService>();
        location.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new LocationPromptStatus(false, false, true));
        location.TryCaptureAsync(false, Arg.Any<CancellationToken>())
            .Returns(captured[0], captured.Skip(1).ToArray());
        return location;
    }

    protected static ILocationService GrantedLocationOnRequest(CatchLocationModel captured)
    {
        var location = Substitute.For<ILocationService>();
        location.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new LocationPromptStatus(false, false, true));
        location.TryCaptureAsync(true, Arg.Any<CancellationToken>()).Returns(captured);
        location.TryCaptureAsync(false, Arg.Any<CancellationToken>()).Returns((CatchLocationModel?)null);
        return location;
    }

    protected static ILocationService PromptLocation()
    {
        var location = Substitute.For<ILocationService>();
        location.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new LocationPromptStatus(true, false, false));
        location.TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((CatchLocationModel?)null);
        return location;
    }

    protected static ILocationService DeniedLocation()
    {
        var location = Substitute.For<ILocationService>();
        location.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new LocationPromptStatus(false, true, false));
        location.TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((CatchLocationModel?)null);
        return location;
    }

    protected static ILocationService HangingCaptureLocation()
    {
        var location = Substitute.For<ILocationService>();
        location.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new LocationPromptStatus(false, false, true));
        location.TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var token = call.ArgAt<CancellationToken>(1);
                var completion = new TaskCompletionSource<CatchLocationModel?>();
                token.Register(() => completion.TrySetCanceled(token));
                return completion.Task;
            });
        return location;
    }

    protected static CatchLocationModel SampleLocation(
        double latitude = 53.2707,
        double longitude = -9.0568)
    {
        return new CatchLocationModel(
            latitude,
            longitude,
            12,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
    }

    protected static InputFileContent PhotographFile(string name, string contentType, params byte[] bytes)
    {
        return InputFileContent.CreateFromBinary(bytes, name, contentType: contentType);
    }

    protected static InputFileContent JpegFile(string name, params byte[] bytes)
    {
        return PhotographFile(name, PhotographContentTypeConstants.Jpeg, bytes);
    }

    protected static InputFileContent PhotographFileModifiedOn(
        string name,
        string contentType,
        DateTimeOffset lastModified,
        params byte[] bytes)
    {
        return InputFileContent.CreateFromBinary(bytes, name, lastModified, contentType);
    }

    protected static byte[] MinimalPng()
    {
        return
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89,
            0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, 0x54,
            0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00, 0x05, 0x00, 0x01,
            0x0D, 0x0A, 0x2D, 0xB4,
            0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
        ];
    }

    protected static Guid VisiblePhotographId(IRenderedComponent<RecordCatch> cut)
    {
        var photographId = cut.Find("#catch-photo-carousel img")
            .GetAttribute("data-photograph-id");
        return Guid.Parse(photographId
            ?? throw new InvalidOperationException("The visible photograph has no photograph id."));
    }

    protected static Guid CurrentMetadataPhotographId(IRenderedComponent<RecordCatch> cut)
    {
        var photographId = cut.Find("#catch-photo-current-metadata")
            .GetAttribute("data-photograph-id");
        return Guid.Parse(photographId
            ?? throw new InvalidOperationException("The displayed metadata has no photograph id."));
    }
}
