using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Import.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Import.Pages.ImportCatchCatalogueTests;

public class BaseImportCatchCatalogueTest
{
    protected static readonly Guid MethodId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid SecondMethodId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid SpeciesId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    protected static readonly DateTimeOffset CapturedOn = DateTimeOffset.Parse("2025-06-14T09:30:00+01:00");

    protected static BunitContext CreateContext(
        IImportCatchProposalService proposal,
        IImportPhotoPreparationService preparation,
        IAnglerPreferencesProvider? preferences = null,
        IModalService? modalService = null,
        IImportTripProposalService? tripProposalService = null,
        IImportPersistenceService? persistenceService = null,
        INetworkService? networkService = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton<IMeasurementService, MeasurementService>();
        context.Services.AddSingleton(proposal);
        context.Services.AddSingleton(preparation);
        context.Services.AddSingleton(tripProposalService ?? new ImportTripProposalService());
        var persistence = persistenceService ?? Substitute.For<IImportPersistenceService>();
        if (persistenceService is null)
        {
            persistence.PersistAsync(
                    Arg.Any<ImportBatchModel>(),
                    Arg.Any<CancellationToken>(),
                    Arg.Any<IProgress<ImportPersistenceProgressModel>>())
                .Returns(new ImportPersistenceResultModel([], [], 0, 0));
        }

        context.Services.AddSingleton(persistence);
        var network = networkService ?? Substitute.For<INetworkService>();
        if (networkService is null)
        {
            network.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(true);
        }

        context.Services.AddSingleton(network);
        var existingTrips = Substitute.For<IImportExistingTripService>();
        existingTrips.GetCandidatesAsync(
                Arg.Any<IReadOnlyList<ImportTripProposalModel>>(),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, IReadOnlyList<TripSummaryDto>>());
        context.Services.AddSingleton(existingTrips);
        context.Services.AddSingleton(preferences ?? Preferences());
        context.Services.AddSingleton(modalService ?? SelectingModal(MethodId, SpeciesId));
        return context;
    }

    protected static IModalService SelectingModal(params Guid[] selections)
    {
        var remaining = new Queue<Guid>(selections);
        var modal = Substitute.For<IModalService>();
        modal.ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Any<CataloguePickerModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var model = call.Arg<CataloguePickerModalModel>();
                var selectedId = remaining.Dequeue();
                return new CataloguePickerModalResult(model.Options.Single(option => option.Id == selectedId));
            });
        return modal;
    }

    protected static IAnglerPreferencesProvider Preferences()
    {
        return Preferences(includeDefaults: true);
    }

    protected static IAnglerPreferencesProvider Preferences(bool includeDefaults)
    {
        var provider = Substitute.For<IAnglerPreferencesProvider>();
        provider.GetAsync(Arg.Any<CancellationToken>()).Returns(new AnglerPreferencesModel(
            new FishingCatalogueDto(
                [
                    new FishingMethodDto(MethodId, "Fly", "Fly"),
                    new FishingMethodDto(SecondMethodId, "Lure", "Lure")
                ],
                [new SpeciesDto(SpeciesId, "BrownTrout", "Brown Trout")]),
            new FishingPreferencesDto(includeDefaults
                ? [
                    new FishingMethodPreferenceDto(
                        MethodId,
                        "Fly",
                        "Fly",
                        true,
                        [new FishingSpeciesPreferenceDto(SpeciesId, "BrownTrout", "Brown Trout", true)])
                ]
                : []),
            WeightUnitEnum.Kg,
            LengthUnitEnum.Cm));
        return provider;
    }

    protected static ImportSelectedPhotoModel ReadyPhoto(
        int index,
        ImportTimestampModel? timestamp = null,
        ImportMetadataStatusEnum metadataStatus = ImportMetadataStatusEnum.Available)
    {
        var photo = new ImportSelectedPhotoModel(
            Guid.Parse($"44444444-4444-4444-4444-{index + 1:D12}"),
            index,
            "image/jpeg",
            1024,
            $"token-{index}",
            $"photo-{index}.jpg",
            $"blob:thumbnail-{index}");
        photo.SetPreparation(ImportPhotoPreparationStatusEnum.Ready, $"token-{index}", $"blob:thumbnail-{index}");
        photo.SetMetadata(
            metadataStatus,
            timestamp ?? ImportTimestampModel.FromExplicitInstant(
                CapturedOn.AddMinutes(index),
                ImportTimestampSourceEnum.ExifOriginal),
            new ImportLocationModel(null, null, false));
        return photo;
    }

    protected static ImportSelectedPhotoModel FailedPhoto(int index)
    {
        var photo = new ImportSelectedPhotoModel(
            Guid.Parse($"55555555-5555-5555-5555-{index + 1:D12}"),
            index,
            "image/heic",
            1024,
            null,
            $"failed-{index}.heic");
        photo.SetPreparation(ImportPhotoPreparationStatusEnum.UnsupportedType);
        return photo;
    }

    protected static IReadOnlyList<ImportCatchProposalModel> ProposalsFor(ImportBatchModel batch)
    {
        return batch.Photos
            .Where(photo => photo.IsReady && !photo.IsRemoved)
            .Select(photo => new ImportCatchProposalModel(
                Guid.NewGuid(),
                [photo.Id],
                photo.Timestamp,
                batch.FishingMethod,
                batch.Species,
                reasons: [photo.Timestamp.IsResolved
                    ? ImportCatchProposalReasonEnum.TrustworthyCaptureTime
                    : ImportCatchProposalReasonEnum.MissingTimestamp]))
            .ToArray();
    }
}
