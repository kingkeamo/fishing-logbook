using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Catch.Clients;
using FishingLogBook.Mobile.Features.Catch.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using FishingLogBook.Mobile.Features.Catch.Services;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;

namespace FishingLogBook.Mobile.Features.Trips.Services;

/// <summary>
/// Moves the signed-in angler's own catches into and out of a trip. A change is an ordinary local catch
/// save, so it works offline and reaches the server through the normal catch synchroniser, which already
/// waits for the trip to exist.
/// </summary>
public sealed class TripCatchService(
    ICatchStore catchStore,
    ICatchRemoteClient remoteClient,
    ICatchWorkingCopyService workingCopies,
    IAccountSessionCoordinator session,
    IConnectivityService connectivity,
    TimeProvider time) : ITripCatchService
{
    public async Task<IReadOnlyList<TripCatchCandidateModel>> GetEligibleAsync(LocalTripRecordModel trip, CancellationToken cancellationToken)
    {
        if (session.Capture() is not { } lease)
        {
            return [];
        }

        var candidates = new Dictionary<Guid, TripCatchCandidateModel>();
        if (connectivity.IsOnline)
        {
            var response = await remoteClient.ListAsync(cancellationToken);
            foreach (var view in response.Value ?? [])
            {
                if (view.TripId is null && view.CaughtByUserId == lease.UserId && view.RecordedByUserId == lease.UserId)
                {
                    candidates[view.Id] = new TripCatchCandidateModel(view.Id, view.CaughtOn, view.SpeciesName, view.Weight, view.Length);
                }
            }
        }

        foreach (var local in await catchStore.ListAsync(cancellationToken))
        {
            if (local.Lifecycle != LocalCatchLifecycleEnum.Saved || local.UserId != lease.UserId)
            {
                continue;
            }

            if (local.TripId is null && local.CaughtByUserId == lease.UserId && local.RecordedByUserId == lease.UserId)
            {
                candidates[local.Id] = new TripCatchCandidateModel(local.Id, local.CaughtOn, local.SpeciesName, (decimal?)local.Weight, (decimal?)local.Length);
            }
            else
            {
                candidates.Remove(local.Id);
            }
        }

        var ceiling = trip.Detail.Trip.EndedOn ?? time.GetUtcNow();
        return candidates.Values
            .Where(candidate => candidate.CaughtOn >= trip.Detail.Trip.StartedOn && candidate.CaughtOn <= ceiling)
            .OrderBy(candidate => candidate.CaughtOn)
            .ThenBy(candidate => candidate.Id)
            .ToArray();
    }

    public async Task<TripCatchChangeModel> AssociateAsync(LocalTripRecordModel trip, IReadOnlyList<Guid> catchIds, CancellationToken cancellationToken)
    {
        var eligible = (await GetEligibleAsync(trip, cancellationToken)).Select(candidate => candidate.Id).ToHashSet();
        var changed = new List<Guid>();
        var rejected = new List<Guid>();
        foreach (var catchId in catchIds.Distinct())
        {
            if (eligible.Contains(catchId) && await ChangeTripAsync(catchId, trip.Detail.Trip.Id, trip.Detail.Trip.Title, cancellationToken))
            {
                changed.Add(catchId);
            }
            else
            {
                rejected.Add(catchId);
            }
        }

        return new TripCatchChangeModel(changed, rejected);
    }

    public Task<bool> RemoveAsync(Guid catchId, CancellationToken cancellationToken)
    {
        return ChangeTripAsync(catchId, null, null, cancellationToken);
    }

    private async Task<bool> ChangeTripAsync(Guid catchId, Guid? tripId, string? tripTitle, CancellationToken cancellationToken)
    {
        if (session.Capture() is not { } lease
            || await workingCopies.EnsureAsync(catchId, cancellationToken) != WorkingCopyOutcomeEnum.Ready
            || await catchStore.GetAsync(catchId, cancellationToken) is not { } local
            || local.Lifecycle != LocalCatchLifecycleEnum.Saved
            || local.UserId != lease.UserId
            || local.CaughtByUserId != lease.UserId
            || local.RecordedByUserId != lease.UserId)
        {
            return false;
        }

        var result = await catchStore.SaveAsync(new LocalCatchSaveArgs
        {
            Id = local.Id,
            TripId = tripId,
            TripTitle = tripTitle,
            CaughtByUserId = local.CaughtByUserId,
            RecordedByUserId = local.RecordedByUserId,
            CaughtOn = local.CaughtOn,
            SpeciesName = local.SpeciesName,
            Weight = local.Weight,
            Length = local.Length,
            Method = local.Method,
            BaitOrLure = local.BaitOrLure,
            Notes = local.Notes,
            PlaceName = local.PlaceName,
            Location = local.Location,
            Lifecycle = LocalCatchLifecycleEnum.Saved,
            Photographs = local.Photographs.Select(photograph => new LocalCatchPhotographSaveArgs
            {
                Id = photograph.Id,
                SortOrder = photograph.SortOrder,
                LocalFilePath = photograph.LocalFilePath,
                ContentType = photograph.ContentType,
                Metadata = photograph.Metadata
            }).ToArray()
        }, cancellationToken);
        return result.IsSuccess;
    }
}
