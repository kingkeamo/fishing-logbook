using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;

namespace FishingLogBook.Mobile.Features.Trips.Services;

public interface IPlannedTripService
{
    Task<LocalTripRecordModel> PlanAsync(string? title, string? placeName, DateTimeOffset plannedStartOn, CancellationToken cancellationToken);
    Task<LocalTripRecordModel> UpdatePlanAsync(LocalTripRecordModel trip, string? title, string? placeName, DateTimeOffset plannedStartOn, CancellationToken cancellationToken);
    Task<LocalTripRecordModel> CancelPlanAsync(LocalTripRecordModel trip, CancellationToken cancellationToken);
    Task<IReadOnlyList<PlannedTripOptionModel>> GetEligibleAsync(CancellationToken cancellationToken);
    Task<Guid> ActivateAsync(PlannedTripOptionModel option, CancellationToken cancellationToken);
}
