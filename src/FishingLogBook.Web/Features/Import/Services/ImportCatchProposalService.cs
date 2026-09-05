using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;

namespace FishingLogBook.Web.Features.Import.Services;

public sealed class ImportCatchProposalService : IImportCatchProposalService
{
    public static readonly TimeSpan SameCatchTimeThreshold = TimeSpan.FromMinutes(5);

    private const double CompatibleLocationDistanceMetres = 250;
    private const double EarthRadiusMetres = 6371000;

    public IReadOnlyList<ImportCatchProposalModel> Propose(ImportBatchModel batch)
    {
        var photos = batch.Photos.Where(photo => !photo.IsRemoved && photo.IsReady).ToArray();
        var explicitInstants = photos
            .Where(HasTrustworthyTimestamp)
            .OrderBy(photo => photo.Timestamp.Instant)
            .ThenBy(photo => photo.SelectionIndex)
            .ToArray();
        var localWallClocks = photos
            .Where(HasCompatibleLocalTimestamp)
            .OrderBy(photo => photo.Timestamp.LocalWallClock)
            .ThenBy(photo => photo.SelectionIndex)
            .ToArray();
        var weakFallbacks = photos
            .Where(HasCompatibleWeakTimestamp)
            .OrderBy(photo => photo.Timestamp.Instant)
            .ThenBy(photo => photo.SelectionIndex)
            .ToArray();
        var unresolved = photos
            .Where(photo => !HasTrustworthyTimestamp(photo)
                && !HasCompatibleLocalTimestamp(photo)
                && !HasCompatibleWeakTimestamp(photo))
            .OrderBy(photo => photo.SelectionIndex)
            .ToArray();

        var proposals = GroupEligiblePhotos(
            explicitInstants,
            photo => photo.Timestamp.Instant!.Value.UtcDateTime,
            batch);
        proposals.AddRange(GroupEligiblePhotos(
            localWallClocks,
            photo => photo.Timestamp.LocalWallClock!.Value,
            batch));
        proposals.AddRange(GroupEligiblePhotos(
            weakFallbacks,
            photo => photo.Timestamp.Instant!.Value.UtcDateTime,
            batch));
        proposals.AddRange(unresolved.Select(photo => CreateProposal([photo], batch)));
        return proposals;
    }

    private static List<ImportCatchProposalModel> GroupEligiblePhotos(
        IReadOnlyList<ImportSelectedPhotoModel> photos,
        Func<ImportSelectedPhotoModel, DateTime> timestamp,
        ImportBatchModel batch)
    {
        var proposals = new List<ImportCatchProposalModel>();
        var group = new List<ImportSelectedPhotoModel>();
        foreach (var photo in photos)
        {
            if (group.Count > 0 && timestamp(photo) - timestamp(group[0]) > SameCatchTimeThreshold)
            {
                proposals.Add(CreateProposal(group, batch));
                group = [];
            }

            group.Add(photo);
        }

        if (group.Count > 0)
        {
            proposals.Add(CreateProposal(group, batch));
        }

        return proposals;
    }

    private static ImportCatchProposalModel CreateProposal(
        IReadOnlyList<ImportSelectedPhotoModel> photos,
        ImportBatchModel batch)
    {
        var hasGpsConflict = HasGpsConflict(photos);
        var reasons = new List<ImportCatchProposalReasonEnum> { TimestampReason(photos[0].Timestamp) };
        if (hasGpsConflict)
        {
            reasons.Add(ImportCatchProposalReasonEnum.ConflictingGps);
        }

        return new ImportCatchProposalModel(
            Guid.NewGuid(),
            photos.Select(photo => photo.Id),
            photos[0].Timestamp,
            batch.FishingMethod,
            batch.Species,
            hasGpsConflict ? null : RepresentativeLocation(photos),
            reasons);
    }

    private static bool HasTrustworthyTimestamp(ImportSelectedPhotoModel photo)
    {
        return photo.Timestamp.IsResolved && photo.Timestamp.Instant.HasValue;
    }

    private static bool HasCompatibleLocalTimestamp(ImportSelectedPhotoModel photo)
    {
        return photo.Timestamp.State == ImportTimestampStateEnum.LocalWallClock
            && photo.Timestamp.LocalWallClock.HasValue;
    }

    private static bool HasCompatibleWeakTimestamp(ImportSelectedPhotoModel photo)
    {
        return photo.Timestamp.State == ImportTimestampStateEnum.WeakFallback
            && photo.Timestamp.Instant.HasValue;
    }

    private static ImportCatchProposalReasonEnum TimestampReason(ImportTimestampModel timestamp)
    {
        return timestamp.State switch
        {
            ImportTimestampStateEnum.LocalWallClock => ImportCatchProposalReasonEnum.AmbiguousTimestamp,
            ImportTimestampStateEnum.WeakFallback => ImportCatchProposalReasonEnum.WeakTimestamp,
            ImportTimestampStateEnum.Unusable => ImportCatchProposalReasonEnum.UnusableTimestamp,
            ImportTimestampStateEnum.Missing => ImportCatchProposalReasonEnum.MissingTimestamp,
            _ => ImportCatchProposalReasonEnum.TrustworthyCaptureTime
        };
    }

    private static ImportLocationModel? RepresentativeLocation(IReadOnlyList<ImportSelectedPhotoModel> photos)
    {
        return photos.FirstOrDefault(photo => photo.Location.HasCanonicalCoordinates)?.Location;
    }

    private static bool HasGpsConflict(IReadOnlyList<ImportSelectedPhotoModel> photos)
    {
        var located = photos.Where(photo => photo.Location.HasCanonicalCoordinates).ToArray();
        for (var first = 0; first < located.Length - 1; first++)
        {
            for (var second = first + 1; second < located.Length; second++)
            {
                if (DistanceMetres(located[first].Location, located[second].Location)
                    > CompatibleLocationDistanceMetres)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static double DistanceMetres(ImportLocationModel first, ImportLocationModel second)
    {
        var firstLatitude = ToRadians(first.Latitude!.Value);
        var secondLatitude = ToRadians(second.Latitude!.Value);
        var latitudeDelta = ToRadians(second.Latitude.Value - first.Latitude.Value);
        var longitudeDelta = ToRadians(second.Longitude!.Value - first.Longitude!.Value);
        var haversine = (Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2))
            + (Math.Cos(firstLatitude) * Math.Cos(secondLatitude)
                * Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2));
        return 2 * EarthRadiusMetres * Math.Asin(Math.Min(1, Math.Sqrt(haversine)));
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
}
