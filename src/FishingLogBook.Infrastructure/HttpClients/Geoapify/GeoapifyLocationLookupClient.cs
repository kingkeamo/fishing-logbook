using System.Globalization;
using System.Net.Http.Json;
using FishingLogBook.Application.LocationLookup.Contracts.HttpClients;
using FishingLogBook.Domain.Config;
using FishingLogBook.Shared.Dtos;
using Microsoft.Extensions.Options;

namespace FishingLogBook.Infrastructure.HttpClients.Geoapify;

public sealed class GeoapifyLocationLookupClient : ILocationLookupClient
{
    private readonly HttpClient _httpClient;
    private readonly GeoapifyConfig _config;

    public GeoapifyLocationLookupClient(
        HttpClient httpClient,
        IOptions<GeoapifyConfig> config)
    {
        _httpClient = httpClient;
        _config = config.Value;
    }

    public async Task<LocationLookupDto?> ReverseGeocodeAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        if (!_config.IsConfigured)
        {
            return null;
        }

        var requestPath = BuildPath(latitude, longitude);
        using var response = await _httpClient.GetAsync(requestPath, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<GeoapifyResponse>(cancellationToken);
        return Map(payload?.Results.FirstOrDefault());
    }

    private string BuildPath(double latitude, double longitude)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"v1/geocode/reverse?lat={latitude:F4}&lon={longitude:F4}&format=json&apiKey={Uri.EscapeDataString(_config.ApiKey)}");
    }

    private static LocationLookupDto? Map(GeoapifyResult? result)
    {
        if (result is null)
        {
            return null;
        }

        var locality = FirstPopulated(result.City, result.Town, result.Village, result.Municipality, result.County);
        var region = FirstPopulated(result.State, result.County);
        var parts = new[] { locality, region, result.Country }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return parts.Length == 0
            ? null
            : new LocationLookupDto(string.Join(", ", parts), locality, region, result.Country);
    }

    private static string? FirstPopulated(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private sealed record GeoapifyResponse(IReadOnlyList<GeoapifyResult> Results);

    private sealed record GeoapifyResult(
        string? City,
        string? Town,
        string? Village,
        string? Municipality,
        string? County,
        string? State,
        string? Country);
}
