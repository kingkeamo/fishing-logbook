using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.LocationLookup.Contracts.Services;

public interface ILocationLookupService
{
    Task<Result<LocationLookupDto?>> ReverseGeocodeAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken);
}
