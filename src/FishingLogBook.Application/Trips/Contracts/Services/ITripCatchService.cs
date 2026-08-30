using FishingLogBook.Application.Args;
using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.Trips.Contracts.Services;

public interface ITripCatchService
{
    Task<Result<TripCatchAssociationDto>> AssociateAsync(
        AssociateTripCatchesArgs args,
        CancellationToken cancellationToken);
}
