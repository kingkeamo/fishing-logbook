using FishingLogBook.Application.Args;
using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.Trips.Contracts.Services;

public interface ITripDetailService
{
    Task<Result<TripDetailDto>> GetAsync(GetTripArgs args, CancellationToken cancellationToken);
}
