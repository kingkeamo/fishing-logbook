using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FishingLogBook.Application.LocationLookup.Contracts.HttpClients;
using FishingLogBook.Domain.Config;
using FishingLogBook.Shared.Dtos;
using MapsterMapper;
using Microsoft.Extensions.Options;

namespace FishingLogBook.Infrastructure.HttpClients.Geoapify;

public sealed class GeoapifyLocationLookupClient : ILocationLookupClient
{
    private readonly HttpClient _httpClient;
    private readonly IMapper _mapper;
    private readonly GeoapifyConfig _config;

    public GeoapifyLocationLookupClient(
        HttpClient httpClient,
        IOptions<GeoapifyConfig> config,
        IMapper mapper)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _mapper = mapper;
    }

    public async Task<LocationLookupDto?> ReverseGeocodeAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        if (!_config.IsConfigured)
        {
            throw new InvalidOperationException("Geoapify location lookup is not configured.");
        }

        var requestPath = BuildPath(latitude, longitude);
        using var response = await _httpClient.GetAsync(requestPath, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<GeoapifyResponse>(cancellationToken);
        var result = payload?.Results.FirstOrDefault();
        return result is null ? null : _mapper.Map<LocationLookupDto>(result);
    }

    private string BuildPath(double latitude, double longitude)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"v1/geocode/reverse?lat={latitude:F4}&lon={longitude:F4}&format=json&apiKey={Uri.EscapeDataString(_config.ApiKey)}");
    }

    private sealed record GeoapifyResponse(IReadOnlyList<GeoapifyResult> Results);

    internal sealed record GeoapifyResult(
        string? Name,
        string? Street,
        [property: JsonPropertyName("address_line1")]
        string? AddressLine1,
        string? Suburb,
        string? District,
        string? City,
        string? Town,
        string? Village,
        string? Municipality,
        string? County,
        string? State,
        string? Country,
        string? Formatted);
}
