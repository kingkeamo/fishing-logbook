using FishingLogBook.Application.LocationLookup.Contracts.HttpClients;
using FishingLogBook.Application.LocationLookup.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Application.LocationLookup.Services;

public sealed class LocationLookupService : ILocationLookupService
{
    private const int CoordinateDecimalPlaces = 4;
    private readonly ILocationLookupClient _client;
    private readonly LocationLookupCacheService _cache;
    private readonly ILogger<LocationLookupService> _logger;

    public LocationLookupService(
        ILocationLookupClient client,
        LocationLookupCacheService cache,
        ILogger<LocationLookupService> logger)
    {
        _client = client;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<LocationLookupDto?>> ReverseGeocodeAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var roundedLatitude = Round(latitude);
        var roundedLongitude = Round(longitude);
        var lookup = _cache.GetOrAdd(
            roundedLatitude,
            roundedLongitude,
            (lookupLatitude, lookupLongitude) => _client.ReverseGeocodeAsync(
                lookupLatitude,
                lookupLongitude,
                CancellationToken.None));
        try
        {
            return Result.Ok(await lookup.Value.WaitAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Historical location lookup failed without blocking Import.");
            _cache.Remove(roundedLatitude, roundedLongitude);
            return Result.Ok<LocationLookupDto?>(null);
        }
    }

    private static double Round(double value)
    {
        return Math.Round(value, CoordinateDecimalPlaces, MidpointRounding.AwayFromZero);
    }
}
