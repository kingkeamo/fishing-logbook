using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;

namespace FishingLogBook.Web.Features.Trips.Services;

public sealed class TripCatchService : ITripCatchService
{
    private readonly ICatchStore _catchStore;
    private readonly ICatchClient _catchClient;
    private readonly ITripClient _tripClient;

    public TripCatchService(
        ICatchStore catchStore,
        ICatchClient catchClient,
        ITripClient tripClient)
    {
        _catchStore = catchStore;
        _catchClient = catchClient;
        _tripClient = tripClient;
    }

    public async Task<IReadOnlyList<CatchModel>> GetEligibleAsync(
        TripCatchScopeModel scope,
        TripStorageEnum storage,
        CancellationToken cancellationToken)
    {
        var candidates = storage == TripStorageEnum.Server
            ? (await _catchClient.GetAllAsync(cancellationToken)).Select(ToCatchModel)
            : await _catchStore.GetMetadataAsync(scope.OwnerUserId, cancellationToken);
        return
        [
            .. candidates
                .Where(candidate => IsEligible(candidate, scope))
                .OrderBy(candidate => candidate.CaughtOn)
        ];
    }

    public async Task<TripCatchAssociationModel> AssociateAsync(
        TripCatchScopeModel scope,
        IReadOnlyList<Guid> catchIds,
        TripStorageEnum storage,
        CancellationToken cancellationToken)
    {
        if (catchIds.Count == 0)
        {
            return new TripCatchAssociationModel([], []);
        }

        if (storage == TripStorageEnum.Server)
        {
            var association = await _tripClient.AssociateCatchesAsync(
                scope.TripId,
                new AssociateTripCatchesDto(catchIds),
                cancellationToken);
            return association is null
                ? new TripCatchAssociationModel([], catchIds)
                : new TripCatchAssociationModel(
                    association.AssociatedCatchIds,
                    association.RejectedCatchIds);
        }

        foreach (var catchId in catchIds)
        {
            await _catchStore.UpdateTripAsync(
                scope.OwnerUserId,
                catchId,
                scope.TripId,
                cancellationToken);
        }

        return new TripCatchAssociationModel(catchIds, []);
    }

    private static bool IsEligible(CatchModel candidate, TripCatchScopeModel scope)
    {
        if (candidate.TripId is not null || candidate.UserId != scope.OwnerUserId)
        {
            return false;
        }

        if (candidate.CaughtOn < scope.StartedOn)
        {
            return false;
        }

        return candidate.CaughtOn <= (scope.EndedOn ?? DateTimeOffset.UtcNow);
    }

    private static CatchModel ToCatchModel(CatchViewDto dto)
    {
        return new CatchModel(
            dto.Id,
            dto.CaughtOn,
            [.. dto.Photographs.Select(photograph => new CatchPhotographModel(
                photograph.Id,
                dto.Id,
                photograph.ContentType,
                RemoteUrl: photograph.Url))],
            SpeciesName: dto.SpeciesName,
            UserId: dto.UserId,
            SyncStatus: SyncStatus.Synchronised,
            MetadataSyncStatus: SyncStatus.Synchronised,
            AnglerUserId: dto.AnglerUserId,
            RecordedByUserId: dto.RecordedByUserId,
            Weight: dto.Weight,
            Length: dto.Length,
            Method: dto.Method,
            TripId: dto.TripId);
    }
}
