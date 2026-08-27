using FishingLogBook.Application.Args;
using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.Contracts.Services;

public interface ITripService
{
    Task<Result<TripDto>> UpsertAsync(UpsertTripArgs args, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<TripSummaryDto>>> GetSummariesAsync(
        GetMyTripsArgs args,
        CancellationToken cancellationToken);
}
