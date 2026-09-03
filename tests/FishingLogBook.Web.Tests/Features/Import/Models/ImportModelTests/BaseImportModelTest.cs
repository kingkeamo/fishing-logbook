using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;

namespace FishingLogBook.Web.Tests.Features.Import.Models.ImportModelTests;

public class BaseImportModelTest
{
    protected static readonly Guid MethodId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid SpeciesId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid PhotoId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    protected static readonly Guid SecondPhotoId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    protected static readonly Guid CatchId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    protected static readonly Guid SecondCatchId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    protected static readonly DateTimeOffset CapturedOn = DateTimeOffset.Parse("2025-06-14T09:30:00+01:00");

    protected static ImportCatalogueSelectionModel Method(string name = "Fly")
    {
        return new ImportCatalogueSelectionModel(MethodId, "Fly", name);
    }

    protected static ImportCatalogueSelectionModel Species(string name = "Brown Trout")
    {
        return new ImportCatalogueSelectionModel(SpeciesId, "BrownTrout", name);
    }

    protected static ImportSelectedPhotoModel Photo(Guid? id = null, int index = 0)
    {
        return new ImportSelectedPhotoModel(
            id ?? PhotoId,
            index,
            "image/jpeg",
            1024,
            $"blob-{index}",
            $"photo-{index}.jpg",
            $"blob:thumbnail-{index}");
    }

    protected static ImportCatchProposalModel Catch(
        Guid? id = null,
        IReadOnlyList<Guid>? photoIds = null,
        ImportTimestampModel? caughtOn = null)
    {
        return new ImportCatchProposalModel(
            id ?? CatchId,
            photoIds ?? [PhotoId],
            caughtOn ?? ImportTimestampModel.UserConfirmed(CapturedOn),
            Method(),
            Species());
    }

    protected static ImportTripProposalModel Trip(
        Guid? id = null,
        IReadOnlyList<Guid>? catchIds = null)
    {
        return new ImportTripProposalModel(
            id ?? Guid.Parse("77777777-7777-7777-7777-777777777777"),
            catchIds ?? [CatchId],
            ImportTripSuggestionConfidenceEnum.Strong,
            [ImportTripSuggestionReasonEnum.SameDate, ImportTripSuggestionReasonEnum.NearbyCoordinates],
            CapturedOn,
            CapturedOn.AddHours(2));
    }

    protected static ImportBatchModel Batch(
        ImportCatalogueSelectionModel? method = null,
        ImportCatalogueSelectionModel? species = null)
    {
        return new ImportBatchModel(Guid.NewGuid(), method ?? Method(), species ?? Species());
    }
}
