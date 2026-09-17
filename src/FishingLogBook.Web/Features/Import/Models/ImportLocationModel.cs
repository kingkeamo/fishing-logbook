using FishingLogBook.Web.Features.Import.Enums;

namespace FishingLogBook.Web.Features.Import.Models;

public sealed record ImportLocationModel
{
    public ImportLocationModel(
        double? latitude,
        double? longitude,
        bool historicalGpsPresent,
        ImportLocationDecisionEnum decision = ImportLocationDecisionEnum.Undecided,
        ImportLocationLookupStatusEnum lookupStatus = ImportLocationLookupStatusEnum.NotRequested,
        ImportLocationLookupResultModel? lookupResult = null,
        string? placeName = null)
    {
        if (latitude.HasValue != longitude.HasValue)
        {
            throw new ArgumentException("Latitude and longitude must be supplied together.");
        }

        if (historicalGpsPresent != latitude.HasValue)
        {
            throw new ArgumentException("Historical GPS presence must match the coordinate state.");
        }

        if (decision == ImportLocationDecisionEnum.Accepted && !latitude.HasValue)
        {
            throw new ArgumentException("A location without coordinates cannot be accepted.");
        }

        if (lookupStatus == ImportLocationLookupStatusEnum.Resolved && lookupResult is null)
        {
            throw new ArgumentException("A resolved lookup requires a result.", nameof(lookupResult));
        }

        Latitude = latitude;
        Longitude = longitude;
        HistoricalGpsPresent = historicalGpsPresent;
        Decision = decision;
        LookupStatus = lookupStatus;
        LookupResult = lookupResult;
        PlaceName = HasCanonicalCoordinates ? NormalizePlaceName(placeName) : null;
    }

    public double? Latitude { get; }

    public double? Longitude { get; }

    public bool HistoricalGpsPresent { get; }

    public ImportLocationDecisionEnum Decision { get; }

    public ImportLocationLookupStatusEnum LookupStatus { get; }

    public ImportLocationLookupResultModel? LookupResult { get; }

    public string? PlaceName { get; }

    public bool HasCanonicalCoordinates
    {
        get
        {
            return Latitude.HasValue && Longitude.HasValue;
        }
    }

    public ImportLocationModel Accept()
    {
        if (!HasCanonicalCoordinates)
        {
            throw new InvalidOperationException("A location without coordinates cannot be accepted.");
        }

        return new ImportLocationModel(
            Latitude,
            Longitude,
            HistoricalGpsPresent,
            ImportLocationDecisionEnum.Accepted,
            LookupStatus,
            LookupResult,
            PlaceName);
    }

    public ImportLocationModel Remove()
    {
        return new ImportLocationModel(
            Latitude,
            Longitude,
            HistoricalGpsPresent,
            ImportLocationDecisionEnum.Removed,
            LookupStatus,
            LookupResult,
            placeName: null);
    }

    public ImportLocationModel WithLookup(
        ImportLocationLookupStatusEnum status,
        ImportLocationLookupResultModel? result = null)
    {
        return new ImportLocationModel(
            Latitude,
            Longitude,
            HistoricalGpsPresent,
            Decision,
            status,
            result,
            status == ImportLocationLookupStatusEnum.Resolved ? result?.DisplayName : null);
    }

    public ImportLocationModel WithPlaceName(string? placeName)
    {
        return new ImportLocationModel(
            Latitude,
            Longitude,
            HistoricalGpsPresent,
            Decision,
            LookupStatus,
            LookupResult,
            placeName);
    }

    private static string? NormalizePlaceName(string? placeName)
    {
        var normalized = placeName?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
