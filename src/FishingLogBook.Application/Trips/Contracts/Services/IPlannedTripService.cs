using FishingLogBook.Application.Args;
using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.Trips.Contracts.Services;

public interface IPlannedTripService
{
    Task<Result<IReadOnlyList<PlannedTripOptionDto>>> GetEligibleAsync(
        GetPlannedTripsArgs args,
        CancellationToken cancellationToken);
}
