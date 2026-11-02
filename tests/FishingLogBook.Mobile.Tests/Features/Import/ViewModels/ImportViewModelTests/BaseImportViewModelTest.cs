using System.Globalization;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Import.Enums;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Features.Import.Services;
using FishingLogBook.Mobile.Features.Import.ViewModels;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Measurements;
using FishingLogBook.Shared.Photographs.Contracts;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Import.ViewModels.ImportViewModelTests;

public abstract class BaseImportViewModelTest
{
    protected static readonly Guid MethodId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid SpeciesId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid OtherMethodId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    protected UiTextService Text { get; } = new(CultureInfo.GetCultureInfo("en-GB"));
    protected IAnglerPreferencesService MockPreferences { get; } = Substitute.For<IAnglerPreferencesService>();
    protected IImportCatchProposalService MockCatchProposals { get; } = Substitute.For<IImportCatchProposalService>();
    protected IImportTripProposalService MockTripProposals { get; } = Substitute.For<IImportTripProposalService>();
    protected IImportExistingTripService MockExistingTrips { get; } = Substitute.For<IImportExistingTripService>();
    protected IImportLocationLookupService MockLocations { get; } = Substitute.For<IImportLocationLookupService>();
    protected IImportPhotoPreparationService MockPreparation { get; } = Substitute.For<IImportPhotoPreparationService>();
    protected IImportPersistenceService MockPersistence { get; } = Substitute.For<IImportPersistenceService>();
    protected IPhotographMediaAdapter MockMedia { get; } = Substitute.For<IPhotographMediaAdapter>();
    protected IPhotographLocalTimeMapper MockLocalTime { get; } = Substitute.For<IPhotographLocalTimeMapper>();
    protected IConnectivityService MockConnectivity { get; } = Substitute.For<IConnectivityService>();
    protected IImportModalService MockModals { get; } = Substitute.For<IImportModalService>();
    protected IAccountUnitsService MockUnits { get; } = Substitute.For<IAccountUnitsService>();
    protected ImportViewModel Sut { get; }

    protected BaseImportViewModelTest()
    {
        MockUnits.GetAsync(Arg.Any<CancellationToken>()).Returns(MeasurementUnitsModel.Default);
        MockPreferences.RefreshAsync(Arg.Any<CancellationToken>()).Returns(ApiResultModel<AnglerPreferencesModel>.Succeeded(Preferences()));
        MockConnectivity.IsOnline.Returns(true);
        MockLocalTime.FromDateTimeLocalValueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => DateTimeOffset.Parse(
                callInfo.Arg<string>(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal));
        Sut = CreateSut();
    }

    protected ImportViewModel CreateSut()
    {
        return new ImportViewModel(
            MockPreferences,
            MockCatchProposals,
            MockTripProposals,
            MockExistingTrips,
            MockLocations,
            MockPreparation,
            MockPersistence,
            MockMedia,
            MockLocalTime,
            MockConnectivity,
            MockModals,
            new MeasurementService(),
            MockUnits,
            Text);
    }

    protected async Task ReviewPreparedPhotoAsync(ImportSelectedPhotoModel photo)
    {
        MockMedia.PickPhotosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PhotographPickResultModel.FromFiles(
            [
                new PhotographPickedFileModel { FullPath = "photo.jpg", ContentType = "image/jpeg" }
            ]));
        MockPreparation.PrepareSelectionAsync(Arg.Any<IReadOnlyList<PhotographPickedFileModel>>(), Arg.Any<CancellationToken>())
            .Returns([photo]);
        MockCatchProposals.Propose(Arg.Any<ImportBatchModel>())
            .Returns(callInfo => new ImportCatchProposalService().Propose(callInfo.Arg<ImportBatchModel>()));

        await Sut.LoadAsync(CancellationToken.None);
        await Sut.ContinueAsync(CancellationToken.None);
        await Sut.PickPhotosAsync(CancellationToken.None);
        await Sut.ContinueAsync(CancellationToken.None);
    }

    protected static ImportSelectedPhotoModel ReadyPhoto(
        ImportTimestampModel? timestamp = null,
        string fileName = "0e0fa2fbeb9442b185757fefb9cf9288.png")
    {
        var photo = new ImportSelectedPhotoModel(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            0,
            "image/jpeg",
            100,
            "token",
            fileName,
            "file://thumb.jpg");
        photo.SetPreparation(ImportPhotoPreparationStatusEnum.Ready, "token", "file://thumb.jpg");
        photo.SetMetadata(
            ImportMetadataStatusEnum.Available,
            timestamp ?? ImportTimestampModel.FromExplicitInstant(
                new DateTimeOffset(2026, 11, 1, 15, 5, 0, TimeSpan.Zero),
                ImportTimestampSourceEnum.ExifOriginal),
            new ImportLocationModel(null, null, false));
        return photo;
    }

    protected static AnglerPreferencesModel Preferences()
    {
        var species = new SpeciesDto(SpeciesId, "BROWN", "Brown trout");
        var method = new FishingMethodDto(MethodId, "FLY", "Fly");
        var other = new FishingMethodDto(OtherMethodId, "SPIN", "Spinning");
        return new AnglerPreferencesModel(
            new FishingCatalogueDto([method, other], [species]),
            new FishingPreferencesDto(
            [
                new FishingMethodPreferenceDto(
                    MethodId,
                    "FLY",
                    "Fly",
                    true,
                    [new FishingSpeciesPreferenceDto(SpeciesId, "BROWN", "Brown trout", true)])
            ]),
            new FishingLocationPreferencesDto([]));
    }
}
