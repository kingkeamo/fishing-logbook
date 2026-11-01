using FishingLogBook.Web.Features.Trips.Models;

namespace FishingLogBook.Web.Features.Trips.Services;

public interface IPlannedTripService
{
    Task<TripModel> PlanAsync(
        Guid ownerUserId,
        string? title,
        string? placeName,
        DateTimeOffset plannedStartOn,
        CancellationToken cancellationToken);

    Task<TripModel?> UpdatePlanAsync(
        Guid ownerUserId,
        Guid tripId,
        string? title,
        string? placeName,
        DateTimeOffset plannedStartOn,
        CancellationToken cancellationToken);

    Task<TripModel?> CancelPlanAsync(Guid ownerUserId, Guid tripId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlannedTripOptionModel>> GetEligibleAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken);

    Task<TripModel> ActivateAsync(
        Guid viewerUserId,
        PlannedTripOptionModel option,
        CancellationToken cancellationToken);
}
