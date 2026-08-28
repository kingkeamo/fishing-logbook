using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;

namespace FishingLogBook.Web.Features.Trips.Services;

public interface ITripCatchService
{
    Task<IReadOnlyList<CatchModel>> GetEligibleAsync(
        TripCatchScopeModel scope,
        TripStorageEnum storage,
        CancellationToken cancellationToken);

    Task<TripCatchAssociationModel> AssociateAsync(
        TripCatchScopeModel scope,
        IReadOnlyList<Guid> catchIds,
        TripStorageEnum storage,
        CancellationToken cancellationToken);
}
