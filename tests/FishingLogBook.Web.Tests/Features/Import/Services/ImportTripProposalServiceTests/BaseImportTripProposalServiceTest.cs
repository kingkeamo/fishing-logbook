using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Import.Services;

namespace FishingLogBook.Web.Tests.Features.Import.Services.ImportTripProposalServiceTests;

public class BaseImportTripProposalServiceTest
{
    protected static readonly DateTimeOffset StartedOn = new(2024, 6, 14, 9, 0, 0, TimeSpan.FromHours(1));
    protected readonly ImportTripProposalService Sut = new();

    protected static ImportBatchModel Batch(params CatchSpec[] catches)
    {
        var method = new ImportCatalogueSelectionModel(Guid.Parse("10000000-0000-0000-0000-000000000001"), "Fly", "Fly");
        var species = new ImportCatalogueSelectionModel(Guid.Parse("20000000-0000-0000-0000-000000000001"), "Trout", "Trout");
        var batch = new ImportBatchModel(Guid.NewGuid(), method, species);
        for (var index = 0; index < catches.Length; index++)
        {
            var spec = catches[index];
            var photoId = Guid.Parse($"30000000-0000-0000-0000-{index + 1:D12}");
            var proposalId = Guid.Parse($"40000000-0000-0000-0000-{index + 1:D12}");
            var timestamp = spec.Timestamp ?? ImportTimestampModel.UserConfirmed(StartedOn.Add(spec.Offset));
            var photo = new ImportSelectedPhotoModel(photoId, index, "image/jpeg", 100, "token", $"{index}.jpg");
            photo.SetPreparation(ImportPhotoPreparationStatusEnum.Ready, "token", "blob:test");
            photo.SetMetadata(ImportMetadataStatusEnum.Available, timestamp, new ImportLocationModel(null, null, false));
            batch.AddPhoto(photo);
            var location = spec.Latitude.HasValue
                ? new ImportLocationModel(
                    spec.Latitude,
                    spec.Longitude,
                    true,
                    lookupStatus: spec.LookupResult is null
                        ? ImportLocationLookupStatusEnum.NotRequested
                        : ImportLocationLookupStatusEnum.Resolved,
                    lookupResult: spec.LookupResult).Accept()
                : null;
            var proposal = new ImportCatchProposalModel(proposalId, [photoId], timestamp, method, species, location);
            if (spec.Reviewed)
            {
                proposal.MarkReviewed();
            }

            batch.AddCatchProposal(proposal);
        }

        return batch;
    }

    protected sealed record CatchSpec(
        TimeSpan Offset,
        double? Latitude = null,
        double? Longitude = null,
        bool Reviewed = true,
        ImportTimestampModel? Timestamp = null,
        ImportLocationLookupResultModel? LookupResult = null);
}
