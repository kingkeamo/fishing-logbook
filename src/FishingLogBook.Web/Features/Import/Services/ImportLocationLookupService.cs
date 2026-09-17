using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Locations.Clients;

namespace FishingLogBook.Web.Features.Import.Services;

public sealed class ImportLocationLookupService : IImportLocationLookupService
{
    private const int CoordinateDecimalPlaces = 4;
    private const int MaximumConcurrentLookups = 4;
    private readonly ILocationLookupClient _client;

    public ImportLocationLookupService(ILocationLookupClient client)
    {
        _client = client;
    }

    public async Task ResolveAsync(
        IReadOnlyList<ImportSelectedPhotoModel> photos,
        CancellationToken cancellationToken)
    {
        var groups = photos
            .Where(RequiresLookup)
            .GroupBy(photo => new CoordinateKey(
                Round(photo.Location.Latitude!.Value),
                Round(photo.Location.Longitude!.Value)))
            .Select(group => group.ToArray())
            .ToArray();
        await Parallel.ForEachAsync(
            groups,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = MaximumConcurrentLookups
            },
            async (group, token) => await ResolveGroupAsync(group, token));
    }

    private async Task ResolveGroupAsync(
        IReadOnlyList<ImportSelectedPhotoModel> photos,
        CancellationToken cancellationToken)
    {
        foreach (var photo in photos)
        {
            photo.SetLocation(photo.Location.WithLookup(ImportLocationLookupStatusEnum.Pending));
        }

        try
        {
            var first = photos[0].Location;
            var result = await _client.GetAsync(
                first.Latitude!.Value,
                first.Longitude!.Value,
                cancellationToken);
            var status = result is null
                ? ImportLocationLookupStatusEnum.NoResult
                : ImportLocationLookupStatusEnum.Resolved;
            foreach (var photo in photos)
            {
                photo.SetLocation(photo.Location.WithLookup(
                    status,
                    result is null
                        ? null
                        : new ImportLocationLookupResultModel(result.Components)
                        {
                            Street = result.Street,
                            VenueOrFacility = result.VenueOrFacility,
                            Locality = result.Locality,
                            DistrictOrNeighbourhood = result.DistrictOrNeighbourhood,
                            Region = result.Region,
                            Country = result.Country
                        }));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            foreach (var photo in photos)
            {
                photo.SetLocation(photo.Location.WithLookup(ImportLocationLookupStatusEnum.Failed));
            }
        }
    }

    private static bool RequiresLookup(ImportSelectedPhotoModel photo)
    {
        return !photo.IsRemoved
            && photo.Location.HasCanonicalCoordinates
            && photo.Location.LookupStatus is ImportLocationLookupStatusEnum.NotRequested
                or ImportLocationLookupStatusEnum.Failed;
    }

    private static double Round(double value)
    {
        return Math.Round(value, CoordinateDecimalPlaces, MidpointRounding.AwayFromZero);
    }

    private readonly record struct CoordinateKey(double Latitude, double Longitude);
}
