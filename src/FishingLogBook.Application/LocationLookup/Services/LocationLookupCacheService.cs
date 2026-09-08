using System.Collections.Concurrent;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Application.LocationLookup.Services;

public sealed class LocationLookupCacheService
{
    private const int MaximumEntries = 500;
    private readonly ConcurrentDictionary<CoordinateKey, Lazy<Task<LocationLookupDto?>>> _entries = new();
    private readonly ConcurrentQueue<CoordinateKey> _order = new();

    public Lazy<Task<LocationLookupDto?>> GetOrAdd(
        double latitude,
        double longitude,
        Func<double, double, Task<LocationLookupDto?>> lookup)
    {
        var key = new CoordinateKey(latitude, longitude);
        var entry = _entries.GetOrAdd(
            key,
            coordinate =>
            {
                _order.Enqueue(coordinate);
                return new Lazy<Task<LocationLookupDto?>>(
                    () => lookup(coordinate.Latitude, coordinate.Longitude),
                    LazyThreadSafetyMode.ExecutionAndPublication);
            });
        Trim();
        return entry;
    }

    public void Remove(double latitude, double longitude)
    {
        _entries.TryRemove(new CoordinateKey(latitude, longitude), out _);
    }

    private void Trim()
    {
        while (_entries.Count > MaximumEntries && _order.TryDequeue(out var oldest))
        {
            _entries.TryRemove(oldest, out _);
        }
    }

    private readonly record struct CoordinateKey(double Latitude, double Longitude);
}
