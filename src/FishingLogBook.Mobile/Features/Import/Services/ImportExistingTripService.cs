using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Import.Services;

public sealed class ImportExistingTripService : IImportExistingTripService
{
    private readonly ITripClient _tripClient;

    public ImportExistingTripService(ITripClient tripClient)
    {
        _tripClient = tripClient;
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<TripSummaryDto>>> GetCandidatesAsync(
        IReadOnlyList<ImportTripProposalModel> proposals,
        CancellationToken cancellationToken)
    {
        var listed = await _tripClient.ListAsync(cancellationToken);
        if (!listed.IsSuccess || listed.Value is null)
        {
            throw new InvalidOperationException("Existing trips could not be loaded.");
        }

        var temporal = proposals.ToDictionary(
            proposal => proposal.Id,
            proposal => listed.Value.Where(trip => IsTemporallyCompatible(proposal, trip)).ToArray());
        var detailIds = proposals
            .Where(proposal => proposal.RepresentativeLocation is not null)
            .SelectMany(proposal => temporal[proposal.Id].Select(trip => trip.Id))
            .Distinct()
            .ToArray();
        var details = await Task.WhenAll(detailIds.Select(id => _tripClient.GetAsync(id, cancellationToken)));
        var locations = details
            .Where(detail => detail.IsSuccess && detail.Value is not null)
            .ToDictionary(detail => detail.Value!.Trip.Id, detail => detail.Value!.Trip.Location);
        return proposals.ToDictionary(
            proposal => proposal.Id,
            proposal => (IReadOnlyList<TripSummaryDto>)temporal[proposal.Id]
                .Where(trip => IsSpatiallyCompatible(proposal, trip.Id, locations))
                .OrderBy(trip => trip.StartedOn)
                .ToArray());
    }

    private static bool IsTemporallyCompatible(ImportTripProposalModel proposal, TripSummaryDto trip)
    {
        return trip.Status == TripConstants.Completed
            && trip.Role is TripParticipantConstants.Owner or TripParticipantConstants.Participant
            && trip.EndedOn.HasValue
            && trip.StartedOn.Date <= proposal.ProposedEndedOn.Date
            && trip.EndedOn.Value.Date >= proposal.ProposedStartedOn.Date;
    }

    private static bool IsSpatiallyCompatible(
        ImportTripProposalModel proposal,
        Guid tripId,
        IReadOnlyDictionary<Guid, TripLocationDto?> locations)
    {
        if (proposal.RepresentativeLocation is not { Latitude: not null, Longitude: not null }
            || !locations.TryGetValue(tripId, out var location)
            || location is null)
        {
            return true;
        }

        return ImportTripProposalService.DistanceKilometres(
            proposal.RepresentativeLocation.Latitude.Value,
            proposal.RepresentativeLocation.Longitude.Value,
            location.Latitude,
            location.Longitude) <= ImportTripSuggestionPolicyModel.Default.NearbyDistanceKilometres;
    }
}
